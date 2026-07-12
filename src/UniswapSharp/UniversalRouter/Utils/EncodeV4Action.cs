using System.Numerics;
using UniswapSharp.UniversalRouter.Types;
using UniswapSharp.V4.Entities;
using UniswapSharp.V4.Utils;

namespace UniswapSharp.UniversalRouter.Utils;

/// <summary>Port of universal-router-sdk <c>utils/encodeV4Action.ts</c>.</summary>
public static class EncodeV4Action
{
    /// <summary>Maps a <see cref="V4Action"/> to a v4-planner <see cref="Actions"/> opcode + positional params.</summary>
    public static (Actions Action, object?[] Params) Encode(V4Action v4Action, UniversalRouterVersion? urVersion = null)
    {
        bool useV2_1_1 = Constants.IsAtLeastV2_1_1(urVersion);

        switch (v4Action)
        {
            case V4SwapExactIn a:
                return (Actions.SWAP_EXACT_IN, new object?[]
                {
                    useV2_1_1
                        ? new object?[] { a.CurrencyIn, PathTuples(a.Path), MinHopArray(a.MinHopPriceX36), a.AmountIn, a.AmountOutMinimum }
                        : new object?[] { a.CurrencyIn, PathTuples(a.Path), a.AmountIn, a.AmountOutMinimum },
                });

            case V4SwapExactInSingle a:
                return (Actions.SWAP_EXACT_IN_SINGLE, new object?[]
                {
                    useV2_1_1
                        ? new object?[] { PoolKeyTuple(a.PoolKey), a.ZeroForOne, a.AmountIn, a.AmountOutMinimum, a.MinHopPriceX36 ?? (object)BigInteger.Zero, a.HookData }
                        : new object?[] { PoolKeyTuple(a.PoolKey), a.ZeroForOne, a.AmountIn, a.AmountOutMinimum, a.HookData },
                });

            case V4SwapExactOut a:
                return (Actions.SWAP_EXACT_OUT, new object?[]
                {
                    useV2_1_1
                        ? new object?[] { a.CurrencyOut, PathTuples(a.Path), MinHopArray(a.MinHopPriceX36), a.AmountOut, a.AmountInMaximum }
                        : new object?[] { a.CurrencyOut, PathTuples(a.Path), a.AmountOut, a.AmountInMaximum },
                });

            case V4SwapExactOutSingle a:
                return (Actions.SWAP_EXACT_OUT_SINGLE, new object?[]
                {
                    useV2_1_1
                        ? new object?[] { PoolKeyTuple(a.PoolKey), a.ZeroForOne, a.AmountOut, a.AmountInMaximum, a.MinHopPriceX36 ?? (object)BigInteger.Zero, a.HookData }
                        : new object?[] { PoolKeyTuple(a.PoolKey), a.ZeroForOne, a.AmountOut, a.AmountInMaximum, a.HookData },
                });

            // payerIsUser=false: same router-custody model as encodeSwapStep — funds are already in the router
            case V4Settle a:
                return (Actions.SETTLE, new object?[] { a.Currency, a.Amount, false });
            case V4SettleAll a:
                return (Actions.SETTLE_ALL, new object?[] { a.Currency, a.MaxAmount });
            case V4Take a:
                return (Actions.TAKE, new object?[] { a.Currency, a.Recipient, a.Amount });
            case V4TakeAll a:
                return (Actions.TAKE_ALL, new object?[] { a.Currency, a.MinAmount });
            case V4TakePortion a:
                return (Actions.TAKE_PORTION, new object?[] { a.Currency, a.Recipient, a.Bips });
            default:
                throw new InvalidOperationException($"Unhandled V4 action: {v4Action.GetType().Name}");
        }
    }

    private static object?[] PoolKeyTuple(PoolKey pk) =>
        new object?[] { pk.Currency0, pk.Currency1, (BigInteger)pk.Fee, pk.TickSpacing, pk.Hooks };

    private static object?[] PathTuples(IEnumerable<PathKey> path) =>
        path.Select(pk => (object?)new object?[]
        {
            pk.IntermediateCurrency, (BigInteger)pk.Fee, pk.TickSpacing, pk.Hooks, pk.HookData,
        }).ToArray();

    private static object?[] MinHopArray(IReadOnlyList<object>? minHop) =>
        (minHop ?? Array.Empty<object>()).ToArray();
}
