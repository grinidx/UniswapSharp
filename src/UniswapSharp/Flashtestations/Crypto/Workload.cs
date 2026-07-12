using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Util;
using UniswapSharp.Flashtestations.Types;

namespace UniswapSharp.Flashtestations.Crypto;

/// <summary>
/// Workload ID computation from TEE measurement registers.
/// Port of upstream <c>crypto/workload.ts</c>.
/// </summary>
public static class Workload
{
    /// <summary>
    /// Converts a hex string to a byte array.
    /// Accepts both '0x'-prefixed and non-prefixed hex strings.
    /// Port of upstream <c>hexToBytes</c>.
    /// </summary>
    /// <exception cref="Exception">If the hex string has an odd number of characters.</exception>
    private static byte[] HexToBytes(string hex)
    {
        var unprefixedHex = hex.StartsWith("0x", StringComparison.Ordinal) ? hex[2..] : hex;
        if (unprefixedHex.Length % 2 != 0)
        {
            throw new Exception("Invalid hex string");
        }

        var result = new byte[unprefixedHex.Length / 2];
        for (var i = 0; i < result.Length; i++)
        {
            result[i] = Convert.ToByte(unprefixedHex.Substring(i * 2, 2), 16);
        }

        return result;
    }

    /// <summary>
    /// Concatenates multiple byte arrays.
    /// Port of upstream <c>concatBytes</c>.
    /// </summary>
    private static byte[] ConcatBytes(params byte[][] arrays)
    {
        var totalLength = arrays.Sum(arr => arr.Length);
        var result = new byte[totalLength];
        var offset = 0;
        foreach (var arr in arrays)
        {
            Buffer.BlockCopy(arr, 0, result, offset, arr.Length);
            offset += arr.Length;
        }

        return result;
    }

    /// <summary>
    /// Computes the workload ID from TEE measurement registers.
    /// Formula (as coded upstream): <c>keccak256(mrTd + rtMr0 + rtMr1 + rtMr2 + rtMr3 + mrConfigId + xFAM + tdAttributes)</c>.
    /// Port of upstream <c>computeWorkloadId</c>.
    /// </summary>
    /// <remarks>
    /// Accepts only singular registers. For registers with multiple candidate values (arrays),
    /// use <see cref="ComputeAllWorkloadIds"/> or <see cref="ExpandToSingularRegisters"/> first.
    /// </remarks>
    public static string ComputeWorkloadId(SingularWorkloadMeasurementRegisters registers)
    {
        // Validate input registers (ensures no arrays)
        Validation.ValidateSingularWorkloadMeasurementRegisters(registers);

        // Convert hex strings to byte arrays
        var mrTd = HexToBytes(registers.Mrtd);
        var rtMr0 = HexToBytes(registers.Rtmr0);
        var rtMr1 = HexToBytes(registers.Rtmr1);
        var rtMr2 = HexToBytes(registers.Rtmr2);
        var rtMr3 = HexToBytes(registers.Rtmr3);
        var mrConfigId = HexToBytes(registers.Mrconfigid);
        var xFam = HexToBytes(registers.Xfam);
        var tdAttributes = HexToBytes(registers.Tdattributes);

        // Concatenate all components and hash (0x-prefixed lowercase, matching viem keccak256)
        var concatenated = ConcatBytes(mrTd, rtMr0, rtMr1, rtMr2, rtMr3, mrConfigId, xFam, tdAttributes);
        return new Sha3Keccack().CalculateHash(concatenated).ToHex(true);
    }

    /// <summary>
    /// Expands registers with array fields into all possible singular register combinations
    /// (cartesian product of <c>mrtd</c> and <c>rtmr0</c> values).
    /// Port of upstream <c>expandToSingularRegisters</c>.
    /// </summary>
    public static IReadOnlyList<SingularWorkloadMeasurementRegisters> ExpandToSingularRegisters(
        WorkloadMeasurementRegisters registers)
    {
        // Validate input first
        Validation.ValidateWorkloadMeasurementRegisters(registers);

        // Normalize mrtd and rtmr0 to arrays
        var mrTdValues = registers.Mrtd.AsList();
        var rtMr0Values = registers.Rtmr0.AsList();

        // Generate cartesian product
        var result = new List<SingularWorkloadMeasurementRegisters>();
        foreach (var mrtd in mrTdValues)
        {
            foreach (var rtmr0 in rtMr0Values)
            {
                result.Add(new SingularWorkloadMeasurementRegisters
                {
                    Tdattributes = registers.Tdattributes,
                    Xfam = registers.Xfam,
                    Mrtd = mrtd,
                    Mrconfigid = registers.Mrconfigid,
                    Rtmr0 = rtmr0,
                    Rtmr1 = registers.Rtmr1,
                    Rtmr2 = registers.Rtmr2,
                    Rtmr3 = registers.Rtmr3,
                });
            }
        }

        return result;
    }

    /// <summary>
    /// Computes all possible workload IDs for the given registers.
    /// If registers contain arrays, computes the ID for each combination.
    /// Port of upstream <c>computeAllWorkloadIds</c>.
    /// </summary>
    public static IReadOnlyList<string> ComputeAllWorkloadIds(WorkloadMeasurementRegisters registers)
    {
        var singularRegisters = ExpandToSingularRegisters(registers);
        return singularRegisters.Select(ComputeWorkloadId).ToList();
    }

    /// <summary>
    /// Checks whether any of the possible workload IDs from the given registers matches the
    /// expected workload ID.
    /// Port of upstream <c>matchesAnyWorkloadId</c>.
    /// </summary>
    public static bool MatchesAnyWorkloadId(WorkloadMeasurementRegisters registers, string expectedWorkloadId)
    {
        var allIds = ComputeAllWorkloadIds(registers);
        return allIds.Contains(expectedWorkloadId);
    }
}
