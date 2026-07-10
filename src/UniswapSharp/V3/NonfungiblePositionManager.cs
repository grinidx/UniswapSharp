using System.Numerics;
using Nethereum.ABI;
using Nethereum.Hex.HexConvertors.Extensions;
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

    private static readonly BigInteger MaxUint128 = BigInteger.Pow(2, 128) - BigInteger.One;

    public class CollectOptions
    {
        public BigInteger TokenId { get; set; }
        public CurrencyAmount<BaseCurrency> ExpectedCurrencyOwed0 { get; set; }
        public CurrencyAmount<BaseCurrency> ExpectedCurrencyOwed1 { get; set; }
        public string Recipient { get; set; }
    }

    public class NFTPermitOptions
    {
        public byte V { get; set; }
        public string R { get; set; }
        public string S { get; set; }
        public BigInteger Deadline { get; set; }
        public string Spender { get; set; }
    }

    public class RemoveLiquidityOptions
    {
        public BigInteger TokenId { get; set; }
        public Percent LiquidityPercentage { get; set; }
        public Percent SlippageTolerance { get; set; }
        public BigInteger Deadline { get; set; }
        public bool BurnToken { get; set; }
        public NFTPermitOptions? Permit { get; set; }
        public CollectOptions CollectOptions { get; set; }
    }

    private static List<string> EncodeCollect(CollectOptions options)
    {
        var calldatas = new List<string>();

        BigInteger tokenId = options.TokenId;
        bool involvesETH = options.ExpectedCurrencyOwed0.Currency.IsNative || options.ExpectedCurrencyOwed1.Currency.IsNative;
        string recipient = AddressValidator.ValidateAndParseAddress(options.Recipient);

        // collect((uint256,address,uint128,uint128))
        calldatas.Add(EncodeFunctionData("collect((uint256,address,uint128,uint128))",
            new ABIValue("uint256", tokenId),
            new ABIValue("address", involvesETH ? Constants.ADDRESS_ZERO : recipient),
            new ABIValue("uint128", MaxUint128),
            new ABIValue("uint128", MaxUint128)));

        if (involvesETH)
        {
            BigInteger ethAmount = options.ExpectedCurrencyOwed0.Currency.IsNative
                ? options.ExpectedCurrencyOwed0.Quotient
                : options.ExpectedCurrencyOwed1.Quotient;
            Token token = (Token)(options.ExpectedCurrencyOwed0.Currency.IsNative
                ? options.ExpectedCurrencyOwed1.Currency
                : options.ExpectedCurrencyOwed0.Currency);
            BigInteger tokenAmount = options.ExpectedCurrencyOwed0.Currency.IsNative
                ? options.ExpectedCurrencyOwed1.Quotient
                : options.ExpectedCurrencyOwed0.Quotient;

            calldatas.Add(Payments.EncodeUnwrapWETH9(ethAmount, recipient));
            calldatas.Add(Payments.EncodeSweepToken(token, tokenAmount, recipient));
        }

        return calldatas;
    }

    public static MethodParameters CollectCallParameters(CollectOptions options)
    {
        var calldatas = EncodeCollect(options);

        return new MethodParameters
        {
            Calldata = Multicall.EncodeMulticall(calldatas),
            Value = Utilities.ToHex(BigInteger.Zero)
        };
    }

    public static MethodParameters RemoveCallParameters(Position position, RemoveLiquidityOptions options)
    {
        var calldatas = new List<string>();

        BigInteger deadline = options.Deadline;
        BigInteger tokenId = options.TokenId;

        // construct a partial position with a percentage of liquidity
        BigInteger partialLiquidity = options.LiquidityPercentage.Multiply(position.Liquidity).Quotient;
        var partialPosition = new Position(position.Pool, position.TickLower, position.TickUpper, partialLiquidity);
        if (partialPosition.Liquidity <= BigInteger.Zero)
        {
            throw new InvalidOperationException("ZERO_LIQUIDITY");
        }

        // slippage-adjusted underlying amounts
        var (amount0Min, amount1Min) = partialPosition.BurnAmountsWithSlippage(options.SlippageTolerance);

        if (options.Permit != null)
        {
            calldatas.Add(EncodeFunctionData("permit(address,uint256,uint256,uint8,bytes32,bytes32)",
                new ABIValue("address", AddressValidator.ValidateAndParseAddress(options.Permit.Spender)),
                new ABIValue("uint256", tokenId),
                new ABIValue("uint256", options.Permit.Deadline),
                new ABIValue("uint8", options.Permit.V),
                new ABIValue("bytes32", options.Permit.R.HexToByteArray()),
                new ABIValue("bytes32", options.Permit.S.HexToByteArray())));
        }

        // remove liquidity: decreaseLiquidity((uint256,uint128,uint256,uint256,uint256))
        calldatas.Add(EncodeFunctionData("decreaseLiquidity((uint256,uint128,uint256,uint256,uint256))",
            new ABIValue("uint256", tokenId),
            new ABIValue("uint128", partialPosition.Liquidity),
            new ABIValue("uint256", amount0Min),
            new ABIValue("uint256", amount1Min),
            new ABIValue("uint256", deadline)));

        var collectOpts = options.CollectOptions;
        var owed0 = collectOpts.ExpectedCurrencyOwed0;
        var owed1 = collectOpts.ExpectedCurrencyOwed1;
        calldatas.AddRange(EncodeCollect(new CollectOptions
        {
            TokenId = tokenId,
            // add the underlying value to the expected currency already owed
            ExpectedCurrencyOwed0 = owed0.Add(CurrencyAmount<BaseCurrency>.FromRawAmount(owed0.Currency, amount0Min)),
            ExpectedCurrencyOwed1 = owed1.Add(CurrencyAmount<BaseCurrency>.FromRawAmount(owed1.Currency, amount1Min)),
            Recipient = collectOpts.Recipient
        }));

        if (options.LiquidityPercentage.Equals(new Percent(1)))
        {
            if (options.BurnToken)
            {
                calldatas.Add(EncodeFunctionData("burn(uint256)", new ABIValue("uint256", tokenId)));
            }
        }
        else
        {
            if (options.BurnToken)
            {
                throw new InvalidOperationException("CANNOT_BURN");
            }
        }

        return new MethodParameters
        {
            Calldata = Multicall.EncodeMulticall(calldatas),
            Value = Utilities.ToHex(BigInteger.Zero)
        };
    }

    public class SafeTransferOptions
    {
        public string Sender { get; set; }
        public string Recipient { get; set; }
        public BigInteger TokenId { get; set; }
        public string? Data { get; set; }
    }

    public static MethodParameters SafeTransferFromParameters(SafeTransferOptions options)
    {
        string recipient = AddressValidator.ValidateAndParseAddress(options.Recipient);
        string sender = AddressValidator.ValidateAndParseAddress(options.Sender);

        string calldata;
        if (options.Data != null)
        {
            calldata = EncodeFunctionData("safeTransferFrom(address,address,uint256,bytes)",
                new ABIValue("address", sender),
                new ABIValue("address", recipient),
                new ABIValue("uint256", options.TokenId),
                new ABIValue("bytes", options.Data.HexToByteArray()));
        }
        else
        {
            calldata = EncodeFunctionData("safeTransferFrom(address,address,uint256)",
                new ABIValue("address", sender),
                new ABIValue("address", recipient),
                new ABIValue("uint256", options.TokenId));
        }

        return new MethodParameters
        {
            Calldata = calldata,
            Value = Utilities.ToHex(BigInteger.Zero)
        };
    }

    public class TypedDataField
    {
        public string Name { get; set; }
        public string Type { get; set; }
    }

    public class TypedDataDomain
    {
        public string Name { get; set; }
        public int ChainId { get; set; }
        public string Version { get; set; }
        public string VerifyingContract { get; set; }
    }

    public class NFTPermitValues
    {
        public string Spender { get; set; }
        public BigInteger TokenId { get; set; }
        public BigInteger Deadline { get; set; }
        public BigInteger Nonce { get; set; }
    }

    public class NFTPermitData
    {
        public TypedDataDomain Domain { get; set; }
        public Dictionary<string, List<TypedDataField>> Types { get; set; }
        public NFTPermitValues Values { get; set; }
    }

    private static Dictionary<string, List<TypedDataField>> NftPermitTypes() => new()
    {
        ["Permit"] = new List<TypedDataField>
        {
            new() { Name = "spender", Type = "address" },
            new() { Name = "tokenId", Type = "uint256" },
            new() { Name = "nonce", Type = "uint256" },
            new() { Name = "deadline", Type = "uint256" },
        }
    };

    // Prepare the params for an EIP-712 signTypedData request.
    public static NFTPermitData GetPermitData(NFTPermitValues permit, string positionManagerAddress, int chainId)
    {
        return new NFTPermitData
        {
            Domain = new TypedDataDomain
            {
                Name = "Uniswap V3 Positions NFT-V1",
                ChainId = chainId,
                Version = "1",
                VerifyingContract = positionManagerAddress
            },
            Types = NftPermitTypes(),
            Values = permit
        };
    }
}
