using System.Numerics;

namespace UniswapSharp.V4;

// Permit2 AllowanceTransfer DTOs shared by Position (permitBatchData) and the PositionManager.
// Kept in their own file so neither entity depends on the other's implementation.

public record PermitDetails(string Token, BigInteger Amount, BigInteger Expiration, BigInteger Nonce);

public record AllowanceTransferPermitBatch(List<PermitDetails> Details, string Spender, BigInteger SigDeadline);

public record AllowanceTransferPermitSingle(PermitDetails Details, string Spender, BigInteger SigDeadline);
