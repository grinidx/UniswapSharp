using System.Numerics;
using UniswapSharp.Permit2;
using UniswapSharp.UniversalRouter.Utils;
using UniswapSharp.V3.Utils;
using UniswapSharp.V4.Utils;
using static UniswapSharp.V3.Utils.AbiFunctionEncoder;
using Constants = UniswapSharp.UniversalRouter.Utils.Constants;

namespace UniswapSharp.UniversalRouter;

/// <summary>Optional config for producing an <c>execute</c> call.</summary>
public sealed class SwapRouterConfig
{
    public string? Sender { get; init; }
    public object? Deadline { get; init; }
}

/// <summary>Options for signed-route execution. Port of <c>SignedRouteOptions</c>.</summary>
public sealed class SignedRouteOptions
{
    /// <summary>bytes32 - application-specific intent identifier.</summary>
    public required string Intent { get; init; }

    /// <summary>bytes32 - application-specific data.</summary>
    public required string Data { get; init; }

    /// <summary>msg.sender to verify, or address(0) to skip sender verification.</summary>
    public required string Sender { get; init; }

    /// <summary>bytes32 - optional nonce. Random if not provided; NONCE_SKIP_CHECK to skip verification.</summary>
    public string? Nonce { get; init; }
}

/// <summary>The typed-data value carried by an <c>ExecuteSigned</c> payload.</summary>
public sealed record EIP712PayloadValue(
    string Commands,
    IReadOnlyList<string> Inputs,
    string Intent,
    string Data,
    string Sender,
    string Nonce,
    string Deadline);

/// <summary>An EIP-712 payload ready to be signed externally. Port of <c>EIP712Payload</c>.</summary>
public sealed record EIP712Payload(
    Eip712Domain Domain,
    IReadOnlyDictionary<string, IReadOnlyList<TypedDataField>> Types,
    EIP712PayloadValue Value);

/// <summary>Decoded <c>executeSigned</c> calldata.</summary>
public sealed record ExecuteSignedDecoded(
    string Commands,
    IReadOnlyList<string> Inputs,
    string Intent,
    string Data,
    bool VerifySender,
    string Nonce,
    string Signature,
    BigInteger Deadline);

/// <summary>
/// Port of universal-router-sdk <c>swapRouter.ts</c> — the top-level Universal Router calldata builder.
/// </summary>
public abstract partial class SwapRouter
{
    private const string EXECUTE_SIG = "execute(bytes,bytes[])";
    private const string EXECUTE_WITH_DEADLINE_SIG = "execute(bytes,bytes[],uint256)";

    private const string EXECUTE_SIGNED_SIG =
        "executeSigned(bytes,bytes[],bytes32,bytes32,bool,bytes32,bytes,uint256)";

    private const string PROXY_EXECUTE_SIG = "execute(address,address,uint256,bytes,bytes[],uint256)";

    private const int DEFAULT_PROXY_DEADLINE_BUFFER_SECONDS = 30 * 60;

    /// <summary>Encoded calldata + native value for a UR call.</summary>
    public sealed class MethodParameters
    {
        public required string Calldata { get; init; }
        public required string Value { get; init; }
    }

    private static string Encode(string sig, string[] types, object?[] values) =>
        "0x" + Selector(sig) + AbiParamEncoder.Encode(types, values)[2..];

    /// <summary>The 4-byte selector (with <c>0x</c>) of a UR ABI function, matching ethers' <c>getSighash</c>.</summary>
    public static string GetSighash(string name) => name switch
    {
        "executeSigned" => "0x" + Selector(EXECUTE_SIGNED_SIG),
        "execute(bytes,bytes[])" => "0x" + Selector(EXECUTE_SIG),
        "execute(bytes,bytes[],uint256)" => "0x" + Selector(EXECUTE_WITH_DEADLINE_SIG),
        _ => throw new ArgumentException($"Unknown function: {name}"),
    };

    // -------- execute(...) encode/decode --------

    private static (string commands, List<string> inputs) DecodeExecute(string calldata)
    {
        string selector = calldata[2..10];
        string body = "0x" + calldata[10..];
        List<object?> decoded;
        if (selector == Selector(EXECUTE_WITH_DEADLINE_SIG))
        {
            decoded = AbiParamDecoder.Decode(new[] { "bytes", "bytes[]", "uint256" }, body);
        }
        else if (selector == Selector(EXECUTE_SIG))
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

    // -------- signed routes --------

    /// <summary>
    /// Generate an EIP712 payload for signed execution (no signing performed). Decodes existing <c>execute()</c>
    /// calldata and prepares it for signing.
    /// </summary>
    public static EIP712Payload GetExecuteSignedPayload(
        string calldata,
        SignedRouteOptions signedOptions,
        object deadline,
        int chainId,
        string routerAddress)
    {
        var (commands, inputs) = DecodeExecute(calldata);

        string nonce = signedOptions.Nonce ?? Eip712.GenerateNonce();
        string sender = signedOptions.Sender;
        var domain = Eip712.GetUniversalRouterDomain(chainId, routerAddress);
        string deadlineStr = AbiParamEncoder.ToBigInteger(deadline).ToString();

        var value = new EIP712PayloadValue(
            commands, inputs, signedOptions.Intent, signedOptions.Data, sender, nonce, deadlineStr);

        return new EIP712Payload(domain, Eip712.EXECUTE_SIGNED_TYPES, value);
    }

    /// <summary>Encode an <c>executeSigned()</c> call with a signature.</summary>
    public static MethodParameters EncodeExecuteSigned(
        string calldata,
        string signature,
        SignedRouteOptions signedOptions,
        object deadline,
        BigInteger? nativeCurrencyValue = null)
    {
        var (commands, inputs) = DecodeExecute(calldata);

        if (signedOptions.Nonce is null)
        {
            throw new InvalidOperationException(
                "Nonce is required for encodeExecuteSigned - use the nonce from getExecuteSignedPayload");
        }
        string nonce = signedOptions.Nonce;

        bool verifySender = signedOptions.Sender != "0x0000000000000000000000000000000000000000";

        string signedCalldata = Encode(EXECUTE_SIGNED_SIG,
            new[] { "bytes", "bytes[]", "bytes32", "bytes32", "bool", "bytes32", "bytes", "uint256" },
            new object?[]
            {
                commands,
                inputs.Cast<object?>().ToArray(),
                signedOptions.Intent,
                signedOptions.Data,
                verifySender,
                nonce,
                signature,
                AbiParamEncoder.ToBigInteger(deadline),
            });

        return new MethodParameters
        {
            Calldata = signedCalldata,
            Value = Utilities.ToHex(nativeCurrencyValue ?? BigInteger.Zero),
        };
    }

    /// <summary>Decode <c>executeSigned()</c> calldata (the inverse of <see cref="EncodeExecuteSigned"/>).</summary>
    public static ExecuteSignedDecoded DecodeExecuteSigned(string calldata)
    {
        string body = "0x" + calldata[10..];
        var decoded = AbiParamDecoder.Decode(
            new[] { "bytes", "bytes[]", "bytes32", "bytes32", "bool", "bytes32", "bytes", "uint256" }, body);
        return new ExecuteSignedDecoded(
            (string)decoded[0]!,
            ((List<object?>)decoded[1]!).Select(x => (string)x!).ToList(),
            (string)decoded[2]!,
            (string)decoded[3]!,
            (bool)decoded[4]!,
            (string)decoded[5]!,
            (string)decoded[6]!,
            (BigInteger)decoded[7]!);
    }

    // -------- plan encoding --------

    /// <summary>Encodes UR <c>execute</c> calldata (with a deadline overload) — ethers' <c>encodeFunctionData</c> equivalent.</summary>
    public static string EncodeExecute(string commands, IReadOnlyList<string> inputs, object? deadline = null) =>
        deadline is not null
            ? Encode(EXECUTE_WITH_DEADLINE_SIG, new[] { "bytes", "bytes[]", "uint256" },
                new object?[] { commands, inputs.Cast<object?>().ToArray(), AbiParamEncoder.ToBigInteger(deadline) })
            : Encode(EXECUTE_SIG, new[] { "bytes", "bytes[]" },
                new object?[] { commands, inputs.Cast<object?>().ToArray() });

    private static MethodParameters EncodePlan(RoutePlanner planner, BigInteger nativeCurrencyValue, object? deadline)
    {
        string calldata = EncodeExecute(planner.Commands, planner.Inputs, deadline);
        return new MethodParameters { Calldata = calldata, Value = Utilities.ToHex(nativeCurrencyValue) };
    }

    private static MethodParameters EncodeProxyCall(
        RoutePlanner planner,
        string inputToken,
        BigInteger inputAmount,
        int chainId,
        UniversalRouterVersion urVersion,
        object? deadline)
    {
        string routerAddress = Constants.UNIVERSAL_ROUTER_ADDRESS(urVersion, chainId);
        BigInteger resolvedDeadline = deadline is not null
            ? AbiParamEncoder.ToBigInteger(deadline)
            : DateTimeOffset.UtcNow.ToUnixTimeSeconds() + DEFAULT_PROXY_DEADLINE_BUFFER_SECONDS;

        string calldata = Encode(PROXY_EXECUTE_SIG,
            new[] { "address", "address", "uint256", "bytes", "bytes[]", "uint256" },
            new object?[]
            {
                routerAddress,
                inputToken,
                inputAmount,
                planner.Commands,
                planner.Inputs.Cast<object?>().ToArray(),
                resolvedDeadline,
            });

        return new MethodParameters { Calldata = calldata, Value = Utilities.ToHex(BigInteger.Zero) };
    }
}
