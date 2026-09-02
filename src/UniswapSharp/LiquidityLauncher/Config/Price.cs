using System.Numerics;
using System.Text.RegularExpressions;

namespace UniswapSharp.LiquidityLauncher.Config;

/// <summary>
/// Floor-price → Q96 conversions and the CCA price-tick granularity. The CCA price model is
/// raw-currency-per-raw-token in Q96. Ported from sdks/liquidity-launcher-sdk/src/config/price.ts.
/// </summary>
public static class Price
{
    private static readonly Regex DecimalRegex = new(@"^\d+(\.\d+)?$", RegexOptions.Compiled);

    private static (BigInteger Num, BigInteger Den) ParseDecimalRatio(string value)
    {
        string trimmed = value.Trim();
        if (!DecimalRegex.IsMatch(trimmed))
        {
            throw new LauncherSdkError(LauncherErrorCode.INVALID_FLOOR_PRICE, "Floor price must be a positive decimal");
        }
        string[] parts = trimmed.Split('.');
        string whole = parts[0];
        string frac = parts.Length > 1 ? parts[1] : "";
        BigInteger num = BigInteger.Parse(whole + frac);
        if (num <= 0)
        {
            throw new LauncherSdkError(LauncherErrorCode.INVALID_FLOOR_PRICE, "Floor price must be greater than zero");
        }
        return (num, BigInteger.Pow(10, frac.Length));
    }

    /// <summary>
    /// CCA floor price = raw-currency-per-raw-token in Q96:
    /// <c>floorX96 = humanFloor * 10^currencyDecimals / 10^tokenDecimals * 2^96</c>.
    /// </summary>
    public static BigInteger FloorPriceToX96(string humanFloorRaisePerToken, int tokenDecimals, int currencyDecimals)
    {
        var (num, den) = ParseDecimalRatio(humanFloorRaisePerToken);
        BigInteger numerator = num * BigInteger.Pow(10, currencyDecimals) * Constants.Q96;
        BigInteger denominator = den * BigInteger.Pow(10, tokenDecimals);
        BigInteger floorX96 = numerator / denominator;
        if (floorX96 <= 0)
        {
            throw new LauncherSdkError(
                LauncherErrorCode.INVALID_FLOOR_PRICE, "Floor price is too small. Raise the floor price and try again.");
        }
        return floorX96;
    }

    /// <summary>Graduation threshold: currency needed to clear the whole auction supply at the floor.</summary>
    public static BigInteger RequiredCurrencyRaised(BigInteger floorPriceX96, BigInteger auctionSupply) =>
        floorPriceX96 * auctionSupply / Constants.Q96;

    /// <summary>
    /// Converts a target FDV in USD into the canonical raise-currency-per-1-token decimal string the
    /// CreateAuction API expects (<c>floor_price_raise_per_token</c> /
    /// <c>graduation_price_raise_per_token</c>):
    /// <c>pricePerToken = (fdvUsd / totalSupplyWholeTokens) / raiseCurrencyUsdPrice</c>.
    /// </summary>
    /// <remarks>
    /// The result is a plain decimal (the liquidity service rejects scientific notation). Unit
    /// discipline: this converts an FDV — a price over the FULL supply — so the USD actually raised at
    /// that price is <c>fdvUsd * soldSupplyShare</c>, never <c>fdvUsd</c> itself.
    /// </remarks>
    public static string FdvUsdToPricePerToken(
        double fdvUsd, BigInteger totalSupplyWholeTokens, double raiseCurrencyUsdPrice)
    {
        double supply = (double)totalSupplyWholeTokens;
        if (!double.IsFinite(fdvUsd) || fdvUsd <= 0)
        {
            throw new LauncherSdkError(LauncherErrorCode.INVALID_INPUT, "FDV must be a positive, finite USD amount");
        }
        if (!double.IsFinite(supply) || supply <= 0)
        {
            throw new LauncherSdkError(
                LauncherErrorCode.INVALID_INPUT, "Total supply must be a positive, finite token amount");
        }
        if (!double.IsFinite(raiseCurrencyUsdPrice) || raiseCurrencyUsdPrice <= 0)
        {
            throw new LauncherSdkError(
                LauncherErrorCode.INVALID_INPUT, "Raise-currency USD price must be a positive, finite amount");
        }

        double pricePerToken = fdvUsd / supply / raiseCurrencyUsdPrice;
        // Matches JS Number.prototype.toFixed(18), then trims trailing zeros and a bare trailing dot.
        string fixedDecimal = pricePerToken.ToString("F18", System.Globalization.CultureInfo.InvariantCulture)
            .TrimEnd('0')
            .TrimEnd('.');

        if (fixedDecimal.Length == 0 || fixedDecimal == "0")
        {
            throw new LauncherSdkError(
                LauncherErrorCode.INVALID_INPUT, "Price per token is below 1e-18 and rounds to zero");
        }
        return fixedDecimal;
    }

    /// <summary>Derives the CCA price-tick granularity from the floor price (minimum 1).</summary>
    public static BigInteger DeriveAuctionTickSpacing(BigInteger floorPriceX96)
    {
        BigInteger tickSpacing = floorPriceX96 / Constants.AUCTION_TICK_DIVISOR;
        return tickSpacing > 0 ? tickSpacing : BigInteger.One;
    }

    /// <summary>
    /// Derives the CCA tick spacing AND snaps the floor price DOWN to the nearest tick boundary (the
    /// CCA constructor requires <c>floorPrice % tickSpacing == 0</c>).
    /// </summary>
    public static (BigInteger FloorPriceX96, BigInteger TickSpacing) DeriveAuctionPricing(BigInteger rawFloorPriceX96)
    {
        BigInteger tickSpacing = DeriveAuctionTickSpacing(rawFloorPriceX96);
        return (rawFloorPriceX96 - rawFloorPriceX96 % tickSpacing, tickSpacing);
    }
}
