using System.Numerics;
using System.Text;
using Nethereum.ABI;
using Nethereum.Hex.HexConvertors.Extensions;
using UniswapSharp.Core.Entities;
using UniswapSharp.Core.Entities.Fractions;
using UniswapSharp.Core.Utils;
using UniswapSharp.V3.Entities;
using UniswapSharp.V3.Utils;
using static UniswapSharp.V3.Utils.AbiFunctionEncoder;

namespace UniswapSharp.V3;

public abstract class Staker
{
    // The IncentiveKey struct, all-static so it encodes as its concatenated fields.
    private const string IncentiveKeyTuple = "(address,address,uint256,uint256,address)";

    private static readonly ABIEncode Abi = new();

    private static ABIValue[] IncentiveKeyValues(IncentiveKey key)
    {
        return new[]
        {
            new ABIValue("address", key.RewardToken.Address),
            new ABIValue("address", Pool.GetAddress(key.Pool.Token0, key.Pool.Token1, key.Pool.Fee)),
            new ABIValue("uint256", key.StartTime),
            new ABIValue("uint256", key.EndTime),
            new ABIValue("address", AddressValidator.ValidateAndParseAddress(key.Refundee)),
        };
    }

    private static ABIValue[] IncentiveKeyWithTokenId(IncentiveKey key, BigInteger tokenId)
    {
        var vals = IncentiveKeyValues(key);
        return new[] { vals[0], vals[1], vals[2], vals[3], vals[4], new ABIValue("uint256", tokenId) };
    }

    // To claim rewards, must unstake and then claim.
    private static string[] EncodeClaim(IncentiveKey incentiveKey, IClaimOptions options)
    {
        string recipient = AddressValidator.ValidateAndParseAddress(options.Recipient);
        BigInteger amount = options.Amount ?? BigInteger.Zero;

        return new[]
        {
            EncodeFunctionData($"unstakeToken({IncentiveKeyTuple},uint256)", IncentiveKeyWithTokenId(incentiveKey, options.TokenId)),
            EncodeFunctionData("claimReward(address,address,uint256)",
                new ABIValue("address", incentiveKey.RewardToken.Address),
                new ABIValue("address", recipient),
                new ABIValue("uint256", amount)),
        };
    }

    public static NonfungiblePositionManager.MethodParameters CollectRewards(IncentiveKey incentiveKey, IClaimOptions options) =>
        CollectRewards(new[] { incentiveKey }, options);

    public static NonfungiblePositionManager.MethodParameters CollectRewards(IncentiveKey[] incentiveKeys, IClaimOptions options)
    {
        var calldatas = new List<string>();

        foreach (var incentiveKey in incentiveKeys)
        {
            calldatas.AddRange(EncodeClaim(incentiveKey, options));
            calldatas.Add(EncodeFunctionData($"stakeToken({IncentiveKeyTuple},uint256)", IncentiveKeyWithTokenId(incentiveKey, options.TokenId)));
        }

        return new NonfungiblePositionManager.MethodParameters
        {
            Calldata = Multicall.EncodeMulticall(calldatas),
            Value = Utilities.ToHex(BigInteger.Zero)
        };
    }

    public static NonfungiblePositionManager.MethodParameters WithdrawToken(IncentiveKey incentiveKey, FullWithdrawOptions withdrawOptions) =>
        WithdrawToken(new[] { incentiveKey }, withdrawOptions);

    public static NonfungiblePositionManager.MethodParameters WithdrawToken(IncentiveKey[] incentiveKeys, FullWithdrawOptions withdrawOptions)
    {
        var calldatas = new List<string>();

        var claimOptions = new ClaimOptions
        {
            TokenId = withdrawOptions.TokenId,
            Recipient = withdrawOptions.Recipient,
            Amount = withdrawOptions.Amount
        };

        foreach (var incentiveKey in incentiveKeys)
        {
            calldatas.AddRange(EncodeClaim(incentiveKey, claimOptions));
        }

        string owner = AddressValidator.ValidateAndParseAddress(withdrawOptions.Owner);
        byte[] data = string.IsNullOrEmpty(withdrawOptions.Data)
            ? Utilities.ToHex(BigInteger.Zero).HexToByteArray()
            : withdrawOptions.Data.HexToByteArray();

        calldatas.Add(EncodeFunctionData("withdrawToken(uint256,address,bytes)",
            new ABIValue("uint256", withdrawOptions.TokenId),
            new ABIValue("address", owner),
            new ABIValue("bytes", data)));

        return new NonfungiblePositionManager.MethodParameters
        {
            Calldata = Multicall.EncodeMulticall(calldatas),
            Value = Utilities.ToHex(BigInteger.Zero)
        };
    }

    public static string EncodeDeposit(IncentiveKey incentiveKey) => EncodeDeposit(new[] { incentiveKey });

    public static string EncodeDeposit(IncentiveKey[] incentiveKeys)
    {
        if (incentiveKeys.Length > 1)
        {
            // ABI encoding of a dynamic array of static tuples: offset, length, then each tuple's fields.
            var sb = new StringBuilder();
            sb.Append(Abi.GetABIEncoded(new ABIValue("uint256", (BigInteger)32)).ToHex());
            sb.Append(Abi.GetABIEncoded(new ABIValue("uint256", (BigInteger)incentiveKeys.Length)).ToHex());
            foreach (var key in incentiveKeys)
            {
                sb.Append(Abi.GetABIEncoded(IncentiveKeyValues(key)).ToHex());
            }
            return "0x" + sb;
        }

        return "0x" + Abi.GetABIEncoded(IncentiveKeyValues(incentiveKeys[0])).ToHex();
    }

    public class FullWithdrawOptions : IClaimOptions, IWithdrawOptions
    {
        public BigInteger TokenId { get; set; }
        public required string Recipient { get; set; }
        public BigInteger? Amount { get; set; }
        public required string Owner { get; set; }
        // upstream `data?: string` — optional.
        public string? Data { get; set; }
    }

    public class IncentiveKey
    {
        public required Token RewardToken { get; set; }
        public required Pool Pool { get; set; }
        public BigInteger StartTime { get; set; }
        public BigInteger EndTime { get; set; }
        public required string Refundee { get; set; }
    }

    public interface IClaimOptions
    {
        public BigInteger TokenId { get; set; }
        public string Recipient { get; set; }
        public BigInteger? Amount { get; set; }
    }

    public class ClaimOptions : IClaimOptions
    {
        public BigInteger TokenId { get; set; }
        public required string Recipient { get; set; }
        public BigInteger? Amount { get; set; }
    }

    public interface IWithdrawOptions
    {
        public string Owner { get; set; }
        public string? Data { get; set; }
    }

    public class SwapOptions
    {
        public required Percent SlippageTolerance { get; set; }
        public required string Recipient { get; set; }
        public BigInteger Deadline { get; set; }
        // upstream `inputTokenPermit?: PermitOptions` / `fee?: FeeOptions` — optional.
        // Accepts SelfPermit.StandardPermitArguments or SelfPermit.AllowedPermitArguments.
        public SelfPermit.IPermitOptions? InputTokenPermit { get; set; }
        public BigInteger? SqrtPriceLimitX96 { get; set; }
        public Payments.FeeOptions? Fee { get; set; }
    }
}
