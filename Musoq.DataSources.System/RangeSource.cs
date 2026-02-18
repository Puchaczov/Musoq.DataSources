using System.Collections.Generic;
using Musoq.Schema;
using Musoq.Schema.DataSources;

namespace Musoq.DataSources.System;

internal class RangeSource : RowSource
{
    private const string RangeSourceName = "range";
    private readonly long _max;
    private readonly long _min;
    private readonly RuntimeContext _runtimeContext;

    public RangeSource(long min, long max, RuntimeContext runtimeContext)
    {
        _min = min;
        _max = max;
        _runtimeContext = runtimeContext;
    }

    public override IEnumerable<IObjectResolver> Rows
    {
        get
        {
            _runtimeContext.ReportDataSourceBegin(RangeSourceName);
            var totalRows = _max - _min;
            _runtimeContext.ReportDataSourceRowsKnown(RangeSourceName, totalRows);
            long totalRowsProcessed = 0;

            try
            {
                for (var i = _min; i < _max; ++i)
                {
                    totalRowsProcessed++;
                    yield return new EntityResolver<RangeItemEntity>(new RangeItemEntity { Value = i },
                        RangeHelper.RangeToIndexMap, RangeHelper.RangeToMethodAccessMap);
                }
            }
            finally
            {
                _runtimeContext.ReportDataSourceEnd(RangeSourceName, totalRowsProcessed);
            }
        }
    }
}