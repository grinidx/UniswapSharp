using System.Text.RegularExpressions;
using AwesomeAssertions;
using UniswapSharp.Flashtestations.Crypto;
using UniswapSharp.Flashtestations.Types;

namespace UniswapSharp.Testing.Flashtestations;

// Ported 1:1 from sdks/flashtestations-sdk/test/crypto/workload.test.ts
public class WorkloadTests
{
    // Register vectors copied verbatim from the upstream test.
    private const string TdAttributes = "0x0000001000000000";
    private const string Xfam = "0xe702060000000000";
    private const string MrConfigId = "0x000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000";
    private const string Rtmr1 = "0xa7157e7c5f932e9babac9209d4527ec9ed837b8e335a931517677fa746db51ee56062e3324e266e3f39ec26a516f4f71";
    private const string Rtmr2 = "0xe63560e50830e22fbc9b06cdce8afe784bf111e4251256cf104050f1347cd4ad9f30da408475066575145da0b098a124";
    private const string Rtmr3 = "0x000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000";

    private const string Mrtd0 = "0x47a1cc074b914df8596bad0ed13d50d561ad1effc7f7cc530ab86da7ea49ffc03e57e7da829f8cba9c629c3970505323";
    private const string Mrtd1 = "0x202c7d38558f7cfa086feca5a23d62fa071cceb0bd55dbd06eeb4cebbd3c204c209f5551914d41ce433fb7fd67cc7136";
    private const string Mrtd2 = "0x3c372ef16cb892bffd91163b8b92322abee6be34473b845bc63075072c2c0d5ba805f314afaddade64437f50018cfbd5";

    private const string Rtmr0A = "0x00e1dad5455e5fa87974edb69e13296dd1ba9fa86356d70b68be15dd5d36767643904de1893c1b4d47fc8d3a90675391";
    private const string Rtmr0B = "0x6da49936a0649f6970be5df8bf7ba0d2efb66a96216c11cc65ac348432a07cfaab037b173e22c54d3f10d59327e7fbc9";
    private const string Rtmr0C = "0xf5cea78565e130d0e2e93429f20d269fa60aaa6bee68dd27afec0f85e3ccb885f4681ba9885b06a2ae8d202f356785a9";

    private static SingularWorkloadMeasurementRegisters ValidRegisters() => new()
    {
        Tdattributes = TdAttributes,
        Xfam = Xfam,
        Mrtd = Mrtd0,
        Mrconfigid = MrConfigId,
        Rtmr0 = Rtmr0A,
        Rtmr1 = Rtmr1,
        Rtmr2 = Rtmr2,
        Rtmr3 = Rtmr3,
    };

    // Helper mirroring the upstream `createHex96`: pad `prefix` to exactly 96 hex chars.
    private static string CreateHex96(string prefix)
    {
        var cleanPrefix = prefix.StartsWith("0x", StringComparison.Ordinal) ? prefix[2..] : prefix;
        var padding = new string('0', 96 - cleanPrefix.Length);
        return $"0x{cleanPrefix}{padding}";
    }

    // ----- computeWorkloadId -----

    [Fact]
    public void ComputeWorkloadId_WithValidRegisters()
    {
        var result = Workload.ComputeWorkloadId(ValidRegisters());

        Regex.IsMatch(result, "^0x[0-9a-f]{64}$").Should().BeTrue();
        result.Length.Should().Be(66); // 0x + 64 hex chars
    }

    [Fact]
    public void ComputeWorkloadId_ProducesConsistentResults()
    {
        var result1 = Workload.ComputeWorkloadId(ValidRegisters());
        var result2 = Workload.ComputeWorkloadId(ValidRegisters());

        result1.Should().Be(result2);
    }

    [Fact]
    public void ComputeWorkloadId_ProducesDifferentResultsForDifferentInputs()
    {
        var modifiedRegisters = ValidRegisters() with { Mrtd = CreateHex96("0xdf0123456789abcdef") };

        var result1 = Workload.ComputeWorkloadId(ValidRegisters());
        var result2 = Workload.ComputeWorkloadId(modifiedRegisters);

        result1.Should().NotBe(result2);
    }

    [Fact]
    public void ComputeWorkloadId_ValidatesInputRegisters()
    {
        var invalidRegisters = ValidRegisters() with { Tdattributes = "0xinvalid" };

        var act = () => Workload.ComputeWorkloadId(invalidRegisters);

        act.Should().Throw<ValidationError>()
            .WithMessage("Invalid tdattributes: expected 16 hex characters, got 7");
    }

    [Fact]
    public void ComputeWorkloadId_ValidatesRegisterLengths()
    {
        var invalidRegisters = ValidRegisters() with { Tdattributes = "0x0" };

        var act = () => Workload.ComputeWorkloadId(invalidRegisters);

        act.Should().Throw<ValidationError>()
            .WithMessage("Invalid tdattributes: expected 16 hex characters, got 1");
    }

    // ----- integration tests -----

    [Fact]
    public void Integration_CompleteWorkloadIdComputationFlow()
    {
        // Same workload ID as an actual TDX report in the Solidity test code.
        const string expectedWorkloadId =
            "0x952569f637f3f7e36cd8f5a7578ae4d03a1cb05ddaf33b35d3054464bb1c862e";

        // Registers with non-0x prefix.
        var registers = new SingularWorkloadMeasurementRegisters
        {
            Tdattributes = "0000001000000000",
            Xfam = "e702060000000000",
            Mrtd = "47a1cc074b914df8596bad0ed13d50d561ad1effc7f7cc530ab86da7ea49ffc03e57e7da829f8cba9c629c3970505323",
            Mrconfigid = "000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000",
            Rtmr0 = "00e1dad5455e5fa87974edb69e13296dd1ba9fa86356d70b68be15dd5d36767643904de1893c1b4d47fc8d3a90675391",
            Rtmr1 = "a7157e7c5f932e9babac9209d4527ec9ed837b8e335a931517677fa746db51ee56062e3324e266e3f39ec26a516f4f71",
            Rtmr2 = "e63560e50830e22fbc9b06cdce8afe784bf111e4251256cf104050f1347cd4ad9f30da408475066575145da0b098a124",
            Rtmr3 = "000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000",
        };

        var workloadId = Workload.ComputeWorkloadId(registers);

        workloadId.Should().Be(expectedWorkloadId);

        // Verify reproducibility.
        Workload.ComputeWorkloadId(registers).Should().Be(workloadId);
    }

    [Fact]
    public void Integration_DifferentSetOfRegisters()
    {
        const string expectedWorkloadId =
            "0xc1978eb1e3db791ebcdf41be6577209cb1a555f9fff06b65abe4d3baf92811a3";

        var registers = new SingularWorkloadMeasurementRegisters
        {
            Tdattributes = "0x0000001000000000",
            Xfam = "0xe702060000000000",
            Mrtd = "0x47a1cc074b914df8596bad0ed13d50d561ad1effc7f7cc530ab86da7ea49ffc03e57e7da829f8cba9c629c3970505323",
            Mrconfigid = "0x000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000",
            Rtmr0 = "0x00e1dad5455e5fa87974edb69e13296dd1ba9fa86356d70b68be15dd5d36767643904de1893c1b4d47fc8d3a90675391",
            Rtmr1 = "0xa7157e7c5f932e9babac9209d4527ec9ed837b8e335a931517677fa746db51ee56062f3324e266e3f39ec26a516f4f71",
            Rtmr2 = "0xe63561e50830e22fbc9b06cdce8afe784bf112e4251256cf104050f1347cd4ad9f30da408475066575145da0b098a124",
            Rtmr3 = "0x000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000",
        };

        var workloadId = Workload.ComputeWorkloadId(registers);

        workloadId.Should().Be(expectedWorkloadId);
        Workload.ComputeWorkloadId(registers).Should().Be(workloadId);
    }

    // ----- expandToSingularRegisters -----

    private static WorkloadMeasurementRegisters BaseFlexible(HexValues mrtd, HexValues rtmr0) => new()
    {
        Tdattributes = TdAttributes,
        Xfam = Xfam,
        Mrconfigid = MrConfigId,
        Rtmr1 = Rtmr1,
        Rtmr2 = Rtmr2,
        Rtmr3 = Rtmr3,
        Mrtd = mrtd,
        Rtmr0 = rtmr0,
    };

    [Fact]
    public void Expand_SingleWhenBothSingle()
    {
        var registers = BaseFlexible(Mrtd0, Rtmr0A);

        var result = Workload.ExpandToSingularRegisters(registers);

        result.Should().HaveCount(1);
        result[0].Should().Be(new SingularWorkloadMeasurementRegisters
        {
            Tdattributes = TdAttributes,
            Xfam = Xfam,
            Mrconfigid = MrConfigId,
            Rtmr1 = Rtmr1,
            Rtmr2 = Rtmr2,
            Rtmr3 = Rtmr3,
            Mrtd = Mrtd0,
            Rtmr0 = Rtmr0A,
        });
    }

    [Fact]
    public void Expand_MrtdArrayWithSingleRtmr0()
    {
        var registers = BaseFlexible(new[] { Mrtd0, Mrtd1 }, Rtmr0A);

        var result = Workload.ExpandToSingularRegisters(registers);

        result.Should().HaveCount(2);
        result[0].Mrtd.Should().Be(Mrtd0);
        result[0].Rtmr0.Should().Be(Rtmr0A);
        result[1].Mrtd.Should().Be(Mrtd1);
        result[1].Rtmr0.Should().Be(Rtmr0A);
    }

    [Fact]
    public void Expand_Rtmr0ArrayWithSingleMrtd()
    {
        var registers = BaseFlexible(Mrtd0, new[] { Rtmr0A, Rtmr0B });

        var result = Workload.ExpandToSingularRegisters(registers);

        result.Should().HaveCount(2);
        result[0].Mrtd.Should().Be(Mrtd0);
        result[0].Rtmr0.Should().Be(Rtmr0A);
        result[1].Mrtd.Should().Be(Mrtd0);
        result[1].Rtmr0.Should().Be(Rtmr0B);
    }

    [Fact]
    public void Expand_CartesianProductWhenBothArrays()
    {
        var registers = BaseFlexible(new[] { Mrtd0, Mrtd1 }, new[] { Rtmr0A, Rtmr0B });

        var result = Workload.ExpandToSingularRegisters(registers);

        result.Should().HaveCount(4);

        result[0].Mrtd.Should().Be(Mrtd0);
        result[0].Rtmr0.Should().Be(Rtmr0A);
        result[1].Mrtd.Should().Be(Mrtd0);
        result[1].Rtmr0.Should().Be(Rtmr0B);
        result[2].Mrtd.Should().Be(Mrtd1);
        result[2].Rtmr0.Should().Be(Rtmr0A);
        result[3].Mrtd.Should().Be(Mrtd1);
        result[3].Rtmr0.Should().Be(Rtmr0B);

        foreach (var singular in result)
        {
            singular.Tdattributes.Should().Be(TdAttributes);
            singular.Xfam.Should().Be(Xfam);
            singular.Rtmr1.Should().Be(Rtmr1);
        }
    }

    [Fact]
    public void Expand_LargeCartesianProduct3X3()
    {
        var registers = BaseFlexible(new[] { Mrtd0, Mrtd1, Mrtd2 }, new[] { Rtmr0A, Rtmr0B, Rtmr0C });

        var result = Workload.ExpandToSingularRegisters(registers);

        result.Should().HaveCount(9);

        var combinations = result.Select(r => $"{r.Mrtd}_{r.Rtmr0}").ToHashSet();
        combinations.Count.Should().Be(9);
    }

    [Fact]
    public void Expand_ThrowsForEmptyMrtdArray()
    {
        var registers = BaseFlexible(Array.Empty<string>(), Rtmr0A);

        var act = () => Workload.ExpandToSingularRegisters(registers);

        act.Should().Throw<ValidationError>().WithMessage("mrtd array cannot be empty");
    }

    [Fact]
    public void Expand_ThrowsForEmptyRtmr0Array()
    {
        var registers = BaseFlexible(Mrtd0, Array.Empty<string>());

        var act = () => Workload.ExpandToSingularRegisters(registers);

        act.Should().Throw<ValidationError>().WithMessage("rtmr0 array cannot be empty");
    }

    [Fact]
    public void Expand_ValidatesArrayElementLengths()
    {
        var registers = BaseFlexible(new[] { Mrtd0, "0xinvalid" }, Rtmr0A);

        var act = () => Workload.ExpandToSingularRegisters(registers);

        act.Should().Throw<ValidationError>().WithMessage("*Invalid mrtd[1]*");
    }

    [Fact]
    public void Expand_ValidatesArrayElementsAreHex()
    {
        var registers = BaseFlexible(new[] { Mrtd0, "invalid" }, Rtmr0A);
        var act = () => Workload.ExpandToSingularRegisters(registers);
        act.Should().Throw<ValidationError>().WithMessage("*Invalid mrtd[1]*");

        var invalidRegisters = BaseFlexible(new[] { Mrtd0 }, new[] { Rtmr0A, "invalid" });
        var act2 = () => Workload.ExpandToSingularRegisters(invalidRegisters);
        act2.Should().Throw<ValidationError>().WithMessage("*Invalid rtmr0[1]*");
    }

    // ----- computeAllWorkloadIds -----

    [Fact]
    public void ComputeAll_SingleIdForSingularRegisters()
    {
        var registers = BaseFlexible(Mrtd0, Rtmr0A);

        var result = Workload.ComputeAllWorkloadIds(registers);

        result.Should().HaveCount(1);
        Regex.IsMatch(result[0], "^0x[0-9a-f]{64}$").Should().BeTrue();
    }

    [Fact]
    public void ComputeAll_MultipleIdsForArrayRegisters()
    {
        var registers = BaseFlexible(new[] { Mrtd0, Mrtd1 }, Rtmr0A);

        var result = Workload.ComputeAllWorkloadIds(registers);

        result.Should().HaveCount(2);
        Regex.IsMatch(result[0], "^0x[0-9a-f]{64}$").Should().BeTrue();
        Regex.IsMatch(result[1], "^0x[0-9a-f]{64}$").Should().BeTrue();
        result[0].Should().NotBe(result[1]);
    }

    [Fact]
    public void ComputeAll_AllIdsForCartesianProduct()
    {
        var registers = BaseFlexible(new[] { Mrtd0, Mrtd1 }, new[] { Rtmr0A, Rtmr0B });

        var result = Workload.ComputeAllWorkloadIds(registers);

        result.Should().HaveCount(4);
        foreach (var id in result)
        {
            Regex.IsMatch(id, "^0x[0-9a-f]{64}$").Should().BeTrue();
        }

        result.ToHashSet().Count.Should().Be(4);
    }

    [Fact]
    public void ComputeAll_Deterministic()
    {
        var registers = BaseFlexible(new[] { Mrtd0, Mrtd1 }, Rtmr0A);

        var result1 = Workload.ComputeAllWorkloadIds(registers);
        var result2 = Workload.ComputeAllWorkloadIds(registers);

        result1.Should().Equal(result2);
    }

    // ----- matchesAnyWorkloadId -----

    [Fact]
    public void Matches_TrueWhenSingleRegisterMatches()
    {
        var registers = BaseFlexible(Mrtd0, Rtmr0A);
        var singularRegisters = new SingularWorkloadMeasurementRegisters
        {
            Tdattributes = TdAttributes,
            Xfam = Xfam,
            Mrconfigid = MrConfigId,
            Rtmr1 = Rtmr1,
            Rtmr2 = Rtmr2,
            Rtmr3 = Rtmr3,
            Mrtd = Mrtd0,
            Rtmr0 = Rtmr0A,
        };

        var workloadId = Workload.ComputeWorkloadId(singularRegisters);

        Workload.MatchesAnyWorkloadId(registers, workloadId).Should().BeTrue();
    }

    [Fact]
    public void Matches_FalseWhenSingleRegisterDoesNotMatch()
    {
        var registers = BaseFlexible(Mrtd0, Rtmr0A);

        Workload.MatchesAnyWorkloadId(
            registers,
            "0x0000000000000000000000000000000000000000000000000000000000000000").Should().BeFalse();
    }

    [Fact]
    public void Matches_TrueWhenAnyArrayRegisterMatches()
    {
        var registers = BaseFlexible(new[] { Mrtd0, Mrtd1 }, Rtmr0A);

        var allIds = Workload.ComputeAllWorkloadIds(registers);

        Workload.MatchesAnyWorkloadId(registers, allIds[0]).Should().BeTrue();
        Workload.MatchesAnyWorkloadId(registers, allIds[1]).Should().BeTrue();
    }

    [Fact]
    public void Matches_FalseWhenNoArrayRegisterMatches()
    {
        var registers = BaseFlexible(new[] { Mrtd0, Mrtd1 }, Rtmr0A);

        Workload.MatchesAnyWorkloadId(
            registers,
            "0x0000000000000000000000000000000000000000000000000000000000000000").Should().BeFalse();
    }

    [Fact]
    public void Matches_CartesianProductMatching()
    {
        var registers = BaseFlexible(new[] { Mrtd0, Mrtd1 }, new[] { Rtmr0A, Rtmr0B });

        var allIds = Workload.ComputeAllWorkloadIds(registers);

        foreach (var id in allIds)
        {
            Workload.MatchesAnyWorkloadId(registers, id).Should().BeTrue();
        }

        Workload.MatchesAnyWorkloadId(
            registers,
            "0x0000000000000000000000000000000000000000000000000000000000000000").Should().BeFalse();
    }

    // ----- validation edge cases -----

    [Fact]
    public void Validation_RejectsSingularValidationWithArrayMrtd()
    {
        // C#'s type system prevents an array from reaching the singular ComputeWorkloadId; the
        // upstream runtime guard is exercised directly via the singular validator instead.
        var registers = BaseFlexible(new[] { Mrtd0 }, Rtmr0A);

        var act = () => Validation.ValidateSingularWorkloadMeasurementRegisters(registers);

        act.Should().Throw<ValidationError>().WithMessage("mrtd must be a single value, not an array");
    }

    [Fact]
    public void Validation_RejectsSingularValidationWithArrayRtmr0()
    {
        var registers = BaseFlexible(Mrtd0, new[] { Rtmr0A });

        var act = () => Validation.ValidateSingularWorkloadMeasurementRegisters(registers);

        act.Should().Throw<ValidationError>().WithMessage("rtmr0 must be a single value, not an array");
    }

    [Fact]
    public void Validation_MixedCaseHexInArrays()
    {
        var registers = BaseFlexible(
            new[]
            {
                "0x47A1CC074B914DF8596BAD0ED13D50D561AD1EFFC7F7CC530AB86DA7EA49FFC03E57E7DA829F8CBA9C629C3970505323",
                Mrtd1,
            },
            Rtmr0A);

        var act = () => Workload.ExpandToSingularRegisters(registers);

        act.Should().NotThrow();
    }
}
