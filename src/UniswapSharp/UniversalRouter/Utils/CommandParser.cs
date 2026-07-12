using System.Numerics;
using UniswapSharp.V3.Utils;
using UniswapSharp.V4.Utils;
using V4Param = UniswapSharp.V4.Utils.Param;

namespace UniswapSharp.UniversalRouter.Utils;

/// <summary>A decoded, named command parameter. Port of universal-router-sdk <c>utils/commandParser.ts</c> (<c>Param</c>).</summary>
public sealed record Param(string Name, object? Value);

/// <summary>A decoded Universal Router command. Port of <c>UniversalRouterCommand</c>.</summary>
public sealed record UniversalRouterCommand(string CommandName, CommandType CommandType, IReadOnlyList<Param> Params);

/// <summary>The structured result of decoding UR calldata. Port of <c>UniversalRouterCall</c>.</summary>
public sealed record UniversalRouterCall(IReadOnlyList<UniversalRouterCommand> Commands);

/// <summary>A decoded V3 path hop. Port of <c>V3PathItem</c>.</summary>
public sealed record V3PathItem(string TokenIn, string TokenOut, int Fee);

/// <summary>
/// Port of universal-router-sdk <c>utils/commandParser.ts</c>: decodes Universal Router <c>execute</c>
/// calldata back into a structured <see cref="UniversalRouterCall"/>.
/// </summary>
public static class CommandParser
{
    private static readonly string ExecuteSelector = AbiFunctionEncoder.Selector("execute(bytes,bytes[])");
    private static readonly string ExecuteWithDeadlineSelector = AbiFunctionEncoder.Selector("execute(bytes,bytes[],uint256)");

    /// <summary>Parses UR <c>execute</c> calldata into commands and their decoded parameters.</summary>
    public static UniversalRouterCall ParseCalldata(
        string calldata, UniversalRouterVersion urVersion = UniversalRouterVersion.V2_0)
    {
        // From V2.1.1 onwards the V2/V3 swap commands carry an extra minHopPriceX36 array, so the matching
        // command definitions must be used to decode them correctly.
        Dictionary<CommandType, CommandDefinition> commandDefinition;
        if (Constants.IsAtLeastV2_1_1(urVersion))
        {
            commandDefinition = new Dictionary<CommandType, CommandDefinition>(RoutePlanner.COMMAND_DEFINITION);
            foreach (var (k, v) in RoutePlanner.V2V3_SWAP_COMMANDS_V2_1_1)
            {
                commandDefinition[k] = v;
            }
        }
        else
        {
            commandDefinition = new Dictionary<CommandType, CommandDefinition>(RoutePlanner.COMMAND_DEFINITION);
        }

        var (commands, inputs) = DecodeExecute(calldata);
        var genericParser = new GenericCommandParser(commandDefinition, urVersion);
        return genericParser.Parse(commands, inputs);
    }

    private static (string commands, List<string> inputs) DecodeExecute(string calldata)
    {
        string selector = calldata[2..10];
        string body = "0x" + calldata[10..];
        List<object?> decoded;
        if (selector == ExecuteWithDeadlineSelector)
        {
            decoded = AbiParamDecoder.Decode(new[] { "bytes", "bytes[]", "uint256" }, body);
        }
        else if (selector == ExecuteSelector)
        {
            decoded = AbiParamDecoder.Decode(new[] { "bytes", "bytes[]" }, body);
        }
        else
        {
            throw new InvalidOperationException($"Unknown execute selector: 0x{selector}");
        }

        var commands = (string)decoded[0]!;
        var inputs = ((List<object?>)decoded[1]!).Select(x => (string)x!).ToList();
        return (commands, inputs);
    }

    /// <summary>Parses V3 exact-in path bytes into path hops.</summary>
    public static IReadOnlyList<V3PathItem> ParseV3PathExactIn(string path)
    {
        string stripped = path.StartsWith("0x") ? path[2..] : path;
        string tokenIn = GetAddress(stripped.Substring(0, 40));
        int loc = 40;
        var res = new List<V3PathItem>();
        while (loc < stripped.Length)
        {
            string feeAndTokenOut = stripped.Substring(loc, 46);
            int fee = Convert.ToInt32(feeAndTokenOut.Substring(0, 6), 16);
            string tokenOut = GetAddress(feeAndTokenOut.Substring(6, 40));
            res.Add(new V3PathItem(tokenIn, tokenOut, fee));
            tokenIn = tokenOut;
            loc += 46;
        }
        return res;
    }

    /// <summary>Parses V3 exact-out path bytes (reverse-ordered) into path hops.</summary>
    public static IReadOnlyList<V3PathItem> ParseV3PathExactOut(string path)
    {
        string stripped = path.StartsWith("0x") ? path[2..] : path;
        string tokenIn = GetAddress(stripped.Substring(stripped.Length - 40));
        int loc = stripped.Length - 86; // 86 = (20 addr + 3 fee + 20 addr) * 2 (hex chars)
        var res = new List<V3PathItem>();
        while (loc >= 0)
        {
            string feeAndTokenOut = stripped.Substring(loc, 46);
            string tokenOut = GetAddress(feeAndTokenOut.Substring(0, 40));
            int fee = Convert.ToInt32(feeAndTokenOut.Substring(40, 6), 16);
            res.Add(new V3PathItem(tokenIn, tokenOut, fee));
            tokenIn = tokenOut;
            loc -= 46;
        }
        return res;
    }

    // ethers getAddress returns a checksummed address; here we normalise to the lowercase 0x form.
    private static string GetAddress(string addr40) => "0x" + addr40.ToLowerInvariant();

    internal static IReadOnlyList<Param> V4RouterCallToParams(IReadOnlyList<V4RouterAction> actions) =>
        actions.Select(action => new Param(
            action.ActionName,
            (object?)action.Params.Select(p => new Param(p.Name, p.Value)).ToList())).ToList();
}

/// <summary>Parses commands based on a given command definition. Port of <c>GenericCommandParser</c>.</summary>
public sealed class GenericCommandParser
{
    private readonly IReadOnlyDictionary<CommandType, CommandDefinition> _commandDefinition;
    private readonly UniversalRouterVersion _urVersion;

    public GenericCommandParser(
        IReadOnlyDictionary<CommandType, CommandDefinition> commandDefinition,
        UniversalRouterVersion urVersion = UniversalRouterVersion.V2_0)
    {
        _commandDefinition = commandDefinition;
        _urVersion = urVersion;
    }

    public UniversalRouterCall Parse(string commands, IReadOnlyList<string> inputs)
    {
        var commandTypes = GetCommands(commands);
        var parsed = new List<UniversalRouterCommand>();

        for (int i = 0; i < commandTypes.Count; i++)
        {
            CommandType commandType = commandTypes[i];
            var commandDef = _commandDefinition[commandType];

            if (commandDef.Parser == Parser.V4Actions)
            {
                var v4 = V4BaseActionsParser.ParseCalldata(inputs[i], ToUR(_urVersion));
                parsed.Add(new UniversalRouterCommand(
                    commandType.ToString(), commandType, CommandParser.V4RouterCallToParams(v4.Actions)));
            }
            else if (commandDef.Parser == Parser.Abi)
            {
                var abiDef = commandDef.Params;
                var rawParams = AbiParamDecoder.Decode(abiDef.Select(c => c.Type).ToArray(), inputs[i]);

                var parameters = new List<Param>();
                for (int j = 0; j < rawParams.Count; j++)
                {
                    object? value = abiDef[j].Subparser switch
                    {
                        Subparser.V3PathExactIn => CommandParser.ParseV3PathExactIn((string)rawParams[j]!),
                        Subparser.V3PathExactOut => CommandParser.ParseV3PathExactOut((string)rawParams[j]!),
                        _ => rawParams[j],
                    };
                    parameters.Add(new Param(abiDef[j].Name, value));
                }
                parsed.Add(new UniversalRouterCommand(commandType.ToString(), commandType, parameters));
            }
            else if (commandDef.Parser == Parser.V3Actions)
            {
                // matches upstream: one 'command' param per input across the whole inputs array
                parsed.Add(new UniversalRouterCommand(commandType.ToString(), commandType,
                    inputs.Select(input => new Param("command", (object?)input)).ToList()));
            }
            else
            {
                throw new InvalidOperationException($"Unsupported parser: {commandDef.Parser}");
            }
        }

        return new UniversalRouterCall(parsed);
    }

    private static List<CommandType> GetCommands(string commands)
    {
        var commandTypes = new List<CommandType>();
        for (int i = 2; i < commands.Length; i += 2)
        {
            string b = commands.Substring(i, 2);
            commandTypes.Add((CommandType)Convert.ToInt32(b, 16));
        }
        return commandTypes;
    }

    // UniversalRouterVersion and URVersion share identical string values; cast across the SDK boundary.
    private static URVersion ToUR(UniversalRouterVersion v) => v.Value() switch
    {
        "2.1.1" => URVersion.V2_1_1,
        "2.2.0" => URVersion.V2_2_0,
        _ => URVersion.V2_0,
    };
}
