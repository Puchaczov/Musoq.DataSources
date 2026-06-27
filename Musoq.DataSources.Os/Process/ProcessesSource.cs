using System.Collections.Generic;
using Musoq.Schema.DataSources;
using Musoq.Schema.Optimization;

namespace Musoq.DataSources.Os.Process;

internal class ProcessesSource(SourceExecutionContext executionContext) : RowSourceBase<System.Diagnostics.Process>
{
    private const string ProcessesSourceName = "processes";
    private const int ChunkSize = 20;

    protected override void CollectChunks(IChunkWriter<System.Diagnostics.Process> writer)
    {
        executionContext.ReportDataSourceBegin(ProcessesSourceName);
        long totalRowsProcessed = 0;

        try
        {
            var chunk = new List<System.Diagnostics.Process>(ChunkSize);

            foreach (var process in System.Diagnostics.Process.GetProcesses())
            {
                writer.CancellationToken.ThrowIfCancellationRequested();

                chunk.Add(process);
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
            executionContext.ReportDataSourceEnd(ProcessesSourceName, totalRowsProcessed);
        }
    }
}
