using System.Numerics;
using System.Text.RegularExpressions;

namespace UniswapSharp.LiquidityLauncher.Config;

public record TieredLpAllocationTier(string RaiseMilestone, double Percent);

/// <summary>Post-auction LP allocation input. Ported from src/config/lpAllocation.ts.</summary>
public abstract record LpAllocationInput;

public sealed record SingleLpAllocationInput(double Percent) : LpAllocationInput;

public sealed record TieredLpAllocationInput(
    // Decimals of the auction raise currency; milestones are human decimals in this unit.
    int RaiseCurrencyDecimals,
    IReadOnlyList<TieredLpAllocationTier> Tiers) : LpAllocationInput;

/// <summary>
/// Post-auction LP allocation → on-chain <c>LiquidityAllocationBracket[]</c>. Ported from
/// sdks/liquidity-launcher-sdk/src/config/lpAllocation.ts.
/// </summary>
public static class LpAllocation
{
    /// <summary>Sentinel for the final tier: all cumulative raises at/above the prior bound use this percent.</summary>
    public const string LP_ALLOCATION_UNBOUNDED_RAISE_MILESTONE = "unbounded";

    private static readonly BigInteger MaxLpAllocationUpperBound = (BigInteger.One << 128) - 1;
    private const int MaxLpAllocationTiers = 32;

    private static readonly Regex DecimalRegex = new(@"^\d+(\.\d+)?$", RegexOptions.Compiled);

    private static int PercentToRate(double percent)
    {
        if (!double.IsFinite(percent) || percent < Constants.MIN_LP_ALLOCATION_PERCENT || percent > 100)
        {
            throw new LauncherSdkError(
                LauncherErrorCode.INVALID_LP_ALLOCATION,
                $"Liquidity allocation must be between {Constants.MIN_LP_ALLOCATION_PERCENT}% and 100%.");
        }
        return (int)MathJs.Round(percent * (Constants.MPS_TOTAL / 100.0));
    }

    private static bool IsUnboundedRaiseMilestone(string raw)
    {
        string t = raw.Trim().ToLowerInvariant();
        return t.Length == 0 || t == LP_ALLOCATION_UNBOUNDED_RAISE_MILESTONE;
    }

    private static BigInteger ParseRaiseMilestoneToRawUnits(string raw, int raiseCurrencyDecimals)
    {
        if (raiseCurrencyDecimals < 0 || raiseCurrencyDecimals > 36)
        {
            throw new LauncherSdkError(
                LauncherErrorCode.INVALID_LP_ALLOCATION, "The raise currency has an unsupported number of decimals.");
        }
        string t = raw.Trim();
        if (!DecimalRegex.IsMatch(t))
        {
            throw new LauncherSdkError(
                LauncherErrorCode.INVALID_LP_ALLOCATION, "Each liquidity allocation milestone must be a positive number.");
        }
        int dot = t.IndexOf('.');
        string intPart = dot == -1 ? t : t[..dot];
        string fracPart = dot == -1 ? "" : t[(dot + 1)..];
        if (fracPart.Length > raiseCurrencyDecimals)
        {
            throw new LauncherSdkError(
                LauncherErrorCode.INVALID_LP_ALLOCATION,
                $"Each liquidity allocation milestone can have at most {raiseCurrencyDecimals} decimal place(s) for this currency.");
        }
        BigInteger intBig = BigInteger.Parse(intPart);
        BigInteger scale = BigInteger.Pow(10, raiseCurrencyDecimals);
        string fracPadded = fracPart.PadRight(raiseCurrencyDecimals, '0');
        BigInteger fracBig = raiseCurrencyDecimals == 0 ? BigInteger.Zero : BigInteger.Parse(fracPadded);
        BigInteger @out = intBig * scale + fracBig;
        if (@out > MaxLpAllocationUpperBound)
        {
            throw new LauncherSdkError(
                LauncherErrorCode.INVALID_LP_ALLOCATION, "A liquidity allocation milestone is too large.");
        }
        return @out;
    }

    private static IReadOnlyList<TieredLpAllocationTier> NormalizeTieredLpAllocationTiers(
        IReadOnlyList<TieredLpAllocationTier> tiers, int raiseCurrencyDecimals)
    {
        var boundedParsed = new List<(TieredLpAllocationTier Tier, BigInteger Upper)>();
        var unbounded = new List<TieredLpAllocationTier>();
        foreach (var tier in tiers)
        {
            if (IsUnboundedRaiseMilestone(tier.RaiseMilestone))
            {
                unbounded.Add(tier);
            }
            else
            {
                boundedParsed.Add((tier, ParseRaiseMilestoneToRawUnits(tier.RaiseMilestone, raiseCurrencyDecimals)));
            }
        }
        if (unbounded.Count == 0)
        {
            throw new LauncherSdkError(
                LauncherErrorCode.INVALID_LP_ALLOCATION,
                "A tiered liquidity allocation must have exactly one open-ended final tier.");
        }
        if (unbounded.Count > 1)
        {
            throw new LauncherSdkError(
                LauncherErrorCode.INVALID_LP_ALLOCATION,
                "A tiered liquidity allocation can have only one open-ended final tier.");
        }
        var sorted = boundedParsed.OrderBy(b => b.Upper).Select(b => b.Tier).ToList();
        sorted.Add(unbounded[0]);
        return sorted;
    }

    /// <summary>Builds on-chain <c>LiquidityAllocationBracket[]</c> (<c>lowerThreshold</c> + <c>rate</c>).</summary>
    public static IReadOnlyList<LiquidityAllocationBracket> BuildLpAllocationSchedule(LpAllocationInput allocation)
    {
        if (allocation is SingleLpAllocationInput single)
        {
            return new[] { new LiquidityAllocationBracket(BigInteger.Zero, PercentToRate(single.Percent)) };
        }
        var tiered = (TieredLpAllocationInput)allocation;
        if (tiered.Tiers.Count == 0 || tiered.Tiers.Count > MaxLpAllocationTiers)
        {
            throw new LauncherSdkError(
                LauncherErrorCode.INVALID_LP_ALLOCATION,
                $"A tiered liquidity allocation must have between 1 and {MaxLpAllocationTiers} tiers.");
        }
        int raiseCurrencyDecimals = tiered.RaiseCurrencyDecimals;
        var tiers = NormalizeTieredLpAllocationTiers(tiered.Tiers, raiseCurrencyDecimals);
        var upperBounds = new List<BigInteger>();
        for (int i = 0; i < tiers.Count - 1; i++)
        {
            upperBounds.Add(ParseRaiseMilestoneToRawUnits(tiers[i].RaiseMilestone, raiseCurrencyDecimals));
        }
        foreach (var u in upperBounds)
        {
            if (u >= MaxLpAllocationUpperBound)
            {
                throw new LauncherSdkError(
                    LauncherErrorCode.INVALID_LP_ALLOCATION, "A liquidity allocation milestone is too large.");
            }
        }
        for (int i = 1; i < upperBounds.Count; i++)
        {
            if (upperBounds[i] <= upperBounds[i - 1])
            {
                throw new LauncherSdkError(
                    LauncherErrorCode.INVALID_LP_ALLOCATION, "Each liquidity allocation milestone must be unique.");
            }
        }
        return tiers
            .Select((tier, i) => new LiquidityAllocationBracket(
                i == 0 ? BigInteger.Zero : upperBounds[i - 1] + 1,
                PercentToRate(tier.Percent)))
            .ToList();
    }
}
