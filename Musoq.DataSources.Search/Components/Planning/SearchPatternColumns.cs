#nullable enable
using System;
using System.Linq.Expressions;
using Musoq.DataSources.Common;
using Musoq.DataSources.Search.Entities;
using Musoq.Schema.Optimization;
namespace Musoq.DataSources.Search.Components.Planning;

/// <summary>Stable source-row strings, including text retained for accepted predicates.</summary>
internal static class SearchPatternColumns
{
    internal static readonly PatternColumnBindings<SearchMatch> SearchMatch = new(
        (nameof(Entities.SearchMatch.Path), static row => row.Path),
        (nameof(Entities.SearchMatch.PatternId), static row => row.PatternId),
        (nameof(Entities.SearchMatch.MatchText), static row => row.MatchText)
    );
    internal static readonly PatternColumnBindings<SearchByteMatch> SearchByteMatch = new(
        (nameof(Entities.SearchByteMatch.Path), static row => row.Path),
        (nameof(Entities.SearchByteMatch.Origin), static row => row.Origin),
        (nameof(Entities.SearchByteMatch.PatternId), static row => row.PatternId),
        (nameof(Entities.SearchByteMatch.MatchText), static row => row.MatchText),
        (nameof(Entities.SearchByteMatch.LineText), static row => row.LineText)
    );
    internal static readonly PatternColumnBindings<SearchLine> SearchLine = new(
        (nameof(Entities.SearchLine.Path), static row => row.Path),
        (nameof(Entities.SearchLine.Origin), static row => row.Origin),
        (nameof(Entities.SearchLine.PatternId), static row => row.PatternId),
        (nameof(Entities.SearchLine.LineText), static row => row.LineText)
    );
    internal static readonly PatternColumnBindings<SearchFile> SearchFile = new(
        (nameof(Entities.SearchFile.Path), static row => row.Path),
        (nameof(Entities.SearchFile.Origin), static row => row.Origin),
        (nameof(Entities.SearchFile.PatternId), static row => row.PatternId)
    );
    internal static readonly PatternColumnBindings<SearchCount> SearchCount = new(
        (nameof(Entities.SearchCount.Path), static row => row.Path),
        (nameof(Entities.SearchCount.Origin), static row => row.Origin),
        (nameof(Entities.SearchCount.PatternId), static row => row.PatternId)
    );
    internal static readonly PatternColumnBindings<SearchPath> SearchPath = new(
        (nameof(Entities.SearchPath.Path), static row => row.Path),
        (nameof(Entities.SearchPath.Origin), static row => row.Origin),
        (nameof(Entities.SearchPath.EntryKind), static row => row.EntryKind)
    );
    internal static readonly PatternColumnBindings<SearchAudit> SearchAudit = new(
        (nameof(Entities.SearchAudit.Root), static row => row.Root),
        (nameof(Entities.SearchAudit.ScanId), static row => row.ScanId),
        (nameof(Entities.SearchAudit.ScopeFingerprint), static row => row.ScopeFingerprint),
        (nameof(Entities.SearchAudit.TerminalReason), static row => row.TerminalReason),
        (nameof(Entities.SearchAudit.FailureCode), static row => row.FailureCode),
        (nameof(Entities.SearchAudit.FailurePath), static row => row.FailurePath)
    );
    internal static IPatternColumnBindings? Get(string name) => name.ToLowerInvariant() switch
    {
        "matches" or "many" => SearchMatch,
        "bytes" => SearchByteMatch,
        "lines" => SearchLine,
        "files" => SearchFile,
        "counts" => SearchCount,
        "paths" => SearchPath,
        "audit" => SearchAudit,
        _ => null
    };

    internal static void Bind<TRow>(TypedPredicatePlan plan, PatternColumnBindings<TRow> columns, SourcePredicateExpression? predicate)
        => plan.Bind<TRow>(predicate, (row, name) => columns.Contains(name) ? columns.Field(row, name) : Expression.PropertyOrField(row, name));
}
