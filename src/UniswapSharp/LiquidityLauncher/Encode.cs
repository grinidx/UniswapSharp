using System.Numerics;
using System.Text;
using UniswapSharp.V3.Utils;
using UniswapSharp.V4.Utils;

namespace UniswapSharp.LiquidityLauncher;

/// <summary>Arguments for <see cref="Encode.EncodeCreateToken"/>.</summary>
public record CreateTokenArgs(
    string Factory,
    string Name,
    string Symbol,
    int Decimals,
    BigInteger InitialSupply,
    string Recipient,
    string TokenData);

/// <summary>
/// Encoders for the LiquidityLauncher / LBPStrategy / ContinuousClearingAuction launch flow. These
/// build the calldata a wallet signs. Ported from sdks/liquidity-launcher-sdk/src/encode.ts.
/// </summary>
public static class Encode
{
    public static readonly BigInteger MaxUint256 = (BigInteger.One << 256) - 1;
    public static readonly BigInteger MaxUint160 = (BigInteger.One << 160) - 1;

    // uint48 max; upstream maps uint48 to `number`, which is safe (< 2^53).
    private const long MaxUint48 = 281474976710655;

    // -----------------------------------------------------------------------
    // abi.encode tuple parameter type strings
    // -----------------------------------------------------------------------

    private const string POOL_PARAMETERS_TYPE = "(uint24,int24,address)";

    /// <summary>The <c>MigratorParameters</c> tuple type — exported because the initializer salt hashes over it.</summary>
    public const string MIGRATOR_PARAMETERS_TYPE =
        "(address,address,uint64,uint128,address,address,(uint24,int24,address),bytes,bytes)";

    private const string AUCTION_PARAMETERS_TYPE =
        "(address,address,address,uint64,uint64,uint64,uint256,address,uint256,uint128,bytes)";

    private const string POSITION_DEFINITIONS_TYPE = "(int24,int24,uint24,address)[]";

    private const string LP_ALLOCATION_SCHEDULE_TYPE = "(uint128,uint24)[]";

    // (string,string,string,bytes): strings encode byte-for-byte like `bytes` over their UTF-8 bytes.
    private const string UERC20_METADATA_TYPE = "(bytes,bytes,bytes,bytes)";

    private const string TOKEN_SPLITTER_SPLITS_TYPE = "(address,uint256)[]";

    // -----------------------------------------------------------------------
    // Positional value builders
    // -----------------------------------------------------------------------

    /// <summary>The positional value array for a <see cref="MigratorParameters"/> tuple.</summary>
    public static object?[] MigratorParametersValue(MigratorParameters m) => new object?[]
    {
        m.Token,
        m.Currency,
        m.MigrationBlock,
        m.ReservedTokenAmountForLP,
        m.Recipient,
        m.PositionRecipient,
        new object?[] { m.PoolParameters.Fee, m.PoolParameters.TickSpacing, m.PoolParameters.Hook },
        m.PositionDefinitions,
        m.LpAllocationSchedule,
    };

    private static object?[] AuctionParametersValue(AuctionParameters p) => new object?[]
    {
        p.Currency,
        p.TokensRecipient,
        p.FundsRecipient,
        p.StartBlock,
        p.EndBlock,
        p.ClaimBlock,
        p.TickSpacing,
        p.ValidationHook,
        p.FloorPrice,
        p.RequiredCurrencyRaised,
        p.AuctionStepsData,
    };

    // -----------------------------------------------------------------------
    // abi.encode helpers (struct / bytes payloads)
    // -----------------------------------------------------------------------

    /// <summary><c>abi.encode(PositionDefinition[])</c> — the <c>MigratorParameters.PositionDefinitions</c> field.</summary>
    public static string EncodePositionDefinitions(IReadOnlyList<PositionDefinition> definitions)
    {
        object?[] value = definitions
            .Select(d => (object?)new object?[] { d.OffsetLower, d.OffsetUpper, d.Weight, d.OverridePositionRecipient })
            .ToArray();
        return AbiParamEncoder.Encode(new[] { POSITION_DEFINITIONS_TYPE }, new object?[] { value });
    }

    /// <summary><c>abi.encode(LiquidityAllocationBracket[])</c> — the <c>MigratorParameters.LpAllocationSchedule</c> field.</summary>
    public static string EncodeLpAllocationSchedule(IReadOnlyList<LiquidityAllocationBracket> brackets)
    {
        object?[] value = brackets
            .Select(b => (object?)new object?[] { b.LowerThreshold, b.Rate })
            .ToArray();
        return AbiParamEncoder.Encode(new[] { LP_ALLOCATION_SCHEDULE_TYPE }, new object?[] { value });
    }

    /// <summary><c>abi.encode(AuctionParameters)</c> — the inner <c>auctionParams</c> bytes the CCA factory decodes.</summary>
    public static string EncodeAuctionParams(AuctionParameters parameters) =>
        AbiParamEncoder.Encode(new[] { AUCTION_PARAMETERS_TYPE }, new object?[] { AuctionParametersValue(parameters) });

    /// <summary><c>abi.encode(MigratorParameters, bytes auctionParams)</c> — the strategy's <c>configData</c>.</summary>
    public static string EncodeConfigData(MigratorParameters migrator, string auctionParams) =>
        AbiParamEncoder.Encode(
            new[] { MIGRATOR_PARAMETERS_TYPE, "bytes" },
            new object?[] { MigratorParametersValue(migrator), auctionParams });

    /// <summary><c>abi.encode(Split[])</c> — the TokenSplitter strategy's <c>configData</c>.</summary>
    public static string EncodeTokenSplitterConfig(IReadOnlyList<Split> splits)
    {
        object?[] value = splits
            .Select(s => (object?)new object?[] { s.Recipient, s.Amount })
            .ToArray();
        return AbiParamEncoder.Encode(new[] { TOKEN_SPLITTER_SPLITS_TYPE }, new object?[] { value });
    }

    /// <summary>
    /// Packs the auction emission schedule into the CCA's tight byte format: one 8-byte word per step =
    /// <c>bytes3(mps) ‖ bytes5(blockDelta)</c>, where <c>blockDelta = endBlock - startBlock</c>.
    /// </summary>
    public static string EncodeAuctionSteps(IReadOnlyList<AuctionStepInput> steps)
    {
        if (steps.Count == 0)
        {
            return "0x";
        }
        var sb = new StringBuilder("0x");
        foreach (var step in steps)
        {
            BigInteger blockDelta = step.EndBlock - step.StartBlock;
            if (blockDelta <= 0)
            {
                throw new LauncherSdkError(
                    LauncherErrorCode.INVALID_AUCTION_STEP, "Auction step endBlock must be greater than startBlock");
            }
            // mps == 0 is allowed (a prebid window emits nothing).
            if (step.Mps < 0 || step.Mps > 0xffffff)
            {
                throw new LauncherSdkError(
                    LauncherErrorCode.INVALID_AUCTION_STEP, "Auction step mps must be a non-negative integer within uint24");
            }
            if (blockDelta > 0xffffffffffL)
            {
                throw new LauncherSdkError(
                    LauncherErrorCode.INVALID_AUCTION_STEP, "Auction step blockDelta out of uint40 range");
            }
            sb.Append(PackBigEndian(step.Mps, 3));
            sb.Append(PackBigEndian(blockDelta, 5));
        }
        return sb.ToString();
    }

    /// <summary>
    /// <c>abi.encode(Uerc20Metadata)</c> for the UERC20Factory <c>tokenData</c> arg. The USUPERC20Factory
    /// variant prepends <c>(uint256 homeChainId, address creator)</c> — use <see cref="EncodeSuperchainTokenData"/>.
    /// </summary>
    public static string EncodeTokenData(Uerc20Metadata metadata) =>
        AbiParamEncoder.Encode(new[] { UERC20_METADATA_TYPE }, new object?[] { Uerc20MetadataValue(metadata) });

    public static string EncodeSuperchainTokenData(BigInteger homeChainId, string creator, Uerc20Metadata metadata) =>
        AbiParamEncoder.Encode(
            new[] { "uint256", "address", UERC20_METADATA_TYPE },
            new object?[] { homeChainId, creator, Uerc20MetadataValue(metadata) });

    private static object?[] Uerc20MetadataValue(Uerc20Metadata m) => new object?[]
    {
        Utf8Hex(m.Description),
        Utf8Hex(m.Website),
        Utf8Hex(m.Image),
        m.ExtraData,
    };

    // -----------------------------------------------------------------------
    // Function calldata encoders
    // -----------------------------------------------------------------------

    public static string EncodeCreateToken(CreateTokenArgs args)
    {
        string selector = AbiFunctionEncoder.Selector("createToken(address,string,string,uint8,uint128,address,bytes)");
        // string encodes byte-for-byte like bytes over its UTF-8 bytes.
        string encoded = AbiParamEncoder.Encode(
            new[] { "address", "bytes", "bytes", "uint8", "uint128", "address", "bytes" },
            new object?[]
            {
                args.Factory, Utf8Hex(args.Name), Utf8Hex(args.Symbol), args.Decimals, args.InitialSupply,
                args.Recipient, args.TokenData,
            });
        return "0x" + selector + encoded[2..];
    }

    public static string EncodeDistributeToken(string token, Distribution distribution, string salt)
    {
        string selector = AbiFunctionEncoder.Selector("distributeToken(address,(address,uint128,bytes),bytes32)");
        string encoded = AbiParamEncoder.Encode(
            new[] { "address", "(address,uint128,bytes)", "bytes32" },
            new object?[]
            {
                token,
                new object?[] { distribution.Strategy, distribution.Amount, distribution.ConfigData },
                salt,
            });
        return "0x" + selector + encoded[2..];
    }

    public static string EncodeDepositToken(string token, BigInteger amount)
    {
        string selector = AbiFunctionEncoder.Selector("depositToken(address,uint160)");
        string encoded = AbiParamEncoder.Encode(new[] { "address", "uint160" }, new object?[] { token, amount });
        return "0x" + selector + encoded[2..];
    }

    /// <summary>Wraps a list of launcher subcalls into a single <c>multicall([...])</c>.</summary>
    public static string EncodeMulticall(IReadOnlyList<string> calls)
    {
        string selector = AbiFunctionEncoder.Selector("multicall(bytes[])");
        object?[] value = calls.Select(c => (object?)c).ToArray();
        string encoded = AbiParamEncoder.Encode(new[] { "bytes[]" }, new object?[] { value });
        return "0x" + selector + encoded[2..];
    }

    /// <summary>
    /// ERC20 <c>approve(spender, amount)</c> — used to approve Permit2. Defaults to the canonical
    /// infinite approval <c>uint256.max</c>.
    /// </summary>
    public static string EncodeErc20Approve(string spender, BigInteger? amount = null)
    {
        string selector = AbiFunctionEncoder.Selector("approve(address,uint256)");
        string encoded = AbiParamEncoder.Encode(
            new[] { "address", "uint256" }, new object?[] { spender, amount ?? MaxUint256 });
        return "0x" + selector + encoded[2..];
    }

    /// <summary>Permit2 on-chain allowance <c>approve(token, spender, amount, expiration)</c>.</summary>
    public static string EncodePermit2Approve(
        string token, string spender, BigInteger? amount = null, long? expiration = null)
    {
        string selector = AbiFunctionEncoder.Selector("approve(address,address,uint160,uint48)");
        string encoded = AbiParamEncoder.Encode(
            new[] { "address", "address", "uint160", "uint48" },
            new object?[] { token, spender, amount ?? MaxUint160, expiration ?? MaxUint48 });
        return "0x" + selector + encoded[2..];
    }

    // -----------------------------------------------------------------------
    // Helpers
    // -----------------------------------------------------------------------

    private static string Utf8Hex(string value) =>
        "0x" + Convert.ToHexString(Encoding.UTF8.GetBytes(value)).ToLowerInvariant();

    /// <summary>Big-endian hex (no <c>0x</c>) of a non-negative integer, zero-padded to <paramref name="sizeBytes"/>.</summary>
    private static string PackBigEndian(BigInteger value, int sizeBytes)
    {
        byte[] be = value.ToByteArray(isUnsigned: true, isBigEndian: true);
        var buffer = new byte[sizeBytes];
        Array.Copy(be, 0, buffer, sizeBytes - be.Length, be.Length);
        return Convert.ToHexString(buffer).ToLowerInvariant();
    }
}
