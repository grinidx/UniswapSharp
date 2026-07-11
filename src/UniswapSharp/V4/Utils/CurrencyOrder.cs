using UniswapSharp.Core.Entities;

namespace UniswapSharp.V4.Utils;

public static class CurrencyOrder
{
    /// <summary>
    /// Currency ordering for v4 pools: a native currency always sorts first; otherwise the wrapped
    /// tokens are compared by address. Ported from v4-sdk/src/utils/sortsBefore.ts.
    /// </summary>
    public static bool SortsBefore(BaseCurrency currencyA, BaseCurrency currencyB)
    {
        if (currencyA.IsNative) return true;
        if (currencyB.IsNative) return false;
        return currencyA.Wrapped().SortsBefore(currencyB.Wrapped());
    }
}
