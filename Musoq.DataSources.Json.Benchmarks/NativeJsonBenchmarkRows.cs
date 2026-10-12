using Musoq.Schema;
using Musoq.Schema.DataSources;
using Musoq.Schema.Optimization;

namespace Musoq.DataSources.Json.Benchmarks;

internal static class NativeJsonBenchmarkRows
{
    internal readonly record struct FlatRow(string Station, decimal Temperature, long Sequence);
    internal readonly record struct TextRow(string Value);
    internal readonly record struct SequenceRow(long Value);
    internal readonly struct EmptyRow;

    internal readonly struct FlatMaterializer : IQueryRowMaterializer<FlatRow>
    {
        public static FlatRow Materialize<TReader>(scoped ref TReader reader) where TReader : IQuerySourceFieldReader, allows ref struct
            => new(reader.Read<string>(0), reader.Read<decimal>(1), reader.Read<long>(2));
    }

    internal readonly struct TextMaterializer : IQueryRowMaterializer<TextRow>
    {
        public static TextRow Materialize<TReader>(scoped ref TReader reader) where TReader : IQuerySourceFieldReader, allows ref struct
            => new(reader.Read<string>(0));
    }

    internal readonly struct SequenceMaterializer : IQueryRowMaterializer<SequenceRow>
    {
        public static SequenceRow Materialize<TReader>(scoped ref TReader reader) where TReader : IQuerySourceFieldReader, allows ref struct
            => new(reader.Read<long>(0));
    }

    internal readonly struct EmptyMaterializer : IQueryRowMaterializer<EmptyRow>
    {
        public static EmptyRow Materialize<TReader>(scoped ref TReader reader) where TReader : IQuerySourceFieldReader, allows ref struct
            => default;
    }

    internal static RowSource<TRow> Open<TRow, TMaterializer>(string path, SourceExecutionContext context)
        where TMaterializer : struct, IQueryRowMaterializer<TRow>
    {
        var shape = JsonRowLayout.Create(JsonSchemaDiscovery.GetSnapshot(path), context).CreateNativeShape();
        return Musoq.DataSources.Tests.Common.NativeSourceCalls.Query(new JsonSchema(),
            SourceArguments.Create(path, SourceArguments.Empty)).Read<TRow, TMaterializer>(
            "file", new QueryScopedRowSourceRequest(context, shape));
    }
}
