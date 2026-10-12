using Musoq.Schema;
using Musoq.Schema.DataSources;
using Musoq.Schema.Optimization;

namespace Musoq.DataSources.Tests.Common;

/// <summary>Infers concrete argument carriers for direct provider fixtures and benchmarks.</summary>
public static class NativeSourceCalls
{
    public static SourceCall<TArguments> Source<TArguments>(ISchema schema, TArguments arguments)
        where TArguments : struct, ISourceArguments => new(schema, arguments);

    public static QueryCall<TArguments> Query<TArguments>(IQueryScopedRowSourceSchema schema, TArguments arguments)
        where TArguments : struct, ISourceArguments => new(schema, arguments);

    public readonly struct SourceCall<TArguments>(ISchema schema, TArguments arguments)
        where TArguments : struct, ISourceArguments
    {
        public RowSource<T> Read<T>(string name, SourceExecutionContext context)
            => schema.GetRowSource<T, TArguments>(name, context, arguments);
    }

    public readonly struct QueryCall<TArguments>(IQueryScopedRowSourceSchema schema, TArguments arguments)
        where TArguments : struct, ISourceArguments
    {
        public RowSource<TRow> Read<TRow, TMaterializer>(string name, QueryScopedRowSourceRequest request)
            where TMaterializer : struct, IQueryRowMaterializer<TRow>
            => schema.GetQueryScopedRowSource<TRow, TMaterializer, TArguments>(name, request, arguments);
    }
}
