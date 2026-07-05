using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Musoq.DataSources.Common;
using Musoq.DataSources.Tests.Common;
using Musoq.Schema;

namespace Musoq.DataSources.SeparatedValues.Tests;

[TestClass]
public class DataSourceProgressReporterTests
{
    [TestMethod]
    public void RowsRead_WhenIntervalIsReached_ReportsCumulativeRows()
    {
        var capture = new DataSourceProgressCapture();
        var context = RuntimeV2TestContexts.CreateExecutionContext(dataSourceProgressCallback: capture.Handler);
        var reporter = new DataSourceProgressReporter(context, "test", 3);

        reporter.Begin();
        reporter.RowRead();
        reporter.RowRead();

        Assert.AreEqual(0, capture.For("test", DataSourcePhase.RowsRead).Count);

        reporter.RowRead();

        var rowsReadEvents = capture.For("test", DataSourcePhase.RowsRead);
        Assert.AreEqual(1, rowsReadEvents.Count);
        Assert.AreEqual(3, rowsReadEvents[0].RowsProcessed);
    }

    [TestMethod]
    public void End_WhenRowsWereRead_FlushesFinalPartialRowsRead()
    {
        var capture = new DataSourceProgressCapture();
        var context = RuntimeV2TestContexts.CreateExecutionContext(dataSourceProgressCallback: capture.Handler);
        var reporter = new DataSourceProgressReporter(context, "test", 3);

        reporter.Begin();
        reporter.RowRead();
        reporter.RowRead();
        reporter.RowRead();
        reporter.RowRead();
        reporter.RowRead();
        reporter.End(2);

        var rowsReadEvents = capture.For("test", DataSourcePhase.RowsRead);
        Assert.AreEqual(2, rowsReadEvents.Count);
        Assert.AreEqual(3, rowsReadEvents[0].RowsProcessed);
        Assert.AreEqual(5, rowsReadEvents[1].RowsProcessed);

        var end = capture.For("test", DataSourcePhase.End).Single();
        Assert.AreEqual(2, end.RowsProcessed);
    }

    [TestMethod]
    public void End_WhenNoRowsWereRead_DoesNotReportRowsRead()
    {
        var capture = new DataSourceProgressCapture();
        var context = RuntimeV2TestContexts.CreateExecutionContext(dataSourceProgressCallback: capture.Handler);
        var reporter = new DataSourceProgressReporter(context, "test", 3);

        reporter.Begin();
        reporter.End(0);

        Assert.AreEqual(1, capture.For("test", DataSourcePhase.Begin).Count);
        Assert.AreEqual(0, capture.For("test", DataSourcePhase.RowsRead).Count);
        Assert.AreEqual(1, capture.For("test", DataSourcePhase.End).Count);
    }

    [TestMethod]
    public void RowsKnown_WhenTotalIsKnown_IsIncludedInRowsReadEvents()
    {
        var capture = new DataSourceProgressCapture();
        var context = RuntimeV2TestContexts.CreateExecutionContext(dataSourceProgressCallback: capture.Handler);
        var reporter = new DataSourceProgressReporter(context, "test", 3);

        reporter.Begin();
        reporter.RowsKnown(10);
        reporter.RowsRead(3);

        var rowsKnown = capture.For("test", DataSourcePhase.RowsKnown).Single();
        Assert.AreEqual(10, rowsKnown.TotalRows);

        var rowsRead = capture.For("test", DataSourcePhase.RowsRead).Single();
        Assert.AreEqual(3, rowsRead.RowsProcessed);
        Assert.AreEqual(10, rowsRead.TotalRows);
    }
}
