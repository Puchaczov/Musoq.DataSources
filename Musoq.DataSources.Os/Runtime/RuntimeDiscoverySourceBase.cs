using System.Collections.Generic;
using System.Linq;
using Musoq.DataSources.Common;
using Musoq.Schema.DataSources;
using Musoq.Schema.Optimization;

namespace Musoq.DataSources.Os.Runtime;

internal abstract class RuntimeDiscoverySourceBase<TEntity>(
    SourceExecutionContext executionContext,
    string dataSourceName) : RowSourceBase<TEntity>
{
    private const int ChunkSize = 100;

    protected override void CollectChunks(IChunkWriter<TEntity> writer)
    {
        var progress = new DataSourceProgressReporter(executionContext, dataSourceName);
        progress.Begin();
        long totalRowsProcessed = 0;

        try
        {
            var rows = GetRows().ToArray();
            progress.RowsKnown(rows.Length);

            var chunk = new List<TEntity>(ChunkSize);
            foreach (var row in rows)
            {
                writer.CancellationToken.ThrowIfCancellationRequested();
                progress.RowRead();

                chunk.Add(row);
                totalRowsProcessed++;

                if (chunk.Count < ChunkSize)
                    continue;

                writer.Write(chunk);
                chunk = [];
            }

            if (chunk.Count > 0)
                writer.Write(chunk);
        }
        finally
        {
            progress.End(totalRowsProcessed);
        }
    }

    protected abstract IEnumerable<TEntity> GetRows();
}
