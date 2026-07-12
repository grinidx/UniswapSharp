namespace UniswapSharp.Flashtestations.Rpc;

/// <summary>
/// ABI definition for Flashtestation contract events and functions.
/// Port of upstream <c>rpc/abi.ts</c> (<c>flashtestationAbi</c>).
/// </summary>
public static class FlashtestationAbi
{
    /// <summary>Name of the event emitted when a block builder proof is verified.</summary>
    public const string BlockBuilderProofVerifiedEventName = "BlockBuilderProofVerified";

    /// <summary>Name of the view function returning workload metadata for a workload ID.</summary>
    public const string GetWorkloadMetadataFunctionName = "getWorkloadMetadata";

    /// <summary>
    /// The raw contract ABI (JSON), matching the upstream <c>flashtestationAbi</c> constant.
    /// Used by the default (Nethereum-backed) RPC client to decode logs and read the contract.
    /// </summary>
    public const string Json =
        """
        [
          {
            "type": "event",
            "name": "BlockBuilderProofVerified",
            "inputs": [
              { "indexed": false, "name": "caller", "type": "address" },
              { "indexed": false, "name": "workloadId", "type": "bytes32" },
              { "indexed": false, "name": "version", "type": "uint8" },
              { "indexed": false, "name": "blockContentHash", "type": "bytes32" },
              { "indexed": false, "name": "commitHash", "type": "string" }
            ]
          },
          {
            "type": "function",
            "name": "getWorkloadMetadata",
            "inputs": [
              { "name": "workloadId", "type": "bytes32", "internalType": "WorkloadId" }
            ],
            "outputs": [
              {
                "name": "",
                "type": "tuple",
                "internalType": "struct IBlockBuilderPolicy.WorkloadMetadata",
                "components": [
                  { "name": "commitHash", "type": "string", "internalType": "string" },
                  { "name": "sourceLocators", "type": "string[]", "internalType": "string[]" }
                ]
              }
            ],
            "stateMutability": "view"
          }
        ]
        """;
}
