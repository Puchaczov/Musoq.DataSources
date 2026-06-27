using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using Musoq.Schema.DataSources;
using Musoq.Schema.Optimization;

namespace Musoq.DataSources.Os.Zip;

internal class ZipSource(string zipPath, SourceExecutionContext executionContext) : RowSourceBase<ZipArchiveEntry>
{
    private const string ZipSourceName = "zip";
    private const int ChunkSize = 100;

    protected override void CollectChunks(IChunkWriter<ZipArchiveEntry> writer)
    {
        executionContext.ReportDataSourceBegin(ZipSourceName);
        long totalRowsProcessed = 0;

        try
        {
            using var file = File.OpenRead(zipPath);
            using var zip = new ZipArchive(file);
            var chunk = new List<ZipArchiveEntry>(ChunkSize);

            executionContext.ReportDataSourceRowsKnown(ZipSourceName, zip.Entries.Count);

            foreach (var entry in zip.Entries)
            {
                writer.CancellationToken.ThrowIfCancellationRequested();

                if (entry.Name == string.Empty)
                    continue;

                chunk.Add(entry);
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
            executionContext.ReportDataSourceEnd(ZipSourceName, totalRowsProcessed);
        }
    }
}
