using System.Numerics;
using UniswapSharp.Core;
using UniswapSharp.UniversalRouter.Types;
using UniswapSharp.V4.Utils;

namespace UniswapSharp.UniversalRouter.Utils;

/// <summary>
/// A pull a step makes straight from the user. Port of upstream <c>UserPaidPull</c>.
/// </summary>
/// <param name="Token">
/// Token the step pulls from the user; <c>null</c> when not extractable (malformed path).
/// </param>
/// <param name="MaxAmount">Contract-enforced maximum the step can pull.</param>
public sealed record UserPaidPull(string? Token, BigInteger MaxAmount);

/// <summary>Port of universal-router-sdk <c>utils/directTransfers.ts</c>.</summary>
public static class DirectTransfers
{
    private static BigInteger Big(object? value) => AbiParamEncoder.ToBigInteger(value!);

    /// <summary>
    /// V3 path: 20-byte address + N × (3-byte fee + 20-byte address); minimum is 43 bytes
    /// (single hop, N=1). Returns N, or <c>null</c> if malformed.
    /// </summary>
    public static int? GetV3HopCount(string path)
    {
        if (!path.StartsWith("0x", StringComparison.Ordinal))
        {
            return null;
        }

        int byteLength = (path.Length - 2) / 2;
        if (byteLength < 43)
        {
            return null;
        }

        int variableSegmentLength = byteLength - 20;
        if (variableSegmentLength < 23 || variableSegmentLength % 23 != 0)
        {
            return null;
        }

        return variableSegmentLength / 23;
    }

    public static string? V3PathFirstToken(string path) =>
        GetV3HopCount(path) is null ? null : "0x" + path.Substring(2, 40);

    public static string? V3PathLastToken(string path) =>
        GetV3HopCount(path) is null ? null : "0x" + path[^40..];

    /// <summary>
    /// SETTLE_ALL / TAKE_ALL are intrinsically direct (no flag on-chain) and are gated separately.
    /// </summary>
    public static bool HasUserPaidFlag(SwapStep step) => step switch
    {
        V2SwapExactIn s => s.PayerIsUser == true,
        V2SwapExactOut s => s.PayerIsUser == true,
        V3SwapExactIn s => s.PayerIsUser == true,
        V3SwapExactOut s => s.PayerIsUser == true,
        V4Swap s => s.V4Actions.Any(a => a is V4Settle { PayerIsUser: true }),
        _ => false,
    };

    private static IEnumerable<UserPaidPull> V4UserPaidPulls(V4Action action) => action switch
    {
        V4Settle a => a.PayerIsUser == true
            ? new[] { new UserPaidPull(a.Currency, Big(a.Amount)) }
            : Array.Empty<UserPaidPull>(),
        // SETTLE_ALL always settles from msgSender (the user), bounded on-chain by maxAmount
        V4SettleAll a => new[] { new UserPaidPull(a.Currency, Big(a.MaxAmount)) },
        _ => Array.Empty<UserPaidPull>(),
    };

    /// <summary>
    /// Contract-enforced maxima: exact-in pulls exactly <c>amountIn</c>; exact-out reverts above
    /// <c>amountInMax</c>.
    /// </summary>
    public static IReadOnlyList<UserPaidPull> StepUserPaidPulls(SwapStep step) => step switch
    {
        V2SwapExactIn s => s.PayerIsUser == true
            ? new[] { new UserPaidPull(s.Path[0], Big(s.AmountIn)) }
            : Array.Empty<UserPaidPull>(),
        V2SwapExactOut s => s.PayerIsUser == true
            ? new[] { new UserPaidPull(s.Path[0], Big(s.AmountInMax)) }
            : Array.Empty<UserPaidPull>(),
        V3SwapExactIn s => s.PayerIsUser == true
            ? new[] { new UserPaidPull(V3PathFirstToken(s.Path), Big(s.AmountIn)) }
            : Array.Empty<UserPaidPull>(),
        // exact-out paths are encoded output-first, so the input token is the path tail
        V3SwapExactOut s => s.PayerIsUser == true
            ? new[] { new UserPaidPull(V3PathLastToken(s.Path), Big(s.AmountInMax)) }
            : Array.Empty<UserPaidPull>(),
        V4Swap s => s.V4Actions.SelectMany(V4UserPaidPulls).ToArray(),
        _ => Array.Empty<UserPaidPull>(),
    };

    /// <summary>
    /// Token-agnostic by design: validation binds every pull to the input token before this sum is
    /// used, so off-token pulls are rejected rather than filtered (unlike output credits below).
    /// </summary>
    public static BigInteger SumUserPaidMax(IEnumerable<SwapStep> steps) =>
        steps.SelectMany(StepUserPaidPulls).Aggregate(BigInteger.Zero, (total, pull) => total + pull.MaxAmount);

    private sealed record DirectOutputCredit(string? Token, BigInteger MinAmount);

    /// <summary>Input/output currencies of a v4 swap action's delta within its command's lock.</summary>
    private static (string Input, string Output)? V4SwapCurrencies(V4Action action)
    {
        switch (action)
        {
            case V4SwapExactIn a:
                return a.Path.Count > 0 ? (a.CurrencyIn, a.Path[^1].IntermediateCurrency) : null;
            // exact-out paths walk from the input side, so the first hop names the input currency
            case V4SwapExactOut a:
                return a.Path.Count > 0 ? (a.Path[0].IntermediateCurrency, a.CurrencyOut) : null;
            case V4SwapExactInSingle a:
                return a.ZeroForOne
                    ? (a.PoolKey.Currency0, a.PoolKey.Currency1)
                    : (a.PoolKey.Currency1, a.PoolKey.Currency0);
            case V4SwapExactOutSingle a:
                return a.ZeroForOne
                    ? (a.PoolKey.Currency0, a.PoolKey.Currency1)
                    : (a.PoolKey.Currency1, a.PoolKey.Currency0);
            default:
                return null;
        }
    }

    /// <summary>
    /// Credits exact-in swap minimums that a single OPEN_DELTA take forwards to the recipient.
    /// Sound because the v4 ledger must zero for the command to succeed: when a currency's only take
    /// is one OPEN_DELTA take to the recipient and nothing else in the block can consume that
    /// currency, any successful transaction paid the recipient everything the block's swaps produced
    /// of it — contract-enforced to be at least the exact-in swaps' summed <c>amountOutMinimum</c>s
    /// (order-independent: an early take strands later credit and reverts the lock).
    /// </summary>
    private static List<DirectOutputCredit> V4OpenDeltaTakeCredits(IReadOnlyList<V4Action> actions, string recipient)
    {
        var swapMins = new Dictionary<string, BigInteger>();
        var takeCounts = new Dictionary<string, int>();
        var soleOpenDeltaToRecipient = new Dictionary<string, bool>();
        var disqualified = new HashSet<string>();

        foreach (var action in actions)
        {
            switch (action)
            {
                case V4SwapExactIn:
                case V4SwapExactOut:
                case V4SwapExactInSingle:
                case V4SwapExactOutSingle:
                    {
                        var currencies = V4SwapCurrencies(action);
                        if (currencies is null)
                        {
                            // unparseable swap: cannot attribute deltas, credit nothing
                            return new List<DirectOutputCredit>();
                        }

                        // the swap consumes this currency's delta
                        disqualified.Add(currencies.Value.Input.ToLowerInvariant());

                        // Only exact-in minimums are creditable: the action enforces them
                        // (V4TooLittleReceived). Exact-out amounts are NOT asserted by the deployed v4
                        // router — a price-limit partial fill succeeds with less — so exact-out swaps
                        // contribute zero (their actual output only adds on top of the credited exact-in
                        // minimums, keeping the credit a valid lower bound).
                        object? amountOutMinimum = action switch
                        {
                            V4SwapExactIn a => a.AmountOutMinimum,
                            V4SwapExactInSingle a => a.AmountOutMinimum,
                            _ => null,
                        };
                        if (amountOutMinimum is not null)
                        {
                            string output = currencies.Value.Output.ToLowerInvariant();
                            swapMins[output] = swapMins.GetValueOrDefault(output) + Big(amountOutMinimum);
                        }
                        break;
                    }
                case V4Settle a:
                    disqualified.Add(a.Currency.ToLowerInvariant());
                    break;
                case V4SettleAll a:
                    disqualified.Add(a.Currency.ToLowerInvariant());
                    break;
                case V4Take a:
                    {
                        string currency = a.Currency.ToLowerInvariant();
                        takeCounts[currency] = takeCounts.GetValueOrDefault(currency) + 1;
                        soleOpenDeltaToRecipient[currency] =
                            Big(a.Amount).IsZero &&
                            string.Equals(a.Recipient, recipient, StringComparison.OrdinalIgnoreCase);
                        break;
                    }
                case V4TakeAll a:
                    {
                        string currency = a.Currency.ToLowerInvariant();
                        takeCounts[currency] = takeCounts.GetValueOrDefault(currency) + 1;
                        soleOpenDeltaToRecipient[currency] = false;
                        break;
                    }
                case V4TakePortion a:
                    {
                        string currency = a.Currency.ToLowerInvariant();
                        takeCounts[currency] = takeCounts.GetValueOrDefault(currency) + 1;
                        soleOpenDeltaToRecipient[currency] = false;
                        break;
                    }
                default:
                    break;
            }
        }

        var credits = new List<DirectOutputCredit>();
        foreach (var (currency, min) in swapMins)
        {
            if (takeCounts.GetValueOrDefault(currency) == 1 &&
                soleOpenDeltaToRecipient.GetValueOrDefault(currency) &&
                !disqualified.Contains(currency))
            {
                credits.Add(new DirectOutputCredit(currency, min));
            }
        }
        return credits;
    }

    private static IReadOnlyList<DirectOutputCredit> V4DirectOutputCredits(V4Action action, string recipient)
    {
        switch (action)
        {
            case V4Take a:
                {
                    if (!string.Equals(a.Recipient, recipient, StringComparison.OrdinalIgnoreCase))
                    {
                        return Array.Empty<DirectOutputCredit>();
                    }
                    BigInteger amount = Big(a.Amount);
                    // OPEN_DELTA (0) / CONTRACT_BALANCE takes are runtime-sized: deliverable, but
                    // guarantee nothing
                    if (amount <= 0 || amount >= Constants.CONTRACT_BALANCE)
                    {
                        return Array.Empty<DirectOutputCredit>();
                    }
                    return new[] { new DirectOutputCredit(a.Currency, amount) };
                }
            // pays msgSender on-chain: counts only when the spec recipient IS the sender sentinel
            case V4TakeAll a:
                return recipient == Constants.SENDER_AS_RECIPIENT
                    ? new[] { new DirectOutputCredit(a.Currency, Big(a.MinAmount)) }
                    : Array.Empty<DirectOutputCredit>();
            default:
                return Array.Empty<DirectOutputCredit>();
        }
    }

    /// <summary>Contract-guaranteed amounts a step delivers directly to <paramref name="recipient"/>.</summary>
    private static IReadOnlyList<DirectOutputCredit> StepDirectOutputCredits(SwapStep step, string recipient)
    {
        bool IsDirect(string stepRecipient) =>
            string.Equals(stepRecipient, recipient, StringComparison.OrdinalIgnoreCase);

        switch (step)
        {
            case V2SwapExactIn s:
                return IsDirect(s.Recipient)
                    ? new[] { new DirectOutputCredit(s.Path[^1], Big(s.AmountOutMin)) }
                    : Array.Empty<DirectOutputCredit>();
            // Not creditable: v2SwapExactOutput only bounds amountIn (<= amountInMaximum) and never
            // asserts amountOut was produced — unlike V3 exact-out (V3InvalidAmountOut) or v4 exact-out
            // (concrete TAKE / CurrencyNotSettled). So a fee-on-transfer input/intermediate makes the leg
            // produce less than amountOut and still succeed; like v4 OPEN_DELTA exact-out, contribute zero.
            case V2SwapExactOut:
                return Array.Empty<DirectOutputCredit>();
            case V3SwapExactIn s:
                return IsDirect(s.Recipient)
                    ? new[] { new DirectOutputCredit(V3PathLastToken(s.Path), Big(s.AmountOutMin)) }
                    : Array.Empty<DirectOutputCredit>();
            // exact-out paths are encoded output-first, so the output token is the path head
            case V3SwapExactOut s:
                return IsDirect(s.Recipient)
                    ? new[] { new DirectOutputCredit(V3PathFirstToken(s.Path), Big(s.AmountOut)) }
                    : Array.Empty<DirectOutputCredit>();
            case UnwrapWeth s:
                return IsDirect(s.Recipient)
                    ? new[] { new DirectOutputCredit(Constants.ETH_ADDRESS, Big(s.AmountMin)) }
                    : Array.Empty<DirectOutputCredit>();
            // Per-action concrete credits and the block-level OPEN_DELTA rule are disjoint:
            // an OPEN_DELTA take never produces a per-action credit.
            case V4Swap s:
                return s.V4Actions
                    .SelectMany(action => V4DirectOutputCredits(action, recipient))
                    .Concat(V4OpenDeltaTakeCredits(s.V4Actions, recipient))
                    .ToArray();
            default:
                return Array.Empty<DirectOutputCredit>();
        }
    }

    /// <summary>
    /// Direct-output coverage in the output token: the summed contract-enforced minimum delivered
    /// straight to the recipient, and the number of coverage-bearing legs it came from. Summed per-leg
    /// floors can trail the trade-level floor by up to one wei each, so the caller uses
    /// <c>Legs</c> to size how much rounding to tolerate; only legs carrying a nonzero minimum count —
    /// a zero-min leg has no flooring error to forgive, so it must not pad the tolerance.
    /// </summary>
    private static (BigInteger Min, int Legs) DirectOutputCoverage(
        IReadOnlyList<SwapStep> steps, string recipient, string outputTokenAddress)
    {
        string target = outputTokenAddress.ToLowerInvariant();
        var credits = steps
            .SelectMany(step => StepDirectOutputCredits(step, recipient))
            .Where(credit => credit.Token is not null && credit.Token.ToLowerInvariant() == target)
            .ToList();

        return (
            credits.Aggregate(BigInteger.Zero, (total, credit) => total + credit.MinAmount),
            credits.Count(credit => credit.MinAmount > 0));
    }

    public static BigInteger SumDirectOutputMin(
        IReadOnlyList<SwapStep> steps, string recipient, string outputTokenAddress) =>
        DirectOutputCoverage(steps, recipient, outputTokenAddress).Min;

    /// <summary>
    /// The final SWEEP floor for a direct-transfer plan: <paramref name="netMin"/> minus what the direct
    /// legs already guarantee to the recipient. Direct legs pay straight from the pools, so their credited
    /// minimums can sum a few wei below <paramref name="netMin"/> — from independent integer flooring, and
    /// from routers that floor slippage in f64 — with no custody balance to cover the gap. A small
    /// tolerance forgives that sub-economic rounding so a fully covered direct plan doesn't revert; a
    /// larger, real coverage gap still enforces the full floor.
    /// </summary>
    public static BigInteger DirectTransferSweepFloor(
        NormalizedSwapSpecification spec,
        BigInteger netMin,
        IReadOnlyList<SwapStep> swapSteps,
        string outputTokenAddress)
    {
        var (directOutputMin, legs) = DirectOutputCoverage(swapSteps, spec.Recipient, outputTokenAddress);
        BigInteger shortfall = netMin - directOutputMin;

        // Flexibility tolerance: min(0.5bps of netMin, 1% of the slippage the user set). Headroom for a
        // router's per-leg slippage math; scales to 0 as slippage -> 0 (and is 0 for exact-output, which
        // has no output-side slippage), so an exact-price trade stays exact.
        BigInteger outputSlippage = spec.TradeType == TradeType.EXACT_INPUT
            ? spec.Routing.Quote.Quotient * spec.SlippageTolerance.Numerator / spec.SlippageTolerance.Denominator
            : BigInteger.Zero;

        BigInteger halfBps = netMin / 20000;
        BigInteger onePercentOfSlippage = outputSlippage / 100;
        BigInteger flexibilityTolerance = halfBps < onePercentOfSlippage ? halfBps : onePercentOfSlippage;

        // Rounding tolerance: 2 wei per direct leg — inherent integer per-leg flooring, always allowed.
        BigInteger roundingTolerance = legs * 2;

        BigInteger toleratedShortfall =
            flexibilityTolerance > roundingTolerance ? flexibilityTolerance : roundingTolerance;

        return shortfall > toleratedShortfall ? shortfall : BigInteger.Zero;
    }
}
