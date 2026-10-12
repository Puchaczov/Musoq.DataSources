#nullable enable

using System;
using System.Collections.Generic;
using Musoq.Schema.Optimization;
using Musoq.DataSources.Search.Entities;

namespace Musoq.DataSources.Search.Components.Planning;

internal readonly record struct SearchPredicatePartition(
    SourcePredicateExpression? Accepted,
    SourcePredicateExpression? Residual);

internal static class SearchPredicatePlanning
{
    private static readonly IReadOnlyDictionary<string, HashSet<string>> AcceptedColumns =
        new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase)
        {
            ["matches"] = CreateColumns(
                nameof(SearchMatch.Path),
                nameof(SearchMatch.PatternId),
                nameof(SearchMatch.MatchIndex),
                nameof(SearchMatch.ByteOffset),
                nameof(SearchMatch.ByteLength),
                nameof(SearchMatch.LineNumber),
                nameof(SearchMatch.Utf16Column),
                nameof(SearchMatch.Utf16Length)),
            ["bytes"] = CreateColumns(
                nameof(SearchByteMatch.Path),
                nameof(SearchByteMatch.Origin),
                nameof(SearchByteMatch.PatternId),
                nameof(SearchByteMatch.MatchIndex),
                nameof(SearchByteMatch.ByteOffset),
                nameof(SearchByteMatch.ByteLength),
                nameof(SearchByteMatch.WindowStartByteOffset),
                nameof(SearchByteMatch.WindowByteLength),
                nameof(SearchByteMatch.WindowComplete)),
            ["lines"] = CreateColumns(
                nameof(SearchLine.Path),
                nameof(SearchLine.Origin),
                nameof(SearchLine.PatternId),
                nameof(SearchLine.LineNumber),
                nameof(SearchLine.ByteOffset),
                nameof(SearchLine.OccurrenceCount)),
            ["files"] = CreateColumns(
                nameof(SearchFile.Path),
                nameof(SearchFile.Origin),
                nameof(SearchFile.PatternId)),
            ["counts"] = CreateColumns(
                nameof(SearchCount.Path),
                nameof(SearchCount.Origin),
                nameof(SearchCount.PatternId),
                nameof(SearchCount.OccurrenceCount),
                nameof(SearchCount.MatchingLineCount),
                nameof(SearchCount.BytesScanned),
                nameof(SearchCount.Complete)),
            ["paths"] = CreateColumns(
                nameof(SearchPath.Path),
                nameof(SearchPath.Origin),
                nameof(SearchPath.EntryKind)),
            ["audit"] = CreateColumns()
        };

    private static readonly IReadOnlyDictionary<string, HashSet<string>> ProjectionColumns =
        new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase)
        {
            ["matches"] = CreateColumns(
                nameof(SearchMatch.Path),
                nameof(SearchMatch.PatternId),
                nameof(SearchMatch.MatchIndex),
                nameof(SearchMatch.ByteOffset),
                nameof(SearchMatch.ByteLength),
                nameof(SearchMatch.LineNumber),
                nameof(SearchMatch.Utf16Column),
                nameof(SearchMatch.Utf16Length),
                nameof(SearchMatch.MatchText),
                nameof(SearchMatch.Captures),
                nameof(SearchMatch.Context)),
            ["bytes"] = CreateColumns(
                nameof(SearchByteMatch.Path),
                nameof(SearchByteMatch.Origin),
                nameof(SearchByteMatch.PatternId),
                nameof(SearchByteMatch.MatchIndex),
                nameof(SearchByteMatch.ByteOffset),
                nameof(SearchByteMatch.ByteLength),
                nameof(SearchByteMatch.MatchedBytes),
                nameof(SearchByteMatch.WindowStartByteOffset),
                nameof(SearchByteMatch.WindowByteLength),
                nameof(SearchByteMatch.WindowComplete),
                nameof(SearchByteMatch.WindowBytes),
                nameof(SearchByteMatch.LineNumber),
                nameof(SearchByteMatch.Utf16Column),
                nameof(SearchByteMatch.Utf16Length),
                nameof(SearchByteMatch.MatchText),
                nameof(SearchByteMatch.LineText),
                nameof(SearchByteMatch.Captures),
                nameof(SearchByteMatch.Context)),
            ["lines"] = CreateColumns(
                nameof(SearchLine.Path),
                nameof(SearchLine.Origin),
                nameof(SearchLine.PatternId),
                nameof(SearchLine.LineNumber),
                nameof(SearchLine.ByteOffset),
                nameof(SearchLine.LineText),
                nameof(SearchLine.OccurrenceCount)),
            ["files"] = CreateColumns(
                nameof(SearchFile.Path),
                nameof(SearchFile.Origin),
                nameof(SearchFile.PatternId)),
            ["counts"] = CreateColumns(
                nameof(SearchCount.Path),
                nameof(SearchCount.Origin),
                nameof(SearchCount.PatternId),
                nameof(SearchCount.OccurrenceCount),
                nameof(SearchCount.MatchingLineCount),
                nameof(SearchCount.BytesScanned),
                nameof(SearchCount.Complete)),
            ["paths"] = CreateColumns(
                nameof(SearchPath.Path),
                nameof(SearchPath.Origin),
                nameof(SearchPath.EntryKind)),
            ["audit"] = CreateColumns(
                nameof(SearchAudit.Root),
                nameof(SearchAudit.ScanId),
                nameof(SearchAudit.ScopeFingerprint),
                nameof(SearchAudit.Outcome),
                nameof(SearchAudit.TerminalReason),
                nameof(SearchAudit.Complete),
                nameof(SearchAudit.ScopeExhausted),
                nameof(SearchAudit.QuerySatisfied),
                nameof(SearchAudit.CountsExact),
                nameof(SearchAudit.ScopeResolved),
                nameof(SearchAudit.VisitedFiles),
                nameof(SearchAudit.EligibleFiles),
                nameof(SearchAudit.FilesOpened),
                nameof(SearchAudit.FilesRead),
                nameof(SearchAudit.FilesCompleted),
                nameof(SearchAudit.FilesFailed),
                nameof(SearchAudit.BinaryFilesSkipped),
                nameof(SearchAudit.BytesScanned),
                nameof(SearchAudit.FilesMatched),
                nameof(SearchAudit.MatchingLines),
                nameof(SearchAudit.Occurrences),
                nameof(SearchAudit.ObservedRows),
                nameof(SearchAudit.FailureCode),
                nameof(SearchAudit.FailurePath))
        };

    public static bool IsSupportedSource(string name)
    {
        return name is not null && AcceptedColumns.ContainsKey(name);
    }

    public static SearchPredicatePartition Split(
        string name,
        SourcePredicateExpression? predicate)
    {
        if (predicate is null)
            return new SearchPredicatePartition(null, null);

        if (CanEvaluatePredicate(name, predicate))
            return new SearchPredicatePartition(predicate, null);

        if (predicate is not SourcePredicateLogical
            {
                Operator: SourcePredicateLogicalOperator.And
            } logical)
            return new SearchPredicatePartition(null, predicate);

        var left = Split(name, logical.Left);
        var right = Split(name, logical.Right);
        return new SearchPredicatePartition(
            CombineAnd(left.Accepted, right.Accepted),
            CombineAnd(left.Residual, right.Residual));
    }

    public static bool CanEvaluatePredicate(
        string name,
        SourcePredicateExpression? predicate)
    {
        if (predicate is null)
            return true;

        return predicate switch
        {
            SourcePredicateComparison comparison =>
                CanEvaluateScalar(name, comparison.Left) &&
                CanEvaluateScalar(name, comparison.Right),
            SourcePredicateLogical logical =>
                CanEvaluatePredicate(name, logical.Left) &&
                CanEvaluatePredicate(name, logical.Right),
            SourcePredicateIn inPredicate =>
                CanEvaluateScalar(name, inPredicate.Expression) &&
                AllLiterals(inPredicate.Values),
            SourcePredicateNullCheck nullCheck =>
                nullCheck.IsNegated &&
                CanEvaluateScalar(name, nullCheck.Expression),
            SourcePredicateLiteral literal => literal.Value is bool,
            _ => false
        };
    }

    public static bool IsAcceptedColumn(string name, string columnName)
    {
        return AcceptedColumns.TryGetValue(name, out var columns) &&
               columns.Contains(NormalizeColumnName(columnName));
    }

    public static IReadOnlyList<SourceColumnRef> AcceptProjectionColumns(
        string name,
        IReadOnlyList<SourceColumnRef> requestedColumns)
    {
        ArgumentNullException.ThrowIfNull(requestedColumns);

        if (!ProjectionColumns.TryGetValue(name, out var columns) || requestedColumns.Count == 0)
            return [];

        var accepted = new List<SourceColumnRef>(requestedColumns.Count);
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var requested in requestedColumns)
        {
            var normalized = NormalizeColumnName(requested.Name);
            if (columns.Contains(normalized) && seen.Add(normalized))
                accepted.Add(new SourceColumnRef(normalized, requested.ReadModifiers));
        }

        return accepted;
    }

    public static string NormalizeColumnName(string columnName)
    {
        ArgumentException.ThrowIfNullOrEmpty(columnName);
        var separator = columnName.LastIndexOf('.');
        return separator < 0 ? columnName : columnName[(separator + 1)..];
    }

    private static bool CanEvaluateScalar(
        string name,
        SourcePredicateExpression expression)
    {
        return expression switch
        {
            SourcePredicateColumn column => IsAcceptedColumn(name, column.Column.Name),
            SourcePredicateLiteral => true,
            _ => false
        };
    }

    private static bool AllLiterals(IReadOnlyList<SourcePredicateExpression> values)
    {
        for (var index = 0; index < values.Count; index++)
        {
            if (values[index] is not SourcePredicateLiteral)
                return false;
        }

        return true;
    }

    private static SourcePredicateExpression? CombineAnd(
        SourcePredicateExpression? left,
        SourcePredicateExpression? right)
    {
        return (left, right) switch
        {
            (null, null) => null,
            (not null, null) => left,
            (null, not null) => right,
            _ => new SourcePredicateLogical(
                SourcePredicateLogicalOperator.And,
                left,
                right)
        };
    }

    private static HashSet<string> CreateColumns(params string[] columns)
    {
        return new HashSet<string>(columns, StringComparer.OrdinalIgnoreCase);
    }
}
