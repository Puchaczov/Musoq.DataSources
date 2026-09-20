using System.Collections.Generic;
using Musoq.DataSources.Common;
using Musoq.Schema.DataSources;
using Musoq.Schema.Optimization;

namespace Musoq.DataSources.Os.Process;

internal class ProcessesSource(SourceExecutionContext executionContext) : RowSourceBase<ProcessEntity>
{
    private const string ProcessesSourceName = "processes";
    private const int ChunkSize = 20;

    protected override void CollectChunks(IChunkWriter<ProcessEntity> writer)
    {
        var progress = new DataSourceProgressReporter(executionContext, ProcessesSourceName);
        progress.Begin();
        long totalRowsProcessed = 0;

        try
        {
            var chunk = new List<ProcessEntity>(ChunkSize);
            var processes = System.Diagnostics.Process.GetProcesses();
            progress.RowsKnown(processes.Length);

            foreach (var process in processes)
            {
                writer.CancellationToken.ThrowIfCancellationRequested();
                progress.RowRead();

                chunk.Add(new ProcessEntity(process));
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
}
