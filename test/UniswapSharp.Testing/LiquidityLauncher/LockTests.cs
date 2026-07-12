using System.Numerics;
using System.Text.RegularExpressions;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Util;
using UniswapSharp.Core.Utils;
using UniswapSharp.LiquidityLauncher;
using UniswapSharp.V4.Utils;
using Lock = UniswapSharp.LiquidityLauncher.Lock;

namespace UniswapSharp.Testing.LiquidityLauncher;

// Ported from sdks/liquidity-launcher-sdk/src/lock.test.ts.
public class LockTests
{
    private const string PM = "0xbD216513d74C8cf14cf4747E6AaA6420FF64ee9e";
    private const string Operator = "0x1111111111111111111111111111111111111111";
    private const string FeeRecipient = "0x2222222222222222222222222222222222222222";
    private static readonly string Salt = "0x" + string.Concat(Enumerable.Repeat("ab", 32));
    private static readonly BigInteger TimelockBlock = 21_000_000;

    private static string Strip0x(string hex) => hex.StartsWith("0x") ? hex[2..] : hex;
    private static string Keccak(string hex) => Sha3Keccack.Current.CalculateHash(hex.HexToByteArray()).ToHex(true);

    [Fact]
    public void BuildLockRecipient_PredictsTimelockRecipientAddressAndDeployCalldata()
    {
        string args = AbiParamEncoder.Encode(
            new[] { "address", "address", "uint256" },
            new object?[] { PM, Operator, TimelockBlock });
        string initCode = LockRecipientBytecode.TIMELOCK + Strip0x(args);
        string expected = AddressValidator.GetCreate2Address(
            Constants.CANONICAL_CREATE2_DEPLOYER, Salt.HexToByteArray(), Keccak(initCode).HexToByteArray());

        var @out = Lock.BuildLockRecipient(new TimelockLockRecipientInput(PM, Operator, TimelockBlock, Salt));

        Assert.Equal(expected, @out.PredictedAddress);
        Assert.Equal("0x" + Strip0x(Salt) + Strip0x(initCode), @out.DeployData);
    }

    [Fact]
    public void BuildLockRecipient_ProducesADistinctAddressPerMode()
    {
        var timelockOnly = Lock.BuildLockRecipient(new TimelockLockRecipientInput(PM, Operator, TimelockBlock, Salt));
        var forwarder = Lock.BuildLockRecipient(
            new FeesForwarderLockRecipientInput(PM, Operator, TimelockBlock, Salt, FeeRecipient));
        Assert.NotEqual(timelockOnly.PredictedAddress, forwarder.PredictedAddress);
    }

    [Fact]
    public void BuildLockRecipient_SupportsTheBuybackBurnMode()
    {
        var @out = Lock.BuildLockRecipient(new BuybackBurnLockRecipientInput(
            PM, Operator, TimelockBlock, Salt,
            Token: "0x15d0e0c55a3e7ee67152ad7e89acf164253ff68d",
            Currency: "0x0000000000000000000000000000000000000000",
            MinTokenBurnAmount: 1_000));
        Assert.Matches(new Regex("^0x[0-9a-fA-F]{40}$"), @out.PredictedAddress);
    }

    // Pins the audited creation bytecode so an accidental recompile/version bump fails loudly.

    [Fact]
    public void CreationBytecodePins_TimelockBytecodeHashIsUnchanged() =>
        Assert.Equal("0x2191c5153dfbfe1eff2d9e1140ea84188b935273eb299c3afbc4f9a82ce8203c",
            Keccak(LockRecipientBytecode.TIMELOCK));

    [Fact]
    public void CreationBytecodePins_FeesForwarderBytecodeHashIsUnchanged() =>
        Assert.Equal("0x507a9a1b056e76a6d3fa727c9cd50aeae62665594832e50dc977e1864de9e539",
            Keccak(LockRecipientBytecode.FEES_FORWARDER));
}
