#nullable enable

using System;
using System.Collections.Generic;
using System.Globalization;
using Musoq.Schema.Optimization;
using Musoq.DataSources.Search.Entities;

namespace Musoq.DataSources.Search.Components.Planning;

// Test-only frozen oracle for differential expected rows; never used by a provider.
internal static class FrozenPredicateOracle
{
    private enum SqlTruthValue
    {
        False,
        True,
        Unknown
    }

    public static bool Matches<T>(
        SourcePredicateExpression? predicate,
        T row,
        Func<T, string, object?> valueAccessor)
    {
        ArgumentNullException.ThrowIfNull(valueAccessor);
        return predicate is null ||
               Evaluate(predicate, column => valueAccessor(
                   row,
                   SearchPredicatePlanning.NormalizeColumnName(column.Column.Name))) ==
               SqlTruthValue.True;
    }

    private static SqlTruthValue Evaluate(
        SourcePredicateExpression expression,
        Func<SourcePredicateColumn, object?> valueAccessor)
    {
        return expression switch
        {
            SourcePredicateComparison comparison => EvaluateComparison(comparison, valueAccessor),
            SourcePredicateLogical logical => EvaluateLogical(logical, valueAccessor),
            SourcePredicateIn inPredicate => EvaluateIn(inPredicate, valueAccessor),
            SourcePredicateNullCheck nullCheck => EvaluateNullCheck(nullCheck, valueAccessor),
            SourcePredicateLiteral literal => EvaluateLiteral(literal.Value),
            _ => SqlTruthValue.Unknown
        };
    }

    private static SqlTruthValue EvaluateComparison(
        SourcePredicateComparison comparison,
        Func<SourcePredicateColumn, object?> valueAccessor)
    {
        if (!TryEvaluateScalar(comparison.Left, valueAccessor, out var left) ||
            !TryEvaluateScalar(comparison.Right, valueAccessor, out var right))
            return SqlTruthValue.Unknown;

        if (left is null || right is null)
            return SqlTruthValue.Unknown;

        var comparisonResult = CompareValues(left, right);
        if (comparisonResult is null)
            return SqlTruthValue.Unknown;

        var result = comparison.Operator switch
        {
            SourcePredicateComparisonOperator.Equal => comparisonResult.Value == 0,
            SourcePredicateComparisonOperator.NotEqual => comparisonResult.Value != 0,
            SourcePredicateComparisonOperator.GreaterThan => comparisonResult.Value > 0,
            SourcePredicateComparisonOperator.GreaterOrEqual => comparisonResult.Value >= 0,
            SourcePredicateComparisonOperator.LessThan => comparisonResult.Value < 0,
            SourcePredicateComparisonOperator.LessOrEqual => comparisonResult.Value <= 0,
            _ => false
        };
        return result ? SqlTruthValue.True : SqlTruthValue.False;
    }

    private static SqlTruthValue EvaluateLogical(
        SourcePredicateLogical logical,
        Func<SourcePredicateColumn, object?> valueAccessor)
    {
        var left = Evaluate(logical.Left, valueAccessor);
        var right = Evaluate(logical.Right, valueAccessor);
        return logical.Operator switch
        {
            SourcePredicateLogicalOperator.And => And(left, right),
            SourcePredicateLogicalOperator.Or => Or(left, right),
            _ => SqlTruthValue.Unknown
        };
    }

    private static SqlTruthValue EvaluateIn(
        SourcePredicateIn inPredicate,
        Func<SourcePredicateColumn, object?> valueAccessor)
    {
        if (!TryEvaluateScalar(inPredicate.Expression, valueAccessor, out var value))
            return SqlTruthValue.Unknown;

        if (value is null)
            return SqlTruthValue.Unknown;

        var hasNull = false;
        for (var index = 0; index < inPredicate.Values.Count; index++)
        {
            if (!TryEvaluateScalar(inPredicate.Values[index], valueAccessor, out var candidate))
                return SqlTruthValue.Unknown;

            if (candidate is null)
            {
                hasNull = true;
                continue;
            }

            var comparison = CompareValues(value, candidate);
            if (comparison == 0)
                return inPredicate.IsNegated ? SqlTruthValue.False : SqlTruthValue.True;
        }

        if (hasNull)
            return SqlTruthValue.Unknown;

        return inPredicate.IsNegated ? SqlTruthValue.True : SqlTruthValue.False;
    }

    private static SqlTruthValue EvaluateNullCheck(
        SourcePredicateNullCheck nullCheck,
        Func<SourcePredicateColumn, object?> valueAccessor)
    {
        if (!TryEvaluateScalar(nullCheck.Expression, valueAccessor, out var value))
            return SqlTruthValue.Unknown;

        var isNull = value is null;
        return isNull ^ nullCheck.IsNegated
            ? SqlTruthValue.True
            : SqlTruthValue.False;
    }

    private static SqlTruthValue EvaluateLiteral(object? value)
    {
        return value switch
        {
            true => SqlTruthValue.True,
            false => SqlTruthValue.False,
            _ => SqlTruthValue.Unknown
        };
    }

    private static bool TryEvaluateScalar(
        SourcePredicateExpression expression,
        Func<SourcePredicateColumn, object?> valueAccessor,
        out object? value)
    {
        switch (expression)
        {
            case SourcePredicateColumn column:
                value = valueAccessor(column);
                return true;
            case SourcePredicateLiteral literal:
                value = literal.Value;
                return true;
            default:
                value = null;
                return false;
        }
    }

    private static SqlTruthValue And(SqlTruthValue left, SqlTruthValue right)
    {
        if (left == SqlTruthValue.False || right == SqlTruthValue.False)
            return SqlTruthValue.False;
        if (left == SqlTruthValue.Unknown || right == SqlTruthValue.Unknown)
            return SqlTruthValue.Unknown;
        return SqlTruthValue.True;
    }

    private static SqlTruthValue Or(SqlTruthValue left, SqlTruthValue right)
    {
        if (left == SqlTruthValue.True || right == SqlTruthValue.True)
            return SqlTruthValue.True;
        if (left == SqlTruthValue.Unknown || right == SqlTruthValue.Unknown)
            return SqlTruthValue.Unknown;
        return SqlTruthValue.False;
    }

    private static int? CompareValues(object left, object right)
    {
        if (IsNumber(left) && IsNumber(right))
        {
            try
            {
                return Convert.ToDecimal(left, CultureInfo.InvariantCulture)
                    .CompareTo(Convert.ToDecimal(right, CultureInfo.InvariantCulture));
            }
            catch (Exception exception) when (exception is FormatException or OverflowException)
            {
                return null;
            }
        }

        if (left is string leftText && right is string rightText)
            return string.Compare(leftText, rightText, StringComparison.Ordinal);

        if (left is bool leftBoolean && right is bool rightBoolean)
            return leftBoolean.CompareTo(rightBoolean);

        if (left.GetType() != right.GetType())
            return null;

        if (left is IComparable comparable)
        {
            try
            {
                return comparable.CompareTo(right);
            }
            catch (ArgumentException)
            {
                return null;
            }
        }

        return Equals(left, right) ? 0 : null;
    }

    private static bool IsNumber(object value)
    {
        return value is byte or sbyte or short or ushort or int or uint or long or ulong or
            nint or nuint or float or double or decimal;
    }
}

internal static class FrozenPredicateValues
{
    public static object? GetByteValue(SearchByteMatch row, string columnName)
    {
        return SearchPredicatePlanning.NormalizeColumnName(columnName) switch
        {
            nameof(SearchByteMatch.Path) => row.Path,
            nameof(SearchByteMatch.Origin) => row.Origin,
            nameof(SearchByteMatch.PatternId) => row.PatternId,
            nameof(SearchByteMatch.MatchIndex) => row.MatchIndex,
            nameof(SearchByteMatch.ByteOffset) => row.ByteOffset,
            nameof(SearchByteMatch.ByteLength) => row.ByteLength,
            nameof(SearchByteMatch.WindowStartByteOffset) => row.WindowStartByteOffset,
            nameof(SearchByteMatch.WindowByteLength) => row.WindowByteLength,
            nameof(SearchByteMatch.WindowComplete) => row.WindowComplete,
            _ => throw UnknownColumn(columnName, nameof(SearchByteMatch))
        };
    }

    public static object? GetMatchValue(SearchMatch row, string columnName)
    {
        return SearchPredicatePlanning.NormalizeColumnName(columnName) switch
        {
            nameof(SearchMatch.Path) => row.Path,
            nameof(SearchMatch.PatternId) => row.PatternId,
            nameof(SearchMatch.MatchIndex) => row.MatchIndex,
            nameof(SearchMatch.ByteOffset) => row.ByteOffset,
            nameof(SearchMatch.ByteLength) => row.ByteLength,
            nameof(SearchMatch.LineNumber) => row.LineNumber,
            nameof(SearchMatch.Utf16Column) => row.Utf16Column,
            nameof(SearchMatch.Utf16Length) => row.Utf16Length,
            _ => throw UnknownColumn(columnName, nameof(SearchMatch))
        };
    }

    public static object? GetLineValue(SearchLine row, string columnName)
    {
        return SearchPredicatePlanning.NormalizeColumnName(columnName) switch
        {
            nameof(SearchLine.Path) => row.Path,
            nameof(SearchLine.Origin) => row.Origin,
            nameof(SearchLine.PatternId) => row.PatternId,
            nameof(SearchLine.LineNumber) => row.LineNumber,
            nameof(SearchLine.ByteOffset) => row.ByteOffset,
            nameof(SearchLine.OccurrenceCount) => row.OccurrenceCount,
            _ => throw UnknownColumn(columnName, nameof(SearchLine))
        };
    }

    public static object? GetFileValue(SearchFile row, string columnName)
    {
        return SearchPredicatePlanning.NormalizeColumnName(columnName) switch
        {
            nameof(SearchFile.Path) => row.Path,
            nameof(SearchFile.Origin) => row.Origin,
            nameof(SearchFile.PatternId) => row.PatternId,
            _ => throw UnknownColumn(columnName, nameof(SearchFile))
        };
    }

    public static object? GetCountValue(SearchCount row, string columnName)
    {
        return SearchPredicatePlanning.NormalizeColumnName(columnName) switch
        {
            nameof(SearchCount.Path) => row.Path,
            nameof(SearchCount.Origin) => row.Origin,
            nameof(SearchCount.PatternId) => row.PatternId,
            nameof(SearchCount.OccurrenceCount) => row.OccurrenceCount,
            nameof(SearchCount.MatchingLineCount) => row.MatchingLineCount,
            nameof(SearchCount.BytesScanned) => row.BytesScanned,
            nameof(SearchCount.Complete) => row.Complete,
            _ => throw UnknownColumn(columnName, nameof(SearchCount))
        };
    }

    public static object? GetPathValue(SearchPath row, string columnName)
    {
        return SearchPredicatePlanning.NormalizeColumnName(columnName) switch
        {
            nameof(SearchPath.Path) => row.Path,
            nameof(SearchPath.Origin) => row.Origin,
            nameof(SearchPath.EntryKind) => row.EntryKind,
            _ => throw UnknownColumn(columnName, nameof(SearchPath))
        };
    }

    public static object? GetAuditValue(SearchAudit row, string columnName)
    {
        return SearchPredicatePlanning.NormalizeColumnName(columnName) switch
        {
            nameof(SearchAudit.Root) => row.Root,
            nameof(SearchAudit.ScanId) => row.ScanId,
            nameof(SearchAudit.ScopeFingerprint) => row.ScopeFingerprint,
            nameof(SearchAudit.Outcome) => row.Outcome,
            nameof(SearchAudit.TerminalReason) => row.TerminalReason,
            nameof(SearchAudit.Complete) => row.Complete,
            nameof(SearchAudit.ScopeExhausted) => row.ScopeExhausted,
            nameof(SearchAudit.QuerySatisfied) => row.QuerySatisfied,
            nameof(SearchAudit.CountsExact) => row.CountsExact,
            nameof(SearchAudit.ScopeResolved) => row.ScopeResolved,
            nameof(SearchAudit.VisitedFiles) => row.VisitedFiles,
            nameof(SearchAudit.EligibleFiles) => row.EligibleFiles,
            nameof(SearchAudit.FilesOpened) => row.FilesOpened,
            nameof(SearchAudit.FilesRead) => row.FilesRead,
            nameof(SearchAudit.FilesCompleted) => row.FilesCompleted,
            nameof(SearchAudit.FilesFailed) => row.FilesFailed,
            nameof(SearchAudit.BinaryFilesSkipped) => row.BinaryFilesSkipped,
            nameof(SearchAudit.BytesScanned) => row.BytesScanned,
            nameof(SearchAudit.FilesMatched) => row.FilesMatched,
            nameof(SearchAudit.MatchingLines) => row.MatchingLines,
            nameof(SearchAudit.Occurrences) => row.Occurrences,
            nameof(SearchAudit.ObservedRows) => row.ObservedRows,
            nameof(SearchAudit.FailureCode) => row.FailureCode,
            nameof(SearchAudit.FailurePath) => row.FailurePath,
            _ => throw UnknownColumn(columnName, nameof(SearchAudit))
        };
    }

    private static InvalidOperationException UnknownColumn(string columnName, string rowType)
    {
        return new InvalidOperationException(
            $"Search predicate execution requested unknown {rowType} column '{columnName}'.");
    }
}
