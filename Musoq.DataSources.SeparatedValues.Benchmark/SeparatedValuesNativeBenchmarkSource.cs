#nullable enable

using Musoq.Schema;
using Musoq.Schema.DataSources;
using Musoq.Schema.Optimization;

namespace Musoq.DataSources.SeparatedValues.Benchmark;

internal readonly record struct NativeBenchmarkRow0;

internal readonly record struct NativeBenchmarkRow1<T0>(T0 Item0);

internal readonly record struct NativeBenchmarkRow2<T0, T1>(T0 Item0, T1 Item1);

internal static class SeparatedValuesNativeBenchmarkSource
{
    public static RowSource<NativeBenchmarkRow0> Create(
        string path,
        string separator,
        bool hasHeader,
        SourceExecutionContext context,
        ISeparatedValuesQueryScanPipeline? pipeline = null)
    {
        return Create<NativeBenchmarkRow0, Row0Materializer>(
            path, separator, hasHeader, context, new QueryRowShape([]), pipeline);
    }

    public static RowSource<NativeBenchmarkRow1<T0>> Create<T0>(
        string path,
        string separator,
        bool hasHeader,
        SourceExecutionContext context,
        ISeparatedValuesQueryScanPipeline? pipeline = null)
    {
        return Create<NativeBenchmarkRow1<T0>, Row1Materializer<T0>>(
            path, separator, hasHeader, context, CreateShape(context, typeof(T0)), pipeline);
    }

    public static RowSource<NativeBenchmarkRow2<T0, T1>> Create<T0, T1>(
        string path,
        string separator,
        bool hasHeader,
        SourceExecutionContext context,
        ISeparatedValuesQueryScanPipeline? pipeline = null)
    {
        return Create<NativeBenchmarkRow2<T0, T1>, Row2Materializer<T0, T1>>(
            path, separator, hasHeader, context, CreateShape(context, typeof(T0), typeof(T1)), pipeline);
    }

    private static RowSource<TRow> Create<TRow, TMaterializer>(
        string path,
        string separator,
        bool hasHeader,
        SourceExecutionContext context,
        QueryRowShape shape,
        ISeparatedValuesQueryScanPipeline? pipeline)
        where TMaterializer : struct, IQueryRowMaterializer<TRow>
    {
        return new SeparatedValuesQueryRowSource<TRow, TMaterializer>(
            path,
            separator,
            hasHeader,
            0,
            new QueryScopedRowSourceRequest(context, shape),
            pipeline ?? new SeparatedValuesScanPipeline());
    }

    private static QueryRowShape CreateShape(SourceExecutionContext context, params Type[] fieldTypes)
    {
        var columns = context.AllColumns.OrderBy(static column => column.ColumnIndex).ToArray();
        if (columns.Length != fieldTypes.Length)
            throw new InvalidOperationException("Benchmark carrier width does not match execution metadata.");

        return new QueryRowShape(columns
            .Select((column, slot) => new QueryRowField(
                slot,
                column.ColumnIndex,
                column.ColumnName,
                fieldTypes[slot],
                !fieldTypes[slot].IsValueType || Nullable.GetUnderlyingType(fieldTypes[slot]) is not null,
                column.ReadModifiers))
            .ToArray());
    }

    private readonly struct Row0Materializer : IQueryRowMaterializer<NativeBenchmarkRow0>
    {
        public static NativeBenchmarkRow0 Materialize<TReader>(scoped ref TReader reader)
            where TReader : IQuerySourceFieldReader, allows ref struct => new();
    }

    private readonly struct Row1Materializer<T0> : IQueryRowMaterializer<NativeBenchmarkRow1<T0>>
    {
        public static NativeBenchmarkRow1<T0> Materialize<TReader>(scoped ref TReader reader)
            where TReader : IQuerySourceFieldReader, allows ref struct => new(reader.Read<T0>(0));
    }

    private readonly struct Row2Materializer<T0, T1> : IQueryRowMaterializer<NativeBenchmarkRow2<T0, T1>>
    {
        public static NativeBenchmarkRow2<T0, T1> Materialize<TReader>(scoped ref TReader reader)
            where TReader : IQuerySourceFieldReader, allows ref struct =>
            new(reader.Read<T0>(0), reader.Read<T1>(1));
    }
}
