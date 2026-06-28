using System.Collections.Generic;
using System.IO;
using Musoq.Schema.DataSources;
using Musoq.Schema.Optimization;
using SharpCompress.Readers;

namespace Musoq.DataSources.Archives;

internal class ArchivesRowSource(string path, SourceExecutionContext executionContext) : RowSourceBase<EntryWrapper>
{
    private const string ArchivesSourceName = "archives";

    protected override void CollectChunks(IChunkWriter<EntryWrapper> writer)
    {
        executionContext.ReportDataSourceBegin(ArchivesSourceName);
        long totalRowsProcessed = 0;

        try
        {
            using var stream = File.OpenRead(path);
            using var reader = ReaderFactory.Open(stream, new ReaderOptions
            {
                LeaveStreamOpen = true
            });

            var index = 0;
            var chunk = new List<EntryWrapper>();
            var acceptedPredicate = executionContext.Plan.AcceptedPredicate;

            while (reader.MoveToNextEntry())
            {
                writer.CancellationToken.ThrowIfCancellationRequested();
                var entry = new EntryWrapper(reader.Entry, path, index++);

                if (!ArchivesSourcePlanner.Matches(acceptedPredicate, entry))
                    continue;

                chunk.Add(entry);
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
            executionContext.ReportDataSourceEnd(ArchivesSourceName, totalRowsProcessed);
        }
    }
}
