#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq.Expressions;
using Musoq.Schema.Optimization;

namespace Musoq.DataSources.Common;

/// <summary>Compilation metadata containing closed row predicates; it never stores query row values.</summary>
internal sealed class TypedPredicatePlan
{
    internal const string PropertyName = "TypedProviderPredicates";
    private readonly Dictionary<Type, Delegate> _bindings = new();

    internal void Bind<TRow>(SourcePredicateExpression? predicate,
        Func<ParameterExpression, string, Expression>? field = null)
    {
        if (predicate is null)
            return;
        var row = Expression.Parameter(typeof(TRow), "row");
        var body = TypedPredicateCompiler.Build(predicate, name => field is null
            ? Expression.PropertyOrField(row, name) : field(row, name));
        _bindings.Add(typeof(TRow), Expression.Lambda<Func<TRow, bool>>(
            Expression.Coalesce(body, Expression.Constant(false)), row).Compile());
    }

    internal Func<TRow, bool>? Get<TRow>() => _bindings.TryGetValue(typeof(TRow), out var binding)
        ? (Func<TRow, bool>)binding : null;

    internal static Func<TRow, bool>? Get<TRow>(SourceExecutionPlan plan)
    {
        if (plan.AcceptedPredicate is null)
            return null;
        if (plan.Properties.TryGetValue(PropertyName, out var metadata) && metadata is TypedPredicatePlan bound &&
            bound.Get<TRow>() is { } predicate)
            return predicate;
        throw new InvalidOperationException("An accepted provider predicate requires a concretely bound source plan.");
    }
}

internal static class TypedPredicateCompiler
{
    private static readonly ConstantExpression Unknown = Expression.Constant(null, typeof(bool?));

    internal static Expression Build(SourcePredicateExpression predicate, Func<string, Expression> field)
    {
        return predicate switch
        {
            SourcePredicateLogical logical => logical.Operator == SourcePredicateLogicalOperator.And
                ? Expression.AndAlso(Build(logical.Left, field), Build(logical.Right, field))
                : Expression.OrElse(Build(logical.Left, field), Build(logical.Right, field)),
            SourcePredicateComparison comparison => Compare(Scalar(comparison.Left, field), Scalar(comparison.Right, field), comparison.Operator),
            SourcePredicateIn values => In(values, field),
            SourcePredicateNullCheck check => Expression.Convert(check.IsNegated
                ? Expression.Not(IsNull(Scalar(check.Expression, field))) : IsNull(Scalar(check.Expression, field)), typeof(bool?)),
            SourcePredicateLiteral { Value: bool value } => Expression.Constant((bool?)value, typeof(bool?)),
            SourcePredicateLiteral { Value: null } => Unknown,
            _ => throw new InvalidOperationException("The provider predicate has no typed operation.")
        };
    }

    private static Expression Scalar(SourcePredicateExpression scalar, Func<string, Expression> field)
        => scalar switch
        {
            SourcePredicateColumn column => field(Normalize(column.Column.Name)),
            SourcePredicateLiteral literal => Expression.Constant(literal.Value, literal.Value?.GetType() ?? typeof(object)),
            _ => throw new InvalidOperationException("A provider predicate scalar must be a concrete field or literal.")
        };

    private static string Normalize(string name) => name[(name.LastIndexOf('.') + 1)..];

    private static Expression In(SourcePredicateIn predicate, Func<string, Expression> field)
    {
        var scalar = Scalar(predicate.Expression, field);
        var value = Expression.Variable(scalar.Type, "value");
        Expression result = Expression.Constant((bool?)false, typeof(bool?));
        foreach (var candidate in predicate.Values)
            result = Expression.OrElse(result, Compare(value, Scalar(candidate, field), SourcePredicateComparisonOperator.Equal));
        if (predicate.IsNegated)
            result = Expression.Not(result);
        return Expression.Block(new[] { value }, Expression.Assign(value, scalar), result);
    }

    private static Expression Compare(Expression left, Expression right, SourcePredicateComparisonOperator op)
    {
        if (left is ConstantExpression { Value: null } || right is ConstantExpression { Value: null })
            return Unknown;
        var leftType = Nullable.GetUnderlyingType(left.Type) ?? left.Type;
        var rightType = Nullable.GetUnderlyingType(right.Type) ?? right.Type;
        if (leftType == typeof(DateTimeOffset) && right is ConstantExpression { Value: not null } rightConstant)
        {
            right = DateConstant(rightConstant);
            rightType = right.Type;
        }
        if (rightType == typeof(DateTimeOffset) && left is ConstantExpression { Value: not null } leftConstant)
        {
            left = DateConstant(leftConstant);
            leftType = left.Type;
        }
        var type = leftType == rightType ? leftType : NumericType(leftType, rightType);
        var leftValue = Expression.Variable(left.Type, "left");
        var rightValue = Expression.Variable(right.Type, "right");
        var nulls = Expression.OrElse(IsNull(leftValue), IsNull(rightValue));
        Expression readLeft = Nullable.GetUnderlyingType(left.Type) is null ? leftValue : Expression.Property(leftValue, "Value");
        Expression readRight = Nullable.GetUnderlyingType(right.Type) is null ? rightValue : Expression.Property(rightValue, "Value");
        if (readLeft.Type != type) readLeft = Expression.ConvertChecked(readLeft, type);
        if (readRight.Type != type) readRight = Expression.ConvertChecked(readRight, type);
        Expression result;
        if (type == typeof(string))
        {
            var comparison = Expression.Call(typeof(string), nameof(string.Compare), null,
                readLeft, readRight, Expression.Constant(StringComparison.Ordinal));
            result = Relation(comparison, Expression.Constant(0), op);
        }
        else
            result = Relation(readLeft, readRight, op);
        return Expression.Block(new[] { leftValue, rightValue }, Expression.Assign(leftValue, left),
            Expression.Assign(rightValue, right), Expression.Condition(nulls, Unknown, Expression.Convert(result, typeof(bool?))));
    }

    private static Expression DateConstant(ConstantExpression constant) => constant.Value switch
    {
        string text => Expression.Constant(DateTimeOffset.Parse(text, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind)),
        DateTime value => Expression.Constant(new DateTimeOffset(value)),
        _ => constant
    };

    private static Expression Relation(Expression left, Expression right, SourcePredicateComparisonOperator op) => op switch
    {
        SourcePredicateComparisonOperator.Equal => Expression.Equal(left, right),
        SourcePredicateComparisonOperator.NotEqual => Expression.NotEqual(left, right),
        SourcePredicateComparisonOperator.GreaterThan => Expression.GreaterThan(left, right),
        SourcePredicateComparisonOperator.GreaterOrEqual => Expression.GreaterThanOrEqual(left, right),
        SourcePredicateComparisonOperator.LessThan => Expression.LessThan(left, right),
        SourcePredicateComparisonOperator.LessOrEqual => Expression.LessThanOrEqual(left, right),
        _ => throw new InvalidOperationException("Unsupported typed comparison operation.")
    };

    private static Expression IsNull(Expression value) => !value.Type.IsValueType || Nullable.GetUnderlyingType(value.Type) is not null
        ? Expression.Equal(value, Expression.Constant(null, value.Type)) : Expression.Constant(false);

    private static Type NumericType(Type left, Type right)
    {
        static bool Numeric(Type type) => type == typeof(byte) || type == typeof(sbyte) || type == typeof(short) || type == typeof(ushort) ||
            type == typeof(int) || type == typeof(uint) || type == typeof(long) || type == typeof(ulong) ||
            type == typeof(float) || type == typeof(double) || type == typeof(decimal);
        if (!Numeric(left) || !Numeric(right))
            throw new InvalidOperationException($"Provider predicate types '{left}' and '{right}' are incompatible.");
        if (left == typeof(double) || right == typeof(double) || left == typeof(float) || right == typeof(float)) return typeof(double);
        if (left == typeof(decimal) || right == typeof(decimal) || left == typeof(ulong) || right == typeof(ulong)) return typeof(decimal);
        return typeof(long);
    }
}
