using System.Collections.Generic;
using System.IO;
using Musoq.Schema.DataSources;
using Musoq.Schema.Optimization;

namespace Musoq.DataSources.FlatFile;

internal class FlatFileSource(string filePath, SourceExecutionContext executionContext)
    : RowSourceBase<FlatFileEntity>
{
    private const string FlatFileSourceName = "flatfile";

    protected override void CollectChunks(IChunkWriter<FlatFileEntity> writer)
    {
        executionContext.ReportDataSourceBegin(FlatFileSourceName);
        long totalRowsProcessed = 0;

        try
        {
            if (executionContext.EndWorkToken.IsCancellationRequested)
                return;

            if (!File.Exists(filePath))
                return;

            var rowNum = 0;
            var chunk = new List<FlatFileEntity>();
            var plan = executionContext.Plan;
            long skipped = 0;
            long emitted = 0;

            using var file = File.OpenRead(filePath);
            using var reader = new StreamReader(file);

            while (!reader.EndOfStream)
            {
                writer.CancellationToken.ThrowIfCancellationRequested();

                var entity = new FlatFileEntity
                {
                    Line = reader.ReadLine(),
                    LineNumber = ++rowNum
                };

                if (!FlatFileSourcePlanner.Matches(plan.AcceptedPredicate, entity))
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
            executionContext.ReportDataSourceEnd(FlatFileSourceName, totalRowsProcessed);
        }
    }
}
