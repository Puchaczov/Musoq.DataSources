using Musoq.Schema.DataSources;
using Musoq.Schema.Optimization;

namespace Musoq.DataSources.System;

internal class DualRowSource(SourceExecutionContext executionContext) : RowSourceBase<DualEntity>
{
    private const string DualSourceName = "dual";

    protected override void CollectChunks(IChunkWriter<DualEntity> writer)
    {
        executionContext.ReportDataSourceBegin(DualSourceName);
        executionContext.ReportDataSourceRowsKnown(DualSourceName, 1);

        try
        {
            writer.Write([new DualEntity()]);
        }
        finally
        {
            executionContext.ReportDataSourceEnd(DualSourceName, 1);
        }
    }
}
