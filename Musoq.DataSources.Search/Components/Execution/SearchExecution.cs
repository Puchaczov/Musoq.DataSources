#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading;

using Musoq.DataSources.Search.Components.Contracts;
using Musoq.DataSources.Search.Components.Diagnostics;

namespace Musoq.DataSources.Search.Components.Execution;

/// <summary>
///     The immutable terminal record for one Search execution.
/// </summary>
internal sealed record SearchTerminalSummary
{
    public SearchTerminalSummary(
        string scanId,
        string scopeFingerprint,
        SearchOutcome outcome,
        string terminalReason,
        bool scopeResolved,
        long visitedFiles,
        long eligibleFiles,
        long filesOpened,
        long filesRead,
        long filesCompleted,
        long filesFailed,
        long binaryFilesSkipped,
        long bytesScanned,
        long filesMatched,
        long matchingLines,
        long occurrences,
        long observedRows,
        string? failureCode,
        string? failurePath)
    {
        ScanId = RequireValue(scanId, nameof(scanId));
        ScopeFingerprint = RequireValue(scopeFingerprint, nameof(scopeFingerprint));
        TerminalReason = RequireValue(terminalReason, nameof(terminalReason));
        ScopeResolved = scopeResolved;
        VisitedFiles = RequireNonNegative(visitedFiles, nameof(visitedFiles));
        EligibleFiles = RequireNonNegative(eligibleFiles, nameof(eligibleFiles));
        FilesOpened = RequireNonNegative(filesOpened, nameof(filesOpened));
        FilesRead = RequireNonNegative(filesRead, nameof(filesRead));
        FilesCompleted = RequireNonNegative(filesCompleted, nameof(filesCompleted));
        FilesFailed = RequireNonNegative(filesFailed, nameof(filesFailed));
        BinaryFilesSkipped = RequireNonNegative(binaryFilesSkipped, nameof(binaryFilesSkipped));
        BytesScanned = RequireNonNegative(bytesScanned, nameof(bytesScanned));
        FilesMatched = RequireNonNegative(filesMatched, nameof(filesMatched));
        MatchingLines = RequireNonNegative(matchingLines, nameof(matchingLines));
        Occurrences = RequireNonNegative(occurrences, nameof(occurrences));
        ObservedRows = RequireNonNegative(observedRows, nameof(observedRows));

        if (outcome is SearchOutcome.Failed or SearchOutcome.Partial &&
            string.IsNullOrWhiteSpace(failureCode))
        {
            throw new ArgumentException(
                "A failed or partial Search execution requires a failure code.",
                nameof(failureCode));
        }

        if (outcome is SearchOutcome.QuerySatisfied or SearchOutcome.ScopeExhausted &&
            (failureCode is not null || failurePath is not null))
        {
            throw new ArgumentException(
                "A successful Search execution cannot carry failure details.",
                nameof(failureCode));
        }

        Outcome = outcome;
        FailureCode = failureCode;
        FailurePath = failurePath;
    }

    public string ScanId { get; }

    public string ScopeFingerprint { get; }

    public SearchOutcome Outcome { get; }

    public string TerminalReason { get; }

    public bool Complete => Outcome is SearchOutcome.QuerySatisfied or SearchOutcome.ScopeExhausted;

    public bool ScopeExhausted => Outcome == SearchOutcome.ScopeExhausted;

    public bool QuerySatisfied => Outcome == SearchOutcome.QuerySatisfied;

    public bool CountsExact => Outcome == SearchOutcome.ScopeExhausted && ScopeResolved;

    public bool ScopeResolved { get; }

    public long VisitedFiles { get; }

    public long EligibleFiles { get; }

    public long FilesOpened { get; }

    public long FilesRead { get; }

    public long FilesCompleted { get; }

    public long FilesFailed { get; }

    public long BinaryFilesSkipped { get; }

    public long BytesScanned { get; }

    public long FilesMatched { get; }

    public long MatchingLines { get; }

    public long Occurrences { get; }

    public long ObservedRows { get; }

    public string? FailureCode { get; }

    public string? FailurePath { get; }

    private static string RequireValue(string value, string parameterName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        return value;
    }

    private static long RequireNonNegative(long value, string parameterName)
    {
        if (value < 0)
            throw new ArgumentOutOfRangeException(parameterName);

        return value;
    }
}

internal sealed class SearchExecutionAccounting
{
    private long _eligibleFiles;
    private long _filesOpened;
    private long _filesRead;
    private long _filesCompleted;
    private long _filesFailed;
    private long _binaryFilesSkipped;
    private long _bytesScanned;
    private long _filesMatched;
    private long _matchingLines;
    private long _occurrences;
    private long _observedRows;
    private int _scopeResolved;
    private string? _failureCode;
    private string? _failurePath;

    public SearchExecutionAccounting(string scanId, string scopeFingerprint)
    {
        ScanId = scanId ?? throw new ArgumentNullException(nameof(scanId));
        ScopeFingerprint = scopeFingerprint ?? throw new ArgumentNullException(nameof(scopeFingerprint));
    }

    public string ScanId { get; }

    public string ScopeFingerprint { get; }

    public long EligibleFiles => Interlocked.Read(ref _eligibleFiles);

    public long BinaryFilesSkipped => Interlocked.Read(ref _binaryFilesSkipped);

    public long Occurrences => Interlocked.Read(ref _occurrences);

    public long ObservedRows => Interlocked.Read(ref _observedRows);

    public void SetScopeResolved(bool resolved)
    {
        Interlocked.Exchange(ref _scopeResolved, resolved ? 1 : 0);
    }

    public void RecordEligibleFile()
    {
        Interlocked.Increment(ref _eligibleFiles);
    }

    public void RecordFileOpened()
    {
        Interlocked.Increment(ref _filesOpened);
    }

    public void RecordFileRead()
    {
        Interlocked.Increment(ref _filesRead);
    }

    public void RecordBinaryFileSkipped()
    {
        Interlocked.Increment(ref _binaryFilesSkipped);
    }

    public void RecordFileCompleted(
        long bytesScanned,
        long occurrences,
        long matchingLines,
        bool matched)
    {
        RequireNonNegative(bytesScanned, nameof(bytesScanned));
        RequireNonNegative(occurrences, nameof(occurrences));
        RequireNonNegative(matchingLines, nameof(matchingLines));

        Interlocked.Increment(ref _filesCompleted);
        Interlocked.Add(ref _bytesScanned, bytesScanned);
        Interlocked.Add(ref _occurrences, occurrences);
        Interlocked.Add(ref _matchingLines, matchingLines);
        if (matched)
            Interlocked.Increment(ref _filesMatched);
    }

    public void RecordFileFailed(string? failureCode, string? failurePath)
    {
        Interlocked.Increment(ref _filesFailed);
        RecordFailure(failureCode, failurePath);
    }

    public void RecordObservedRows(long rows)
    {
        RequireNonNegative(rows, nameof(rows));
        Interlocked.Add(ref _observedRows, rows);
    }

    public void RecordFailure(string? failureCode, string? failurePath)
    {
        if (!string.IsNullOrWhiteSpace(failureCode))
            Interlocked.CompareExchange(ref _failureCode, failureCode, null);
        if (!string.IsNullOrWhiteSpace(failurePath))
            Interlocked.CompareExchange(ref _failurePath, failurePath, null);
    }

    public SearchTerminalSummary Finish(
        SearchOutcome outcome,
        string terminalReason,
        long visitedFiles,
        string? failureCode = null,
        string? failurePath = null)
    {
        RequireNonNegative(visitedFiles, nameof(visitedFiles));

        var effectiveFailureCode = failureCode ?? Volatile.Read(ref _failureCode);
        var effectiveFailurePath = failurePath ?? Volatile.Read(ref _failurePath);
        if (outcome is SearchOutcome.Failed or SearchOutcome.Partial &&
            string.IsNullOrWhiteSpace(effectiveFailureCode))
        {
            effectiveFailureCode = "failure";
        }

        return new SearchTerminalSummary(
            ScanId,
            ScopeFingerprint,
            outcome,
            terminalReason,
            Volatile.Read(ref _scopeResolved) == 1,
            visitedFiles,
            Interlocked.Read(ref _eligibleFiles),
            Interlocked.Read(ref _filesOpened),
            Interlocked.Read(ref _filesRead),
            Interlocked.Read(ref _filesCompleted),
            Interlocked.Read(ref _filesFailed),
            Interlocked.Read(ref _binaryFilesSkipped),
            Interlocked.Read(ref _bytesScanned),
            Interlocked.Read(ref _filesMatched),
            Interlocked.Read(ref _matchingLines),
            Interlocked.Read(ref _occurrences),
            Interlocked.Read(ref _observedRows),
            effectiveFailureCode,
            effectiveFailurePath);
    }

    private static long RequireNonNegative(long value, string parameterName)
    {
        if (value < 0)
            throw new ArgumentOutOfRangeException(parameterName);

        return value;
    }
}

internal readonly record struct SearchTerminalFailure(
    string Reason,
    string Code,
    string? Path)
{
    public static SearchTerminalFailure From(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);

        if (exception is OperationCanceledException)
            return new("cancelled", "cancelled", null);

        if (exception is ISearchDiagnosticException diagnosticException)
        {
            var diagnostic = diagnosticException.Diagnostic;
            var path = diagnostic.Location?.Path;
            if (exception is SearchResourceLimitException resourceLimit)
            {
                return new(
                    "budget-exhausted",
                    resourceLimit.BudgetCode ??
                    InferBudgetCode(diagnostic.Location?.ArgumentName),
                    path);
            }

            var reason = diagnostic.Code switch
            {
                SearchDiagnosticCodes.MissingRoot => "root-missing",
                SearchDiagnosticCodes.SourceChanged => "source-changed",
                SearchDiagnosticCodes.ResourceLimit => "budget-exhausted",
                SearchDiagnosticCodes.OutputFailed => "output-failure",
                SearchDiagnosticCodes.InvalidArgument or
                    SearchDiagnosticCodes.InvalidRegex or
                    SearchDiagnosticCodes.InvalidRecordFraming => "validation-failure",
                _ => "unreadable-file"
            };
            return new(reason, diagnostic.Code, path);
        }

        return exception switch
        {
            IOException or UnauthorizedAccessException =>
                new("unreadable-file", "unreadable-file", null),
            ArgumentException => new("validation-failure", "validation-failure", null),
            _ => new("failure", "failure", null)
        };
    }

    private static string InferBudgetCode(string? argumentName)
    {
        return argumentName switch
        {
            "output-cap" => "output-cap",
            "read-bytes" => "read-bytes",
            "file-bytes" => "file-bytes",
            "files" => "file-count",
            "pattern" or "pattern-size" => "pattern-size",
            "pattern-count" => "pattern-count",
            "compile-cost" or "pattern compilation milliseconds" => "compile-cost",
            "record" or "recordBytes" or "record-bytes" => "record-bytes",
            "contextBytes" or "context-bytes" => "context-bytes",
            "match-count" => "match-count",
            _ => SearchDiagnosticCodes.ResourceLimit
        };
    }
}

internal static class SearchScopeFingerprint
{
    public static string Create(string requestedRoot, ScopePolicy scope)
    {
        ArgumentNullException.ThrowIfNull(requestedRoot);
        ArgumentNullException.ThrowIfNull(scope);

        var value = new StringBuilder();
        Append(value, requestedRoot);
        Append(value, scope.Recursive);
        Append(value, scope.RepositoryIgnores);
        Append(value, scope.GlobalIgnores);
        Append(value, scope.HiddenEntries);
        Append(value, scope.FollowLinks);
        Append(value, scope.InaccessibleEntries);
        Append(value, scope.Include);
        Append(value, scope.Exclude);
        Append(value, scope.GlobalIgnoreRules);
        Append(value, scope.Metadata.NameIncludes);
        Append(value, scope.Metadata.NameExcludes);
        Append(value, scope.Metadata.ExtensionIncludes);
        Append(value, scope.Metadata.ExtensionExcludes);
        Append(value, scope.Metadata.MinimumSizeBytes);
        Append(value, scope.Metadata.MaximumSizeBytes);
        Append(value, scope.Metadata.ModifiedAfterOrEqualUtc);
        Append(value, scope.Metadata.ModifiedBeforeOrEqualUtc);

        return "sha256:" + Convert.ToHexString(
                SHA256.HashData(Encoding.UTF8.GetBytes(value.ToString())))
            .ToLowerInvariant();
    }

    private static void Append(StringBuilder value, string? item)
    {
        value.Append(item?.Length ?? -1).Append(':').Append(item).Append('|');
    }

    private static void Append(StringBuilder value, bool item)
    {
        value.Append(item ? "true" : "false").Append('|');
    }

    private static void Append<T>(StringBuilder value, T item)
        where T : struct, Enum
    {
        value.Append(item).Append('|');
    }

    private static void Append(StringBuilder value, long? item)
    {
        value.Append(item?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "null").Append('|');
    }

    private static void Append(StringBuilder value, DateTimeOffset? item)
    {
        value.Append(item?.ToUniversalTime().ToString("O") ?? "null").Append('|');
    }

    private static void Append(StringBuilder value, IReadOnlyList<string> items)
    {
        value.Append(items.Count).Append('[');
        for (var index = 0; index < items.Count; index++)
            Append(value, items[index]);
        value.Append(']').Append('|');
    }
}
