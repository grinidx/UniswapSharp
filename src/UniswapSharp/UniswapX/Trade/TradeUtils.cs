using UniswapSharp.Core.Entities;

namespace UniswapSharp.UniswapX.Trade;

/// <summary>The native-asset symbols used when a native output is encoded by symbol (uniswapx-sdk <c>NativeAssets</c>).</summary>
public enum NativeAssets
{
    MATIC,
    BNB,
    AVAX,
    ETH,
}

/// <summary>Port of uniswapx-sdk <c>trade/utils.ts</c>.</summary>
public static class TradeUtils
{
    private const string AddressZero = "0x0000000000000000000000000000000000000000";

    private static string NativeCurrencyAddressString(int chainId) => chainId switch
    {
        137 => NativeAssets.MATIC.ToString(),
        56 => NativeAssets.BNB.ToString(),
        43114 => NativeAssets.AVAX.ToString(),
        _ => NativeAssets.ETH.ToString(),
    };

    /// <summary>Whether <paramref name="currency"/> corresponds to the encoded token <paramref name="address"/> on <paramref name="chainId"/>.</summary>
    public static bool AreCurrenciesEqual(BaseCurrency currency, string? address, int chainId)
    {
        if (currency.ChainId != chainId)
        {
            return false;
        }
        if (currency.IsNative)
        {
            return address == AddressZero || address == NativeCurrencyAddressString(chainId);
        }
        return currency.Wrapped().Address.ToLowerInvariant() == address?.ToLowerInvariant();
    }
}
