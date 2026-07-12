using System.Numerics;
using Nethereum.ABI;
using UniswapSharp.Core.Entities;
using UniswapSharp.Core.Entities.Fractions;
using UniswapSharp.V3;
using UniswapSharp.V3.Entities;
using UniswapSharp.V4.Utils;
using static UniswapSharp.V3.Utils.AbiFunctionEncoder;

namespace UniswapSharp.Router;

public enum ApprovalTypes
{
    NOT_REQUIRED = 0,
    MAX = 1,
    MAX_MINUS_ONE = 2,
    ZERO_THEN_MAX = 3,
    ZERO_THEN_MAX_MINUS_ONE = 4,
}

/// <summary>
/// Condensed version of the v3-sdk AddLiquidityOptions containing only the swap+add attributes.
/// A non-null <see cref="Recipient"/> denotes a mint; a non-null <see cref="TokenId"/> an increase.
/// </summary>
public class CondensedAddLiquidityOptions
{
    public string? Recipient { get; init; }
    public BigInteger? TokenId { get; init; }
}

/// <summary>
/// Port of router-sdk <c>approveAndCall.ts</c>. Encodes approvals and add-liquidity calls to the
/// NFT position manager via the ApproveAndCall periphery.
/// </summary>
public abstract class ApproveAndCall
{
    private ApproveAndCall() { }

    public static bool IsMint(CondensedAddLiquidityOptions options) => options.Recipient is not null;

    public static string EncodeApproveMax(Token token) =>
        EncodeFunctionData("approveMax(address)", new ABIValue("address", token.Address));

    public static string EncodeApproveMaxMinusOne(Token token) =>
        EncodeFunctionData("approveMaxMinusOne(address)", new ABIValue("address", token.Address));

    public static string EncodeApproveZeroThenMax(Token token) =>
        EncodeFunctionData("approveZeroThenMax(address)", new ABIValue("address", token.Address));

    public static string EncodeApproveZeroThenMaxMinusOne(Token token) =>
        EncodeFunctionData("approveZeroThenMaxMinusOne(address)", new ABIValue("address", token.Address));

    public static string EncodeCallPositionManager(List<string> calldatas)
    {
        if (calldatas.Count == 0)
        {
            throw new ArgumentException("NULL_CALLDATA");
        }

        if (calldatas.Count == 1)
        {
            return "0x" + Selector("callPositionManager(bytes)") + AbiParamEncoder.Encode(new[] { "bytes" }, new object?[] { calldatas[0] })[2..];
        }

        string encodedMulticall = Multicall.EncodeMulticall(calldatas);
        return "0x" + Selector("callPositionManager(bytes)") + AbiParamEncoder.Encode(new[] { "bytes" }, new object?[] { encodedMulticall })[2..];
    }

    /// <summary>Encode adding liquidity to a position in the NFT manager contract.</summary>
    public static string EncodeAddLiquidity(
        Position position,
        Position minimalPosition,
        CondensedAddLiquidityOptions addLiquidityOptions,
        Percent slippageTolerance)
    {
        var (amount0Min, amount1Min) = position.MintAmountsWithSlippage(slippageTolerance);

        // position.mintAmountsWithSlippage() can produce amounts that aren't dependable (e.g. range orders),
        // so allow a custom minimal position to override.
        if (minimalPosition.Amount0.Quotient < amount0Min)
        {
            amount0Min = minimalPosition.Amount0.Quotient;
        }
        if (minimalPosition.Amount1.Quotient < amount1Min)
        {
            amount1Min = minimalPosition.Amount1.Quotient;
        }

        if (IsMint(addLiquidityOptions))
        {
            var body = AbiParamEncoder.Encode(
                new[] { "(address,address,uint24,int24,int24,uint256,uint256,address)" },
                new object?[]
                {
                    new object?[]
                    {
                        position.Pool.Token0.Address,
                        position.Pool.Token1.Address,
                        (int)position.Pool.Fee,
                        position.TickLower,
                        position.TickUpper,
                        amount0Min,
                        amount1Min,
                        addLiquidityOptions.Recipient!,
                    },
                });
            return "0x" + Selector("mint((address,address,uint24,int24,int24,uint256,uint256,address))") + body[2..];
        }
        else
        {
            var body = AbiParamEncoder.Encode(
                new[] { "(address,address,uint256,uint256,uint256)" },
                new object?[]
                {
                    new object?[]
                    {
                        position.Pool.Token0.Address,
                        position.Pool.Token1.Address,
                        amount0Min,
                        amount1Min,
                        addLiquidityOptions.TokenId!.Value,
                    },
                });
            return "0x" + Selector("increaseLiquidity((address,address,uint256,uint256,uint256))") + body[2..];
        }
    }

    public static string EncodeApprove(BaseCurrency token, ApprovalTypes approvalType) => approvalType switch
    {
        ApprovalTypes.MAX => EncodeApproveMax(token.Wrapped()),
        ApprovalTypes.MAX_MINUS_ONE => EncodeApproveMaxMinusOne(token.Wrapped()),
        ApprovalTypes.ZERO_THEN_MAX => EncodeApproveZeroThenMax(token.Wrapped()),
        ApprovalTypes.ZERO_THEN_MAX_MINUS_ONE => EncodeApproveZeroThenMaxMinusOne(token.Wrapped()),
        _ => throw new ArgumentException("Error: invalid ApprovalType"),
    };
}
