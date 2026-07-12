using System.Numerics;

namespace UniswapSharp.LiquidityLauncher;

/// <summary>A minimal, chain-agnostic transaction. Consumers add <c>from</c> / <c>chainId</c> / gas.</summary>
public record TransactionRequest(string To, string Data, BigInteger Value);

/// <summary>How the launcher acquires the tokens before distributing them.</summary>
public abstract record TokenAcquisition;

/// <summary>Existing token: the launcher pulls <c>Amount</c> (uint160) from the wallet via Permit2 in depositToken.</summary>
public sealed record DepositTokenAcquisition(BigInteger Amount) : TokenAcquisition;

/// <summary>New token: the launcher mints <c>initialSupply</c> to itself (set <c>recipient</c> to the launcher).</summary>
public sealed record CreateTokenAcquisition(CreateTokenArgs Args) : TokenAcquisition;

/// <summary>Inputs for assembling a launch transaction. Ported from src/build.ts <c>BuildLaunchParams</c>.</summary>
public record BuildLaunchParams(
    // The LiquidityLauncher address (from Addresses.GetLauncherAddresses).
    string LiquidityLauncher,
    // The token being launched (predicted new-token address, or the existing token).
    string Token,
    // The user salt (bytes32) passed to every distributeToken in this launch.
    string Salt,
    // How the launcher acquires the tokens before distributing them.
    TokenAcquisition Acquire,
    // Distributions executed in order within the multicall.
    IReadOnlyList<Distribution> Distributions,
    // Approval transactions to prepend (existing-token path).
    IReadOnlyList<TransactionRequest>? Approvals = null);

/// <summary>
/// Pure transaction assembler. Ported from sdks/liquidity-launcher-sdk/src/build.ts.
/// </summary>
public static class Build
{
    /// <summary>Builds just the LiquidityLauncher <c>multicall</c> calldata (acquire + distribute subcalls).</summary>
    public static string BuildLaunchMulticall(BuildLaunchParams p)
    {
        var calls = new List<string>
        {
            p.Acquire is CreateTokenAcquisition create
                ? Encode.EncodeCreateToken(create.Args)
                : Encode.EncodeDepositToken(p.Token, ((DepositTokenAcquisition)p.Acquire).Amount),
        };
        foreach (var distribution in p.Distributions)
        {
            calls.Add(Encode.EncodeDistributeToken(p.Token, distribution, p.Salt));
        }
        return Encode.EncodeMulticall(calls);
    }

    /// <summary>Builds the ordered transaction list: any approvals, then the single launcher <c>multicall</c>.</summary>
    public static IReadOnlyList<TransactionRequest> BuildLaunchTransactions(BuildLaunchParams p)
    {
        var multicall = new TransactionRequest(p.LiquidityLauncher, BuildLaunchMulticall(p), BigInteger.Zero);
        var result = new List<TransactionRequest>();
        if (p.Approvals is not null)
        {
            result.AddRange(p.Approvals);
        }
        result.Add(multicall);
        return result;
    }

    /// <summary>Convenience: the ERC20 <c>approve(permit2, max)</c> transaction for an existing-token launch.</summary>
    public static TransactionRequest BuildErc20ApprovePermit2Tx(string token, string permit2) =>
        new(token, Encode.EncodeErc20Approve(permit2), BigInteger.Zero);

    /// <summary>Convenience: the Permit2 <c>approve(token, launcher, max)</c> transaction for an existing-token launch.</summary>
    public static TransactionRequest BuildPermit2ApproveLauncherTx(string permit2, string token, string launcher) =>
        new(permit2, Encode.EncodePermit2Approve(token, launcher), BigInteger.Zero);
}
