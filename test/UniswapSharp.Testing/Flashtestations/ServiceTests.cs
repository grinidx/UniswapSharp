using AwesomeAssertions;
using UniswapSharp.Flashtestations.Crypto;
using UniswapSharp.Flashtestations.Rpc;
using UniswapSharp.Flashtestations.Types;
using UniswapSharp.Flashtestations.Verification;

namespace UniswapSharp.Testing.Flashtestations;

// Ported 1:1 from sdks/flashtestations-sdk/test/verification/service.test.ts
// The RpcClient is injected via a fake IRpcClientFactory (mirroring the upstream
// `jest.spyOn(rpcClientModule, 'RpcClient')`). The pure, deterministic dependencies
// (Workload.ComputeAllWorkloadIds and Chains.GetBlockExplorerUrl) are used for real rather than
// mocked; where the upstream mocks getBlockExplorerUrl to return '', the C# test exercises a chain
// whose real config has an empty explorer URL instead.
public class ServiceTests
{
    private readonly FakeRpcClientFactory _factory = new();

    private FlashtestationService Service => new(_factory);

    private FakeRpcClient Rpc => _factory.Client;

    private const string SourceLocator = "https://github.com/flashbots/flashbots-images/commit/b7c707667393cc4c0173786ee32ec3a79009b04f";

    // ===== with workload ID string =====

    private const string WorkloadId = "0xabcdef1234567890abcdef1234567890abcdef1234567890abcdef1234567890";

    private static ClientConfig StringConfig => new() { ChainId = 1301, RpcUrl = "https://test-rpc.example.com" };

    [Fact]
    public async Task String_TrueWhenWorkloadIdMatches()
    {
        Rpc.GetFlashtestationEvent.Returns(new FlashtestationEvent
        {
            Caller = "0xbuilder123",
            WorkloadId = WorkloadId,
            Version = 1,
            BlockContentHash = "0xblockhash",
            CommitHash = "abc123def456",
            SourceLocators = new[] { SourceLocator },
        });
        Rpc.GetBlock.Returns(new EvmBlock { Number = 12345, Hash = "0xblockhash" });

        var result = await Service.VerifyFlashtestationInBlockAsync(WorkloadId, "latest", StringConfig);

        result.Should().BeEquivalentTo(new VerificationResult
        {
            IsBuiltByExpectedTee = true,
            BlockExplorerLink = "https://sepolia.uniscan.xyz/block/12345",
            WorkloadMetadata = new WorkloadMetadata
            {
                WorkloadId = WorkloadId,
                CommitHash = "abc123def456",
                BuilderAddress = "0xbuilder123",
                Version = 1,
                SourceLocators = new[] { SourceLocator },
            },
        });

        Rpc.GetFlashtestationEvent.LastCall.Should().Be((BlockParameter)"latest");
        Rpc.GetBlock.LastCall.Should().Be((BlockParameter)"latest");
    }

    [Fact]
    public async Task String_NormalizesWorkloadIdWith0xPrefix()
    {
        const string workloadIdWithoutPrefix = "abcdef1234567890abcdef1234567890abcdef1234567890abcdef1234567890";
        Rpc.GetFlashtestationEvent.Returns(new FlashtestationEvent
        {
            Caller = "0xbuilder123",
            WorkloadId = "0x" + workloadIdWithoutPrefix,
            Version = 1,
            BlockContentHash = "0xblockhash",
            CommitHash = "abc123def456",
            SourceLocators = new[] { SourceLocator },
        });
        Rpc.GetBlock.Returns(new EvmBlock { Number = 12345, Hash = "0xblockhash" });

        var result = await Service.VerifyFlashtestationInBlockAsync(workloadIdWithoutPrefix, "latest", StringConfig);

        result.IsBuiltByExpectedTee.Should().BeTrue();
    }

    [Fact]
    public async Task String_CaseInsensitiveComparison()
    {
        const string workloadIdUpperCase = "0xABCDEF1234567890ABCDEF1234567890ABCDEF1234567890ABCDEF1234567890";
        Rpc.GetFlashtestationEvent.Returns(new FlashtestationEvent
        {
            Caller = "0xbuilder123",
            WorkloadId = workloadIdUpperCase.ToLowerInvariant(),
            Version = 1,
            BlockContentHash = "0xblockhash",
            CommitHash = "abc123def456",
            SourceLocators = new[] { SourceLocator },
        });
        Rpc.GetBlock.Returns(new EvmBlock { Number = 12345, Hash = "0xblockhash" });

        var result = await Service.VerifyFlashtestationInBlockAsync(workloadIdUpperCase, "latest", StringConfig);

        result.IsBuiltByExpectedTee.Should().BeTrue();
    }

    [Fact]
    public async Task String_FalseWhenNoFlashtestationTransaction()
    {
        Rpc.GetFlashtestationEvent.Returns(null);

        var result = await Service.VerifyFlashtestationInBlockAsync(WorkloadId, "latest", StringConfig);

        result.Should().BeEquivalentTo(new VerificationResult
        {
            IsBuiltByExpectedTee = false,
            BlockExplorerLink = null,
            WorkloadMetadata = null,
        });

        Rpc.GetFlashtestationEvent.LastCall.Should().Be((BlockParameter)"latest");
        Rpc.GetBlock.CallCount.Should().Be(0);
    }

    [Fact]
    public async Task String_FalseWhenWorkloadIdDoesNotMatch()
    {
        const string differentWorkloadId = "0x1111111111111111111111111111111111111111111111111111111111111111";
        Rpc.GetFlashtestationEvent.Returns(new FlashtestationEvent
        {
            Caller = "0xbuilder123",
            WorkloadId = differentWorkloadId,
            Version = 1,
            BlockContentHash = "0xblockhash",
            CommitHash = "abc123def456",
            SourceLocators = new[] { SourceLocator },
        });
        Rpc.GetBlock.Returns(new EvmBlock { Number = 12345, Hash = "0xblockhash" });

        var result = await Service.VerifyFlashtestationInBlockAsync(WorkloadId, "latest", StringConfig);

        result.Should().BeEquivalentTo(new VerificationResult
        {
            IsBuiltByExpectedTee = false,
            BlockExplorerLink = "https://sepolia.uniscan.xyz/block/12345",
            WorkloadMetadata = new WorkloadMetadata
            {
                WorkloadId = differentWorkloadId,
                CommitHash = "abc123def456",
                BuilderAddress = "0xbuilder123",
                Version = 1,
                SourceLocators = new[] { SourceLocator },
            },
        });

        Rpc.GetFlashtestationEvent.LastCall.Should().Be((BlockParameter)"latest");
        Rpc.GetBlock.LastCall.Should().Be((BlockParameter)"latest");
    }

    [Fact]
    public async Task String_HandlesEmptyBlockExplorerUrl()
    {
        // Upstream mocks getBlockExplorerUrl to return ''. Here we use a real chain
        // (Unichain Alphanet) whose configured explorer URL is empty.
        var config = new ClientConfig { ChainId = 22444422 };
        Rpc.GetFlashtestationEvent.Returns(new FlashtestationEvent
        {
            Caller = "0xbuilder123",
            WorkloadId = WorkloadId,
            Version = 1,
            BlockContentHash = "0xblockhash",
            CommitHash = "abc123def456",
        });
        Rpc.GetBlock.Returns(new EvmBlock { Number = 12345, Hash = "0xblockhash" });

        var result = await Service.VerifyFlashtestationInBlockAsync(WorkloadId, "latest", config);

        result.BlockExplorerLink.Should().BeNull();
    }

    [Fact]
    public async Task String_WorksWithDifferentBlockParameters()
    {
        Rpc.GetFlashtestationEvent.Returns(new FlashtestationEvent
        {
            Caller = "0xbuilder123",
            WorkloadId = WorkloadId,
            Version = 1,
            BlockContentHash = "0xblockhash",
            CommitHash = "abc123def456",
        });
        Rpc.GetBlock.Returns(new EvmBlock { Number = 54321, Hash = "0xblockhash" });

        await Service.VerifyFlashtestationInBlockAsync(WorkloadId, 54321, StringConfig);
        Rpc.GetFlashtestationEvent.LastCall.Should().Be((BlockParameter)54321);

        await Service.VerifyFlashtestationInBlockAsync(WorkloadId, "finalized", StringConfig);
        Rpc.GetFlashtestationEvent.LastCall.Should().Be((BlockParameter)"finalized");

        await Service.VerifyFlashtestationInBlockAsync(WorkloadId, "0xd431", StringConfig);
        Rpc.GetFlashtestationEvent.LastCall.Should().Be((BlockParameter)"0xd431");
    }

    [Fact]
    public async Task String_PropagatesBlockNotFoundError()
    {
        Rpc.GetFlashtestationEvent.Throws(new BlockNotFoundError("latest"));

        await ((Func<Task>)(() => Service.VerifyFlashtestationInBlockAsync(WorkloadId, "latest", StringConfig)))
            .Should().ThrowAsync<BlockNotFoundError>();
    }

    [Fact]
    public async Task String_PropagatesNetworkError()
    {
        Rpc.GetFlashtestationEvent.Throws(new NetworkError("Connection failed"));

        await ((Func<Task>)(() => Service.VerifyFlashtestationInBlockAsync(WorkloadId, "latest", StringConfig)))
            .Should().ThrowAsync<NetworkError>();
    }

    // ===== with WorkloadMeasurementRegisters =====

    private static WorkloadMeasurementRegisters Registers() => new()
    {
        Tdattributes = "0x0000001000000000",
        Xfam = "0xe702060000000000",
        Mrtd = "0x47a1cc074b914df8596bad0ed13d50d561ad1effc7f7cc530ab86da7ea49ffc03e57e7da829f8cba9c629c3970505323",
        Mrconfigid = "0x000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000",
        Rtmr0 = "0x00e1dad5455e5fa87974edb69e13296dd1ba9fa86356d70b68be15dd5d36767643904de1893c1b4d47fc8d3a90675391",
        Rtmr1 = "0xa7157e7c5f932e9babac9209d4527ec9ed837b8e335a931517677fa746db51ee56062e3324e266e3f39ec26a516f4f71",
        Rtmr2 = "0xe63560e50830e22fbc9b06cdce8afe784bf111e4251256cf104050f1347cd4ad9f30da408475066575145da0b098a124",
        Rtmr3 = "0x000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000",
    };

    private static ClientConfig RegistersConfig => new() { ChainId = 1301 };

    [Fact]
    public async Task Registers_ComputeAndVerify()
    {
        var registers = Registers();
        var computedWorkloadId = Workload.ComputeAllWorkloadIds(registers)[0];

        Rpc.GetFlashtestationEvent.Returns(new FlashtestationEvent
        {
            Caller = "0xbuilder456",
            WorkloadId = computedWorkloadId,
            Version = 1,
            BlockContentHash = "0xblockhash",
            CommitHash = "register-commit",
            SourceLocators = new[] { SourceLocator },
        });
        Rpc.GetBlock.Returns(new EvmBlock { Number = 99999, Hash = "0xblockhash" });

        var result = await Service.VerifyFlashtestationInBlockAsync(registers, "latest", RegistersConfig);

        result.Should().BeEquivalentTo(new VerificationResult
        {
            IsBuiltByExpectedTee = true,
            BlockExplorerLink = "https://sepolia.uniscan.xyz/block/99999",
            WorkloadMetadata = new WorkloadMetadata
            {
                WorkloadId = computedWorkloadId,
                CommitHash = "register-commit",
                BuilderAddress = "0xbuilder456",
                Version = 1,
                SourceLocators = new[] { SourceLocator },
            },
        });
    }

    [Fact]
    public async Task Registers_DifferentCaseStillMatches()
    {
        var registers = Registers();
        var computedWorkloadId = Workload.ComputeAllWorkloadIds(registers)[0];

        var uppercaseRegisters = new WorkloadMeasurementRegisters
        {
            Tdattributes = "0x" + registers.Tdattributes[2..].ToUpperInvariant(),
            Xfam = "0x" + registers.Xfam[2..].ToUpperInvariant(),
            Mrtd = "0x" + registers.Mrtd.Single[2..].ToUpperInvariant(),
            Mrconfigid = "0x" + registers.Mrconfigid[2..].ToUpperInvariant(),
            Rtmr0 = "0x" + registers.Rtmr0.Single[2..].ToUpperInvariant(),
            Rtmr1 = "0x" + registers.Rtmr1[2..].ToUpperInvariant(),
            Rtmr2 = "0x" + registers.Rtmr2[2..].ToUpperInvariant(),
            Rtmr3 = "0x" + registers.Rtmr3[2..].ToUpperInvariant(),
        };

        Rpc.GetFlashtestationEvent.Returns(new FlashtestationEvent
        {
            Caller = "0xbuilder456",
            WorkloadId = computedWorkloadId,
            Version = 1,
            BlockContentHash = "0xblockhash",
            CommitHash = "register-commit",
            SourceLocators = new[] { SourceLocator },
        });
        Rpc.GetBlock.Returns(new EvmBlock { Number = 99999, Hash = "0xblockhash" });

        var result = await Service.VerifyFlashtestationInBlockAsync(uppercaseRegisters, "latest", RegistersConfig);

        result.Should().BeEquivalentTo(new VerificationResult
        {
            IsBuiltByExpectedTee = true,
            BlockExplorerLink = "https://sepolia.uniscan.xyz/block/99999",
            WorkloadMetadata = new WorkloadMetadata
            {
                WorkloadId = computedWorkloadId,
                CommitHash = "register-commit",
                BuilderAddress = "0xbuilder456",
                Version = 1,
                SourceLocators = new[] { SourceLocator },
            },
        });
    }

    [Fact]
    public async Task Registers_FalseWhenComputedIdDoesNotMatch()
    {
        const string differentWorkloadId = "0x9999999999999999999999999999999999999999999999999999999999999999";
        var registers = Registers();

        Rpc.GetFlashtestationEvent.Returns(new FlashtestationEvent
        {
            Caller = "0xbuilder456",
            WorkloadId = differentWorkloadId,
            Version = 1,
            BlockContentHash = "0xblockhash",
            CommitHash = "register-commit",
            SourceLocators = new[] { SourceLocator },
        });
        Rpc.GetBlock.Returns(new EvmBlock { Number = 99999, Hash = "0xblockhash" });

        var result = await Service.VerifyFlashtestationInBlockAsync(registers, "latest", RegistersConfig);

        result.Should().BeEquivalentTo(new VerificationResult
        {
            IsBuiltByExpectedTee = false,
            BlockExplorerLink = "https://sepolia.uniscan.xyz/block/99999",
            WorkloadMetadata = new WorkloadMetadata
            {
                WorkloadId = differentWorkloadId,
                CommitHash = "register-commit",
                BuilderAddress = "0xbuilder456",
                Version = 1,
                SourceLocators = new[] { SourceLocator },
            },
        });
    }

    [Fact]
    public async Task Registers_PropagatesValidationErrors()
    {
        var alteredRegisters = Registers() with { Tdattributes = "0x000000100000000" }; // odd length (15)

        await ((Func<Task>)(() => Service.VerifyFlashtestationInBlockAsync(alteredRegisters, "latest", RegistersConfig)))
            .Should().ThrowAsync<ValidationError>()
            .WithMessage("Invalid tdattributes: expected 16 hex characters, got 15");
    }

    // ===== RpcClient configuration =====

    [Fact]
    public async Task Config_PassesChainIdToRpcClient()
    {
        Rpc.GetFlashtestationEvent.Returns(null);

        await Service.VerifyFlashtestationInBlockAsync("0xabc", "latest", new ClientConfig { ChainId = 1301 });

        _factory.LastConfig.Should().Be(new RpcClientConfig { ChainId = 1301, RpcUrl = null });
    }

    [Fact]
    public async Task Config_PassesCustomRpcUrlToRpcClient()
    {
        Rpc.GetFlashtestationEvent.Returns(null);

        await Service.VerifyFlashtestationInBlockAsync("0xabc", "latest", new ClientConfig { ChainId = 1301, RpcUrl = "https://custom.rpc" });

        _factory.LastConfig.Should().Be(new RpcClientConfig { ChainId = 1301, RpcUrl = "https://custom.rpc" });
    }

    // ===== with WorkloadMeasurementRegisters with multiple values =====

    [Fact]
    public async Task MultiValue_MatchesWhenAnyCombinationMatches()
    {
        var registers = new WorkloadMeasurementRegisters
        {
            Tdattributes = "0x0000001000000000",
            Xfam = "0xe702060000000000",
            Mrtd = new[]
            {
                "0x47a1cc074b914df8596bad0ed13d50d561ad1effc7f7cc530ab86da7ea49ffc03e57e7da829f8cba9c629c3970505323",
                "0x202c7d38558f7cfa086feca5a23d62fa071cceb0bd55dbd06eeb4cebbd3c204c209f5551914d41ce433fb7fd67cc7136",
            },
            Mrconfigid = "0x000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000",
            Rtmr0 = "0x00e1dad5455e5fa87974edb69e13296dd1ba9fa86356d70b68be15dd5d36767643904de1893c1b4d47fc8d3a90675391",
            Rtmr1 = "0xa7157e7c5f932e9babac9209d4527ec9ed837b8e335a931517677fa746db51ee56062e3324e266e3f39ec26a516f4f71",
            Rtmr2 = "0xe63560e50830e22fbc9b06cdce8afe784bf111e4251256cf104050f1347cd4ad9f30da408475066575145da0b098a124",
            Rtmr3 = "0x000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000",
        };
        var eventWorkloadId = Workload.ComputeAllWorkloadIds(registers)[0]; // first combination matches

        Rpc.GetFlashtestationEvent.Returns(new FlashtestationEvent
        {
            Caller = "0xbuilder789",
            WorkloadId = eventWorkloadId,
            Version = 1,
            BlockContentHash = "0xblockhash",
            CommitHash = "array-test-commit",
            SourceLocators = new[] { SourceLocator },
        });
        Rpc.GetBlock.Returns(new EvmBlock { Number = 88888, Hash = "0xblockhash" });

        var result = await Service.VerifyFlashtestationInBlockAsync(registers, "latest", RegistersConfig);

        result.Should().BeEquivalentTo(new VerificationResult
        {
            IsBuiltByExpectedTee = true,
            BlockExplorerLink = "https://sepolia.uniscan.xyz/block/88888",
            WorkloadMetadata = new WorkloadMetadata
            {
                WorkloadId = eventWorkloadId,
                CommitHash = "array-test-commit",
                BuilderAddress = "0xbuilder789",
                Version = 1,
                SourceLocators = new[] { SourceLocator },
            },
        });
    }

    [Fact]
    public async Task MultiValue_DoesNotMatchWhenNoCombinationMatches()
    {
        const string eventWorkloadId = "0xaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
        var registers = new WorkloadMeasurementRegisters
        {
            Tdattributes = "0x0000001000000000",
            Xfam = "0xe702060000000000",
            Mrtd = new[]
            {
                "0x47a1cc074b914df8596bad0ed13d50d561ad1effc7f7cc530ab86da7ea49ffc03e57e7da829f8cba9c629c3970505323",
                "0x202c7d38558f7cfa086feca5a23d62fa071cceb0bd55dbd06eeb4cebbd3c204c209f5551914d41ce433fb7fd67cc7136",
            },
            Mrconfigid = "0x000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000",
            Rtmr0 = "0x00e1dad5455e5fa87974edb69e13296dd1ba9fa86356d70b68be15dd5d36767643904de1893c1b4d47fc8d3a90675391",
            Rtmr1 = "0xa7157e7c5f932e9babac9209d4527ec9ed837b8e335a931517677fa746db51ee56062e3324e266e3f39ec26a516f4f71",
            Rtmr2 = "0xe63560e50830e22fbc9b06cdce8afe784bf111e4251256cf104050f1347cd4ad9f30da408475066575145da0b098a124",
            Rtmr3 = "0x000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000",
        };

        Rpc.GetFlashtestationEvent.Returns(new FlashtestationEvent
        {
            Caller = "0xbuilder789",
            WorkloadId = eventWorkloadId,
            Version = 1,
            BlockContentHash = "0xblockhash",
            CommitHash = "array-test-commit",
            SourceLocators = new[] { SourceLocator },
        });
        Rpc.GetBlock.Returns(new EvmBlock { Number = 88888, Hash = "0xblockhash" });

        var result = await Service.VerifyFlashtestationInBlockAsync(registers, "latest", RegistersConfig);

        result.Should().BeEquivalentTo(new VerificationResult
        {
            IsBuiltByExpectedTee = false,
            BlockExplorerLink = "https://sepolia.uniscan.xyz/block/88888",
            WorkloadMetadata = new WorkloadMetadata
            {
                WorkloadId = eventWorkloadId,
                CommitHash = "array-test-commit",
                BuilderAddress = "0xbuilder789",
                Version = 1,
                SourceLocators = new[] { SourceLocator },
            },
        });
    }

    [Fact]
    public async Task MultiValue_HandlesCartesianProductOfBothArrays()
    {
        var registers = new WorkloadMeasurementRegisters
        {
            Tdattributes = "0x0000001000000000",
            Xfam = "0xe702060000000000",
            Mrtd = new[]
            {
                "0x47a1cc074b914df8596bad0ed13d50d561ad1effc7f7cc530ab86da7ea49ffc03e57e7da829f8cba9c629c3970505323",
                "0x202c7d38558f7cfa086feca5a23d62fa071cceb0bd55dbd06eeb4cebbd3c204c209f5551914d41ce433fb7fd67cc7136",
            },
            Mrconfigid = "0x000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000",
            Rtmr0 = new[]
            {
                "0x00e1dad5455e5fa87974edb69e13296dd1ba9fa86356d70b68be15dd5d36767643904de1893c1b4d47fc8d3a90675391",
                "0x6da49936a0649f6970be5df8bf7ba0d2efb66a96216c11cc65ac348432a07cfaab037b173e22c54d3f10d59327e7fbc9",
            },
            Rtmr1 = "0xa7157e7c5f932e9babac9209d4527ec9ed837b8e335a931517677fa746db51ee56062e3324e266e3f39ec26a516f4f71",
            Rtmr2 = "0xe63560e50830e22fbc9b06cdce8afe784bf111e4251256cf104050f1347cd4ad9f30da408475066575145da0b098a124",
            Rtmr3 = "0x000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000",
        };
        var allIds = Workload.ComputeAllWorkloadIds(registers);
        var eventWorkloadId = allIds[^1]; // last combination matches

        Rpc.GetFlashtestationEvent.Returns(new FlashtestationEvent
        {
            Caller = "0xbuilder999",
            WorkloadId = eventWorkloadId,
            Version = 1,
            BlockContentHash = "0xblockhash",
            CommitHash = "cartesian-commit",
            SourceLocators = new[] { SourceLocator },
        });
        Rpc.GetBlock.Returns(new EvmBlock { Number = 77777, Hash = "0xblockhash" });

        var result = await Service.VerifyFlashtestationInBlockAsync(registers, "latest", RegistersConfig);

        result.Should().BeEquivalentTo(new VerificationResult
        {
            IsBuiltByExpectedTee = true,
            BlockExplorerLink = "https://sepolia.uniscan.xyz/block/77777",
            WorkloadMetadata = new WorkloadMetadata
            {
                WorkloadId = eventWorkloadId,
                CommitHash = "cartesian-commit",
                BuilderAddress = "0xbuilder999",
                Version = 1,
                SourceLocators = new[] { SourceLocator },
            },
        });
    }
}
