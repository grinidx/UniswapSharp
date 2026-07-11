using UniswapSharp.Core.Entities;

namespace UniswapSharp.V4.Utils;

public static class CurrencyMap
{
    /// <summary>
    /// Uniswap v4 supports native pools; those currencies are represented by the zero address.
    /// Ported from v4-sdk/src/utils/currencyMap.ts (<c>toAddress</c>).
    /// </summary>
    public static string ToAddress(BaseCurrency currency)
    {
        return currency.IsNative ? Constants.ADDRESS_ZERO : currency.Wrapped().Address;
    }
}
