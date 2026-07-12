namespace UniswapSharp.Flashtestations.Types;

/// <summary>
/// Represents a value that is either a single hex string or an array of hex strings.
/// Port of the upstream TypeScript union <c>string | string[]</c> used for the
/// <c>mrtd</c> and <c>rtmr0</c> measurement registers.
///
/// The GCP TEE measurements for <c>mrtd</c> / <c>rtmr0</c> are not 100% reproducible from the
/// VM image alone, so multiple candidate values may be supplied; verification succeeds if any
/// candidate yields a matching workload ID.
/// </summary>
public readonly struct HexValues : IEquatable<HexValues>
{
    private readonly string? _single;
    private readonly IReadOnlyList<string>? _array;

    public HexValues(string value)
    {
        _single = value;
        _array = null;
    }

    public HexValues(IReadOnlyList<string> values)
    {
        _single = null;
        _array = values;
    }

    /// <summary>True when this value holds an array of hex strings (mirrors <c>Array.isArray</c>).</summary>
    public bool IsArray => _array is not null;

    /// <summary>The single hex string (only valid when <see cref="IsArray"/> is false).</summary>
    public string Single => _single ?? throw new InvalidOperationException("HexValues does not hold a single value.");

    /// <summary>The array of hex strings (only valid when <see cref="IsArray"/> is true).</summary>
    public IReadOnlyList<string> Array => _array ?? throw new InvalidOperationException("HexValues does not hold an array.");

    /// <summary>
    /// Normalizes the value to a list: the single value wrapped in a one-element list, or the array itself.
    /// Mirrors upstream's <c>Array.isArray(x) ? x : [x]</c>.
    /// </summary>
    public IReadOnlyList<string> AsList() => _array ?? new[] { _single ?? string.Empty };

    public static implicit operator HexValues(string value) => new(value);

    public static implicit operator HexValues(string[] values) => new(values);

    public static implicit operator HexValues(List<string> values) => new(values);

    public bool Equals(HexValues other)
    {
        if (IsArray != other.IsArray)
        {
            return false;
        }

        if (IsArray)
        {
            return _array!.SequenceEqual(other._array!);
        }

        return string.Equals(_single, other._single, StringComparison.Ordinal);
    }

    public override bool Equals(object? obj) => obj is HexValues other && Equals(other);

    public override int GetHashCode()
    {
        if (_array is not null)
        {
            var hash = new HashCode();
            foreach (var item in _array)
            {
                hash.Add(item);
            }

            return hash.ToHashCode();
        }

        return _single?.GetHashCode() ?? 0;
    }
}

/// <summary>
/// TEE workload measurement registers used for workload ID computation.
/// Port of the upstream <c>WorkloadMeasurementRegisters</c> interface (types/index.ts).
///
/// All hex values can be provided with or without the '0x' prefix. The <c>Mrtd</c> and
/// <c>Rtmr0</c> registers may hold multiple candidate values (see <see cref="HexValues"/>).
/// </summary>
public sealed record WorkloadMeasurementRegisters
{
    /// <summary>TD attributes (8 bytes hex).</summary>
    public required string Tdattributes { get; init; }

    /// <summary>xfam (8 bytes hex).</summary>
    public required string Xfam { get; init; }

    /// <summary>MRTD - Measurement of the TD (48 bytes hex), or an array of candidate values.</summary>
    public required HexValues Mrtd { get; init; }

    /// <summary>MR Config ID - VMM configuration (48 bytes hex).</summary>
    public required string Mrconfigid { get; init; }

    /// <summary>Runtime Measurement Register 0 (48 bytes hex), or an array of candidate values.</summary>
    public required HexValues Rtmr0 { get; init; }

    /// <summary>Runtime Measurement Register 1 (48 bytes hex).</summary>
    public required string Rtmr1 { get; init; }

    /// <summary>Runtime Measurement Register 2 (48 bytes hex).</summary>
    public required string Rtmr2 { get; init; }

    /// <summary>Runtime Measurement Register 3 (48 bytes hex).</summary>
    public required string Rtmr3 { get; init; }
}

/// <summary>
/// TEE workload measurement registers with single values only (no arrays).
/// Port of the upstream <c>SingularWorkloadMeasurementRegisters</c> interface (types/index.ts).
///
/// Used for workload ID computation, where only one concrete set of register values is processed
/// at a time.
/// </summary>
public sealed record SingularWorkloadMeasurementRegisters
{
    /// <summary>TD attributes (8 bytes hex).</summary>
    public required string Tdattributes { get; init; }

    /// <summary>xfam (8 bytes hex).</summary>
    public required string Xfam { get; init; }

    /// <summary>MRTD - Measurement of the TD (48 bytes hex) - single value only.</summary>
    public required string Mrtd { get; init; }

    /// <summary>MR Config ID - VMM configuration (48 bytes hex).</summary>
    public required string Mrconfigid { get; init; }

    /// <summary>Runtime Measurement Register 0 (48 bytes hex) - single value only.</summary>
    public required string Rtmr0 { get; init; }

    /// <summary>Runtime Measurement Register 1 (48 bytes hex).</summary>
    public required string Rtmr1 { get; init; }

    /// <summary>Runtime Measurement Register 2 (48 bytes hex).</summary>
    public required string Rtmr2 { get; init; }

    /// <summary>Runtime Measurement Register 3 (48 bytes hex).</summary>
    public required string Rtmr3 { get; init; }

    /// <summary>
    /// Widens this singular register set to the flexible <see cref="WorkloadMeasurementRegisters"/>
    /// shape (with single-valued <c>Mrtd</c> / <c>Rtmr0</c>). Mirrors upstream's structural cast
    /// (<c>registers as WorkloadMeasurementRegisters</c>).
    /// </summary>
    public WorkloadMeasurementRegisters ToFlexible() => new()
    {
        Tdattributes = Tdattributes,
        Xfam = Xfam,
        Mrtd = Mrtd,
        Mrconfigid = Mrconfigid,
        Rtmr0 = Rtmr0,
        Rtmr1 = Rtmr1,
        Rtmr2 = Rtmr2,
        Rtmr3 = Rtmr3,
    };
}
