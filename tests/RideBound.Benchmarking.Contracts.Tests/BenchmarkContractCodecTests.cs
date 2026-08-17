using System.Buffers;
using System.Diagnostics;
using System.Reflection;
using System.Text;
using System.Text.Json;
using RideBound.Benchmarking.Contracts;

namespace RideBound.Benchmarking.Contracts.Tests;

public sealed class BenchmarkContractCodecTests
{
    public static TheoryData<string, Type> PositiveDocuments => new()
    {
        { "dataset-descriptor.json", typeof(DatasetDescriptor) },
        { "normalization-report.json", typeof(NormalizationReport) },
        { "scenario-content.json", typeof(ScenarioContent) },
        { "benchmark-plan.json", typeof(BenchmarkPlan) },
        { "run-record.json", typeof(RunRecord) },
        { "observation-index-row.json", typeof(ObservationIndexRow) },
        { "failure-record.json", typeof(FailureRecord) },
        { "exclusion-record.json", typeof(ExclusionRecord) },
        { "metric-row.json", typeof(MetricRow) },
        { "bundle-manifest.json", typeof(LogicalBundleManifest) },
    };

    public static TheoryData<string, string> FailureCodesAndStages => new()
    {
        { "input.invalid", "preflight" },
        { "artifact.mismatch", "preflight" },
        { "artifact.mismatch", "postflight" },
        { "capability.divergence", "negotiation" },
        { "process.start-failed", "execution" },
        { "process.crash", "execution" },
        { "process.cancelled", "execution" },
        { "harness.persistence-incomplete", "persistence" },
        { "resource.wall-time-exceeded", "execution" },
        { "resource.cpu-time-exceeded", "execution" },
        { "resource.memory-exceeded", "execution" },
        { "resource.process-count-exceeded", "execution" },
        { "resource.stdin-bytes-exceeded", "execution" },
        { "resource.stdout-bytes-exceeded", "execution" },
        { "resource.stderr-bytes-exceeded", "execution" },
        { "solver.unknown", "decision" },
        { "protocol.invalid-output", "parsing" },
        { "protocol.incomplete-output", "completion" },
        { "state.divergence", "validation" },
        { "metric.oracle-mismatch", "metrics" },
        { "bundle.invalid", "packaging" },
    };

    public static TheoryData<string> ExclusionRuleIds => new()
    {
        "source.license-not-accepted",
        "source.checksum-mismatch",
        "source.invalid-record",
        "source.unreachable-node-pair",
        "scenario.exceeds-declared-capability",
        "scenario.unsupported-position-model",
        "arm.missing-required-capability",
        "arm.incomparable-pairing-class",
    };

    [Theory]
    [MemberData(nameof(PositiveDocuments))]
    public void Positive_fixture_decodes_and_round_trips_to_exact_canonical_bytes(
        string fileName,
        Type runtimeType)
    {
        var bytes = File.ReadAllBytes(Path.Combine(FixturePaths.PositiveRoot, fileName));
        var first = Decode(runtimeType, bytes);

        Assert.True(first.Success, first.Error?.ToString());
        Assert.NotNull(first.Canonical);

        var second = Decode(runtimeType, first.Canonical!);

        Assert.True(second.Success, second.Error?.ToString());
        Assert.Equal(first.Canonical, second.Canonical);
    }

    [Fact]
    public void Property_order_does_not_change_scenario_canonical_bytes_or_identity()
    {
        var original = File.ReadAllBytes(
            Path.Combine(FixturePaths.PositiveRoot, "scenario-content.json"));
        var reordered = ReverseTopLevelProperties(original);
        var first = BenchmarkContractCodec.Decode<ScenarioContent>(original);
        var second = BenchmarkContractCodec.Decode<ScenarioContent>(reordered);

        Assert.True(first.IsSuccess, first.Error?.ToString());
        Assert.True(second.IsSuccess, second.Error?.ToString());
        Assert.Equal(first.CanonicalBytes, second.CanonicalBytes);
        Assert.Equal(
            BenchmarkIdentity.CalculateScenario(first.CanonicalBytes!),
            BenchmarkIdentity.CalculateScenario(second.CanonicalBytes!));
    }

    [Theory]
    [MemberData(nameof(PositiveDocuments))]
    public async Task Nested_property_permutation_and_parallel_decode_are_exact_for_every_document(
        string fileName,
        Type runtimeType)
    {
        var original = File.ReadAllBytes(Path.Combine(FixturePaths.PositiveRoot, fileName));
        var permuted = ReverseObjectPropertiesRecursively(original);
        var expected = Decode(runtimeType, original);
        var changed = Decode(runtimeType, permuted);

        Assert.True(expected.Success, expected.Error?.ToString());
        Assert.True(changed.Success, changed.Error?.ToString());
        Assert.Equal(expected.Canonical, changed.Canonical);

        var tasks = Enumerable.Range(0, 16)
            .Select(_ => Task.Run(() => Decode(runtimeType, permuted)))
            .ToArray();
        var parallel = await Task.WhenAll(tasks);
        Assert.All(parallel, result => Assert.True(result.Success, result.Error?.ToString()));
        Assert.All(parallel, result => Assert.Equal(expected.Canonical, result.Canonical));
    }

    [Theory]
    [InlineData("duplicate-property.json", typeof(DatasetDescriptor), BenchmarkContractErrorCode.DuplicateProperty)]
    [InlineData("invalid-unicode.json", typeof(DatasetDescriptor), BenchmarkContractErrorCode.InvalidUnicode)]
    [InlineData("null-value.json", typeof(DatasetDescriptor), BenchmarkContractErrorCode.NullNotAllowed)]
    [InlineData("noninteger-number.json", typeof(MetricRow), BenchmarkContractErrorCode.NonIntegerNumber)]
    [InlineData("integer-overflow.json", typeof(MetricRow), BenchmarkContractErrorCode.IntegerOutOfRange)]
    [InlineData("unknown-field.json", typeof(DatasetDescriptor), BenchmarkContractErrorCode.UnknownField)]
    [InlineData("wrong-enum.json", typeof(DatasetDescriptor), BenchmarkContractErrorCode.InvalidValue)]
    [InlineData("missing-required.json", typeof(DatasetDescriptor), BenchmarkContractErrorCode.MissingRequiredField)]
    [InlineData("scenario-self-hash.json", typeof(ScenarioContent), BenchmarkContractErrorCode.SelfHashForbidden)]
    public void Negative_fixture_fails_with_typed_code_and_path(
        string fileName,
        Type runtimeType,
        BenchmarkContractErrorCode expectedCode)
    {
        var bytes = File.ReadAllBytes(Path.Combine(FixturePaths.NegativeRoot, fileName));
        var result = Decode(runtimeType, bytes);

        Assert.False(result.Success);
        Assert.Equal(expectedCode, result.Error!.Code);
        Assert.StartsWith("$", result.Error.Path, StringComparison.Ordinal);
    }

    [Fact]
    public void Semantic_invariants_reject_conservation_pairing_terminal_and_metric_mismatch()
    {
        var report = DecodeFixture<NormalizationReport>("normalization-report.json");
        var invalidReport = report with { InputRecordCount = report.InputRecordCount + 1 };
        Assert.Equal(
            BenchmarkContractErrorCode.ConditionalFieldViolation,
            BenchmarkContractValidator.Validate(invalidReport)!.Code);

        var plan = DecodeFixture<BenchmarkPlan>("benchmark-plan.json");
        var invalidPlan = plan with
        {
            PairingClassId = "wp4-common-candidate-v1",
        };
        Assert.Equal(
            BenchmarkContractErrorCode.InvalidValue,
            BenchmarkContractValidator.Validate(invalidPlan)!.Code);

        var run = DecodeFixture<RunRecord>("run-record.json");
        var invalidRun = run with { ExitCode = 1 };
        Assert.Equal(
            BenchmarkContractErrorCode.ConditionalFieldViolation,
            BenchmarkContractValidator.Validate(invalidRun)!.Code);

        var metric = DecodeFixture<MetricRow>("metric-row.json");
        var invalidMetric = metric with
        {
            ValueStatus = MetricValueStatus.Missing,
            MissingReasonId = null,
        };
        Assert.Equal(
            BenchmarkContractErrorCode.ConditionalFieldViolation,
            BenchmarkContractValidator.Validate(invalidMetric)!.Code);
    }

    [Theory]
    [MemberData(nameof(FailureCodesAndStages))]
    public void Failure_contract_accepts_every_code_only_at_its_canonical_stage(
        string code,
        string stage)
    {
        var failure = DecodeFixture<FailureRecord>("failure-record.json") with
        {
            Code = code,
            Stage = stage,
        };

        Assert.Null(BenchmarkContractValidator.Validate(failure));
        Assert.NotNull(
            BenchmarkContractValidator.Validate(
                failure with
                {
                    Stage = stage == "execution" ? "validation" : "execution",
                }));
    }

    [Theory]
    [MemberData(nameof(ExclusionRuleIds))]
    public void Every_exclusion_rule_is_typed_and_remains_pre_outcome(string ruleId)
    {
        var exclusion = DecodeFixture<ExclusionRecord>("exclusion-record.json") with
        {
            RuleId = ruleId,
        };

        Assert.Null(BenchmarkContractValidator.Validate(exclusion));
        Assert.NotNull(
            BenchmarkContractValidator.Validate(exclusion with { BeforeOutcome = false }));
    }

    [Fact]
    public void Canonical_sets_and_embedded_event_payload_are_checked_not_trusted()
    {
        var dataset = DecodeFixture<DatasetDescriptor>("dataset-descriptor.json");
        var invalidDataset = dataset with
        {
            AllowedUse = dataset.AllowedUse.Reverse().ToArray(),
        };
        Assert.Equal(
            BenchmarkContractErrorCode.InvalidValue,
            BenchmarkContractValidator.Validate(invalidDataset)!.Code);

        var scenario = DecodeFixture<ScenarioContent>("scenario-content.json");
        var eventRow = scenario.Events[0] with
        {
            PayloadSha256 = new string('0', 64),
        };
        var invalidScenario = scenario with { Events = [eventRow] };
        var error = BenchmarkContractValidator.Validate(invalidScenario);
        Assert.Equal(BenchmarkContractErrorCode.ConditionalFieldViolation, error!.Code);
        Assert.Equal("$.events[0].payloadSha256", error.Path);
    }

    [Fact]
    public void Identity_helpers_bind_frame_order_values_and_canonical_input()
    {
        var scenario = BenchmarkContractCodec.Decode<ScenarioContent>(
            File.ReadAllBytes(
                Path.Combine(FixturePaths.PositiveRoot, "scenario-content.json")));
        Assert.True(scenario.IsSuccess);

        var scenarioHash = BenchmarkIdentity.CalculateScenario(scenario.CanonicalBytes!);
        var changed = BenchmarkContractCodec.Encode(
            scenario.Value! with { ScenarioId = "tiny-public-mechanical-002" });
        Assert.NotEqual(scenarioHash, BenchmarkIdentity.CalculateScenario(changed));

        var planHash = new string('1', 64);
        Assert.NotEqual(
            BenchmarkIdentity.CalculateRun(planHash, scenarioHash, "b1", 0, 0),
            BenchmarkIdentity.CalculateRun(planHash, scenarioHash, "b1", 1, 0));

        Assert.Throws<ArgumentException>(
            () => BenchmarkIdentity.CalculateScenario("{ \"a\": 1 }"u8));
    }

    [Fact]
    public void Scenario_identity_precedes_report_identity_without_a_hash_cycle()
    {
        var scenario = BenchmarkContractCodec.Decode<ScenarioContent>(
            File.ReadAllBytes(
                Path.Combine(FixturePaths.PositiveRoot, "scenario-content.json")));
        var report = BenchmarkContractCodec.Decode<NormalizationReport>(
            File.ReadAllBytes(
                Path.Combine(FixturePaths.PositiveRoot, "normalization-report.json")));
        Assert.True(scenario.IsSuccess);
        Assert.True(report.IsSuccess);

        var scenarioContentSha256 = BenchmarkIdentity.CalculateFileSha256(
            scenario.CanonicalBytes!);
        var scenarioHash = BenchmarkIdentity.CalculateScenario(scenario.CanonicalBytes!);
        var boundReport = report.Value! with
        {
            ScenarioContentSha256 = scenarioContentSha256,
            ScenarioHash = scenarioHash,
        };
        var canonicalReport = BenchmarkContractCodec.Encode(boundReport);
        var reportHash = BenchmarkIdentity.CalculateNormalizationReport(canonicalReport);

        Assert.Equal(scenarioHash, boundReport.ScenarioHash);
        Assert.Equal(scenarioContentSha256, boundReport.ScenarioContentSha256);
        Assert.Equal(64, reportHash.Length);
        Assert.DoesNotContain(
            "normalizationReportHash",
            Encoding.UTF8.GetString(scenario.CanonicalBytes!),
            StringComparison.Ordinal);
    }

    [Fact]
    public void Hand_authored_plan_cannot_bypass_pairing_symmetry_or_b5_separation()
    {
        var fixture = DecodeFixture<BenchmarkPlan>("benchmark-plan.json");
        var b1 = fixture.Arms.Single() with
        {
            PairingClassId = "wp4-common-candidate-v1",
        };
        var c1 = b1 with { ArmId = "c1" };
        var symmetric = fixture with
        {
            Arms = [b1, c1],
            PairingClassId = "wp4-common-candidate-v1",
        };
        Assert.Null(BenchmarkContractValidator.Validate(symmetric));

        var asymmetric = symmetric with
        {
            Arms = [b1, c1 with { SolverWorkBudget = 1 }],
        };
        var asymmetricError = BenchmarkContractValidator.Validate(asymmetric);
        Assert.Equal("$.arms", asymmetricError!.Path);

        var mixedB5 = fixture with
        {
            Arms =
            [
                fixture.Arms.Single() with
                {
                    ArmId = "b5",
                    PairingClassId = "wp4-multiple-plan-v1",
                },
                fixture.Arms.Single() with
                {
                    ArmId = "c1",
                    PairingClassId = "wp4-multiple-plan-v1",
                },
            ],
            PairingClassId = "wp4-multiple-plan-v1",
        };
        Assert.Equal("$.arms", BenchmarkContractValidator.Validate(mixedB5)!.Path);
    }

    [Fact]
    public async Task Two_clean_vector_processes_match_each_other_and_published_vector()
    {
        using var vectorDocument = JsonDocument.Parse(
            File.ReadAllBytes(
                Path.Combine(
                    FixturePaths.RepositoryRoot,
                    "benchmarks",
                    "fixtures",
                    "wp6",
                    "contracts",
                    "identity-vectors.json")));
        var expectedObject = vectorDocument.RootElement.GetProperty("expected");
        var expected = string.Join(
            '|',
            expectedObject.GetProperty("scenarioHash").GetString(),
            expectedObject.GetProperty("normalizationReportHash").GetString(),
            expectedObject.GetProperty("planHash").GetString(),
            expectedObject.GetProperty("runId").GetString(),
            expectedObject.GetProperty("metricSetHash").GetString(),
            expectedObject.GetProperty("bundleHash").GetString());

        var first = await RunVectorProcess();
        var second = await RunVectorProcess();

        Assert.Equal(expected, first);
        Assert.Equal(first, second);
    }

    private static T DecodeFixture<T>(string fileName)
        where T : class, IBenchmarkDocument
    {
        var result = BenchmarkContractCodec.Decode<T>(
            File.ReadAllBytes(Path.Combine(FixturePaths.PositiveRoot, fileName)));
        Assert.True(result.IsSuccess, result.Error?.ToString());
        return result.Value!;
    }

    private static (bool Success, byte[]? Canonical, BenchmarkContractError? Error)
        Decode(Type type, byte[] bytes)
    {
        if (type == typeof(DatasetDescriptor))
        {
            return Convert(BenchmarkContractCodec.Decode<DatasetDescriptor>(bytes));
        }

        if (type == typeof(NormalizationReport))
        {
            return Convert(BenchmarkContractCodec.Decode<NormalizationReport>(bytes));
        }

        if (type == typeof(ScenarioContent))
        {
            return Convert(BenchmarkContractCodec.Decode<ScenarioContent>(bytes));
        }

        if (type == typeof(BenchmarkPlan))
        {
            return Convert(BenchmarkContractCodec.Decode<BenchmarkPlan>(bytes));
        }

        if (type == typeof(RunRecord))
        {
            return Convert(BenchmarkContractCodec.Decode<RunRecord>(bytes));
        }

        if (type == typeof(ObservationIndexRow))
        {
            return Convert(BenchmarkContractCodec.Decode<ObservationIndexRow>(bytes));
        }

        if (type == typeof(FailureRecord))
        {
            return Convert(BenchmarkContractCodec.Decode<FailureRecord>(bytes));
        }

        if (type == typeof(ExclusionRecord))
        {
            return Convert(BenchmarkContractCodec.Decode<ExclusionRecord>(bytes));
        }

        if (type == typeof(MetricRow))
        {
            return Convert(BenchmarkContractCodec.Decode<MetricRow>(bytes));
        }

        if (type == typeof(LogicalBundleManifest))
        {
            return Convert(BenchmarkContractCodec.Decode<LogicalBundleManifest>(bytes));
        }

        throw new ArgumentOutOfRangeException(nameof(type));
    }

    private static (bool Success, byte[]? Canonical, BenchmarkContractError? Error)
        Convert<T>(BenchmarkDecodeResult<T> result)
        where T : class, IBenchmarkDocument =>
        (result.IsSuccess, result.CanonicalBytes, result.Error);

    private static byte[] ReverseTopLevelProperties(byte[] source)
    {
        using var document = JsonDocument.Parse(source);
        var buffer = new ArrayBufferWriter<byte>();

        using (var writer = new Utf8JsonWriter(buffer))
        {
            writer.WriteStartObject();

            foreach (var property in document.RootElement.EnumerateObject().Reverse())
            {
                property.WriteTo(writer);
            }

            writer.WriteEndObject();
        }

        return buffer.WrittenSpan.ToArray();
    }

    private static byte[] ReverseObjectPropertiesRecursively(byte[] source)
    {
        using var document = JsonDocument.Parse(source);
        var buffer = new ArrayBufferWriter<byte>();

        using (var writer = new Utf8JsonWriter(buffer))
        {
            WriteReversed(document.RootElement, writer);
        }

        return buffer.WrittenSpan.ToArray();
    }

    private static void WriteReversed(JsonElement element, Utf8JsonWriter writer)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            writer.WriteStartObject();

            foreach (var property in element.EnumerateObject().Reverse())
            {
                writer.WritePropertyName(property.Name);
                WriteReversed(property.Value, writer);
            }

            writer.WriteEndObject();
            return;
        }

        if (element.ValueKind == JsonValueKind.Array)
        {
            writer.WriteStartArray();

            foreach (var item in element.EnumerateArray())
            {
                WriteReversed(item, writer);
            }

            writer.WriteEndArray();
            return;
        }

        element.WriteTo(writer);
    }

    private static async Task<string> RunVectorProcess()
    {
        var configuration = new DirectoryInfo(AppContext.BaseDirectory).Parent!.Name;
        var toolPath = Path.Combine(
            FixturePaths.RepositoryRoot,
            "tools",
            "RideBound.Wp6ContractVectors",
            "bin",
            configuration,
            "net10.0",
            "RideBound.Wp6ContractVectors.dll");
        Assert.True(File.Exists(toolPath), $"Vector tool was not built: {toolPath}");

        var startInfo = new ProcessStartInfo("dotnet")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        startInfo.ArgumentList.Add(toolPath);
        startInfo.ArgumentList.Add(FixturePaths.RepositoryRoot);

        using var process = Process.Start(startInfo)!;
        var stdout = await process.StandardOutput.ReadToEndAsync();
        var stderr = await process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();
        Assert.True(process.ExitCode == 0, stderr);
        return stdout.Trim();
    }
}
