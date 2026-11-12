#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using Musoq.Schema;
using Musoq.Schema.Optimization;

namespace Musoq.DataSources.Structured;

// Bound once per scan, never through boxed rows or a per-record value array.
internal sealed class StructuredComputedLayout
{
    public QueryRowShape Output { get; }
    public QueryRowShape Input { get; }
    public int[] OutputSlots { get; }

    public StructuredComputedLayout(QueryRowShape output, IReadOnlyList<QueryRowField> available)
    {
        Output = output;
        var inputs = new List<QueryRowField>();
        int Add(string name)
        {
            var index = inputs.FindIndex(field => field.Name == name);
            if (index >= 0) return index;
            var field = available.Single(field => field.Name == name);
            index = inputs.Count;
            inputs.Add(new QueryRowField(index, field.SourceColumnIndex, field.Name, field.FieldType,
                field.SourceReadType, field.EnumType, field.IsNullable, field.ReadModifiers, field.Stability));
            return index;
        }

        OutputSlots = new int[output.Fields.Count];
        foreach (var field in output.Fields)
        {
            if (field.ComputedProjection is not { } projection)
                OutputSlots[field.Slot] = Add(field.Name);
            else
            {
                OutputSlots[field.Slot] = -1;
                foreach (var column in Columns(projection.Expression)) Add(column.Column.Name);
            }
        }
        Input = new QueryRowShape(inputs, output.Replayability);
    }

    internal static IEnumerable<SourceScalarColumn> Columns(SourceScalarExpression expression) => expression switch
    {
        SourceScalarColumn column => [column],
        SourceScalarUnary unary => Columns(unary.Operand),
        SourceScalarBinary binary => Columns(binary.Left).Concat(Columns(binary.Right)),
        SourceScalarCast cast => Columns(cast.Operand),
        SourceScalarNullCheck check => Columns(check.Operand),
        SourceScalarCoalesce coalesce => coalesce.Expressions.SelectMany(Columns),
        _ => []
    };

    public static void Validate(QueryRowShape shape, SourceExecutionPlan plan)
    {
        var requested = shape.Fields.Where(field => field.ComputedProjection is not null)
            .Select(field => field.ComputedProjection!).ToArray();
        if (!SourceComputedProjectionNegotiator.TryPartition(plan.AcceptedComputedProjections, requested,
                SourceComputedProjectionCapabilities.AllPortable, out var partition, out var diagnostic))
            throw new InvalidOperationException("Computed row shape differs from the accepted source plan: " + diagnostic);
        if (partition.Residual.Count != 0)
            throw new InvalidOperationException("Computed row shape is missing an accepted source value.");
    }

    public static SourceComputedProjectionPartition Partition(SourcePlanRequest request,
        Func<string, Type?> columnType)
    {
        var accepted = request.RequestedComputedProjections.Where(projection =>
        {
            if (!SourceComputedProjectionFacts.CanProviderEvaluate(projection, SourceComputedProjectionCapabilities.AllPortable))
                return false;
            try
            {
                _ = StructuredScalarExpression.Bind(projection.Expression, column =>
                {
                    if (columnType(column.Column.Name) != column.ResultType)
                        throw new NotSupportedException("Computed column type does not match the source contract.");
                    return Expression.Default(column.ResultType);
                });
                return true;
            }
            catch (Exception error) when (error is ArgumentException or InvalidOperationException or NotSupportedException)
            {
                return false;
            }
        }).ToArray();
        if (!SourceComputedProjectionNegotiator.TryPartition(request.RequestedComputedProjections, accepted,
                SourceComputedProjectionCapabilities.AllPortable, out var partition, out var diagnostic))
            throw new ArgumentException(diagnostic, nameof(request));
        return partition;
    }
}

internal delegate TValue StructuredComputedRead<TReader, TValue>(ref TReader reader)
    where TReader : IQuerySourceFieldReader, allows ref struct;

internal sealed class StructuredComputedPlan<TReader>
    where TReader : IQuerySourceFieldReader, allows ref struct
{
    private readonly StructuredComputedLayout _layout;
    private readonly Delegate?[] _readers;

    public StructuredComputedPlan(StructuredComputedLayout layout)
    {
        _layout = layout;
        _readers = new Delegate?[layout.Output.Fields.Count];
        foreach (var field in layout.Output.Fields)
            if (field.ComputedProjection is { } projection)
                _readers[field.Slot] = (Delegate)typeof(StructuredComputedPlan<TReader>)
                    .GetMethod(nameof(Compile), BindingFlags.Instance | BindingFlags.NonPublic)!
                    .MakeGenericMethod(field.FieldType).Invoke(this, [projection.Expression])!;
    }

    public T Read<T>(ref TReader reader, int slot) => _readers[slot] is { } computed
        ? ((StructuredComputedRead<TReader, T>)computed)(ref reader)
        : reader.Read<T>(_layout.OutputSlots[slot]);

    private Delegate Compile<T>(SourceScalarExpression expression)
    {
        var reader = Expression.Parameter(typeof(TReader).MakeByRefType(), "reader");
        var body = StructuredScalarExpression.Bind(expression, column =>
        {
            var field = _layout.Input.Fields.Single(field => field.Name == column.Column.Name);
            if (field.FieldType != column.ResultType)
                throw new InvalidOperationException("Computed projection column type changed after planning.");
            return Expression.Call(typeof(StructuredComputedPlan<TReader>)
                .GetMethod(nameof(ReadInput), BindingFlags.Static | BindingFlags.NonPublic)!
                .MakeGenericMethod(column.ResultType), reader, Expression.Constant(field.Slot));
        });
        return Expression.Lambda<StructuredComputedRead<TReader, T>>(body, reader).Compile();
    }

    private static T ReadInput<T>(ref TReader reader, int slot) => reader.Read<T>(slot);
}

internal ref struct StructuredComputedReader<TReader> : IQuerySourceFieldReader
    where TReader : IQuerySourceFieldReader, allows ref struct
{
    private TReader _reader;
    private readonly StructuredComputedPlan<TReader> _plan;
    public StructuredComputedReader(TReader reader, StructuredComputedPlan<TReader> plan)
    {
        _reader = reader;
        _plan = plan;
    }
    public T Read<T>(int slot) => _plan.Read<T>(ref _reader, slot);
}

internal static class StructuredScalarExpression
{
    public static Expression Bind(SourceScalarExpression expression, Func<SourceScalarColumn, Expression> column)
    {
        Expression Child(SourceScalarExpression child) => Bind(child, column);
        var result = expression switch
        {
            SourceScalarLiteral literal => Literal(literal),
            SourceScalarColumn input => column(input),
            SourceScalarUnary unary => Unary(unary, Child(unary.Operand)),
            SourceScalarBinary binary => Binary(binary, Child(binary.Left), Child(binary.Right)),
            SourceScalarCast cast => Cast(Child(cast.Operand), cast.ResultType),
            SourceScalarNullCheck check => NullCheck(Child(check.Operand), check.Negated),
            SourceScalarCoalesce coalesce => Coalesce(coalesce, Child),
            _ => throw new NotSupportedException("Unsupported computed expression.")
        };
        if (result.Type != expression.ResultType)
            throw new NotSupportedException("Computed result type does not match the portable contract.");
        return result;
    }

    private static Type Underlying(Type type) => System.Nullable.GetUnderlyingType(type) ?? type;
    private static bool Nullable(Type type) => !type.IsValueType || System.Nullable.GetUnderlyingType(type) is not null;
    private static bool Numeric(Type type) => Type.GetTypeCode(Underlying(type)) is >= TypeCode.SByte and <= TypeCode.Decimal
        && Underlying(type) != typeof(char);

    private static Expression Literal(SourceScalarLiteral literal)
    {
        if (literal.Value is null) return Expression.Constant(null, literal.ResultType);
        var value = Expression.Constant(literal.Value);
        return value.Type == literal.ResultType ? value : Expression.Convert(value, literal.ResultType);
    }

    private static Expression Cast(Expression value, Type type)
    {
        if (value.Type == type) return value;
        if (!Numeric(value.Type) || !Numeric(type))
            throw new NotSupportedException("Only exact and numeric portable casts are supported.");
        return Expression.ConvertChecked(value, type);
    }

    private static Expression Unary(SourceScalarUnary unary, Expression operand) => unary.Operator switch
    {
        SourceScalarUnaryOperator.Positive => Expression.UnaryPlus(operand),
        SourceScalarUnaryOperator.Negate => Expression.NegateChecked(operand),
        SourceScalarUnaryOperator.Not when Underlying(operand.Type) == typeof(bool) => Expression.Not(operand),
        SourceScalarUnaryOperator.BitwiseNot => Expression.OnesComplement(operand),
        _ => throw new NotSupportedException()
    };

    private static Expression NullCheck(Expression operand, bool negated)
    {
        if (!Nullable(operand.Type))
            // Preserve operand evaluation even when its type cannot be null.
            return Expression.Block(operand, Expression.Constant(negated));
        return negated ? Expression.NotEqual(operand, Expression.Constant(null, operand.Type))
            : Expression.Equal(operand, Expression.Constant(null, operand.Type));
    }

    private static Expression Coalesce(SourceScalarCoalesce coalesce, Func<SourceScalarExpression, Expression> child)
    {
        Expression result = Cast(child(coalesce.Expressions[^1]), coalesce.ResultType);
        for (var index = coalesce.Expressions.Count - 2; index >= 0; index--)
        {
            var operand = child(coalesce.Expressions[index]);
            if (!Nullable(operand.Type)) result = Cast(operand, coalesce.ResultType);
            else
            {
                var target = coalesce.ResultType;
                if (target.IsValueType && !Nullable(target)) target = typeof(Nullable<>).MakeGenericType(target);
                operand = Cast(operand, target);
                result = Expression.Coalesce(operand, result);
            }
        }
        return result;
    }

    private static Expression Binary(SourceScalarBinary binary, Expression left, Expression right)
    {
        var operation = binary.Operator;
        var comparison = operation is >= SourceScalarBinaryOperator.Equal and <= SourceScalarBinaryOperator.GreaterThanOrEqual;
        if (operation is SourceScalarBinaryOperator.And or SourceScalarBinaryOperator.Or)
        {
            if (Underlying(left.Type) != typeof(bool) || Underlying(right.Type) != typeof(bool)) throw new NotSupportedException();
            var type = binary.ResultType;
            left = Expression.Convert(left, type);
            right = Expression.Convert(right, type);
            return operation == SourceScalarBinaryOperator.And ? Expression.AndAlso(left, right) : Expression.OrElse(left, right);
        }
        if (!Numeric(left.Type) || !Numeric(right.Type)) throw new NotSupportedException();
        var target = comparison ? CommonNumeric(left.Type, right.Type) : binary.ResultType;
        if (comparison && (Nullable(left.Type) || Nullable(right.Type))) target = typeof(Nullable<>).MakeGenericType(Underlying(target));
        left = Expression.Convert(left, target);
        right = Expression.Convert(right, target);
        var kind = operation switch
        {
            SourceScalarBinaryOperator.Add => ExpressionType.AddChecked,
            SourceScalarBinaryOperator.Subtract => ExpressionType.SubtractChecked,
            SourceScalarBinaryOperator.Multiply => ExpressionType.MultiplyChecked,
            SourceScalarBinaryOperator.Divide => ExpressionType.Divide,
            SourceScalarBinaryOperator.Modulo => ExpressionType.Modulo,
            SourceScalarBinaryOperator.Equal => ExpressionType.Equal,
            SourceScalarBinaryOperator.NotEqual => ExpressionType.NotEqual,
            SourceScalarBinaryOperator.LessThan => ExpressionType.LessThan,
            SourceScalarBinaryOperator.LessThanOrEqual => ExpressionType.LessThanOrEqual,
            SourceScalarBinaryOperator.GreaterThan => ExpressionType.GreaterThan,
            SourceScalarBinaryOperator.GreaterThanOrEqual => ExpressionType.GreaterThanOrEqual,
            _ => throw new NotSupportedException()
        };
        var lifted = comparison && binary.UsesSqlNullSemantics && Nullable(target);
        Expression result = Expression.MakeBinary(kind, left, right, lifted, null);
        return result.Type == binary.ResultType ? result : Expression.Convert(result, binary.ResultType);
    }

    private static Type CommonNumeric(Type left, Type right)
    {
        left = Underlying(left); right = Underlying(right);
        if ((left == typeof(decimal) && (right == typeof(float) || right == typeof(double))) ||
            (right == typeof(decimal) && (left == typeof(float) || left == typeof(double))))
            throw new NotSupportedException("Decimal and floating-point operands need an explicit Core conversion.");
        if ((left == typeof(ulong) && IsSigned(right)) || (right == typeof(ulong) && IsSigned(left)))
            throw new NotSupportedException("Mixed signed and UInt64 operands need an explicit Core conversion.");
        foreach (var type in new[] { typeof(decimal), typeof(double), typeof(float), typeof(ulong), typeof(long) })
            if (left == type || right == type) return type;
        if (left == typeof(uint) || right == typeof(uint))
            return left == typeof(uint) && right == typeof(uint) ? typeof(uint) : typeof(long);
        return typeof(int);
    }

    private static bool IsSigned(Type type) => type == typeof(sbyte) || type == typeof(short) ||
        type == typeof(int) || type == typeof(long);
}
