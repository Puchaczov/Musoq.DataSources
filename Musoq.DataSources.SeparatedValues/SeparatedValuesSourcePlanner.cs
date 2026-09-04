using System;
using System.Collections.Generic;
using System.Globalization;
using Musoq.DataSources.Structured;
using Musoq.Schema;
using Musoq.Schema.Optimization;

namespace Musoq.DataSources.SeparatedValues;

internal static class SeparatedValuesSourcePlanner
{
    public static SourcePlanResult Plan(
        SeparatedValuesSourceContract contract,
        SourcePlanRequest request)
    {
        ArgumentNullException.ThrowIfNull(contract);
        var snapshot = contract.Snapshot;
        var requiredColumns = request.RequiredColumns ?? [];
        var (acceptedPredicate, residualPredicate) = SplitPredicate(
            request.Predicate,
            expression => IsSupportedPredicate(contract, expression));
        var residualOrderBy = request.OrderBy ?? [];
        var acceptsSlice = residualPredicate is null && residualOrderBy.Count == 0;
        var layout = StructuredExecutionLayout.Bind(
            snapshot,
            GetColumnNames(requiredColumns),
            IncludesCompleteSchema(snapshot, request.RequiredColumns));
        var readPlan = new SeparatedValuesReadPlan
        {
            ProjectionAccepted = request.RequiredColumns is not null,
            AcceptedPredicate = acceptedPredicate,
            HasResidualWork = residualPredicate is not null || residualOrderBy.Count > 0
        };
        var properties = SeparatedValuesReadPlan.CreateProperties(readPlan);
        properties[SeparatedValuesPlanning.LayoutPropertyName] = layout;
        properties[SeparatedValuesSourceContract.PropertyName] = contract;

        return new SourcePlanResult
        {
            ExecutionPlan = new SourceExecutionPlan
            {
                Identity = request.Identity,
                AcceptedColumns = requiredColumns,
                AcceptedPredicate = acceptedPredicate,
                AcceptedOrderBy = [],
                AcceptedSkip = acceptsSlice ? request.Skip : null,
                AcceptedTake = acceptsSlice ? request.Take : null,
                Properties = properties
            },
            AcceptedColumns = requiredColumns,
            AcceptedPredicate = acceptedPredicate,
            ResidualPredicate = residualPredicate,
            AcceptedOrderBy = [],
            ResidualOrderBy = residualOrderBy,
            AcceptedSkip = acceptsSlice ? request.Skip : null,
            ResidualSkip = acceptsSlice ? null : request.Skip,
            AcceptedTake = acceptsSlice ? request.Take : null,
            ResidualTake = acceptsSlice ? null : request.Take,
            Cardinality = CreateCardinality(contract, acceptedPredicate),
            Diagnostics = contract.Diagnostics,
            ContractDiagnostics = []
        };
    }

    private static CardinalityEstimate CreateCardinality(
        SeparatedValuesSourceContract contract,
        SourcePredicateExpression? acceptedPredicate)
    {
        if (!contract.HasExactCardinality)
            return CardinalityEstimate.Unknown("Separated-values cardinality is unknown after bounded schema resolution.");

        return acceptedPredicate is null
            ? CardinalityEstimate.Exact(contract.Snapshot.RowCount, "Exact separated-values completed-scan row count.")
            : CardinalityEstimate.Bounded(
                0,
                contract.Snapshot.RowCount,
                1.0,
                "Separated-values predicate pushdown bounds the completed-scan rows.");
    }

    internal static bool TryGetComparisonParts(
        SourcePredicateComparison comparison,
        out string columnName,
        out SourcePredicateLiteral literal,
        out SourcePredicateComparisonOperator op)
    {
        if (comparison.Left is SourcePredicateColumn leftColumn &&
            comparison.Right is SourcePredicateLiteral rightLiteral)
        {
            columnName = NormalizeColumnName(leftColumn.Column.Name);
            literal = rightLiteral;
            op = comparison.Operator;
            return true;
        }

        if (comparison.Right is SourcePredicateColumn rightColumn &&
            comparison.Left is SourcePredicateLiteral leftLiteral)
        {
            columnName = NormalizeColumnName(rightColumn.Column.Name);
            literal = leftLiteral;
            op = Invert(comparison.Operator);
            return true;
        }

        columnName = string.Empty;
        literal = null!;
        op = comparison.Operator;
        return false;
    }

    internal static bool TryGetEnumComparisonParts(
        SourcePredicateComparison comparison,
        out string columnName,
        out SourcePredicateEnumLiteral literal,
        out SourcePredicateComparisonOperator op)
    {
        if (comparison.Left is SourcePredicateColumn leftColumn &&
            comparison.Right is SourcePredicateEnumLiteral rightLiteral)
        {
            columnName = NormalizeColumnName(leftColumn.Column.Name);
            literal = rightLiteral;
            op = comparison.Operator;
            return true;
        }

        if (comparison.Right is SourcePredicateColumn rightColumn &&
            comparison.Left is SourcePredicateEnumLiteral leftLiteral)
        {
            columnName = NormalizeColumnName(rightColumn.Column.Name);
            literal = leftLiteral;
            op = Invert(comparison.Operator);
            return true;
        }

        columnName = string.Empty;
        literal = null!;
        op = comparison.Operator;
        return false;
    }

    internal static string NormalizeColumnName(string name)
    {
        var dotIndex = name.LastIndexOf('.');
        return dotIndex >= 0 ? name[(dotIndex + 1)..] : name;
    }

    private static IEnumerable<string> GetColumnNames(IReadOnlyList<SourceColumnRef> columns)
    {
        foreach (var column in columns)
            yield return NormalizeColumnName(column.Name);
    }

    private static bool IncludesCompleteSchema(
        StructuredSchemaSnapshot snapshot,
        IReadOnlyList<SourceColumnRef>? requiredColumns)
    {
        if (requiredColumns is null)
            return true;
        if (requiredColumns.Count != snapshot.Columns.Length)
            return false;

        for (var index = 0; index < requiredColumns.Count; index++)
        {
            if (!string.Equals(
                    NormalizeColumnName(requiredColumns[index].Name),
                    snapshot.Columns[index].Name,
                    StringComparison.Ordinal))
                return false;
        }

        return true;
    }

    private static bool IsSupportedPredicate(
        SeparatedValuesSourceContract contract,
        SourcePredicateExpression expression)
    {
        var snapshot = contract.Snapshot;
        if (expression is SourcePredicateComparison comparison)
        {
            if (TryGetEnumComparisonParts(comparison, out var enumName, out var enumLiteral, out var enumOp))
                return IsSupportedEnumComparison(contract, enumName, enumLiteral, enumOp);

            if (!TryGetComparisonParts(comparison, out var name, out var literal, out var op) ||
                literal.Value is null ||
                !IsSupportedOperator(op) ||
                !snapshot.TryGetColumn(name, out var column))
                return false;

            // A declared enum must only be pushed down through its descriptor-driven
            // literal representation. Treat ordinary literals on enum columns as
            // residual Core predicates rather than falling through to integral parsing.
            if (column.EnumType is not null)
                return false;

            return IsSupportedOrdinaryComparison(contract, column, literal, op);
        }

        if (expression is SourcePredicateIn membership)
            return IsSupportedEnumMembership(contract, membership);

        if (expression is SourcePredicateNullCheck nullCheck)
            return IsSupportedEnumNullCheck(contract, nullCheck);

        if (expression is SourcePredicateFlags flags)
            return IsSupportedEnumFlags(contract, flags);

        return false;
    }

    private static bool IsSupportedOrdinaryComparison(
        SeparatedValuesSourceContract contract,
        StructuredColumnSnapshot column,
        SourcePredicateLiteral literal,
        SourcePredicateComparisonOperator op)
    {
        var literalValue = literal.Value!;
        if (contract.Mode == SeparatedValuesSchemaResolutionMode.Declared &&
            contract.ColumnContracts.Length > column.SourceOrdinal)
        {
            var exact = contract.ColumnContracts[column.SourceOrdinal];
            var conversion = SeparatedValuesValueConverter.GetConversion(exact.ClrType, exact.TypeState);
            return conversion switch
            {
                SeparatedValuesConversion.String or SeparatedValuesConversion.Character =>
                    IsEqualityOperator(op) && literal.Value is string,
                SeparatedValuesConversion.Boolean =>
                    IsEqualityOperator(op) && literal.Value is bool,
                SeparatedValuesConversion.DateTime or
                    SeparatedValuesConversion.DateTimeOffset or
                    SeparatedValuesConversion.DateOnly or
                SeparatedValuesConversion.TimeOnly or
                SeparatedValuesConversion.TimeSpan or
                SeparatedValuesConversion.Guid => IsEqualityOperator(op) && literal.Value is string,
                _ => CanConvertLiteral(literalValue, conversion)
            };
        }

        return column.TypeState.Kind switch
        {
            StructuredValueKind.Long => CanConvert<long>(literalValue),
            StructuredValueKind.Decimal => CanConvert<decimal>(literalValue),
            StructuredValueKind.Double => CanConvert<double>(literalValue),
            StructuredValueKind.Boolean => IsEqualityOperator(op) && literal.Value is bool,
            StructuredValueKind.String => IsEqualityOperator(op) && literal.Value is string,
            _ => false
        };
    }

    private static bool IsSupportedEnumComparison(
        SeparatedValuesSourceContract contract,
        string name,
        SourcePredicateEnumLiteral literal,
        SourcePredicateComparisonOperator op)
    {
        if (!IsEqualityOperator(op) ||
            !TryGetEnumColumn(contract, name, out _, out var descriptor, out _))
            return false;

        return IsMatchingEnumLiteral(literal, descriptor);
    }

    private static bool IsSupportedEnumMembership(
        SeparatedValuesSourceContract contract,
        SourcePredicateIn membership)
    {
        if (!TryGetEnumColumn(contract, membership.Expression, out _, out var descriptor, out _))
            return false;

        if (membership.Values.Count == 0)
            return true;

        foreach (var value in membership.Values)
        {
            if (value is not SourcePredicateEnumLiteral literal ||
                !IsMatchingEnumLiteral(literal, descriptor))
                return false;
        }

        return true;
    }

    private static bool IsSupportedEnumNullCheck(
        SeparatedValuesSourceContract contract,
        SourcePredicateNullCheck nullCheck)
    {
        return TryGetEnumColumn(contract, nullCheck.Expression, out _, out _, out _);
    }

    private static bool IsSupportedEnumFlags(
        SeparatedValuesSourceContract contract,
        SourcePredicateFlags flags)
    {
        if (!TryGetEnumColumn(contract, flags.Expression, out _, out var descriptor, out _))
            return false;

        return descriptor.IsFlags && IsMatchingEnumLiteral(flags.Mask, descriptor);
    }

    internal static bool TryGetEnumColumn(
        SeparatedValuesSourceContract contract,
        SourcePredicateExpression expression,
        out StructuredColumnSnapshot column,
        out EnumTypeDescriptor descriptor,
        out SeparatedValuesEnumPlan plan)
    {
        if (expression is SourcePredicateColumn sourceColumn)
            return TryGetEnumColumn(contract, NormalizeColumnName(sourceColumn.Column.Name), out column, out descriptor, out plan);

        column = null!;
        descriptor = null!;
        plan = null!;
        return false;
    }

    internal static bool TryGetEnumColumn(
        SeparatedValuesSourceContract contract,
        string name,
        out StructuredColumnSnapshot column,
        out EnumTypeDescriptor descriptor,
        out SeparatedValuesEnumPlan plan)
    {
        if (contract.Snapshot.TryGetColumn(name, out column!) &&
            column.EnumType is not null &&
            (uint)column.SourceOrdinal < (uint)contract.ColumnContracts.Length)
        {
            var contractColumn = contract.ColumnContracts[column.SourceOrdinal];
            if (contractColumn.EnumType is not null && contractColumn.EnumPlan is not null)
            {
                descriptor = contractColumn.EnumType;
                plan = contractColumn.EnumPlan;
                return true;
            }
        }

        column = null!;
        descriptor = null!;
        plan = null!;
        return false;
    }

    private static bool IsMatchingEnumLiteral(
        SourcePredicateEnumLiteral literal,
        EnumTypeDescriptor descriptor)
    {
        return string.Equals(literal.EnumFingerprint, descriptor.Fingerprint, StringComparison.Ordinal) &&
               literal.Value.Kind == descriptor.UnderlyingKind;
    }

    private static bool CanConvertLiteral(object value, SeparatedValuesConversion conversion)
    {
        try
        {
            switch (conversion)
            {
                case SeparatedValuesConversion.Byte:
                    _ = Convert.ToByte(value, CultureInfo.InvariantCulture);
                    break;
                case SeparatedValuesConversion.SByte:
                    _ = Convert.ToSByte(value, CultureInfo.InvariantCulture);
                    break;
                case SeparatedValuesConversion.Int16:
                    _ = Convert.ToInt16(value, CultureInfo.InvariantCulture);
                    break;
                case SeparatedValuesConversion.Int32:
                    _ = Convert.ToInt32(value, CultureInfo.InvariantCulture);
                    break;
                case SeparatedValuesConversion.Int64:
                    _ = Convert.ToInt64(value, CultureInfo.InvariantCulture);
                    break;
                case SeparatedValuesConversion.UInt16:
                    _ = Convert.ToUInt16(value, CultureInfo.InvariantCulture);
                    break;
                case SeparatedValuesConversion.UInt32:
                    _ = Convert.ToUInt32(value, CultureInfo.InvariantCulture);
                    break;
                case SeparatedValuesConversion.UInt64:
                    _ = Convert.ToUInt64(value, CultureInfo.InvariantCulture);
                    break;
                case SeparatedValuesConversion.Decimal:
                    _ = Convert.ToDecimal(value, CultureInfo.InvariantCulture);
                    break;
                case SeparatedValuesConversion.Single:
                    _ = Convert.ToSingle(value, CultureInfo.InvariantCulture);
                    break;
                case SeparatedValuesConversion.Double:
                    _ = Convert.ToDouble(value, CultureInfo.InvariantCulture);
                    break;
                default:
                    throw new InvalidCastException();
            }
            return true;
        }
        catch (Exception exception) when (exception is InvalidCastException or FormatException or OverflowException)
        {
            return false;
        }
    }

    private static bool CanConvert<T>(object value)
    {
        try
        {
            _ = Convert.ChangeType(value, typeof(T), CultureInfo.InvariantCulture);
            return true;
        }
        catch (Exception exception) when (exception is InvalidCastException or FormatException or OverflowException)
        {
            return false;
        }
    }

    private static bool IsSupportedOperator(SourcePredicateComparisonOperator op)
    {
        return op is SourcePredicateComparisonOperator.Equal or
            SourcePredicateComparisonOperator.NotEqual or
            SourcePredicateComparisonOperator.GreaterThan or
            SourcePredicateComparisonOperator.GreaterOrEqual or
            SourcePredicateComparisonOperator.LessThan or
            SourcePredicateComparisonOperator.LessOrEqual;
    }

    private static bool IsEqualityOperator(SourcePredicateComparisonOperator op)
    {
        return op is SourcePredicateComparisonOperator.Equal or SourcePredicateComparisonOperator.NotEqual;
    }

    private static (SourcePredicateExpression? Accepted, SourcePredicateExpression? Residual) SplitPredicate(
        SourcePredicateExpression? predicate,
        Func<SourcePredicateExpression, bool> canAccept)
    {
        if (predicate is null)
            return (null, null);

        if (predicate is SourcePredicateLogical { Operator: SourcePredicateLogicalOperator.And } logical)
        {
            var left = SplitPredicate(logical.Left, canAccept);
            var right = SplitPredicate(logical.Right, canAccept);

            return (CombineAnd(left.Accepted, right.Accepted), CombineAnd(left.Residual, right.Residual));
        }

        return canAccept(predicate) ? (predicate, null) : (null, predicate);
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
            _ => new SourcePredicateLogical(SourcePredicateLogicalOperator.And, left, right)
        };
    }

    private static SourcePredicateComparisonOperator Invert(SourcePredicateComparisonOperator op)
    {
        return op switch
        {
            SourcePredicateComparisonOperator.GreaterThan => SourcePredicateComparisonOperator.LessThan,
            SourcePredicateComparisonOperator.GreaterOrEqual => SourcePredicateComparisonOperator.LessOrEqual,
            SourcePredicateComparisonOperator.LessThan => SourcePredicateComparisonOperator.GreaterThan,
            SourcePredicateComparisonOperator.LessOrEqual => SourcePredicateComparisonOperator.GreaterOrEqual,
            _ => op
        };
    }
}
