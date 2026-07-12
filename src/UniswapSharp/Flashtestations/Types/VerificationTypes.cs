namespace UniswapSharp.Flashtestations.Types;

/// <summary>
/// Metadata describing the TEE workload that (may have) built a block.
/// Port of the upstream <c>WorkloadMetadata</c> type (types/index.ts).
/// </summary>
public sealed record WorkloadMetadata
{
    /// <summary>Workload ID of the TEE workload.</summary>
    public required string WorkloadId { get; init; }

    /// <summary>Commit hash of the TEE workload source code.</summary>
    public required string CommitHash { get; init; }

    /// <summary>Address of the block builder.</summary>
    public required string BuilderAddress { get; init; }

    /// <summary>Version of the flashtestation protocol.</summary>
    public required int Version { get; init; }

    /// <summary>Source locators (e.g., GitHub URLs) for the workload source code.</summary>
    public IReadOnlyList<string>? SourceLocators { get; init; }
}

/// <summary>
/// Result of flashtestation verification.
/// Port of the upstream <c>VerificationResult</c> union (types/index.ts).
///
/// When <see cref="IsBuiltByExpectedTee"/> is <c>true</c> the <see cref="WorkloadMetadata"/> is
/// always populated; when <c>false</c> it may be <c>null</c> (no flashtestation transaction) or
/// populated (a different TEE built the block).
/// </summary>
public sealed record VerificationResult
{
    /// <summary>Whether the block was built by the expected TEE workload.</summary>
    public required bool IsBuiltByExpectedTee { get; init; }

    /// <summary>Block explorer link for the block, or <c>null</c> when unavailable.</summary>
    public required string? BlockExplorerLink { get; init; }

    /// <summary>Workload metadata for the block's flashtestation, or <c>null</c>.</summary>
    public required WorkloadMetadata? WorkloadMetadata { get; init; }
}

/// <summary>
/// Parsed flashtestation event from the <c>BlockBuilderProofVerified</c> log.
/// Port of the upstream <c>FlashtestationEvent</c> interface (types/index.ts).
/// </summary>
public sealed record FlashtestationEvent
{
    /// <summary>Address of the block builder.</summary>
    public required string Caller { get; init; }

    /// <summary>Hash identifier for the workload (bytes32 hex).</summary>
    public required string WorkloadId { get; init; }

    /// <summary>Version of the flashtestation protocol.</summary>
    public required int Version { get; init; }

    /// <summary>Hash of the block content (bytes32 hex).</summary>
    public required string BlockContentHash { get; init; }

    /// <summary>git commit ID of the code used to reproducibly build the workload.</summary>
    public required string CommitHash { get; init; }

    /// <summary>Source locators (e.g., GitHub URLs) for the workload source code.</summary>
    public IReadOnlyList<string>? SourceLocators { get; init; }
}
