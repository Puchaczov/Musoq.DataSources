#nullable enable

using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;

namespace Musoq.DataSources.Search.Benchmarks.Harness;

public sealed record BenchmarkCandidateSnapshot(
    string Id,
    string Version,
    long EligibleBytes,
    string ResultUnit,
    long ResultCount,
    string ResultHash,
    bool Complete,
    string TerminalState,
    int InvocationCount);

public sealed record BenchmarkTrial(
    int Number,
    string FirstInvocation,
    double BaselineMilliseconds,
    double CandidateMilliseconds,
    bool BaselineComplete,
    bool CandidateComplete);

public sealed record BenchmarkCell(
    string ScopeId,
    string CohortId,
    string WorkloadId,
    string ResultUnit,
    string EligibleInputManifestHash,
    long EligibleBytes,
    string BaselineId,
    string CandidateId,
    string ProcessState,
    string FilesystemCache,
    int WarmupsExcluded,
    string TimingBoundary,
    IReadOnlyList<BenchmarkTrial> Trials,
    bool Complete,
    string TerminalState,
    BenchmarkCandidateSnapshot? Baseline,
    BenchmarkCandidateSnapshot? Candidate);

public sealed class BenchmarkContractException : Exception
{
    public BenchmarkContractException(string code, string message)
        : base($"[{code}] {message}")
    {
        Code = code;
    }

    public string Code { get; }
}

public static class BenchmarkHarnessContract
{
    public const int MinimumMeasuredTrials = 7;

    private static readonly HashSet<string> CacheStates =
        new(StringComparer.Ordinal)
        {
            "controlled_cold",
            "controlled_warm",
            "unknown"
        };

    private static readonly HashSet<string> FirstInvocationValues =
        new(StringComparer.Ordinal)
        {
            "baseline,candidate",
            "candidate,baseline"
        };

    public static void Validate(
        BenchmarkCell cell,
        string expectedScopeId,
        string expectedResultUnit)
    {
        ArgumentNullException.ThrowIfNull(cell);
        ArgumentException.ThrowIfNullOrWhiteSpace(expectedScopeId);
        ArgumentException.ThrowIfNullOrWhiteSpace(expectedResultUnit);

        if (!string.Equals(cell.ScopeId, expectedScopeId, StringComparison.Ordinal))
            Reject(
                "scope-mismatch",
                $"Expected scope '{expectedScopeId}', got '{cell.ScopeId}'.");
        if (!string.Equals(cell.ResultUnit, expectedResultUnit, StringComparison.Ordinal))
            Reject(
                "output-unit-mismatch",
                $"Expected result unit '{expectedResultUnit}', got '{cell.ResultUnit}'.");
        if (!Regex.IsMatch(
                cell.EligibleInputManifestHash ?? string.Empty,
                "^[0-9a-f]{64}$",
                RegexOptions.CultureInvariant))
            Reject(
                "missing-input-manifest",
                "The eligible input manifest hash must be a lowercase SHA-256 digest.");
        if (cell.EligibleBytes <= 0)
            Reject(
                "zero-byte-scan",
                "A measured cell must record positive eligible input bytes, including no-match workloads.");
        if (!CacheStates.Contains(cell.FilesystemCache))
            Reject(
                "invalid-cache-state",
                $"Filesystem cache state '{cell.FilesystemCache}' is not a frozen cache label.");
        if (string.Equals(cell.FilesystemCache, "unknown", StringComparison.Ordinal) &&
            cell.ProcessState.Contains("cold", StringComparison.OrdinalIgnoreCase))
            Reject(
                "unknown-cache-labeled-cold",
                "Unknown filesystem cache state cannot be called cold.");
        if (cell.WarmupsExcluded < 0)
            Reject("invalid-warmups", "WarmupsExcluded cannot be negative.");
        if (string.IsNullOrWhiteSpace(cell.TimingBoundary) ||
            !cell.TimingBoundary.Contains("traversal", StringComparison.OrdinalIgnoreCase) ||
            !cell.TimingBoundary.Contains("output", StringComparison.OrdinalIgnoreCase))
            Reject(
                "incomplete-timing-boundary",
                "The timing boundary must include traversal and output costs.");
        if (!cell.Complete ||
            !string.Equals(cell.TerminalState, "complete", StringComparison.Ordinal))
            Reject(
                "incomplete-run",
                "A partial, cancelled or budget-limited run cannot be recorded as a complete measurement.");

        if (cell.Baseline is null)
            Reject("missing-baseline", "Every cell requires a recorded baseline candidate.");
        if (cell.Candidate is null)
            Reject("missing-candidate", "Every cell requires a recorded candidate.");

        ValidateCandidate(
            cell.Baseline,
            cell.BaselineId,
            cell.ResultUnit,
            cell.EligibleBytes,
            "baseline");
        ValidateCandidate(
            cell.Candidate,
            cell.CandidateId,
            cell.ResultUnit,
            cell.EligibleBytes,
            "candidate");

        if (!string.Equals(
                cell.Baseline.ResultHash,
                cell.Candidate.ResultHash,
                StringComparison.Ordinal))
            Reject(
                "output-mismatch",
                "Baseline and candidate result hashes differ for the declared result unit.");
        if (cell.Baseline.ResultCount != cell.Candidate.ResultCount)
            Reject(
                "output-mismatch",
                "Baseline and candidate result counts differ for the declared result unit.");

        if (cell.Trials is null || cell.Trials.Count < MinimumMeasuredTrials)
            Reject(
                "insufficient-trials",
                $"At least {MinimumMeasuredTrials} measured trials are required per cell.");

        var numbers = new HashSet<int>();
        foreach (var trial in cell.Trials)
        {
            if (!numbers.Add(trial.Number) || trial.Number <= 0)
                Reject("duplicate-trial", "Trial numbers must be unique positive integers.");
            if (!FirstInvocationValues.Contains(trial.FirstInvocation))
                Reject("invalid-pair-order", "Each trial must record randomized baseline/candidate order.");
            if (!double.IsFinite(trial.BaselineMilliseconds) || trial.BaselineMilliseconds <= 0 ||
                !double.IsFinite(trial.CandidateMilliseconds) || trial.CandidateMilliseconds <= 0)
                Reject("invalid-duration", "Measured durations must be finite and positive.");
            if (!trial.BaselineComplete || !trial.CandidateComplete)
                Reject("incomplete-trial", "A trial with an incomplete candidate cannot be measured.");
        }
    }

    private static void ValidateCandidate(
        BenchmarkCandidateSnapshot? snapshot,
        string expectedId,
        string expectedResultUnit,
        long expectedBytes,
        string role)
    {
        if (snapshot is null)
            return;
        if (!string.Equals(snapshot.Id, expectedId, StringComparison.Ordinal))
            Reject(
                "candidate-identity-mismatch",
                $"The {role} snapshot identity does not match the cell declaration.");
        if (string.IsNullOrWhiteSpace(snapshot.Version))
            Reject(
                "missing-candidate-version",
                $"The {role} version/build identity is required.");
        if (snapshot.EligibleBytes != expectedBytes || snapshot.EligibleBytes <= 0)
            Reject(
                "eligible-byte-mismatch",
                $"The {role} does not report the same positive eligible byte count as the cell.");
        if (!string.Equals(snapshot.ResultUnit, expectedResultUnit, StringComparison.Ordinal))
            Reject(
                "output-unit-mismatch",
                $"The {role} result unit does not match the cell.");
        if (!Regex.IsMatch(
                snapshot.ResultHash ?? string.Empty,
                "^[0-9a-f]{64}$",
                RegexOptions.CultureInvariant))
            Reject(
                "missing-result-hash",
                $"The {role} result hash must be a lowercase SHA-256 digest.");
        if (!snapshot.Complete ||
            !string.Equals(snapshot.TerminalState, "complete", StringComparison.Ordinal))
            Reject(
                "incomplete-candidate",
                $"The {role} candidate is not complete.");
        if (snapshot.InvocationCount <= 0)
            Reject(
                "missing-invocation",
                $"The {role} invocation count must be positive.");
    }

    [DoesNotReturn]
    private static void Reject(string code, string message)
    {
        throw new BenchmarkContractException(code, message);
    }
}
