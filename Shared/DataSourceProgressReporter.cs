using System;
using Musoq.Schema.DataSources;
using Musoq.Schema.Optimization;

namespace Musoq.DataSources.Common;

internal sealed class DataSourceProgressReporter
{
    private readonly SourceExecutionContext _executionContext;
    private readonly string _dataSourceName;
    private long _rowsReadReportInterval;
    private long? _totalRows;
    private long _rowsRead;
    private long _lastReportedRowsRead;

    public DataSourceProgressReporter(
        SourceExecutionContext executionContext,
        string dataSourceName,
        int rowsReadReportInterval = RowChunking.DefaultChunkSize)
    {
        _executionContext = executionContext;
        _dataSourceName = dataSourceName;
        _rowsReadReportInterval = Math.Max(1, rowsReadReportInterval);
    }

    public void SetRowsReadReportInterval(int rowsReadReportInterval)
    {
        _rowsReadReportInterval = Math.Max(1, rowsReadReportInterval);
    }

    public void Begin()
    {
        _executionContext.ReportDataSourceBegin(_dataSourceName);
    }

    public void RowsKnown(long totalRows)
    {
        _totalRows = totalRows;
        _executionContext.ReportDataSourceRowsKnown(_dataSourceName, totalRows);
    }

    public void RowRead()
    {
        RowsRead(1);
    }

    public void RowsRead(long count)
    {
        if (count <= 0)
            return;

        _rowsRead += count;

        if (_rowsRead - _lastReportedRowsRead >= _rowsReadReportInterval)
            ReportRowsRead();
    }

    public void FlushRowsRead()
    {
        if (_rowsRead > _lastReportedRowsRead)
            ReportRowsRead();
    }

    public void End(long? totalRowsProcessed)
    {
        FlushRowsRead();
        _executionContext.ReportDataSourceEnd(_dataSourceName, totalRowsProcessed);
    }

    private void ReportRowsRead()
    {
        _executionContext.ReportDataSourceRowsRead(_dataSourceName, _rowsRead, _totalRows);
        _lastReportedRowsRead = _rowsRead;
    }
}
