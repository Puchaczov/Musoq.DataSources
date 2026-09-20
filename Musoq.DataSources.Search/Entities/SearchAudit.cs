#nullable enable

using Musoq.Schema.Attributes;

using Musoq.DataSources.Search.Components.Contracts;
using Musoq.DataSources.Search.Components.Execution;

namespace Musoq.DataSources.Search.Entities;

/// <summary>
///     One fresh terminal summary for an explicitly executed Search audit.
/// </summary>
public sealed class SearchAudit
{
    internal SearchAudit(string root, SearchTerminalSummary summary)
    {
        Root = root;
        ScanId = summary.ScanId;
        ScopeFingerprint = summary.ScopeFingerprint;
        Outcome = summary.Outcome;
        TerminalReason = summary.TerminalReason;
        Complete = summary.Complete;
        ScopeExhausted = summary.ScopeExhausted;
        QuerySatisfied = summary.QuerySatisfied;
        CountsExact = summary.CountsExact;
        ScopeResolved = summary.ScopeResolved;
        VisitedFiles = summary.VisitedFiles;
        EligibleFiles = summary.EligibleFiles;
        FilesOpened = summary.FilesOpened;
        FilesRead = summary.FilesRead;
        FilesCompleted = summary.FilesCompleted;
        FilesFailed = summary.FilesFailed;
        BinaryFilesSkipped = summary.BinaryFilesSkipped;
        BytesScanned = summary.BytesScanned;
        FilesMatched = summary.FilesMatched;
        MatchingLines = summary.MatchingLines;
        Occurrences = summary.Occurrences;
        ObservedRows = summary.ObservedRows;
        FailureCode = summary.FailureCode;
        FailurePath = summary.FailurePath;
    }

    /// <summary>Gets the requested root representation.</summary>
    [EntityProperty]
    public string Root { get; }

    /// <summary>Gets the opaque identifier for this execution.</summary>
    [EntityProperty]
    public string ScanId { get; }

    /// <summary>Gets the stable fingerprint of the requested Search scope.</summary>
    [EntityProperty]
    public string ScopeFingerprint { get; }

    /// <summary>Gets the terminal execution outcome.</summary>
    [EntityProperty]
    public SearchOutcome Outcome { get; }

    /// <summary>Gets the stable terminal reason.</summary>
    [EntityProperty]
    public string TerminalReason { get; }

    /// <summary>Gets whether the result is complete under the outcome contract.</summary>
    [EntityProperty]
    public bool Complete { get; }

    /// <summary>Gets whether the eligible scope was exhausted.</summary>
    [EntityProperty]
    public bool ScopeExhausted { get; }

    /// <summary>Gets whether an explicit positive query demand was satisfied.</summary>
    [EntityProperty]
    public bool QuerySatisfied { get; }

    /// <summary>Gets whether the recorded counters are exact for the resolved scope.</summary>
    [EntityProperty]
    public bool CountsExact { get; }

    /// <summary>Gets whether the requested root was resolved.</summary>
    [EntityProperty]
    public bool ScopeResolved { get; }

    /// <summary>Gets the number of visited file candidates.</summary>
    [EntityProperty]
    public long VisitedFiles { get; }

    /// <summary>Gets the number of eligible file candidates.</summary>
    [EntityProperty]
    public long EligibleFiles { get; }

    /// <summary>Gets the number of content files opened for scanning.</summary>
    [EntityProperty]
    public long FilesOpened { get; }

    /// <summary>Gets the number of files whose reader opened successfully.</summary>
    [EntityProperty]
    public long FilesRead { get; }

    /// <summary>Gets the number of files completed by the scan.</summary>
    [EntityProperty]
    public long FilesCompleted { get; }

    /// <summary>Gets the number of files that failed during processing.</summary>
    [EntityProperty]
    public long FilesFailed { get; }

    /// <summary>Gets the number of files skipped by binary policy.</summary>
    [EntityProperty]
    public long BinaryFilesSkipped { get; }

    /// <summary>Gets the bytes attributed to completed text-file scans.</summary>
    [EntityProperty]
    public long BytesScanned { get; }

    /// <summary>Gets the number of completed files with observed matches.</summary>
    [EntityProperty]
    public long FilesMatched { get; }

    /// <summary>Gets the number of distinct matching physical lines observed.</summary>
    [EntityProperty]
    public long MatchingLines { get; }

    /// <summary>Gets the number of matcher occurrences observed.</summary>
    [EntityProperty]
    public long Occurrences { get; }

    /// <summary>Gets the number of rows observed by the underlying scan source.</summary>
    [EntityProperty]
    public long ObservedRows { get; }

    /// <summary>Gets the typed failure code, when execution failed or was partial.</summary>
    [EntityProperty]
    public string? FailureCode { get; }

    /// <summary>Gets the bounded failure path, when one is available.</summary>
    [EntityProperty]
    public string? FailurePath { get; }
}
