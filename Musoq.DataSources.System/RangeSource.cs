using System.Collections.Generic;
using Musoq.Schema.DataSources;
using Musoq.Schema.Optimization;

namespace Musoq.DataSources.System;

internal class RangeSource(long min, long max, SourceExecutionContext executionContext) : RowSourceBase<RangeItemEntity>
{
    private const string RangeSourceName = "range";

    protected override void CollectChunks(IChunkWriter<RangeItemEntity> writer)
    {
        executionContext.ReportDataSourceBegin(RangeSourceName);
        var totalRows = max - min;
        executionContext.ReportDataSourceRowsKnown(RangeSourceName, totalRows);
        long totalRowsProcessed = 0;
        var chunk = new List<RangeItemEntity>();

        try
        {
            for (var i = min; i < max; ++i)
            {
                writer.CancellationToken.ThrowIfCancellationRequested();
                chunk.Add(new RangeItemEntity { Value = i });
                totalRowsProcessed++;

                if (chunk.Count < RowChunking.DefaultChunkSize)
                    continue;

                writer.Write(chunk);
                chunk = [];
            }

            if (chunk.Count > 0)
                writer.Write(chunk);
        }
        finally
        {
            executionContext.ReportDataSourceEnd(RangeSourceName, totalRowsProcessed);
        }
    }
}
