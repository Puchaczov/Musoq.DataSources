using Musoq.Schema;
using Musoq.Schema.DataSources;

namespace Musoq.DataSources.Json;

internal sealed class JsonQueryRowSource<TRow, TMaterializer> : RowSourceBase<TRow>
    where TMaterializer : struct, IQueryRowMaterializer<TRow>
{
    private readonly JsonScanPipeline<TRow, TypedJsonRecordProcessor<TRow, TMaterializer>,
        TypedJsonRowProcessorFactory<TRow, TMaterializer>> _pipeline;

    public JsonQueryRowSource(string path, QueryScopedRowSourceRequest request)
    {
        _pipeline = new(path, request.ExecutionContext, request.Shape);
    }

    protected override void CollectChunks(IChunkWriter<TRow> writer) => _pipeline.Execute(writer);
}
