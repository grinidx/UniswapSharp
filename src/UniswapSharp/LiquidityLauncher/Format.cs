using System.Numerics;
using System.Text;

namespace UniswapSharp.LiquidityLauncher;

/// <summary>
/// Display formatting for launch UIs and error messages. Ported from
/// sdks/liquidity-launcher-sdk/src/format.ts.
/// </summary>
public static class Format
{
    /// <summary>
    /// Formats a Uniswap pool fee (hundredths of a bip — 3000 = 0.3%, 1_000_000 = 100%) as a
    /// percentage: 3000 -&gt; "0.3%", 3001 -&gt; "0.3001%", 100 -&gt; "0.01%", 10000 -&gt; "1%".
    /// </summary>
    public static string FormatFeePercent(int fee)
    {
        // Hundredths of a bip -> percent is a divide by 10_000; an integer fee yields at most 4 decimals.
        // Computed with integer arithmetic to match `parseFloat(percent.toFixed(4))` exactly.
        bool negative = fee < 0;
        int abs = Math.Abs(fee);
        int whole = abs / 10_000;
        string fraction = (abs % 10_000).ToString("D4").TrimEnd('0');
        string body = fraction.Length == 0 ? whole.ToString() : $"{whole}.{fraction}";
        return $"{(negative ? "-" : "")}{body}%";
    }

    /// <summary>
    /// Formats a raw on-chain token amount (base units) as a grouped human-readable decimal, with an
    /// optional symbol: (57500000000000000000000000, 18, "WBTC") -&gt; "57,500,000 WBTC".
    /// </summary>
    public static string FormatTokenAmount(BigInteger rawAmount, int decimals, string? symbol = null)
    {
        var (whole, fraction) = FormatUnits(rawAmount, decimals);
        string grouped = GroupThousands(whole);
        string amount = fraction.Length > 0 ? $"{grouped}.{fraction}" : grouped;
        return symbol is not null ? $"{amount} {symbol}" : amount;
    }

    // Mirrors viem `formatUnits`: split into integer / fraction, trailing-zero-trim the fraction.
    private static (string Whole, string Fraction) FormatUnits(BigInteger value, int decimals)
    {
        bool negative = value.Sign < 0;
        string digits = BigInteger.Abs(value).ToString();
        digits = digits.PadLeft(decimals, '0');
        string integer = digits[..^decimals];
        string fraction = digits[^decimals..].TrimEnd('0');
        if (integer.Length == 0)
        {
            integer = "0";
        }
        return (negative ? "-" + integer : integer, fraction);
    }

    private static string GroupThousands(string whole)
    {
        bool negative = whole.StartsWith('-');
        string digits = negative ? whole[1..] : whole;
        var sb = new StringBuilder();
        for (int i = 0; i < digits.Length; i++)
        {
            if (i > 0 && (digits.Length - i) % 3 == 0)
            {
                sb.Append(',');
            }
            sb.Append(digits[i]);
        }
        return negative ? "-" + sb : sb.ToString();
    }
}
