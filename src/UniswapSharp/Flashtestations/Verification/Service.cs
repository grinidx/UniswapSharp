using UniswapSharp.Flashtestations.Config;
using UniswapSharp.Flashtestations.Crypto;
using UniswapSharp.Flashtestations.Rpc;
using UniswapSharp.Flashtestations.Types;

namespace UniswapSharp.Flashtestations.Verification;

/// <summary>
/// Flashtestation verification service.
/// Port of upstream <c>verification/service.ts</c>.
///
/// The upstream module-level functions become instance methods on this service so the RPC client
/// can be injected (mirroring the test's <c>jest.spyOn(rpcClientModule, 'RpcClient')</c>). The pure,
/// deterministic dependencies (<see cref="Workload.ComputeAllWorkloadIds"/> and
/// <see cref="Chains.GetBlockExplorerUrl"/>) are used directly rather than mocked.
/// </summary>
public sealed class FlashtestationService
{
    private readonly IRpcClientFactory _rpcClientFactory;

    /// <summary>
    /// Create a verification service.
    /// </summary>
    /// <param name="rpcClientFactory">
    /// Optional RPC client factory; defaults to the real <see cref="RpcClientFactory"/>.
    /// Tests inject a fake to avoid network access.
    /// </param>
    public FlashtestationService(IRpcClientFactory? rpcClientFactory = null)
    {
        _rpcClientFactory = rpcClientFactory ?? new RpcClientFactory();
    }

    /// <summary>
    /// Fetch the flashtestation transaction's event data from the latest block.
    /// </summary>
    public Task<FlashtestationEvent?> GetFlashtestationEventAsync(ClientConfig config) =>
        GetFlashtestationEventAsync("latest", config);

    /// <summary>
    /// Fetch the flashtestation transaction's event data from a specific block.
    /// Port of upstream <c>getFlashtestationEvent</c>.
    /// </summary>
    /// <param name="blockParameter">Block identifier (tag, number, or hash).</param>
    /// <param name="config">Configuration for chain and RPC connection.</param>
    /// <returns>The flashtestation event, or <c>null</c> if the block contains none.</returns>
    public Task<FlashtestationEvent?> GetFlashtestationEventAsync(BlockParameter blockParameter, ClientConfig config)
    {
        // Create RPC client
        var client = _rpcClientFactory.Create(new RpcClientConfig
        {
            ChainId = config.ChainId,
            RpcUrl = config.RpcUrl,
        });

        // Get the flashtestation transaction's event data from the block
        return client.GetFlashtestationEventAsync(blockParameter);
    }

    /// <summary>
    /// Verify if a block was built by a TEE running a specific workload, identified by workload ID.
    /// Port of upstream <c>verifyFlashtestationInBlock</c> (string-argument overload).
    /// </summary>
    public Task<VerificationResult> VerifyFlashtestationInBlockAsync(
        string workloadId,
        BlockParameter blockParameter,
        ClientConfig config) =>
        VerifyInternalAsync(new[] { workloadId }, blockParameter, config);

    /// <summary>
    /// Verify if a block was built by a TEE running a specific workload, identified by measurement
    /// registers (all possible workload IDs are computed, including array combinations).
    /// Port of upstream <c>verifyFlashtestationInBlock</c> (registers-argument overload).
    /// </summary>
    public async Task<VerificationResult> VerifyFlashtestationInBlockAsync(
        WorkloadMeasurementRegisters registers,
        BlockParameter blockParameter,
        ClientConfig config)
    {
        // Compute all possible workload IDs from measurement registers
        // (handles arrays in mrtd and rtmr0 fields).
        var workloadIds = Workload.ComputeAllWorkloadIds(registers).ToArray();
        return await VerifyInternalAsync(workloadIds, blockParameter, config);
    }

    private async Task<VerificationResult> VerifyInternalAsync(
        string[] workloadIds,
        BlockParameter blockParameter,
        ClientConfig config)
    {
        // Normalize workload IDs (ensure they have 0x prefix and are lowercase)
        var normalizedIds = workloadIds.Select(id =>
        {
            if (!id.StartsWith("0x", StringComparison.Ordinal))
            {
                id = "0x" + id;
            }

            return id.ToLowerInvariant();
        }).ToArray();

        // Create RPC client
        var client = _rpcClientFactory.Create(new RpcClientConfig
        {
            ChainId = config.ChainId,
            RpcUrl = config.RpcUrl,
        });

        // Get the flashtestation event data from the block
        var flashtestationEvent = await client.GetFlashtestationEventAsync(blockParameter);

        // If no flashtestation event data found, block was not TEE-built
        if (flashtestationEvent is null)
        {
            return new VerificationResult
            {
                IsBuiltByExpectedTee = false,
                BlockExplorerLink = null,
                WorkloadMetadata = null,
            };
        }

        // Get block explorer URL for this chain
        var blockExplorerBaseUrl = Chains.GetBlockExplorerUrl(config.ChainId);

        // Get the block to construct the explorer link
        var block = await client.GetBlockAsync(blockParameter);

        // Construct block explorer link if available
        string? blockExplorerLink = null;
        if (!string.IsNullOrEmpty(blockExplorerBaseUrl))
        {
            // Use block number for the explorer link
            blockExplorerLink = $"{blockExplorerBaseUrl}/block/{block.Number}";
        }

        // Normalize event workload ID for comparison
        var eventWorkloadId = flashtestationEvent.WorkloadId.ToLowerInvariant();

        // Compare workload IDs - check if any of the possible IDs match
        var workloadMatches = normalizedIds.Contains(eventWorkloadId);

        var workloadMetadata = new WorkloadMetadata
        {
            WorkloadId = flashtestationEvent.WorkloadId,
            CommitHash = flashtestationEvent.CommitHash,
            BuilderAddress = flashtestationEvent.Caller,
            Version = flashtestationEvent.Version,
            SourceLocators = flashtestationEvent.SourceLocators,
        };

        return new VerificationResult
        {
            IsBuiltByExpectedTee = workloadMatches,
            BlockExplorerLink = blockExplorerLink,
            WorkloadMetadata = workloadMetadata,
        };
    }
}
