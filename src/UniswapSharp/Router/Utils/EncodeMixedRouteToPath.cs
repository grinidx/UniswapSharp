using UniswapSharp.Core.Entities;
using UniswapSharp.Router.Entities.MixedRoute;
using V2Pair = UniswapSharp.V2.Entities.Pair;
using V3Pool = UniswapSharp.V3.Entities.Pool;
using V4Pool = UniswapSharp.V4.Entities.Pool;

namespace UniswapSharp.Router.Utils;

/// <summary>
/// Port of router-sdk <c>utils/encodeMixedRouteToPath.ts</c>. Converts a mixed route to a
/// Solidity <c>encodePacked</c> hex path (only supports exact-in route encodings).
/// </summary>
public static class EncodeMixedRouteToPath
{
    /// <param name="route">the mixed path to convert to an encoded path</param>
    /// <param name="useMixedRouterQuoteV2">
    /// if true, uses the Mixed Quoter V2 encoding for v4 pools. By default (null) it is derived
    /// from whether the route contains a V4 pool. Only used in SOR for explicit onchain quoting.
    /// </param>
    /// <returns>the exact-in encoded path as a lower-case <c>0x</c>-prefixed hex string</returns>
    public static string Encode<TInput, TOutput>(MixedRouteSDK<TInput, TOutput> route, bool? useMixedRouterQuoteV2 = null)
        where TInput : BaseCurrency where TOutput : BaseCurrency
    {
        bool containsV4Pool = useMixedRouterQuoteV2 ?? route.Pools.Any(pool => pool is V4Pool);

        var buffer = new List<byte>();

        if (containsV4Pool)
        {
            PackAddress(buffer, route.PathInput.IsNative ? Constants.ADDRESS_ZERO : ((Token)route.PathInput).Address);
            BaseCurrency currencyIn = route.PathInput;

            foreach (var pool in route.Pools)
            {
                BaseCurrency currencyOut = currencyIn.Equals(TPool.Token0(pool)) ? TPool.Token1(pool) : TPool.Token0(pool);

                if (pool is V4Pool v4)
                {
                    // a tickSpacing of 0 indicates a "fake" v4 pool where the quote actually requires a wrap or
                    // unwrap; the fake v4 pool always has native as token0 and wrapped native as token1
                    if (v4.TickSpacing == 0)
                    {
                        PackUint(buffer, 0, 1); // wrapOrUnwrapEncoding (uint8)
                        PackAddress(buffer, currencyOut.IsNative ? Constants.ADDRESS_ZERO : currencyOut.Wrapped().Address);
                    }
                    else
                    {
                        PackUint(buffer, v4.Fee + Constants.MIXED_QUOTER_V2_V4_FEE_PATH_PLACEHOLDER, 3); // uint24
                        PackUint(buffer, v4.TickSpacing, 3); // uint24
                        PackAddress(buffer, v4.Hooks);
                        PackAddress(buffer, currencyOut.IsNative ? Constants.ADDRESS_ZERO : currencyOut.Wrapped().Address);
                    }
                }
                else if (pool is V3Pool v3)
                {
                    PackUint(buffer, (int)v3.Fee + Constants.MIXED_QUOTER_V2_V3_FEE_PATH_PLACEHOLDER, 3); // uint24
                    PackAddress(buffer, currencyOut.Wrapped().Address);
                }
                else if (pool is V2Pair)
                {
                    PackUint(buffer, Constants.MIXED_QUOTER_V2_V2_FEE_PATH_PLACEHOLDER, 1); // uint8
                    PackAddress(buffer, currencyOut.Wrapped().Address);
                }
                else
                {
                    throw new ArgumentException($"Unsupported pool type {pool.GetType().Name}");
                }

                currencyIn = currencyOut;
            }
        }
        else
        {
            // TODO(upstream ROUTE-276): the non-v4 mixed-route legacy encoding (Mixed Quoter V1).
            BaseCurrency inputToken = route.Input;
            for (int index = 0; index < route.Pools.Count; index++)
            {
                var pool = route.Pools[index];
                BaseCurrency outputToken = TPool.Token0(pool).Equals(inputToken) ? TPool.Token1(pool) : TPool.Token0(pool);

                if (index == 0)
                {
                    PackAddress(buffer, inputToken.Wrapped().Address);
                }

                int fee = pool is V3Pool v3 ? (int)v3.Fee : Constants.MIXED_QUOTER_V1_V2_FEE_PATH_PLACEHOLDER;
                PackUint(buffer, fee, 3); // uint24
                PackAddress(buffer, outputToken.Wrapped().Address);

                inputToken = outputToken;
            }
        }

        return "0x" + Convert.ToHexStringLower(buffer.ToArray());
    }

    private static void PackAddress(List<byte> buffer, string address)
    {
        string hex = address.StartsWith("0x") || address.StartsWith("0X") ? address[2..] : address;
        if (hex.Length != 40)
        {
            throw new ArgumentException($"Invalid address length: {address}");
        }
        for (int i = 0; i < 20; i++)
        {
            buffer.Add(Convert.ToByte(hex.Substring(i * 2, 2), 16));
        }
    }

    private static void PackUint(List<byte> buffer, int value, int numBytes)
    {
        for (int i = numBytes - 1; i >= 0; i--)
        {
            buffer.Add((byte)((value >> (8 * i)) & 0xFF));
        }
    }
}
