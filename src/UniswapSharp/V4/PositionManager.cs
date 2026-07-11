using System.Numerics;
using UniswapSharp.Core.Entities;
using UniswapSharp.Core.Entities.Fractions;
using UniswapSharp.Core.Utils;
using UniswapSharp.V3;
using UniswapSharp.V3.Utils;
using UniswapSharp.V4.Entities;
using UniswapSharp.V4.Utils;
using static UniswapSharp.V3.Utils.AbiFunctionEncoder;

namespace UniswapSharp.V4;

/// <summary>Generated method parameters for executing a call. Ported from v4-sdk/src/utils/calldata.ts.</summary>
public sealed class MethodParameters
{
    /// <summary>The hex-encoded calldata to perform the given operation.</summary>
    public required string Calldata { get; init; }

    /// <summary>The amount of ether (wei) to send, in hex.</summary>
    public required string Value { get; init; }
}

/// <summary>Options common to every PositionManager call.</summary>
public abstract class CommonOptions
{
    /// <summary>How much the pool price is allowed to move from the specified action.</summary>
    public required Percent SlippageTolerance { get; init; }

    /// <summary>Optional data to pass to hooks.</summary>
    public string? HookData { get; init; }

    /// <summary>When the transaction expires, in epoch seconds.</summary>
    public required BigInteger Deadline { get; init; }
}

/// <summary>
/// Options for producing the calldata to add liquidity (mint or increase). Merges upstream
/// <c>CommonAddLiquidityOptions</c> into the base of the add-liquidity hierarchy.
/// </summary>
public abstract class AddLiquidityOptions : CommonOptions
{
    /// <summary>Whether to spend ether. If set, one of the currencies must be the native currency.</summary>
    public NativeCurrency? UseNative { get; init; }

    /// <summary>The optional Permit2 batch-permit parameters for spending token0 and token1.</summary>
    public BatchPermitOptions? BatchPermit { get; init; }
}

/// <summary>Options for minting a new position.</summary>
public sealed class MintOptions : AddLiquidityOptions
{
    /// <summary>The account that should receive the minted NFT.</summary>
    public required string Recipient { get; init; }

    /// <summary>Creates the pool if not initialized before mint.</summary>
    public bool? CreatePool { get; init; }

    /// <summary>Initial price to set on the pool if creating.</summary>
    public BigInteger? SqrtPriceX96 { get; init; }

    /// <summary>Whether the mint is part of a migration from V3 to V4.</summary>
    public bool? Migrate { get; init; }
}

/// <summary>Options for increasing liquidity on an existing position.</summary>
public sealed class IncreaseLiquidityOptions : AddLiquidityOptions
{
    /// <summary>Indicates the ID of the position to increase liquidity for.</summary>
    public required BigInteger TokenId { get; init; }
}

/// <summary>Options for producing the calldata to exit a position.</summary>
public sealed class RemoveLiquidityOptions : CommonOptions
{
    /// <summary>Indicates the ID of the position to decrease liquidity for.</summary>
    public required BigInteger TokenId { get; init; }

    /// <summary>The percentage of position liquidity to exit.</summary>
    public required Percent LiquidityPercentage { get; init; }

    /// <summary>Whether the NFT should be burned if the entire position is being exited (default false).</summary>
    public bool? BurnToken { get; init; }

    /// <summary>
    /// The optional permit of the token ID being exited, in case the exit transaction is being sent
    /// by an account that does not own the NFT.
    /// </summary>
    public NFTPermitOptions? Permit { get; init; }
}

/// <summary>Options for collecting fees from a position.</summary>
public sealed class CollectOptions : CommonOptions
{
    /// <summary>Indicates the ID of the position to collect for.</summary>
    public required BigInteger TokenId { get; init; }

    /// <summary>The account that should receive the tokens.</summary>
    public required string Recipient { get; init; }
}

/// <summary>The Permit2 batch-permit parameters for spending token0 and token1.</summary>
public sealed class BatchPermitOptions
{
    public required string Owner { get; init; }
    public required AllowanceTransferPermitBatch PermitBatch { get; init; }
    public required string Signature { get; init; }
}

/// <summary>The values signed by an ERC721 permit for the position NFT.</summary>
public class NFTPermitValues
{
    public required string Spender { get; init; }
    public required BigInteger TokenId { get; init; }
    public required BigInteger Deadline { get; init; }
    public required BigInteger Nonce { get; init; }
}

/// <summary>An ERC721 permit, i.e. its <see cref="NFTPermitValues"/> plus the signature.</summary>
public sealed class NFTPermitOptions : NFTPermitValues
{
    public required string Signature { get; init; }
}

/// <summary>One named field of an EIP-712 type.</summary>
public sealed class TypedDataField
{
    public required string Name { get; init; }
    public required string Type { get; init; }
}

/// <summary>The EIP-712 domain for the V4 position NFT permit (no version field, matching upstream).</summary>
public sealed class TypedDataDomain
{
    public required string Name { get; init; }
    public required int ChainId { get; init; }
    public required string VerifyingContract { get; init; }
}

/// <summary>The bundle needed for an EIP-712 <c>signTypedData</c> request for a position-NFT permit.</summary>
public sealed class NFTPermitData
{
    public required TypedDataDomain Domain { get; init; }
    public required Dictionary<string, List<TypedDataField>> Types { get; init; }
    public required NFTPermitValues Values { get; init; }
}

/// <summary>
/// Static calldata builders for the Uniswap V4 <c>PositionManager</c> contract. Ported from
/// v4-sdk/src/PositionManager.ts (named <c>V4PositionManager</c> here to avoid clashing with the V3
/// <see cref="UniswapSharp.V3.NonfungiblePositionManager"/>). The upstream ABI JSON is not ported;
/// the four function encoders build their selectors from the canonical signatures the same way the
/// V3 periphery builders do.
/// </summary>
public static class V4PositionManager
{
    private const string INITIALIZE_POOL_SIGNATURE = "initializePool((address,address,uint24,int24,address),uint160)";
    private const string MODIFY_LIQUIDITIES_SIGNATURE = "modifyLiquidities(bytes,uint256)";

    private static readonly Dictionary<string, List<TypedDataField>> NftPermitTypes = new()
    {
        ["Permit"] = new List<TypedDataField>
        {
            new() { Name = "spender", Type = "address" },
            new() { Name = "tokenId", Type = "uint256" },
            new() { Name = "nonce", Type = "uint256" },
            new() { Name = "deadline", Type = "uint256" },
        },
    };

    /// <summary>Encodes the calldata to initialize a pool.</summary>
    public static MethodParameters CreateCallParameters(PoolKey poolKey, BigInteger sqrtPriceX96) =>
        new()
        {
            Calldata = EncodeInitializePool(poolKey, sqrtPriceX96),
            Value = Utilities.ToHex(Constants.ZERO),
        };

    /// <summary>Encodes the calldata to add liquidity to a position (mint or increase).</summary>
    public static MethodParameters AddCallParameters(Position position, AddLiquidityOptions options)
    {
        if (!(position.Liquidity > Constants.ZERO))
        {
            throw new InvalidOperationException(Constants.ZERO_LIQUIDITY);
        }

        var calldataList = new List<string>();
        var planner = new V4PositionPlanner();

        bool isMint = options is MintOptions;
        var mintOptions = options as MintOptions;

        // Encode initialize pool. No planner used here because initializePool is not an Action.
        if (isMint && ShouldCreatePool(mintOptions!))
        {
            calldataList.Add(EncodeInitializePool(position.Pool.PoolKey, mintOptions!.SqrtPriceX96!.Value));
        }

        // position.pool.currency0 is native if and only if options.useNative is set.
        bool nativeOk =
            (options.UseNative is not null && position.Pool.Currency0.Equals(options.UseNative)) ||
            (!position.Pool.Currency0.IsNative && options.UseNative is null);
        if (!nativeOk)
        {
            throw new InvalidOperationException(Constants.NATIVE_NOT_SET);
        }

        // adjust for slippage
        var (amount0Max, amount1Max) = position.MintAmountsWithSlippage(options.SlippageTolerance);

        // We use permit2 to approve tokens to the position manager.
        if (options.BatchPermit is not null)
        {
            calldataList.Add(EncodePermitBatch(
                options.BatchPermit.Owner,
                options.BatchPermit.PermitBatch,
                options.BatchPermit.Signature));
        }

        // mint
        if (isMint)
        {
            string recipient = AddressValidator.ValidateAndParseAddress(mintOptions!.Recipient);
            planner.AddMint(
                position.Pool,
                position.TickLower,
                position.TickUpper,
                position.Liquidity,
                amount0Max,
                amount1Max,
                recipient,
                options.HookData ?? Constants.EMPTY_BYTES);
        }
        else
        {
            // increase
            var increase = (IncreaseLiquidityOptions)options;
            planner.AddIncrease(
                increase.TokenId,
                position.Liquidity,
                amount0Max,
                amount1Max,
                options.HookData ?? Constants.EMPTY_BYTES);
        }

        string value = Utilities.ToHex(Constants.ZERO);

        // If migrating, we need to settle and sweep both currencies individually.
        if (isMint && mintOptions!.Migrate == true)
        {
            if (options.UseNative is not null)
            {
                // unwrap the exact amount needed to send to the pool manager
                planner.AddUnwrap(Constants.OPEN_DELTA);
            }
            // payer is v4 position manager
            planner.AddSettle(position.Pool.Currency0, false);
            planner.AddSettle(position.Pool.Currency1, false);
            // sweep any leftover wrapped native that was not unwrapped;
            // recipient will be the same as the v4 lp token recipient
            planner.AddSweep(
                options.UseNative is not null ? position.Pool.Currency0.Wrapped() : position.Pool.Currency0,
                mintOptions.Recipient);
            planner.AddSweep(position.Pool.Currency1, mintOptions.Recipient);
        }
        else
        {
            if (isMint)
            {
                // Mint: the user can never be owed a token when minting (delta is always >= 0), so SETTLE_PAIR is safe.
                planner.AddSettlePair(position.Pool.Currency0, position.Pool.Currency1);
            }
            else
            {
                // Increase: use CLOSE_CURRENCY instead of SETTLE_PAIR because accrued fees on existing
                // positions can flip the delta positive on one side, causing SETTLE_PAIR to revert.
                planner.AddCloseCurrency(position.Pool.Currency0);
                planner.AddCloseCurrency(position.Pool.Currency1);
            }
            if (options.UseNative is not null)
            {
                // Any sweeping must happen after the settling. Native currency is always currency0 in v4.
                value = Utilities.ToHex(amount0Max);
                planner.AddSweep(position.Pool.Currency0, ActionConstants.MSG_SENDER);
            }
        }

        calldataList.Add(EncodeModifyLiquidities(planner.Finalize(), options.Deadline));

        return new MethodParameters
        {
            Calldata = Multicall.EncodeMulticall(calldataList),
            Value = value,
        };
    }

    /// <summary>Produces the calldata for completely or partially exiting a position.</summary>
    public static MethodParameters RemoveCallParameters(Position position, RemoveLiquidityOptions options)
    {
        var calldataList = new List<string>();
        var planner = new V4PositionPlanner();

        BigInteger tokenId = options.TokenId;

        if (options.BurnToken == true)
        {
            // if burnToken is true, the specified liquidity percentage must be 100%
            if (!options.LiquidityPercentage.Equals(new Percent(Constants.ONE)))
            {
                throw new InvalidOperationException(Constants.CANNOT_BURN);
            }

            // if there is a permit, encode the ERC721Permit permit call
            if (options.Permit is not null)
            {
                calldataList.Add(EncodeERC721Permit(
                    options.Permit.Spender,
                    options.Permit.TokenId,
                    options.Permit.Deadline,
                    options.Permit.Nonce,
                    options.Permit.Signature));
            }

            // slippage-adjusted amounts derived from current position liquidity
            var (amount0Min, amount1Min) = position.BurnAmountsWithSlippage(options.SlippageTolerance);
            planner.AddBurn(tokenId, amount0Min, amount1Min, options.HookData ?? Constants.EMPTY_BYTES);
        }
        else
        {
            // construct a partial position with a percentage of liquidity
            var partialPosition = new Position(
                position.Pool,
                options.LiquidityPercentage.Multiply(position.Liquidity).Quotient,
                position.TickLower,
                position.TickUpper);

            // If the partial position has liquidity=0, this is a collect call and collectCallParameters should be used.
            if (!(partialPosition.Liquidity > Constants.ZERO))
            {
                throw new InvalidOperationException(Constants.ZERO_LIQUIDITY);
            }

            // slippage-adjusted underlying amounts
            var (amount0Min, amount1Min) = partialPosition.BurnAmountsWithSlippage(options.SlippageTolerance);

            planner.AddDecrease(
                tokenId,
                partialPosition.Liquidity,
                amount0Min,
                amount1Min,
                options.HookData ?? Constants.EMPTY_BYTES);
        }

        planner.AddTakePair(position.Pool.Currency0, position.Pool.Currency1, ActionConstants.MSG_SENDER);

        calldataList.Add(EncodeModifyLiquidities(planner.Finalize(), options.Deadline));

        return new MethodParameters
        {
            Calldata = Multicall.EncodeMulticall(calldataList),
            Value = Utilities.ToHex(Constants.ZERO),
        };
    }

    /// <summary>Produces the calldata for collecting fees from a position.</summary>
    public static MethodParameters CollectCallParameters(Position position, CollectOptions options)
    {
        var calldataList = new List<string>();
        var planner = new V4PositionPlanner();

        BigInteger tokenId = options.TokenId;
        string recipient = AddressValidator.ValidateAndParseAddress(options.Recipient);

        // To collect fees in V4 we encode a decrease-liquidity by 0 and then a TAKE_PAIR.
        planner.AddDecrease(tokenId, Constants.ZERO, Constants.ZERO, Constants.ZERO, options.HookData ?? Constants.EMPTY_BYTES);
        planner.AddTakePair(position.Pool.Currency0, position.Pool.Currency1, recipient);

        calldataList.Add(EncodeModifyLiquidities(planner.Finalize(), options.Deadline));

        return new MethodParameters
        {
            Calldata = Multicall.EncodeMulticall(calldataList),
            Value = Utilities.ToHex(Constants.ZERO),
        };
    }

    /// <summary>Initialize a pool. <c>initializePool((address,address,uint24,int24,address),uint160)</c>.</summary>
    private static string EncodeInitializePool(PoolKey poolKey, BigInteger sqrtPriceX96)
    {
        string encoded = AbiParamEncoder.Encode(
            new[] { "(address,address,uint24,int24,address)", "uint160" },
            new object?[]
            {
                new object?[] { poolKey.Currency0, poolKey.Currency1, poolKey.Fee, poolKey.TickSpacing, poolKey.Hooks },
                sqrtPriceX96,
            });
        return "0x" + Selector(INITIALIZE_POOL_SIGNATURE) + encoded[2..];
    }

    /// <summary>Encode a modify-liquidities call. <c>modifyLiquidities(bytes,uint256)</c>.</summary>
    public static string EncodeModifyLiquidities(string unlockData, BigInteger deadline)
    {
        string encoded = AbiParamEncoder.Encode(
            new[] { "bytes", "uint256" },
            new object?[] { unlockData, deadline });
        return "0x" + Selector(MODIFY_LIQUIDITIES_SIGNATURE) + encoded[2..];
    }

    /// <summary>Encode a permit-batch call (selector <c>0x002a3e3a</c>, inherited from PermitForwarder).</summary>
    public static string EncodePermitBatch(string owner, AllowanceTransferPermitBatch permitBatch, string signature)
    {
        object?[] details = permitBatch.Details
            .Select(d => (object?)new object?[] { d.Token, d.Amount, d.Expiration, d.Nonce })
            .ToArray();

        string encoded = AbiParamEncoder.Encode(
            new[] { "address", "((address,uint160,uint48,uint48)[],address,uint256)", "bytes" },
            new object?[]
            {
                owner,
                new object?[] { details, permitBatch.Spender, permitBatch.SigDeadline },
                signature,
            });
        return PositionFunctions.PERMIT_BATCH + encoded[2..];
    }

    /// <summary>Encode an ERC721Permit permit call (selector <c>0x0f5730f1</c>, inherited from ERC721Permit).</summary>
    public static string EncodeERC721Permit(
        string spender,
        BigInteger tokenId,
        BigInteger deadline,
        BigInteger nonce,
        string signature)
    {
        string encoded = AbiParamEncoder.Encode(
            new[] { "address", "uint256", "uint256", "uint256", "bytes" },
            new object?[] { spender, tokenId, deadline, nonce, signature });
        return PositionFunctions.ERC721PERMIT_PERMIT + encoded[2..];
    }

    /// <summary>Prepare the params for an EIP-712 <c>signTypedData</c> request. No signing is performed.</summary>
    public static NFTPermitData GetPermitData(NFTPermitValues permit, string positionManagerAddress, int chainId) =>
        new()
        {
            Domain = new TypedDataDomain
            {
                Name = "Uniswap V4 Positions NFT",
                ChainId = chainId,
                VerifyingContract = positionManagerAddress,
            },
            Types = NftPermitTypes,
            Values = permit,
        };

    private static bool ShouldCreatePool(MintOptions options)
    {
        if (options.CreatePool == true)
        {
            if (options.SqrtPriceX96 is null)
            {
                throw new InvalidOperationException(Constants.NO_SQRT_PRICE);
            }
            return true;
        }
        return false;
    }
}
