using UniswapSharp.Core.Utils;
using UniswapSharp.V4.Utils;

namespace UniswapSharp.UniswapX.Order;

/// <summary>The kind of custom order validation (uniswapx-sdk <c>ValidationType</c>).</summary>
public enum ValidationType
{
    None,
    ExclusiveFiller,
}

/// <summary>The decoded exclusive-filler validation data (uniswapx-sdk <c>ExclusiveFillerData</c>).</summary>
public sealed record ExclusiveFillerData(string Filler, long LastExclusiveTimestamp);

/// <summary>The additional-validation fields to attach to an order (uniswapx-sdk <c>ValidationInfo</c>).</summary>
public sealed record ValidationInfo
{
    public required string AdditionalValidationContract { get; set; }
    public required string AdditionalValidationData { get; set; }
}

/// <summary>A parsed custom validation (uniswapx-sdk <c>CustomOrderValidation</c> union).</summary>
public sealed record CustomOrderValidation(ValidationType Type, ExclusiveFillerData? Data);

/// <summary>Port of uniswapx-sdk <c>order/validation.ts</c>.</summary>
public static class Validation
{
    private static readonly CustomOrderValidation NoneValidation = new(ValidationType.None, null);

    /// <summary>Parses the additional-validation data on <paramref name="info"/> into a <see cref="CustomOrderValidation"/>.</summary>
    public static CustomOrderValidation ParseValidation(OrderInfo info)
    {
        var data = ParseExclusiveFillerData(info.AdditionalValidationData);
        if (data.Type != ValidationType.None)
        {
            return data;
        }
        return NoneValidation;
    }

    /// <summary>Decodes exclusive-filler data, or returns <see cref="ValidationType.None"/> if the encoding is invalid.</summary>
    public static CustomOrderValidation ParseExclusiveFillerData(string encoded)
    {
        try
        {
            var decoded = AbiParamDecoder.Decode(new[] { "address", "uint256" }, encoded);
            string address = AddressValidator.GetAddress((string)decoded[0]!);
            long timestamp = (long)(System.Numerics.BigInteger)decoded[1]!;
            return new CustomOrderValidation(
                ValidationType.ExclusiveFiller,
                new ExclusiveFillerData(address, timestamp));
        }
        catch
        {
            return NoneValidation;
        }
    }

    /// <summary>Encodes exclusive-filler validation data (uniswapx-sdk <c>encodeExclusiveFillerData</c>).</summary>
    public static ValidationInfo EncodeExclusiveFillerData(
        string fillerAddress,
        long lastExclusiveTimestamp,
        int? chainId = null,
        string? additionalValidationContractAddress = null)
    {
        string additionalValidationContract;
        if (additionalValidationContractAddress != null)
        {
            additionalValidationContract = additionalValidationContractAddress;
        }
        else if (chainId != null)
        {
            additionalValidationContract = Constants.ExclusiveFillerValidationMapping[chainId.Value];
        }
        else
        {
            throw new InvalidOperationException("No validation contract provided");
        }

        string encoded = AbiParamEncoder.Encode(
            new[] { "address", "uint256" },
            new object?[] { fillerAddress, (System.Numerics.BigInteger)lastExclusiveTimestamp });

        return new ValidationInfo
        {
            AdditionalValidationContract = additionalValidationContract,
            AdditionalValidationData = encoded,
        };
    }
}
