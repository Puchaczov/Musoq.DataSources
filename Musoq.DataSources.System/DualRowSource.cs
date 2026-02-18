using System.Collections.Concurrent;
using System.Collections.Generic;
using Musoq.Schema;
using Musoq.Schema.DataSources;

namespace Musoq.DataSources.System;

internal class DualRowSource : RowSourceBase<DualEntity>
{
    private const string DualSourceName = "dual";
    private readonly RuntimeContext _runtimeContext;

    public DualRowSource(RuntimeContext runtimeContext)
    {
        _runtimeContext = runtimeContext;
    }

    protected override void CollectChunks(BlockingCollection<IReadOnlyList<IObjectResolver>> chunkedSource)
    {
        _runtimeContext.ReportDataSourceBegin(DualSourceName);
        _runtimeContext.ReportDataSourceRowsKnown(DualSourceName, 1);

        try
        {
            chunkedSource.Add(
            [
                new EntityResolver<DualEntity>(new DualEntity(), SystemSchemaHelper.FlatNameToIndexMap,
                    SystemSchemaHelper.FlatIndexToMethodAccessMap)
            ]);
        }
        finally
        {
            _runtimeContext.ReportDataSourceEnd(DualSourceName, 1);
        }
    }
}