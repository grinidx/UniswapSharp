using System.Globalization;
using UniswapSharp.Core;

namespace UniswapSharp.SmartWallet;

/// <summary>
/// Call types for smart wallet calls, following ERC-7579. Port of the <c>ModeType</c> enum in
/// <c>smart-wallet-sdk/src/constants.ts</c>. Upstream keys the enum by its 32-byte mode word;
/// here the member maps to that word via <see cref="Constants.ModeToBytes32"/>.
/// </summary>
public enum ModeType
{
    /// <summary>Batched call that reverts if any sub-call fails (mode word <c>0x0100…</c>).</summary>
    BATCHED_CALL,

    /// <summary>Batched call that continues if a sub-call fails (mode word <c>0x0101…</c>).</summary>
    BATCHED_CALL_CAN_REVERT,
}

/// <summary>
/// Supported chain ids for the smart wallet. Port of the <c>SupportedChainIds</c> enum in
/// <c>smart-wallet-sdk/src/constants.ts</c>; each member mirrors the matching
/// <see cref="ChainId"/> numeric value.
/// </summary>
public enum SupportedChainIds
{
    MAINNET = (int)ChainId.MAINNET,
    UNICHAIN = (int)ChainId.UNICHAIN,
    UNICHAIN_SEPOLIA = (int)ChainId.UNICHAIN_SEPOLIA,
    SEPOLIA = (int)ChainId.SEPOLIA,
    BASE = (int)ChainId.BASE,
    OPTIMISM = (int)ChainId.OPTIMISM,
    BNB = (int)ChainId.BNB,
    ARBITRUM_ONE = (int)ChainId.ARBITRUM_ONE,
    XLAYER = (int)ChainId.XLAYER,
    ARC = (int)ChainId.ARC,
    ROBINHOOD = (int)ChainId.ROBINHOOD,
}

/// <summary>
/// Supported smart wallet versions, keyed by GitHub release tag. Port of the
/// <c>SmartWalletVersion</c> enum in <c>smart-wallet-sdk/src/constants.ts</c>.
/// </summary>
public enum SmartWalletVersion
{
    LATEST,
    v1_1_0,
    v1_0_0,
    v1_0_0_staging,
}

/// <summary>
/// An insertion-ordered map from <see cref="SmartWalletVersion"/> to deployment address. Mirrors the
/// TypeScript <c>SmartWalletVersionMap</c> object: keyed lookup via the indexer and ordered
/// enumeration via <see cref="Values"/> (equivalent to JS <c>Object.values</c>).
/// </summary>
public sealed class SmartWalletVersionMap
{
    private readonly (SmartWalletVersion Version, string Address)[] _entries;

    public SmartWalletVersionMap(params (SmartWalletVersion Version, string Address)[] entries)
    {
        _entries = entries;
    }

    /// <summary>The address registered for <paramref name="version"/>.</summary>
    public string this[SmartWalletVersion version] =>
        _entries.First(entry => entry.Version == version).Address;

    /// <summary>All addresses in insertion order (mirrors JS <c>Object.values</c>).</summary>
    public IReadOnlyList<string> Values => Array.ConvertAll(_entries, entry => entry.Address);
}

/// <summary>
/// Constants for the smart wallet SDK. Port of <c>smart-wallet-sdk/src/constants.ts</c>.
/// </summary>
public static class Constants
{
    /// <summary>EIP-7702 delegation designator magic prefix.</summary>
    public const string DELEGATION_MAGIC_PREFIX = "0xef0100";

    /// <summary>The target address for self-calls is <c>address(0)</c>.</summary>
    public const string SELF_CALL_TARGET = "0x0000000000000000000000000000000000000000";

    /// <summary>ERC-7579 mode word for <see cref="ModeType.BATCHED_CALL"/>.</summary>
    public const string MODE_BATCHED_CALL =
        "0x0100000000000000000000000000000000000000000000000000000000000000";

    /// <summary>ERC-7579 mode word for <see cref="ModeType.BATCHED_CALL_CAN_REVERT"/>.</summary>
    public const string MODE_BATCHED_CALL_CAN_REVERT =
        "0x0101000000000000000000000000000000000000000000000000000000000000";

    // Canonical addresses are the same across all chains (deterministic deployment).
    private const string CANONICAL_V1_1_0_ADDRESS = "0x000000005c84F8Fd50b21CAC312528A64437030e";
    private const string CANONICAL_V1_0_0_ADDRESS = "0x000000009B1D0aF20D8C6d0A44e162d11F9b8f00";
    private const string CANONICAL_V1_0_0_STAGING_ADDRESS = "0x3cbad1e3b9049ecdb9588fb48dd61d80faf41bd5";

    /// <summary>Smart wallet versions for supported chains.</summary>
    public static readonly IReadOnlyDictionary<SupportedChainIds, SmartWalletVersionMap> SmartWalletVersions =
        new Dictionary<SupportedChainIds, SmartWalletVersionMap>
        {
            [SupportedChainIds.MAINNET] = WithStaging(),
            [SupportedChainIds.UNICHAIN] = WithStaging(),
            [SupportedChainIds.BASE] = WithStaging(),
            [SupportedChainIds.OPTIMISM] = WithStaging(),
            [SupportedChainIds.BNB] = WithStaging(),
            [SupportedChainIds.ARBITRUM_ONE] = WithoutStaging(),
            [SupportedChainIds.UNICHAIN_SEPOLIA] = WithStaging(),
            [SupportedChainIds.SEPOLIA] = WithStaging(),
            [SupportedChainIds.XLAYER] = WithoutStaging(),
            [SupportedChainIds.ARC] = WithoutStaging(),
            [SupportedChainIds.ROBINHOOD] = WithoutStaging(),
        };

    /// <summary>
    /// Mapping of numeric chain id to the latest Smart Wallet contract address.
    /// <para>Prefer <see cref="GetSmartWalletAddress"/> over indexing this map directly.</para>
    /// </summary>
    public static readonly IReadOnlyDictionary<int, string> SmartWalletAddresses =
        SmartWalletVersions.ToDictionary(
            entry => (int)entry.Key,
            entry => entry.Value[SmartWalletVersion.LATEST]);

    /// <summary>Get all historical smart wallet versions (addresses, in order) for a chain id.</summary>
    public static IReadOnlyList<string> GetAllSmartWalletVersions(SupportedChainIds chainId) =>
        SmartWalletVersions[chainId].Values;

    /// <summary>
    /// Get the latest Smart Wallet address for a given chain id. Accepts a numeric id, a
    /// <see cref="ChainId"/>/<see cref="SupportedChainIds"/>, or a numeric string; throws for any
    /// unsupported or non-numeric input. Mirrors <c>getSmartWalletAddress</c> upstream.
    /// </summary>
    public static string GetSmartWalletAddress(object chainIdLike)
    {
        int? normalized = chainIdLike switch
        {
            int i => i,
            long l when l is >= int.MinValue and <= int.MaxValue => (int)l,
            ChainId c => (int)c,
            SupportedChainIds s => (int)s,
            string s when int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out int n) => n,
            _ => null,
        };

        if (normalized is null || !SmartWalletAddresses.TryGetValue(normalized.Value, out string? address))
        {
            throw new ArgumentException($"Smart wallet not found for chainId: {chainIdLike}");
        }

        return address;
    }

    /// <summary>The 32-byte ERC-7579 mode word for <paramref name="mode"/>.</summary>
    public static string ModeToBytes32(ModeType mode) => mode switch
    {
        ModeType.BATCHED_CALL => MODE_BATCHED_CALL,
        ModeType.BATCHED_CALL_CAN_REVERT => MODE_BATCHED_CALL_CAN_REVERT,
        _ => throw new ArgumentException($"Invalid mode: {mode}"),
    };

    private static SmartWalletVersionMap WithStaging() => new(
        (SmartWalletVersion.LATEST, CANONICAL_V1_1_0_ADDRESS),
        (SmartWalletVersion.v1_1_0, CANONICAL_V1_1_0_ADDRESS),
        (SmartWalletVersion.v1_0_0, CANONICAL_V1_0_0_ADDRESS),
        (SmartWalletVersion.v1_0_0_staging, CANONICAL_V1_0_0_STAGING_ADDRESS));

    private static SmartWalletVersionMap WithoutStaging() => new(
        (SmartWalletVersion.LATEST, CANONICAL_V1_1_0_ADDRESS),
        (SmartWalletVersion.v1_1_0, CANONICAL_V1_1_0_ADDRESS),
        (SmartWalletVersion.v1_0_0, CANONICAL_V1_0_0_ADDRESS));
}
