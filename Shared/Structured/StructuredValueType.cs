#nullable enable

using System;

namespace Musoq.DataSources.Structured;

internal enum StructuredValueKind : byte
{
    Unknown,
    Null,
    Boolean,
    Long,
    Decimal,
    Double,
    String,
    Object
}

internal enum StructuredTypeConflictBehavior : byte
{
    WidenToString,
    WidenToObject
}

internal readonly record struct StructuredTypeState(StructuredValueKind Kind, bool IsNullable)
{
    public static StructuredTypeState Empty => new(StructuredValueKind.Unknown, false);

    public StructuredTypeState Observe(
        StructuredValueKind observedKind,
        StructuredTypeConflictBehavior conflictBehavior)
    {
        if (observedKind is StructuredValueKind.Unknown)
            return this;

        if (observedKind is StructuredValueKind.Null)
            return this with { IsNullable = true };

        if (Kind is StructuredValueKind.Unknown or StructuredValueKind.Null)
            return new StructuredTypeState(observedKind, IsNullable || Kind == StructuredValueKind.Null);

        if (Kind == observedKind)
            return this;

        if (IsNumeric(Kind) && IsNumeric(observedKind))
            return this with { Kind = WidenNumeric(Kind, observedKind) };

        return this with
        {
            Kind = conflictBehavior == StructuredTypeConflictBehavior.WidenToString
                ? StructuredValueKind.String
                : StructuredValueKind.Object
        };
    }

    public StructuredTypeState WithMissingValue()
    {
        return this with { IsNullable = true };
    }

    public Type ToClrType()
    {
        var type = Kind switch
        {
            StructuredValueKind.Boolean => typeof(bool),
            StructuredValueKind.Long => typeof(long),
            StructuredValueKind.Decimal => typeof(decimal),
            StructuredValueKind.Double => typeof(double),
            StructuredValueKind.String => typeof(string),
            StructuredValueKind.Unknown or StructuredValueKind.Null or StructuredValueKind.Object => typeof(object),
            _ => throw new ArgumentOutOfRangeException(nameof(Kind), Kind, "Unsupported structured value kind.")
        };

        return IsNullable && type.IsValueType
            ? typeof(Nullable<>).MakeGenericType(type)
            : type;
    }

    private static bool IsNumeric(StructuredValueKind kind)
    {
        return kind is StructuredValueKind.Long or StructuredValueKind.Decimal or StructuredValueKind.Double;
    }

    private static StructuredValueKind WidenNumeric(StructuredValueKind left, StructuredValueKind right)
    {
        if (left == StructuredValueKind.Double || right == StructuredValueKind.Double)
            return StructuredValueKind.Double;

        if (left == StructuredValueKind.Decimal || right == StructuredValueKind.Decimal)
            return StructuredValueKind.Decimal;

        return StructuredValueKind.Long;
    }
}
