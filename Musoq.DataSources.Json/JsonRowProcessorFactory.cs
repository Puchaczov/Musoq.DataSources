using System;
using System.Threading;
using Musoq.DataSources.Common;
using Musoq.DataSources.Structured;
using Musoq.Schema;
using Musoq.Schema.DataSources;
using Musoq.Schema.Optimization;

namespace Musoq.DataSources.Json;

internal interface IJsonRowProcessorFactory<TRow, out TProcessor>
    where TProcessor : IJsonRecordProcessor
{
    static abstract TRow EmptyRow { get; }

    static abstract TProcessor Create(StructuredSchemaSnapshot snapshot, SourceExecutionContext context,
        QueryRowShape shape, IChunkWriter<TRow> writer, DataSourceProgressReporter progress,
        CancellationToken cancellationToken);
}

internal readonly struct TypedJsonRowProcessorFactory<TRow, TMaterializer>
    : IJsonRowProcessorFactory<TRow, TypedJsonRecordProcessor<TRow, TMaterializer>>
    where TMaterializer : struct, IQueryRowMaterializer<TRow>
{
    public static TRow EmptyRow => EmptyMaterialization.Row;

    private static class EmptyMaterialization
    {
        internal static readonly TRow Row = Create();
        private static TRow Create()
        {
            var reader = new EmptyReader();
            return TMaterializer.Materialize(ref reader);
        }
    }

    private readonly struct EmptyReader : IQuerySourceFieldReader
    {
        public T Read<T>(int slot) => throw new InvalidOperationException("An empty projection cannot read a field.");
    }

    public static TypedJsonRecordProcessor<TRow, TMaterializer> Create(StructuredSchemaSnapshot snapshot,
        SourceExecutionContext context, QueryRowShape shape, IChunkWriter<TRow> writer,
        DataSourceProgressReporter progress, CancellationToken cancellationToken)
        => new(new JsonQueryRowProcessor<TRow, TMaterializer>(snapshot, context, shape, writer, progress, cancellationToken));
}
