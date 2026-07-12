using System.Numerics;

namespace UniswapSharp.Flashtestations.Types;

/// <summary>
/// Block parameter for identifying blocks.
/// Port of the upstream <c>BlockParameter</c> union
/// (<c>'earliest' | 'latest' | 'safe' | 'finalized' | 'pending' | string | number | bigint</c>).
///
/// In C# a block parameter is either a string (a block tag, hex block number, or block hash)
/// or a number (a decimal / bigint block number). Implicit conversions mirror the ergonomic
/// TypeScript usage (<c>'latest'</c>, <c>12345</c>, <c>BigInteger</c>).
/// </summary>
public readonly struct BlockParameter : IEquatable<BlockParameter>
{
    private readonly string? _stringValue;
    private readonly BigInteger _numberValue;
    private readonly bool _isNumber;

    private BlockParameter(string value)
    {
        _stringValue = value;
        _numberValue = default;
        _isNumber = false;
    }

    private BlockParameter(BigInteger value)
    {
        _stringValue = null;
        _numberValue = value;
        _isNumber = true;
    }

    /// <summary>True when the parameter is a numeric (decimal / bigint) block number.</summary>
    public bool IsNumber => _isNumber;

    /// <summary>True when the parameter is a string (block tag, hex number, or block hash).</summary>
    public bool IsString => !_isNumber && _stringValue is not null;

    /// <summary>The underlying string value (only valid when <see cref="IsString"/>).</summary>
    public string StringValue => _stringValue ?? string.Empty;

    /// <summary>The underlying numeric value (only valid when <see cref="IsNumber"/>).</summary>
    public BigInteger NumberValue => _numberValue;

    public static implicit operator BlockParameter(string value) => new(value);

    public static implicit operator BlockParameter(int value) => new((BigInteger)value);

    public static implicit operator BlockParameter(long value) => new((BigInteger)value);

    public static implicit operator BlockParameter(BigInteger value) => new(value);

    public bool Equals(BlockParameter other)
    {
        if (_isNumber != other._isNumber)
        {
            return false;
        }

        return _isNumber
            ? _numberValue == other._numberValue
            : string.Equals(_stringValue, other._stringValue, StringComparison.Ordinal);
    }

    public override bool Equals(object? obj) => obj is BlockParameter other && Equals(other);

    public override int GetHashCode() =>
        _isNumber ? _numberValue.GetHashCode() : (_stringValue?.GetHashCode() ?? 0);

    /// <summary>
    /// String form used in error messages, mirroring JS template-literal coercion
    /// (e.g. <c>`Block not found: ${blockParameter}`</c>).
    /// </summary>
    public override string ToString() =>
        _isNumber ? _numberValue.ToString() : (_stringValue ?? string.Empty);
}
