using Nethereum.ABI;
using Nethereum.Hex.HexConvertors.Extensions;
using UniswapSharp.Core.Entities;
using UniswapSharp.V3.Entities;

namespace UniswapSharp.V3.Utils;

public static class EncodeRouteToPath
{
    /// <summary>
    /// Converts a route to a hex encoded path
    /// <summary>
    /// Converts a route to a hex encoded path
    /// </summary>
    /// <param name="route">The v3 path to convert to an encoded path</param>
    /// <param name="exactOutput">Whether the route should be encoded in reverse, for making exact output swaps</param>
    /// <returns>The hex encoded path</returns>
    public static string Encode(Route<BaseCurrency, BaseCurrency> route, bool exactOutput)
    {
        var firstInputToken = route.Input.Wrapped();
        var types = new List<string>();
        var path = new List<object>();

        for (int i = 0; i < route.Pools.Count; i++)
        {
            var pool = route.Pools[i];
            var outputToken = pool.Token0.Address == firstInputToken.Address ? pool.Token1 : pool.Token0;

            if (i == 0)
            {
                types.AddRange(new[] { "address", "uint24", "address" });
                path.AddRange(new object[] { firstInputToken.Address, (int)pool.Fee, outputToken.Address });
            }
            else
            {
                types.AddRange(new[] { "uint24", "address" });
                path.AddRange(new object[] { (int)pool.Fee, outputToken.Address });
            }

            firstInputToken = outputToken;
        }

        var abiEncoder = new ABIEncode();
        if (exactOutput)
        {
            types.Reverse();
            path.Reverse();
        }

        // Upstream uses ethers' `pack` (Solidity encodePacked): tightly packed,
        // no 32-byte padding — address is 20 bytes, uint24 is 3 bytes.
        var abiValues = new ABIValue[types.Count];
        for (int i = 0; i < types.Count; i++)
        {
            abiValues[i] = new ABIValue(types[i], path[i]);
        }

        return abiEncoder.GetABIEncodedPacked(abiValues).ToHex(true);
    }
}
