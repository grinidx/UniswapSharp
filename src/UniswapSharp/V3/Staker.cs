using System.Numerics;
using Nethereum.ABI;
using Nethereum.Contracts;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.JsonRpc.Client;
using Nethereum.RPC;
using Nethereum.Util;
using UniswapSharp.Core.Entities;
using UniswapSharp.Core.Entities.Fractions;
using UniswapSharp.V3.Entities;
using UniswapSharp.V3.Utils;

namespace UniswapSharp.V3;

public abstract class Staker
{
    public static Contract Interface { get; }
    private const string INCENTIVE_KEY_ABI = "tuple(address rewardToken, address pool, uint256 startTime, uint256 endTime, address refundee)";

    private static string[] EncodeClaim(IncentiveKey incentiveKey, IClaimOptions options)
    {
        var calldatas = new List<string>();
        calldatas.Add(Interface.GetFunction("unstakeToken").GetData(EncodeIncentiveKey(incentiveKey), options.TokenId.ToHex(false)));

        var recipient = new AddressUtil().ConvertToChecksumAddress(options.Recipient);
        var amount = options.Amount ?? BigInteger.Zero;
        calldatas.Add(Interface.GetFunction("claimReward").GetData(incentiveKey.RewardToken.Address, recipient, amount.ToHex(false)));

        return calldatas.ToArray();
    }

    public static NonfungiblePositionManager.MethodParameters CollectRewards(IncentiveKey[] incentiveKeys, IClaimOptions options)
    {
        var calldatas = new List<string>();

        foreach (var incentiveKey in incentiveKeys)
        {
            calldatas.AddRange(EncodeClaim(incentiveKey, options));
            calldatas.Add(Interface.GetFunction("stakeToken").GetData(EncodeIncentiveKey(incentiveKey), options.TokenId.ToHex(false)));
        }

        return new NonfungiblePositionManager.MethodParameters
        {
            Calldata = Multicall.EncodeMulticall(calldatas),
            Value = BigInteger.Zero.ToHex(false)
        };
    }

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

        var owner = new AddressUtil().ConvertToChecksumAddress(withdrawOptions.Owner);
        calldatas.Add(Interface.GetFunction("withdrawToken").GetData(
            withdrawOptions.TokenId.ToHex(false),
            owner,
            string.IsNullOrEmpty(withdrawOptions.Data) ? BigInteger.Zero.ToHex(false) : withdrawOptions.Data
        ));

        return new NonfungiblePositionManager.MethodParameters
        {
            Calldata = Multicall.EncodeMulticall(calldatas),
            Value = BigInteger.Zero.ToHex(false)
        };
    }

    public static string EncodeDeposit(IncentiveKey[] incentiveKeys)
    {
        if (incentiveKeys.Length > 1)
        {
            var keys = incentiveKeys.Select(EncodeIncentiveKey).ToArray();
            return new ABIEncode().GetABIEncoded(new ABIValue($"{INCENTIVE_KEY_ABI}[]", keys)).ToHex();
        }
        else
        {
            return new ABIEncode().GetABIEncoded(new ABIValue(INCENTIVE_KEY_ABI, EncodeIncentiveKey(incentiveKeys[0]))).ToHex();
        }
    }

    private static object EncodeIncentiveKey(IncentiveKey incentiveKey)
    {
        var refundee = new AddressUtil().ConvertToChecksumAddress(incentiveKey.Refundee);
        return new
        {
            rewardToken = incentiveKey.RewardToken.Address,
            pool = Pool.GetAddress(incentiveKey.Pool.Token0, incentiveKey.Pool.Token1, incentiveKey.Pool.Fee),
            startTime = incentiveKey.StartTime.ToHex(false),
            endTime = incentiveKey.EndTime.ToHex(false),
            refundee
        };
    }

    public class FullWithdrawOptions : IClaimOptions, IWithdrawOptions
    {
        public BigInteger TokenId { get; set; }
        public string Recipient { get; set; }
        public BigInteger? Amount { get; set; }
        public string Owner { get; set; }
        public string Data { get; set; }
    }
    public class IncentiveKey
    {
        public Token RewardToken { get; set; }
        public Pool Pool { get; set; }
        public BigInteger StartTime { get; set; }
        public BigInteger EndTime { get; set; }
        public string Refundee { get; set; }
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
        public string Recipient { get; set; }
        public BigInteger? Amount { get; set; }
    }


    public interface IWithdrawOptions
    {
        public string Owner { get; set; }
        public string Data { get; set; }
    }

    public class PermitOptions
    {
        // Implement PermitOptions
    }

    public class SwapOptions
    {
        public Percent SlippageTolerance { get; set; }
        public string Recipient { get; set; }
        public BigInteger Deadline { get; set; }
        public PermitOptions InputTokenPermit { get; set; }
        public BigInteger? SqrtPriceLimitX96 { get; set; }
        public Payments.FeeOptions Fee { get; set; }
    }
}
