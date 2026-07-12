using UniswapSharp.UniversalRouter.Entities.Actions;
using UniswapSharp.V4.Utils;

namespace UniswapSharp.UniversalRouter.Utils;

/// <summary>
/// CommandTypes. Flags that modify a command's execution. Port of universal-router-sdk
/// <c>utils/routerCommands.ts</c> (<c>CommandType</c>).
/// </summary>
public enum CommandType
{
    V3_SWAP_EXACT_IN = 0x00,
    V3_SWAP_EXACT_OUT = 0x01,
    PERMIT2_TRANSFER_FROM = 0x02,
    PERMIT2_PERMIT_BATCH = 0x03,
    SWEEP = 0x04,
    TRANSFER = 0x05,
    PAY_PORTION = 0x06,
    PAY_PORTION_FULL_PRECISION = 0x07,

    V2_SWAP_EXACT_IN = 0x08,
    V2_SWAP_EXACT_OUT = 0x09,
    PERMIT2_PERMIT = 0x0a,
    WRAP_ETH = 0x0b,
    UNWRAP_WETH = 0x0c,
    PERMIT2_TRANSFER_FROM_BATCH = 0x0d,
    BALANCE_CHECK_ERC20 = 0x0e,

    V4_SWAP = 0x10,
    V3_POSITION_MANAGER_PERMIT = 0x11,
    V3_POSITION_MANAGER_CALL = 0x12,
    V4_INITIALIZE_POOL = 0x13,
    V4_POSITION_MANAGER_CALL = 0x14,

    EXECUTE_SUB_PLAN = 0x21,

    // 3rd party integrations (0x40-0x5f range)
    ACROSS_V4_DEPOSIT_V3 = 0x40,
}

/// <summary>Identifies how a parameter should be expanded by the parser phase.</summary>
public enum Subparser
{
    V3PathExactIn,
    V3PathExactOut,
}

/// <summary>Selects how a command's inputs are (de)coded.</summary>
public enum Parser
{
    Abi,
    V4Actions,
    V3Actions,
}

/// <summary>One named ABI parameter of a command, optionally annotated with its <see cref="Subparser"/>.</summary>
public sealed record ParamType(string Name, string Type, Subparser? Subparser = null);

/// <summary>A command's decode/encode definition: parser + (for <see cref="Parser.Abi"/>) parameter list.</summary>
public sealed class CommandDefinition
{
    public required Parser Parser { get; init; }
    public IReadOnlyList<ParamType> Params { get; init; } = Array.Empty<ParamType>();
}

/// <summary>An encoded command: its (possibly revert-flagged) type and ABI-encoded input.</summary>
public sealed class RouterCommand
{
    public required CommandType Type { get; set; }
    public required string EncodedInput { get; init; }
}

/// <summary>
/// Port of universal-router-sdk <c>utils/routerCommands.ts</c>: the command-buffer encoder and the
/// per-command ABI definitions. Encodes each <c>Parser.Abi</c> command's inputs with
/// <see cref="AbiParamEncoder"/> (ethers' <c>defaultAbiCoder</c> equivalent).
/// </summary>
public class RoutePlanner
{
    private const int ALLOW_REVERT_FLAG = 0x80;

    private static readonly HashSet<CommandType> REVERTIBLE_COMMANDS = new() { CommandType.EXECUTE_SUB_PLAN };

    private const string PERMIT_STRUCT =
        "((address token,uint160 amount,uint48 expiration,uint48 nonce) details,address spender,uint256 sigDeadline)";

    private const string PERMIT_BATCH_STRUCT =
        "((address token,uint160 amount,uint48 expiration,uint48 nonce)[] details,address spender,uint256 sigDeadline)";

    private const string POOL_KEY_STRUCT =
        "(address currency0,address currency1,uint24 fee,int24 tickSpacing,address hooks)";

    private const string PERMIT2_TRANSFER_FROM_STRUCT = "(address from,address to,uint160 amount,address token)";
    private const string PERMIT2_TRANSFER_FROM_BATCH_STRUCT = PERMIT2_TRANSFER_FROM_STRUCT + "[]";

    private static ParamType P(string name, string type, Subparser? subparser = null) => new(name, type, subparser);

    private static CommandDefinition Abi(params ParamType[] parms) =>
        new() { Parser = Parser.Abi, Params = parms };

    /// <summary>Port of upstream <c>COMMAND_DEFINITION</c>.</summary>
    public static readonly IReadOnlyDictionary<CommandType, CommandDefinition> COMMAND_DEFINITION =
        new Dictionary<CommandType, CommandDefinition>
        {
            // Batch Reverts
            [CommandType.EXECUTE_SUB_PLAN] = Abi(P("commands", "bytes"), P("inputs", "bytes[]")),

            // Permit2 Actions
            [CommandType.PERMIT2_PERMIT] = Abi(P("permit", PERMIT_STRUCT), P("signature", "bytes")),
            [CommandType.PERMIT2_PERMIT_BATCH] = Abi(P("permit", PERMIT_BATCH_STRUCT), P("signature", "bytes")),
            [CommandType.PERMIT2_TRANSFER_FROM] =
                Abi(P("token", "address"), P("recipient", "address"), P("amount", "uint160")),
            [CommandType.PERMIT2_TRANSFER_FROM_BATCH] = Abi(P("transferFrom", PERMIT2_TRANSFER_FROM_BATCH_STRUCT)),

            // Uniswap Actions
            [CommandType.V3_SWAP_EXACT_IN] = Abi(
                P("recipient", "address"),
                P("amountIn", "uint256"),
                P("amountOutMin", "uint256"),
                P("path", "bytes", Subparser.V3PathExactIn),
                P("payerIsUser", "bool")),
            [CommandType.V3_SWAP_EXACT_OUT] = Abi(
                P("recipient", "address"),
                P("amountOut", "uint256"),
                P("amountInMax", "uint256"),
                P("path", "bytes", Subparser.V3PathExactOut),
                P("payerIsUser", "bool")),
            [CommandType.V2_SWAP_EXACT_IN] = Abi(
                P("recipient", "address"),
                P("amountIn", "uint256"),
                P("amountOutMin", "uint256"),
                P("path", "address[]"),
                P("payerIsUser", "bool")),
            [CommandType.V2_SWAP_EXACT_OUT] = Abi(
                P("recipient", "address"),
                P("amountOut", "uint256"),
                P("amountInMax", "uint256"),
                P("path", "address[]"),
                P("payerIsUser", "bool")),
            [CommandType.V4_SWAP] = new CommandDefinition { Parser = Parser.V4Actions },

            // Token Actions and Checks
            [CommandType.WRAP_ETH] = Abi(P("recipient", "address"), P("amount", "uint256")),
            [CommandType.UNWRAP_WETH] = Abi(P("recipient", "address"), P("amountMin", "uint256")),
            [CommandType.SWEEP] = Abi(P("token", "address"), P("recipient", "address"), P("amountMin", "uint256")),
            [CommandType.TRANSFER] = Abi(P("token", "address"), P("recipient", "address"), P("value", "uint256")),
            [CommandType.PAY_PORTION] = Abi(P("token", "address"), P("recipient", "address"), P("bips", "uint256")),
            [CommandType.PAY_PORTION_FULL_PRECISION] =
                Abi(P("token", "address"), P("recipient", "address"), P("portion", "uint256")),
            [CommandType.BALANCE_CHECK_ERC20] =
                Abi(P("owner", "address"), P("token", "address"), P("minBalance", "uint256")),
            [CommandType.V4_INITIALIZE_POOL] = Abi(P("poolKey", POOL_KEY_STRUCT), P("sqrtPriceX96", "uint160")),

            // Position Actions
            [CommandType.V3_POSITION_MANAGER_PERMIT] = new CommandDefinition { Parser = Parser.V3Actions },
            [CommandType.V3_POSITION_MANAGER_CALL] = new CommandDefinition { Parser = Parser.V3Actions },
            [CommandType.V4_POSITION_MANAGER_CALL] = new CommandDefinition { Parser = Parser.V4Actions },

            // 3rd Party Integrations
            [CommandType.ACROSS_V4_DEPOSIT_V3] = Abi(
                P("depositor", "address"),
                P("recipient", "address"),
                P("inputToken", "address"),
                P("outputToken", "address"),
                P("inputAmount", "uint256"),
                P("outputAmount", "uint256"),
                P("destinationChainId", "uint256"),
                P("exclusiveRelayer", "address"),
                P("quoteTimestamp", "uint32"),
                P("fillDeadline", "uint32"),
                P("exclusivityDeadline", "uint32"),
                P("message", "bytes"),
                P("useNative", "bool")),
        };

    /// <summary>V2.1.1 ABI definitions for V2/V3 swap commands (extended with <c>minHopPriceX36</c>).</summary>
    public static readonly IReadOnlyDictionary<CommandType, CommandDefinition> V2V3_SWAP_COMMANDS_V2_1_1 =
        new Dictionary<CommandType, CommandDefinition>
        {
            [CommandType.V3_SWAP_EXACT_IN] = Abi(
                P("recipient", "address"),
                P("amountIn", "uint256"),
                P("amountOutMin", "uint256"),
                P("path", "bytes", Subparser.V3PathExactIn),
                P("payerIsUser", "bool"),
                P("minHopPriceX36", "uint256[]")),
            [CommandType.V3_SWAP_EXACT_OUT] = Abi(
                P("recipient", "address"),
                P("amountOut", "uint256"),
                P("amountInMax", "uint256"),
                P("path", "bytes", Subparser.V3PathExactOut),
                P("payerIsUser", "bool"),
                P("minHopPriceX36", "uint256[]")),
            [CommandType.V2_SWAP_EXACT_IN] = Abi(
                P("recipient", "address"),
                P("amountIn", "uint256"),
                P("amountOutMin", "uint256"),
                P("path", "address[]"),
                P("payerIsUser", "bool"),
                P("minHopPriceX36", "uint256[]")),
            [CommandType.V2_SWAP_EXACT_OUT] = Abi(
                P("recipient", "address"),
                P("amountOut", "uint256"),
                P("amountInMax", "uint256"),
                P("path", "address[]"),
                P("payerIsUser", "bool"),
                P("minHopPriceX36", "uint256[]")),
        };

    /// <summary>The concatenated command-byte string (lower-case, <c>0x</c>-prefixed).</summary>
    public string Commands { get; private set; }

    /// <summary>The ABI-encoded input for each command, aligned with <see cref="Commands"/>.</summary>
    public List<string> Inputs { get; }

    public RoutePlanner()
    {
        Commands = "0x";
        Inputs = new List<string>();
    }

    /// <summary>Adds a revertible sub-plan (EXECUTE_SUB_PLAN).</summary>
    public RoutePlanner AddSubPlan(RoutePlanner subplan)
    {
        AddCommand(CommandType.EXECUTE_SUB_PLAN,
            new object?[] { subplan.Commands, subplan.Inputs.Cast<object?>().ToArray() }, allowRevert: true);
        return this;
    }

    /// <summary>Appends a command with its ABI-ordered parameter values.</summary>
    public RoutePlanner AddCommand(
        CommandType type,
        object?[] parameters,
        bool allowRevert = false,
        UniversalRouterVersion? urVersion = null)
    {
        var command = CreateCommand(type, parameters, urVersion);
        Inputs.Add(command.EncodedInput);
        if (allowRevert)
        {
            if (!REVERTIBLE_COMMANDS.Contains(command.Type))
            {
                throw new InvalidOperationException($"command type: {(int)command.Type} cannot be allowed to revert");
            }
            command.Type = (CommandType)((int)command.Type | ALLOW_REVERT_FLAG);
        }

        Commands += ((int)command.Type).ToString("x2");
        return this;
    }

    /// <summary>Adds an Across bridge deposit command for cross-chain bridging.</summary>
    public RoutePlanner AddAcrossBridge(AcrossV4DepositV3Params parms)
    {
        AddCommand(CommandType.ACROSS_V4_DEPOSIT_V3, new object?[]
        {
            parms.Depositor,
            parms.Recipient,
            parms.InputToken,
            parms.OutputToken,
            parms.InputAmount,
            parms.OutputAmount,
            parms.DestinationChainId,
            parms.ExclusiveRelayer,
            parms.QuoteTimestamp,
            parms.FillDeadline,
            parms.ExclusivityDeadline,
            parms.Message,
            parms.UseNative,
        });
        return this;
    }

    /// <summary>Port of upstream <c>createCommand</c>.</summary>
    public static RouterCommand CreateCommand(
        CommandType type, object?[] parameters, UniversalRouterVersion? urVersion = null)
    {
        CommandDefinition commandDef =
            Constants.IsAtLeastV2_1_1(urVersion) && V2V3_SWAP_COMMANDS_V2_1_1.ContainsKey(type)
                ? V2V3_SWAP_COMMANDS_V2_1_1[type]
                : COMMAND_DEFINITION[type];

        switch (commandDef.Parser)
        {
            case Parser.Abi:
                string encodedInput = AbiParamEncoder.Encode(
                    commandDef.Params.Select(abi => abi.Type).ToArray(), parameters);
                return new RouterCommand { Type = type, EncodedInput = encodedInput };
            case Parser.V4Actions:
                // v4 swap data comes pre-encoded at index 0
                return new RouterCommand { Type = type, EncodedInput = (string)parameters[0]! };
            case Parser.V3Actions:
                // v3 swap data comes pre-encoded at index 0
                return new RouterCommand { Type = type, EncodedInput = (string)parameters[0]! };
            default:
                throw new InvalidOperationException($"Unsupported parser: {commandDef.Parser}");
        }
    }
}
