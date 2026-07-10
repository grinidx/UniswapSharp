using System.Numerics;
using Nethereum.ABI;
using UniswapSharp.Core.Entities;
using UniswapSharp.Core.Entities.Fractions;
using UniswapSharp.Core.Utils;
using UniswapSharp.V3.Entities;
using UniswapSharp.V3.Utils;
using static UniswapSharp.V3.Utils.AbiFunctionEncoder;

namespace UniswapSharp.V3;

public class NonfungiblePositionManager
{
    public class MethodParameters
    {
        public string Calldata { get; set; }
        public string Value { get; set; }
    }

    // Common options for adding liquidity (mint or increase).
    public abstract class AddLiquidityOptions
    {
        public Percent SlippageTolerance { get; set; }
        public BigInteger Deadline { get; set; }

        // Whether to spend ether. If set, one of the pool tokens must be WETH.
        public NativeCurrency? UseNative { get; set; }

        // Optional permit parameters for spending token0 / token1 (SelfPermit options).
        public object? Token0Permit { get; set; }
        public object? Token1Permit { get; set; }
    }

    public class MintOptions : AddLiquidityOptions
    {
        // The account that should receive the minted NFT.
        public string Recipient { get; set; }

        // Creates the pool if not initialized before mint.
        public bool CreatePool { get; set; }
    }

    public class IncreaseOptions : AddLiquidityOptions
    {
        // The ID of the position to increase liquidity for.
        public BigInteger TokenId { get; set; }
    }

    private NonfungiblePositionManager() { }

    private static string EncodeCreate(Pool pool)
    {
        return EncodeFunctionData("createAndInitializePoolIfNecessary(address,address,uint24,uint160)",
            new ABIValue("address", pool.Token0.Address),
            new ABIValue("address", pool.Token1.Address),
            new ABIValue("uint24", (int)pool.Fee),
            new ABIValue("uint160", pool.SqrtRatioX96));
    }

    public static MethodParameters CreateCallParameters(Pool pool)
    {
        return new MethodParameters
        {
            Calldata = EncodeCreate(pool),
            Value = Utilities.ToHex(BigInteger.Zero)
        };
    }

    public static MethodParameters AddCallParameters(Position position, AddLiquidityOptions options)
    {
        if (position.Liquidity <= BigInteger.Zero)
        {
            throw new InvalidOperationException("ZERO_LIQUIDITY");
        }

        var calldatas = new List<string>();

        var (amount0Desired, amount1Desired) = position.MintAmounts;
        var (amount0Min, amount1Min) = position.MintAmountsWithSlippage(options.SlippageTolerance);

        // create pool if needed
        if (options is MintOptions { CreatePool: true })
        {
            calldatas.Add(EncodeCreate(position.Pool));
        }

        // permits if necessary
        if (options.Token0Permit != null)
        {
            calldatas.Add(SelfPermit.EncodePermit(position.Pool.Token0, options.Token0Permit));
        }
        if (options.Token1Permit != null)
        {
            calldatas.Add(SelfPermit.EncodePermit(position.Pool.Token1, options.Token1Permit));
        }

        if (options is MintOptions mintOptions)
        {
            string recipient = AddressValidator.ValidateAndParseAddress(mintOptions.Recipient);

            // mint((address,address,uint24,int24,int24,uint256,uint256,uint256,uint256,address,uint256))
            calldatas.Add(EncodeFunctionData(
                "mint((address,address,uint24,int24,int24,uint256,uint256,uint256,uint256,address,uint256))",
                new ABIValue("address", position.Pool.Token0.Address),
                new ABIValue("address", position.Pool.Token1.Address),
                new ABIValue("uint24", (int)position.Pool.Fee),
                new ABIValue("int24", position.TickLower),
                new ABIValue("int24", position.TickUpper),
                new ABIValue("uint256", amount0Desired),
                new ABIValue("uint256", amount1Desired),
                new ABIValue("uint256", amount0Min),
                new ABIValue("uint256", amount1Min),
                new ABIValue("address", recipient),
                new ABIValue("uint256", options.Deadline)));
        }
        else
        {
            var increaseOptions = (IncreaseOptions)options;

            // increaseLiquidity((uint256,uint256,uint256,uint256,uint256,uint256))
            calldatas.Add(EncodeFunctionData(
                "increaseLiquidity((uint256,uint256,uint256,uint256,uint256,uint256))",
                new ABIValue("uint256", increaseOptions.TokenId),
                new ABIValue("uint256", amount0Desired),
                new ABIValue("uint256", amount1Desired),
                new ABIValue("uint256", amount0Min),
                new ABIValue("uint256", amount1Min),
                new ABIValue("uint256", options.Deadline)));
        }

        BigInteger value = BigInteger.Zero;

        if (options.UseNative != null)
        {
            Token wrapped = options.UseNative.Wrapped();
            if (!position.Pool.Token0.Equals(wrapped) && !position.Pool.Token1.Equals(wrapped))
            {
                throw new InvalidOperationException("NO_WETH");
            }

            BigInteger wrappedValue = position.Pool.Token0.Equals(wrapped) ? amount0Desired : amount1Desired;

            // we only need to refund if we're actually sending ETH
            if (wrappedValue > BigInteger.Zero)
            {
                calldatas.Add(Payments.EncodeRefundETH());
            }

            value = wrappedValue;
        }

        return new MethodParameters
        {
            Calldata = Multicall.EncodeMulticall(calldatas),
            Value = Utilities.ToHex(value)
        };
    }
}
