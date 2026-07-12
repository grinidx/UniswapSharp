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
