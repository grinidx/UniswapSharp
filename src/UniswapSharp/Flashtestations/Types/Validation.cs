using System.Text.RegularExpressions;

namespace UniswapSharp.Flashtestations.Types;

/// <summary>
/// Validators and hex helpers for measurement registers.
/// Port of upstream <c>types/validation.ts</c>.
/// </summary>
public static partial class Validation
{
    [GeneratedRegex("^[0-9a-fA-F]+$")]
    private static partial Regex HexRegex();

    /// <summary>
    /// Validates whether a string is a valid hex string with an optional '0x' prefix.
    /// Port of upstream <c>isValidHex</c>.
    /// </summary>
    public static bool IsValidHex(string value, int? expectedLength = null)
    {
        var cleanHex = value.StartsWith("0x", StringComparison.Ordinal) ? value[2..] : value;

        if (!HexRegex().IsMatch(cleanHex))
        {
            return false;
        }

        // Upstream: `if (expectedLength && cleanHex.length !== expectedLength)`.
        // A JS `0` length is falsy, so a zero expected length is treated as "no length check".
        if (expectedLength is { } len && len != 0 && cleanHex.Length != len)
        {
            return false;
        }

        return true;
    }

    /// <summary>
    /// Validates a hex string and throws <see cref="ValidationError"/> if invalid.
    /// Port of upstream <c>validateHex</c>.
    /// </summary>
    private static void ValidateHex(string value, int expectedLength, string fieldName)
    {
        if (!IsValidHex(value, expectedLength))
        {
            var cleanHex = value.StartsWith("0x", StringComparison.Ordinal) ? value[2..] : value;
            throw new ValidationError(
                $"Invalid {fieldName}: expected {expectedLength} hex characters, got {cleanHex.Length}",
                fieldName);
        }
    }

    /// <summary>
    /// Validates <see cref="WorkloadMeasurementRegisters"/> structure and field formats.
    /// Supports arrays for the <c>mrtd</c> and <c>rtmr0</c> fields.
    /// Port of upstream <c>validateWorkloadMeasurementRegisters</c>.
    /// </summary>
    public static void ValidateWorkloadMeasurementRegisters(WorkloadMeasurementRegisters registers)
    {
        // Validate tdattributes (8 bytes = 16 hex chars)
        ValidateHex(registers.Tdattributes, 16, "tdattributes");

        // Validate xfam (8 bytes = 16 hex chars)
        ValidateHex(registers.Xfam, 16, "xfam");

        // Validate mrtd (48 bytes = 96 hex chars) - can be single value or array
        if (registers.Mrtd.IsArray)
        {
            var values = registers.Mrtd.Array;
            if (values.Count == 0)
            {
                throw new ValidationError("mrtd array cannot be empty", "mrtd");
            }

            for (var index = 0; index < values.Count; index++)
            {
                ValidateHex(values[index], 96, $"mrtd[{index}]");
            }
        }
        else
        {
            ValidateHex(registers.Mrtd.Single, 96, "mrtd");
        }

        // Validate mrconfigid (48 bytes = 96 hex chars)
        ValidateHex(registers.Mrconfigid, 96, "mrconfigid");

        // Validate rtmr0 (48 bytes = 96 hex chars) - can be single value or array
        if (registers.Rtmr0.IsArray)
        {
            var values = registers.Rtmr0.Array;
            if (values.Count == 0)
            {
                throw new ValidationError("rtmr0 array cannot be empty", "rtmr0");
            }

            for (var index = 0; index < values.Count; index++)
            {
                ValidateHex(values[index], 96, $"rtmr0[{index}]");
            }
        }
        else
        {
            ValidateHex(registers.Rtmr0.Single, 96, "rtmr0");
        }

        // Validate runtime measurement registers (48 bytes = 96 hex chars each)
        ValidateHex(registers.Rtmr1, 96, "rtmr1");
        ValidateHex(registers.Rtmr2, 96, "rtmr2");
        ValidateHex(registers.Rtmr3, 96, "rtmr3");
    }

    /// <summary>
    /// Validates that all fields are single values (no arrays), then validates all fields.
    /// Port of upstream <c>validateSingularWorkloadMeasurementRegisters</c>.
    ///
    /// This overload accepts the flexible <see cref="WorkloadMeasurementRegisters"/> shape so the
    /// runtime "must be a single value, not an array" guard can be exercised, mirroring upstream's
    /// runtime type check. (In C#, a genuinely singular register set cannot hold an array, so this
    /// guard only fires for flexible input.)
    /// </summary>
    public static void ValidateSingularWorkloadMeasurementRegisters(WorkloadMeasurementRegisters registers)
    {
        // Runtime check that mrtd and rtmr0 are NOT arrays (type guard)
        if (registers.Mrtd.IsArray)
        {
            throw new ValidationError("mrtd must be a single value, not an array", "mrtd");
        }

        if (registers.Rtmr0.IsArray)
        {
            throw new ValidationError("rtmr0 must be a single value, not an array", "rtmr0");
        }

        // Then validate all fields using the standard validator.
        ValidateWorkloadMeasurementRegisters(registers);
    }

    /// <summary>
    /// Validates a genuinely singular register set.
    /// Convenience overload used by <see cref="Crypto.Workload.ComputeWorkloadId"/>.
    /// </summary>
    public static void ValidateSingularWorkloadMeasurementRegisters(SingularWorkloadMeasurementRegisters registers) =>
        ValidateSingularWorkloadMeasurementRegisters(registers.ToFlexible());

    /// <summary>
    /// Normalizes a hex string by removing the '0x' prefix and converting to lowercase.
    /// Port of upstream <c>normalizeHex</c>.
    /// </summary>
    public static string NormalizeHex(string value)
    {
        var cleanHex = value.StartsWith("0x", StringComparison.Ordinal) ? value[2..] : value;
        return cleanHex.ToLowerInvariant();
    }
}
