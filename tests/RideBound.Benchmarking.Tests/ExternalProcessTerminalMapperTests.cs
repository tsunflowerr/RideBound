using RideBound.Benchmarking.Execution;
using RideBound.Benchmarking.Storage;

namespace RideBound.Benchmarking.Tests;

public sealed class ExternalProcessTerminalMapperTests
{
    public static TheoryData<string, string, RawRunEvidenceRole> FailureEvidenceMatrix => new()
    {
        { "input.invalid", "preflight", RawRunEvidenceRole.Input },
        { "artifact.mismatch", "preflight", RawRunEvidenceRole.ArtifactPreflight },
        { "artifact.mismatch", "postflight", RawRunEvidenceRole.ArtifactPostflight },
        { "capability.divergence", "negotiation", RawRunEvidenceRole.Output },
        { "process.start-failed", "execution", RawRunEvidenceRole.ResourceSamples },
        { "process.crash", "execution", RawRunEvidenceRole.StandardError },
        { "process.cancelled", "execution", RawRunEvidenceRole.ResourceSamples },
        { "harness.persistence-incomplete", "persistence", RawRunEvidenceRole.ResourceSamples },
        { "resource.wall-time-exceeded", "execution", RawRunEvidenceRole.ResourceSamples },
        { "resource.cpu-time-exceeded", "execution", RawRunEvidenceRole.ResourceSamples },
        { "resource.memory-exceeded", "execution", RawRunEvidenceRole.ResourceSamples },
        { "resource.process-count-exceeded", "execution", RawRunEvidenceRole.ResourceSamples },
        { "resource.stdin-bytes-exceeded", "execution", RawRunEvidenceRole.Input },
        { "resource.stdout-bytes-exceeded", "execution", RawRunEvidenceRole.Output },
        { "resource.stderr-bytes-exceeded", "execution", RawRunEvidenceRole.StandardError },
        { "solver.unknown", "decision", RawRunEvidenceRole.Output },
        { "protocol.invalid-output", "parsing", RawRunEvidenceRole.Output },
        { "protocol.incomplete-output", "completion", RawRunEvidenceRole.Output },
        { "state.divergence", "validation", RawRunEvidenceRole.Output },
        { "metric.oracle-mismatch", "metrics", RawRunEvidenceRole.ResourceSamples },
        { "bundle.invalid", "packaging", RawRunEvidenceRole.ResourceSamples },
    };

    [Fact]
    public void Protocol_failure_is_bound_to_captured_output_evidence()
    {
        using var temp = new TestDirectory();
        var result = ResultWithEvidence(temp, "protocol.invalid-output");

        var submission = ExternalProcessTerminalMapper.CreateSubmission(
            Intent(),
            result,
            "2026-08-11T00:00:00Z",
            "2026-08-11T00:00:01Z",
            ["planned-runs"]);

        Assert.Equal(RawRunEvidenceRole.Output, submission.Failure!.EvidenceRole);
        Assert.Equal(6, submission.EvidenceSources.Count);
    }

    [Fact]
    public void Preflight_result_without_raw_files_requires_typed_persistence_recovery()
    {
        using var temp = new TestDirectory();
        var missing = Path.Combine(temp.Root, "missing");
        var result = new ExternalProcessRunResult(
            ExternalProcessTerminalStatus.Failed,
            null,
            missing,
            Path.Combine(missing, "stdin.ndjson"),
            Path.Combine(missing, "stdout.ndjson"),
            Path.Combine(missing, "stderr.log"),
            Path.Combine(missing, "resource-samples.ndjson"),
            Path.Combine(missing, "artifact-preflight.json"),
            Path.Combine(missing, "artifact-postflight.json"),
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            new string('0', 64),
            new string('0', 64),
            new string('1', 64),
            [],
            new ExternalProcessFailure(
                "artifact.mismatch",
                "preflight",
                "Runtime inventory differs from the plan.",
                "not-started"));

        var exception = Assert.Throws<InvalidOperationException>(
            () => ExternalProcessTerminalMapper.CreateSubmission(
                Intent(),
                result,
                "2026-08-11T00:00:00Z",
                "2026-08-11T00:00:00Z",
                ["planned-runs"]));

        Assert.Contains("persistence-incomplete", exception.Message, StringComparison.Ordinal);
    }

    [Theory]
    [MemberData(nameof(FailureEvidenceMatrix))]
    public void Every_failure_code_binds_a_deterministic_raw_evidence_role(
        string failureCode,
        string stage,
        RawRunEvidenceRole expectedRole)
    {
        using var temp = new TestDirectory();
        var result = ResultWithEvidence(temp, failureCode, stage);

        var submission = ExternalProcessTerminalMapper.CreateSubmission(
            Intent(),
            result,
            "2026-08-11T00:00:00Z",
            "2026-08-11T00:00:01Z",
            ["planned-runs"]);

        Assert.Equal(expectedRole, submission.Failure!.EvidenceRole);
        Assert.Equal(failureCode, submission.Failure.Code);
        Assert.Equal(stage, submission.Failure.Stage);
    }

    private static ExternalProcessRunResult ResultWithEvidence(
        TestDirectory temp,
        string failureCode,
        string stage = "parsing")
    {
        var root = Path.Combine(temp.Root, "external");
        Directory.CreateDirectory(root);
        var input = Write(root, "stdin.ndjson", []);
        var output = Write(root, "stdout.ndjson", []);
        var stderr = Write(root, "stderr.log", []);
        var resources = Write(root, "resource-samples.ndjson", []);
        var preflight = Write(root, "artifact-preflight.json", []);
        var postflight = Write(root, "artifact-postflight.json", []);
        return new ExternalProcessRunResult(
            ExternalProcessTerminalStatus.Failed,
            17,
            root,
            input,
            output,
            stderr,
            resources,
            preflight,
            postflight,
            0,
            0,
            0,
            100,
            50,
            1024,
            1,
            new string('8', 64),
            new string('8', 64),
            new string('9', 64),
            [],
            new ExternalProcessFailure(
                failureCode,
                stage,
                "External output failed protocol validation.",
                "sampled-process-tree"));
    }

    private static string Write(string root, string name, byte[] bytes)
    {
        var path = Path.Combine(root, name);
        File.WriteAllBytes(path, bytes);
        return path;
    }

    private static RunStoreIntent Intent()
    {
        var plan = new string('1', 64);
        var scenario = new string('2', 64);
        var runId = RideBound.Benchmarking.Contracts.BenchmarkIdentity.CalculateRun(
            plan,
            scenario,
            "b1",
            0,
            0);
        return new RunStoreIntent(
            runId,
            plan,
            scenario,
            "b1",
            0,
            0,
            new string('3', 64),
            new string('4', 64),
            new string('5', 64),
            new string('6', 64),
            new string('7', 64),
            1,
            false,
            new string('8', 64));
    }
}
