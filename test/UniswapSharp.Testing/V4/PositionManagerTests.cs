using System.Numerics;
using Nethereum.Util;
using UniswapSharp.Core.Entities;
using UniswapSharp.Core.Entities.Fractions;
using UniswapSharp.V3.Utils;
using UniswapSharp.V4;
using UniswapSharp.V4.Utils;
using Constants = UniswapSharp.V4.Constants;
using Multicall = UniswapSharp.V3.Multicall;
using Pool = UniswapSharp.V4.Entities.Pool;
using PoolKey = UniswapSharp.V4.Entities.PoolKey;
using Position = UniswapSharp.V4.Entities.Position;
using Tick = UniswapSharp.V3.Entities.Tick;

namespace UniswapSharp.Testing.V4;

// Ported 1:1 from sdks/v4-sdk/src/PositionManager.test.ts.
// Pool/Position/PoolKey/Tick are aliased so the clashing V3 Entities namespace is not imported.
public class PositionManagerTests
{
    private static readonly Token Currency0 =
        new(1, "0x0000000000000000000000000000000000000001", 18, "t0", "currency0");

    private static readonly Token Currency1 =
        new(1, "0x0000000000000000000000000000000000000002", 18, "t1", "currency1");

    private static readonly Ether CurrencyNative = Ether.OnChain(1);

    private const int Fee = Constants.FEE_AMOUNT_MEDIUM; // MEDIUM
    private const int TickSpacing = 60; // for MEDIUM

    private static readonly BigInteger SqrtPrice11 = Constants.SQRT_PRICE_1_1;

    private static readonly PoolKey PoolKey01 =
        Pool.GetPoolKey(Currency0, Currency1, Fee, TickSpacing, Constants.EMPTY_HOOK);

    private static Pool Pool01() =>
        new(Currency0, Currency1, Fee, TickSpacing, Constants.EMPTY_HOOK, SqrtPrice11, 0, 0, new List<Tick>());

    private static Pool Pool1Eth() =>
        new(CurrencyNative, Currency1, Fee, TickSpacing, Constants.EMPTY_HOOK, SqrtPrice11, 0, 0, new List<Tick>());

    private const int TokenId = 1;
    private static readonly Percent SlippageTolerance = new(1, 100);
    private const int Deadline = 123;

    private const string MockOwner = "0x000000000000000000000000000000000000000a";
    private const string MockSpender = "0x000000000000000000000000000000000000000b";
    private const string Recipient = "0x000000000000000000000000000000000000000c";
    private const string MockBytes32 = "0x0000000000000000000000000000000000000000000000000000000000000000";

    // ---- helpers ----

    private static object?[] PoolKeyTuple(PoolKey pk) =>
        new object?[] { pk.Currency0, pk.Currency1, pk.Fee, pk.TickSpacing, pk.Hooks };

    private static string ToAddr(BaseCurrency currency) => CurrencyMap.ToAddress(currency);

    private static string ToHex(BigInteger value) => Utilities.ToHex(value);

    // ================= #createCallParameters =================

    [Fact]
    public void CreateCallParameters_Succeeds()
    {
        var result = V4PositionManager.CreateCallParameters(PoolKey01, SqrtPrice11);

        Assert.Equal(
            "0xf7020405000000000000000000000000000000000000000000000000000000000000000100000000000000000000000000000000000000000000000000000000000000020000000000000000000000000000000000000000000000000000000000000bb8000000000000000000000000000000000000000000000000000000000000003c00000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000001000000000000000000000000",
            result.Calldata);
        Assert.Equal("0x00", result.Value);
    }

    [Fact]
    public void CreateCallParameters_SucceedsWithNonzeroHook()
    {
        const string hook = "0x1100000000000000000000000000000000002401";
        PoolKey poolKey = Pool.GetPoolKey(Currency0, Currency1, Fee, TickSpacing, hook);

        var result = V4PositionManager.CreateCallParameters(poolKey, SqrtPrice11);

        Assert.Equal(
            "0xf7020405000000000000000000000000000000000000000000000000000000000000000100000000000000000000000000000000000000000000000000000000000000020000000000000000000000000000000000000000000000000000000000000bb8000000000000000000000000000000000000000000000000000000000000003c00000000000000000000000011000000000000000000000000000000000024010000000000000000000000000000000000000001000000000000000000000000",
            result.Calldata);
        Assert.Equal("0x00", result.Value);
    }

    // ================= #addCallParameters =================

    [Fact]
    public void AddCallParameters_ThrowsIfLiquidityIsZero()
    {
        var position = new Position(Pool01(), 0, -TickSpacing, TickSpacing);
        Assert.Equal("ZERO_LIQUIDITY", Assert.Throws<InvalidOperationException>(() =>
            V4PositionManager.AddCallParameters(position, new MintOptions
            {
                Recipient = Recipient,
                SlippageTolerance = SlippageTolerance,
                Deadline = Deadline,
            })).Message);
    }

    [Fact]
    public void AddCallParameters_ThrowsIfPoolNotEtherButUseNativeSet()
    {
        var position = new Position(Pool01(), 8888888, -TickSpacing, TickSpacing);
        Assert.Equal(Constants.NATIVE_NOT_SET, Assert.Throws<InvalidOperationException>(() =>
            V4PositionManager.AddCallParameters(position, new MintOptions
            {
                Recipient = Recipient,
                SlippageTolerance = SlippageTolerance,
                Deadline = Deadline,
                UseNative = CurrencyNative,
            })).Message);
    }

    [Fact]
    public void AddCallParameters_ThrowsIfPoolEtherButUseNativeNotSet()
    {
        var position = new Position(Pool1Eth(), 8888888, -TickSpacing, TickSpacing);
        Assert.Equal(Constants.NATIVE_NOT_SET, Assert.Throws<InvalidOperationException>(() =>
            V4PositionManager.AddCallParameters(position, new MintOptions
            {
                Recipient = Recipient,
                SlippageTolerance = SlippageTolerance,
                Deadline = Deadline,
            })).Message);
    }

    [Fact]
    public void AddCallParameters_ThrowsIfCreatePoolButNoSqrtPrice()
    {
        var position = new Position(Pool01(), 1, -TickSpacing, TickSpacing);
        Assert.Equal("NO_SQRT_PRICE", Assert.Throws<InvalidOperationException>(() =>
            V4PositionManager.AddCallParameters(position, new MintOptions
            {
                CreatePool = true,
                Recipient = Recipient,
                SlippageTolerance = SlippageTolerance,
                Deadline = Deadline,
            })).Message);
    }

    [Fact]
    public void AddCallParameters_SucceedsForMint()
    {
        var pool = Pool01();
        var position = new Position(pool, 5000000, -TickSpacing, TickSpacing);

        var result = V4PositionManager.AddCallParameters(position, new MintOptions
        {
            Recipient = Recipient,
            SlippageTolerance = SlippageTolerance,
            Deadline = Deadline,
        });

        var (amount0Max, amount1Max) = position.MintAmountsWithSlippage(SlippageTolerance);
        var planner = new V4Planner();
        planner.AddAction(Actions.MINT_POSITION, new object?[]
        {
            PoolKeyTuple(pool.PoolKey), -TickSpacing, TickSpacing, 5000000, amount0Max, amount1Max, Recipient, Constants.EMPTY_BYTES,
        });
        planner.AddAction(Actions.SETTLE_PAIR, new object?[] { ToAddr(pool.Currency0), ToAddr(pool.Currency1) });

        Assert.Equal(V4PositionManager.EncodeModifyLiquidities(planner.Finalize(), Deadline), result.Calldata);
        Assert.Equal("0x00", result.Value);
    }

    [Fact]
    public void AddCallParameters_SucceedsForIncrease()
    {
        var pool = Pool01();
        var position = new Position(pool, 666, -TickSpacing, TickSpacing);

        var result = V4PositionManager.AddCallParameters(position, new IncreaseLiquidityOptions
        {
            TokenId = TokenId,
            SlippageTolerance = SlippageTolerance,
            Deadline = Deadline,
        });

        var (amount0Max, amount1Max) = position.MintAmountsWithSlippage(SlippageTolerance);
        var planner = new V4Planner();
        planner.AddAction(Actions.INCREASE_LIQUIDITY, new object?[]
        {
            (BigInteger)TokenId, 666, amount0Max, amount1Max, Constants.EMPTY_BYTES,
        });
        planner.AddAction(Actions.CLOSE_CURRENCY, new object?[] { ToAddr(pool.Currency0) });
        planner.AddAction(Actions.CLOSE_CURRENCY, new object?[] { ToAddr(pool.Currency1) });

        Assert.Equal(V4PositionManager.EncodeModifyLiquidities(planner.Finalize(), Deadline), result.Calldata);
        Assert.Equal("0x00", result.Value);
    }

    [Fact]
    public void AddCallParameters_SucceedsWhenCreatePoolIsTrue()
    {
        var pool = Pool01();
        var position = new Position(pool, 90000000000000, -TickSpacing, TickSpacing);

        var result = V4PositionManager.AddCallParameters(position, new MintOptions
        {
            Recipient = Recipient,
            SlippageTolerance = SlippageTolerance,
            Deadline = Deadline,
            CreatePool = true,
            SqrtPriceX96 = SqrtPrice11,
        });

        var calldataList = Multicall.DecodeMulticall(result.Calldata);
        // Expect initializePool to be called correctly (createCallParameters yields exactly that calldata).
        Assert.Equal(V4PositionManager.CreateCallParameters(pool.PoolKey, SqrtPrice11).Calldata, calldataList[0]);

        var (amount0Max, amount1Max) = position.MintAmountsWithSlippage(SlippageTolerance);
        var planner = new V4Planner();
        planner.AddAction(Actions.MINT_POSITION, new object?[]
        {
            PoolKeyTuple(pool.PoolKey), -TickSpacing, TickSpacing, (BigInteger)90000000000000, amount0Max, amount1Max, Recipient, Constants.EMPTY_BYTES,
        });
        planner.AddAction(Actions.SETTLE_PAIR, new object?[] { ToAddr(pool.Currency0), ToAddr(pool.Currency1) });

        Assert.Equal(V4PositionManager.EncodeModifyLiquidities(planner.Finalize(), Deadline), calldataList[1]);
        Assert.Equal("0x00", result.Value);
    }

    [Fact]
    public void AddCallParameters_SucceedsWhenUseNativeIsSet()
    {
        var pool = Pool1Eth();
        var position = new Position(pool, 1, -TickSpacing, TickSpacing);

        var result = V4PositionManager.AddCallParameters(position, new MintOptions
        {
            Recipient = Recipient,
            SlippageTolerance = SlippageTolerance,
            Deadline = Deadline,
            UseNative = CurrencyNative,
        });

        var (amount0Max, amount1Max) = position.MintAmountsWithSlippage(SlippageTolerance);
        var planner = new V4Planner();
        planner.AddAction(Actions.MINT_POSITION, new object?[]
        {
            PoolKeyTuple(pool.PoolKey), -TickSpacing, TickSpacing, 1, amount0Max, amount1Max, Recipient, Constants.EMPTY_BYTES,
        });
        planner.AddAction(Actions.SETTLE_PAIR, new object?[] { ToAddr(pool.Currency0), ToAddr(pool.Currency1) });
        planner.AddAction(Actions.SWEEP, new object?[] { ToAddr(pool.Currency0), ActionConstants.MSG_SENDER });

        Assert.Equal(V4PositionManager.EncodeModifyLiquidities(planner.Finalize(), Deadline), result.Calldata);
        Assert.Equal(ToHex(amount0Max), result.Value);
    }

    [Fact]
    public void AddCallParameters_SucceedsWhenMigrateIsTrue()
    {
        var pool = Pool01();
        var position = new Position(pool, 1, -TickSpacing, TickSpacing);

        var result = V4PositionManager.AddCallParameters(position, new MintOptions
        {
            Recipient = Recipient,
            SlippageTolerance = SlippageTolerance,
            Deadline = Deadline,
            Migrate = true,
        });

        var (amount0Max, amount1Max) = position.MintAmountsWithSlippage(SlippageTolerance);
        var planner = new V4Planner();
        planner.AddAction(Actions.MINT_POSITION, new object?[]
        {
            PoolKeyTuple(pool.PoolKey), -TickSpacing, TickSpacing, 1, amount0Max, amount1Max, Recipient, Constants.EMPTY_BYTES,
        });
        planner.AddAction(Actions.SETTLE, new object?[] { ToAddr(pool.Currency0), Constants.OPEN_DELTA, false });
        planner.AddAction(Actions.SETTLE, new object?[] { ToAddr(pool.Currency1), Constants.OPEN_DELTA, false });
        planner.AddAction(Actions.SWEEP, new object?[] { ToAddr(pool.Currency0), Recipient });
        planner.AddAction(Actions.SWEEP, new object?[] { ToAddr(pool.Currency1), Recipient });

        Assert.Equal(V4PositionManager.EncodeModifyLiquidities(planner.Finalize(), Deadline), result.Calldata);
        Assert.Equal("0x00", result.Value);
    }

    [Fact]
    public void AddCallParameters_SucceedsWhenMigratingToEthPosition()
    {
        var pool = Pool1Eth();
        var position = new Position(pool, 1, -TickSpacing, TickSpacing);

        var result = V4PositionManager.AddCallParameters(position, new MintOptions
        {
            Recipient = Recipient,
            SlippageTolerance = SlippageTolerance,
            Deadline = Deadline,
            Migrate = true,
            UseNative = CurrencyNative,
        });

        var (amount0Max, amount1Max) = position.MintAmountsWithSlippage(SlippageTolerance);
        var planner = new V4Planner();
        planner.AddAction(Actions.MINT_POSITION, new object?[]
        {
            PoolKeyTuple(pool.PoolKey), -TickSpacing, TickSpacing, 1, amount0Max, amount1Max, Recipient, Constants.EMPTY_BYTES,
        });
        planner.AddAction(Actions.UNWRAP, new object?[] { Constants.OPEN_DELTA });
        planner.AddAction(Actions.SETTLE, new object?[] { ToAddr(pool.Currency0), Constants.OPEN_DELTA, false });
        planner.AddAction(Actions.SETTLE, new object?[] { ToAddr(pool.Currency1), Constants.OPEN_DELTA, false });
        planner.AddAction(Actions.SWEEP, new object?[] { ToAddr(pool.Currency0.Wrapped()), Recipient });
        planner.AddAction(Actions.SWEEP, new object?[] { ToAddr(pool.Currency1), Recipient });

        Assert.Equal(V4PositionManager.EncodeModifyLiquidities(planner.Finalize(), Deadline), result.Calldata);
        Assert.Equal("0x00", result.Value);
    }

    [Fact]
    public void AddCallParameters_SucceedsForBatchPermit()
    {
        var pool = Pool01();
        var position = new Position(pool, 1, -TickSpacing, TickSpacing);

        var batchPermit = new BatchPermitOptions
        {
            Owner = MockOwner,
            PermitBatch = new AllowanceTransferPermitBatch(new List<PermitDetails>(), MockSpender, Deadline),
            Signature = MockBytes32,
        };

        var result = V4PositionManager.AddCallParameters(position, new MintOptions
        {
            Recipient = Recipient,
            SlippageTolerance = SlippageTolerance,
            Deadline = Deadline,
            BatchPermit = batchPermit,
        });

        var calldataList = Multicall.DecodeMulticall(result.Calldata);
        Assert.Equal(
            V4PositionManager.EncodePermitBatch(batchPermit.Owner, batchPermit.PermitBatch, batchPermit.Signature),
            calldataList[0]);

        var (amount0Max, amount1Max) = position.MintAmountsWithSlippage(SlippageTolerance);
        var planner = new V4Planner();
        planner.AddAction(Actions.MINT_POSITION, new object?[]
        {
            PoolKeyTuple(pool.PoolKey), -TickSpacing, TickSpacing, 1, amount0Max, amount1Max, Recipient, Constants.EMPTY_BYTES,
        });
        planner.AddAction(Actions.SETTLE_PAIR, new object?[] { ToAddr(pool.Currency0), ToAddr(pool.Currency1) });

        Assert.Equal(V4PositionManager.EncodeModifyLiquidities(planner.Finalize(), Deadline), calldataList[1]);
        Assert.Equal("0x00", result.Value);
    }

    // ================= #removeCallParameters =================

    private static Position RemovePosition() => new(Pool01(), 100, -TickSpacing, TickSpacing);

    [Fact]
    public void RemoveCallParameters_ThrowsForZeroLiquidity()
    {
        var zeroLiquidityPosition = new Position(Pool01(), 0, -TickSpacing, TickSpacing);
        Assert.Equal(Constants.ZERO_LIQUIDITY, Assert.Throws<InvalidOperationException>(() =>
            V4PositionManager.RemoveCallParameters(zeroLiquidityPosition, new RemoveLiquidityOptions
            {
                TokenId = TokenId,
                LiquidityPercentage = new Percent(1),
                SlippageTolerance = SlippageTolerance,
                Deadline = Deadline,
            })).Message);
    }

    [Fact]
    public void RemoveCallParameters_ThrowsWhenBurnTrueButNot100Percent()
    {
        var fullLiquidityPosition = new Position(Pool01(), 999, -TickSpacing, TickSpacing);
        Assert.Equal(Constants.CANNOT_BURN, Assert.Throws<InvalidOperationException>(() =>
            V4PositionManager.RemoveCallParameters(fullLiquidityPosition, new RemoveLiquidityOptions
            {
                BurnToken = true,
                LiquidityPercentage = new Percent(1, 100),
                TokenId = TokenId,
                SlippageTolerance = SlippageTolerance,
                Deadline = Deadline,
            })).Message);
    }

    [Fact]
    public void RemoveCallParameters_SucceedsForBurn()
    {
        var position = RemovePosition();
        var result = V4PositionManager.RemoveCallParameters(position, new RemoveLiquidityOptions
        {
            BurnToken = true,
            TokenId = TokenId,
            LiquidityPercentage = new Percent(1),
            SlippageTolerance = SlippageTolerance,
            Deadline = Deadline,
        });

        var (amount0Min, amount1Min) = position.BurnAmountsWithSlippage(SlippageTolerance);
        var planner = new V4PositionPlanner();
        planner.AddAction(Actions.BURN_POSITION, new object?[] { (BigInteger)TokenId, amount0Min, amount1Min, Constants.EMPTY_BYTES });
        planner.AddAction(Actions.TAKE_PAIR, new object?[] { ToAddr(Currency0), ToAddr(Currency1), ActionConstants.MSG_SENDER });

        Assert.Equal(V4PositionManager.EncodeModifyLiquidities(planner.Finalize(), Deadline), result.Calldata);
        Assert.Equal("0x00", result.Value);
    }

    [Fact]
    public void RemoveCallParameters_SucceedsForRemovePartialLiquidity()
    {
        var position = RemovePosition();
        var result = V4PositionManager.RemoveCallParameters(position, new RemoveLiquidityOptions
        {
            TokenId = TokenId,
            LiquidityPercentage = new Percent(1, 100),
            SlippageTolerance = SlippageTolerance,
            Deadline = Deadline,
        });

        // remove 1% of 100 = 1
        var (amount0Min, amount1Min) = position.BurnAmountsWithSlippage(SlippageTolerance);
        var planner = new V4Planner();
        planner.AddAction(Actions.DECREASE_LIQUIDITY, new object?[]
        {
            (BigInteger)TokenId, BigInteger.One, amount0Min, amount1Min, Constants.EMPTY_BYTES,
        });
        planner.AddAction(Actions.TAKE_PAIR, new object?[] { ToAddr(Currency0), ToAddr(Currency1), ActionConstants.MSG_SENDER });

        Assert.Equal(V4PositionManager.EncodeModifyLiquidities(planner.Finalize(), Deadline), result.Calldata);
        Assert.Equal("0x00", result.Value);
    }

    [Fact]
    public void RemoveCallParameters_SucceedsForBurnWithPermit()
    {
        var position = RemovePosition();
        var permit = new NFTPermitOptions
        {
            Spender = MockSpender,
            TokenId = TokenId,
            Deadline = Deadline,
            Nonce = 1,
            Signature = "0x00",
        };
        var result = V4PositionManager.RemoveCallParameters(position, new RemoveLiquidityOptions
        {
            BurnToken = true,
            TokenId = TokenId,
            LiquidityPercentage = new Percent(1),
            SlippageTolerance = SlippageTolerance,
            Deadline = Deadline,
            Permit = permit,
        });

        var (amount0Min, amount1Min) = position.BurnAmountsWithSlippage(SlippageTolerance);
        var planner = new V4PositionPlanner();
        planner.AddAction(Actions.BURN_POSITION, new object?[] { (BigInteger)TokenId, amount0Min, amount1Min, Constants.EMPTY_BYTES });
        planner.AddAction(Actions.TAKE_PAIR, new object?[] { ToAddr(Currency0), ToAddr(Currency1), ActionConstants.MSG_SENDER });

        var calldataList = Multicall.DecodeMulticall(result.Calldata);
        Assert.Equal(
            V4PositionManager.EncodeERC721Permit(permit.Spender, permit.TokenId, permit.Deadline, permit.Nonce, permit.Signature),
            calldataList[0]);
        Assert.Equal(V4PositionManager.EncodeModifyLiquidities(planner.Finalize(), Deadline), calldataList[1]);
        Assert.Equal("0x00", result.Value);
    }

    // ================= #collectCallParameters =================

    [Fact]
    public void CollectCallParameters_Succeeds()
    {
        var position = new Position(Pool01(), 100, -TickSpacing, TickSpacing);
        var result = V4PositionManager.CollectCallParameters(position, new CollectOptions
        {
            TokenId = TokenId,
            SlippageTolerance = SlippageTolerance,
            Deadline = Deadline,
            Recipient = Recipient,
        });

        var planner = new V4Planner();
        planner.AddAction(Actions.DECREASE_LIQUIDITY, new object?[]
        {
            (BigInteger)TokenId, BigInteger.Zero, BigInteger.Zero, BigInteger.Zero, Constants.EMPTY_BYTES,
        });
        planner.AddAction(Actions.TAKE_PAIR, new object?[] { ToAddr(Currency0), ToAddr(Currency1), Recipient });

        Assert.Equal(V4PositionManager.EncodeModifyLiquidities(planner.Finalize(), Deadline), result.Calldata);
        Assert.Equal("0x00", result.Value);
    }

    // ================= #getPermitData =================

    [Fact]
    public void GetPermitData_Succeeds()
    {
        var permit = new NFTPermitValues
        {
            Spender = MockSpender,
            TokenId = 1,
            Deadline = 123,
            Nonce = 1,
        };
        var data = V4PositionManager.GetPermitData(permit, MockOwner, 1);

        Assert.Equal("Uniswap V4 Positions NFT", data.Domain.Name);
        Assert.Equal(1, data.Domain.ChainId);
        Assert.Equal(MockOwner, data.Domain.VerifyingContract);

        var expectedTypes = new (string Name, string Type)[]
        {
            ("spender", "address"),
            ("tokenId", "uint256"),
            ("nonce", "uint256"),
            ("deadline", "uint256"),
        };
        var permitFields = data.Types["Permit"];
        Assert.Equal(expectedTypes.Length, permitFields.Count);
        for (int i = 0; i < expectedTypes.Length; i++)
        {
            Assert.Equal(expectedTypes[i].Name, permitFields[i].Name);
            Assert.Equal(expectedTypes[i].Type, permitFields[i].Type);
        }

        Assert.Same(permit, data.Values);

        // Compute the EIP-712 type hash from the encoded type and compare to the reference
        // (github.com/Uniswap/v3-periphery ERC721Permit.sol).
        string encodedType = "Permit(" + string.Join(",", permitFields.Select(f => $"{f.Type} {f.Name}")) + ")";
        string typeHash = Sha3Keccack.Current.CalculateHash(encodedType);
        if (!typeHash.StartsWith("0x"))
        {
            typeHash = "0x" + typeHash;
        }
        Assert.Equal("0x49ecf333e5b8c95c40fdafc95c1ad136e8914a8fb55e9dc8bb01eaa83a2df9ad", typeHash);
    }
}
