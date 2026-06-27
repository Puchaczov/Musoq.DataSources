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

            using var file = File.OpenRead(filePath);
            using var reader = new StreamReader(file);

            while (!reader.EndOfStream)
            {
                writer.CancellationToken.ThrowIfCancellationRequested();

                chunk.Add(new FlatFileEntity
                {
                    Line = reader.ReadLine(),
                    LineNumber = ++rowNum
                });

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
