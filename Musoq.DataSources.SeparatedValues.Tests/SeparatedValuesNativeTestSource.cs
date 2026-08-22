#nullable enable

using System;
using System.Linq;
using Musoq.Schema;
using Musoq.Schema.DataSources;
using Musoq.Schema.Optimization;

namespace Musoq.DataSources.SeparatedValues.Tests;

internal readonly record struct TestRow0;

internal readonly record struct TestRow1<T0>(T0 Item0)
{
    public object? this[int index] => index == 0
        ? Item0
        : throw new ArgumentOutOfRangeException(nameof(index));
}

internal readonly record struct TestRow2<T0, T1>(T0 Item0, T1 Item1)
{
    public object? this[int index] => index switch
    {
        0 => Item0,
        1 => Item1,
        _ => throw new ArgumentOutOfRangeException(nameof(index))
    };
}

internal readonly record struct TestRow3<T0, T1, T2>(T0 Item0, T1 Item1, T2 Item2)
{
    public object? this[int index] => index switch
    {
        0 => Item0,
        1 => Item1,
        2 => Item2,
        _ => throw new ArgumentOutOfRangeException(nameof(index))
    };
}

internal readonly record struct TestRow4<T0, T1, T2, T3>(T0 Item0, T1 Item1, T2 Item2, T3 Item3);

internal readonly record struct TestRow5<T0, T1, T2, T3, T4>(T0 Item0, T1 Item1, T2 Item2, T3 Item3, T4 Item4);

internal static class SeparatedValuesNativeTestSource
{
    public static RowSource<TestRow0> Create(
        string path,
        string separator,
        bool hasHeader,
        int skipLines,
        SourceExecutionContext context,
        ISeparatedValuesQueryScanPipeline? pipeline = null,
        SeparatedValuesDialect? dialect = null)
    {
        return Create<TestRow0, TestRow0Materializer>(
            path,
            separator,
            hasHeader,
            skipLines,
            context,
            new QueryRowShape([]),
            pipeline,
            dialect);
    }

    public static RowSource<TestRow1<T0>> Create<T0>(
        string path,
        string separator,
        bool hasHeader,
        int skipLines,
        SourceExecutionContext context,
        ISeparatedValuesQueryScanPipeline? pipeline = null,
        SeparatedValuesDialect? dialect = null)
    {
        return Create<TestRow1<T0>, TestRow1Materializer<T0>>(
            path,
            separator,
            hasHeader,
            skipLines,
            context,
            CreateShape(context, typeof(T0)),
            pipeline,
            dialect);
    }

    public static RowSource<TestRow2<T0, T1>> Create<T0, T1>(
        string path,
        string separator,
        bool hasHeader,
        int skipLines,
        SourceExecutionContext context,
        ISeparatedValuesQueryScanPipeline? pipeline = null,
        SeparatedValuesDialect? dialect = null)
    {
        return Create<TestRow2<T0, T1>, TestRow2Materializer<T0, T1>>(
            path,
            separator,
            hasHeader,
            skipLines,
            context,
            CreateShape(context, typeof(T0), typeof(T1)),
            pipeline,
            dialect);
    }

    public static RowSource<TestRow3<T0, T1, T2>> Create<T0, T1, T2>(
        string path,
        string separator,
        bool hasHeader,
        int skipLines,
        SourceExecutionContext context,
        ISeparatedValuesQueryScanPipeline? pipeline = null,
        SeparatedValuesDialect? dialect = null)
    {
        return Create<TestRow3<T0, T1, T2>, TestRow3Materializer<T0, T1, T2>>(
            path,
            separator,
            hasHeader,
            skipLines,
            context,
            CreateShape(context, typeof(T0), typeof(T1), typeof(T2)),
            pipeline,
            dialect);
    }

    public static RowSource<TestRow4<T0, T1, T2, T3>> Create<T0, T1, T2, T3>(
        string path,
        string separator,
        bool hasHeader,
        int skipLines,
        SourceExecutionContext context,
        ISeparatedValuesQueryScanPipeline? pipeline = null,
        SeparatedValuesDialect? dialect = null)
    {
        return Create<TestRow4<T0, T1, T2, T3>, TestRow4Materializer<T0, T1, T2, T3>>(
            path,
            separator,
            hasHeader,
            skipLines,
            context,
            CreateShape(context, typeof(T0), typeof(T1), typeof(T2), typeof(T3)),
            pipeline,
            dialect);
    }

    public static RowSource<TestRow5<T0, T1, T2, T3, T4>> Create<T0, T1, T2, T3, T4>(
        string path,
        string separator,
        bool hasHeader,
        int skipLines,
        SourceExecutionContext context,
        ISeparatedValuesQueryScanPipeline? pipeline = null,
        SeparatedValuesDialect? dialect = null)
    {
        return Create<TestRow5<T0, T1, T2, T3, T4>, TestRow5Materializer<T0, T1, T2, T3, T4>>(
            path,
            separator,
            hasHeader,
            skipLines,
            context,
            CreateShape(context, typeof(T0), typeof(T1), typeof(T2), typeof(T3), typeof(T4)),
            pipeline,
            dialect);
    }

    private static RowSource<TRow> Create<TRow, TMaterializer>(
        string path,
        string separator,
        bool hasHeader,
        int skipLines,
        SourceExecutionContext context,
        QueryRowShape shape,
        ISeparatedValuesQueryScanPipeline? pipeline,
        SeparatedValuesDialect? dialect)
        where TMaterializer : struct, IQueryRowMaterializer<TRow>
    {
        return new SeparatedValuesQueryRowSource<TRow, TMaterializer>(
            path,
            separator,
            hasHeader,
            skipLines,
            new QueryScopedRowSourceRequest(context, shape),
            pipeline ?? new SeparatedValuesScanPipeline(),
            dialect);
    }

    internal static QueryRowShape CreateShape(
        SourceExecutionContext context,
        params Type[] fieldTypes)
    {
        var columns = context.AllColumns
            .OrderBy(static column => column.ColumnIndex)
            .ToArray();
        if (columns.Length != fieldTypes.Length)
        {
            throw new InvalidOperationException(
                $"Native test carrier has {fieldTypes.Length} fields but execution metadata has {columns.Length} columns.");
        }

        return new QueryRowShape(columns
            .Select((column, slot) => new QueryRowField(
                slot,
                column.ColumnIndex,
                column.ColumnName,
                fieldTypes[slot],
                IsNullable(fieldTypes[slot]),
                column.ReadModifiers))
            .ToArray());
    }

    private static bool IsNullable(Type type) =>
        !type.IsValueType || Nullable.GetUnderlyingType(type) is not null;

    internal readonly struct TestRow0Materializer : IQueryRowMaterializer<TestRow0>
    {
        public static TestRow0 Materialize<TReader>(scoped ref TReader reader)
            where TReader : IQuerySourceFieldReader, allows ref struct => new();
    }

    internal readonly struct TestRow1Materializer<T0> : IQueryRowMaterializer<TestRow1<T0>>
    {
        public static TestRow1<T0> Materialize<TReader>(scoped ref TReader reader)
            where TReader : IQuerySourceFieldReader, allows ref struct => new(reader.Read<T0>(0));
    }

    internal readonly struct TestRow2Materializer<T0, T1> : IQueryRowMaterializer<TestRow2<T0, T1>>
    {
        public static TestRow2<T0, T1> Materialize<TReader>(scoped ref TReader reader)
            where TReader : IQuerySourceFieldReader, allows ref struct =>
            new(reader.Read<T0>(0), reader.Read<T1>(1));
    }

    internal readonly struct TestRow3Materializer<T0, T1, T2> : IQueryRowMaterializer<TestRow3<T0, T1, T2>>
    {
        public static TestRow3<T0, T1, T2> Materialize<TReader>(scoped ref TReader reader)
            where TReader : IQuerySourceFieldReader, allows ref struct =>
            new(reader.Read<T0>(0), reader.Read<T1>(1), reader.Read<T2>(2));
    }

    internal readonly struct TestRow4Materializer<T0, T1, T2, T3>
        : IQueryRowMaterializer<TestRow4<T0, T1, T2, T3>>
    {
        public static TestRow4<T0, T1, T2, T3> Materialize<TReader>(scoped ref TReader reader)
            where TReader : IQuerySourceFieldReader, allows ref struct =>
            new(reader.Read<T0>(0), reader.Read<T1>(1), reader.Read<T2>(2), reader.Read<T3>(3));
    }

    internal readonly struct TestRow5Materializer<T0, T1, T2, T3, T4>
        : IQueryRowMaterializer<TestRow5<T0, T1, T2, T3, T4>>
    {
        public static TestRow5<T0, T1, T2, T3, T4> Materialize<TReader>(scoped ref TReader reader)
            where TReader : IQuerySourceFieldReader, allows ref struct =>
            new(
                reader.Read<T0>(0),
                reader.Read<T1>(1),
                reader.Read<T2>(2),
                reader.Read<T3>(3),
                reader.Read<T4>(4));
    }
}
