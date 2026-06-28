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
        var plan = executionContext.Plan;
        if (plan.AcceptedPredicate is null && !plan.AcceptedSkip.HasValue && !plan.AcceptedTake.HasValue)
            executionContext.ReportDataSourceRowsKnown(RangeSourceName, totalRows);
        long totalRowsProcessed = 0;
        var chunk = new List<RangeItemEntity>();
        long skipped = 0;
        long emitted = 0;

        try
        {
            for (var i = min; i < max; ++i)
            {
                writer.CancellationToken.ThrowIfCancellationRequested();
                var entity = new RangeItemEntity { Value = i };

                if (!SystemSourcePlanner.Matches(plan.AcceptedPredicate, entity))
                    continue;

                if (plan.AcceptedSkip.HasValue && skipped < plan.AcceptedSkip.Value)
                {
                    skipped++;
                    continue;
                }

                if (plan.AcceptedTake.HasValue && emitted >= plan.AcceptedTake.Value)
                    break;

                chunk.Add(entity);
                emitted++;
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
