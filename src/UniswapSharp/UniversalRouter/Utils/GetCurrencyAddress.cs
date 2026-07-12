using UniswapSharp.Core.Entities;

namespace UniswapSharp.UniversalRouter.Utils;

/// <summary>Port of universal-router-sdk <c>utils/getCurrencyAddress.ts</c>.</summary>
public static class CurrencyAddress
{
    /// <summary>Returns <see cref="Constants.ETH_ADDRESS"/> for a native currency, else its wrapped token address.</summary>
    public static string GetCurrencyAddress(BaseCurrency currency) =>
        currency.IsNative ? Constants.ETH_ADDRESS : currency.Wrapped().Address;
}
