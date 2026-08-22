using System;
using System.Linq;
using System.Threading;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using Musoq.DataSources.Tests.Common;
using Musoq.Evaluator;
using Musoq.Schema;

namespace Musoq.DataSources.Time.Tests;

[TestClass]
public class TimeTests
{
    static TimeTests()
    {
        Culture.ApplyWithDefaultCulture();
    }

    [TestMethod]
    public void EnumerateAllDaysInMonthTest()
    {
        var query = "select Day from time.interval('01.04.2018 00:00:00', '30.04.2018 00:00:00', 'days') order by Day";

        var vm = CreateAndRunVirtualMachine(query);
        var table = vm.Run();

        Assert.AreEqual(30, table.Count);

        for (var i = 1; i <= 30; i++) Assert.AreEqual(i, table[i - 1][0]);
    }

    [TestMethod]
    public void TimeSource_CancelledLoadTest()
    {
        var mockLogger = new Mock<ILogger>();
        using var tokenSource = new CancellationTokenSource();
        tokenSource.Cancel();
        var now = DateTimeOffset.Now;
        var nextHour = now.AddHours(1);
        var source = new TimeSource(now, nextHour, "minutes",
            RuntimeV2TestContexts.CreateExecutionContext(tokenSource.Token, logger: mockLogger.Object));

        var fired = source.Chunks.SelectMany(chunk => chunk).Count();

        Assert.AreEqual(0, fired);
    }

    [TestMethod]
    public void TimeSource_FullLoadTest()
    {
        var mockLogger = new Mock<ILogger>();
        var capture = new DataSourceProgressCapture();
        var now = DateTimeOffset.Parse("01/01/2000");
        var nextHour = now.AddHours(1);
        var source = new TimeSource(now, nextHour, "minutes",
            RuntimeV2TestContexts.CreateExecutionContext(
                CancellationToken.None,
                logger: mockLogger.Object,
                dataSourceProgressCallback: capture.Handler));

        var fired = source.Chunks.SelectMany(chunk => chunk).Count();

        Assert.AreEqual(61, fired);
        Assert.AreEqual(1, capture.For("time", DataSourcePhase.Begin).Count);
        Assert.AreEqual(61L, capture.For("time", DataSourcePhase.RowsRead).Single().RowsProcessed);
        Assert.AreEqual(61L, capture.For("time", DataSourcePhase.End).Single().RowsProcessed);
    }

    private CompiledQuery CreateAndRunVirtualMachine(string script)
    {
        return InstanceCreatorHelpers.CompileForExecution(script, Guid.NewGuid().ToString(), new TimeSchemaProvider(),
            EnvironmentVariablesHelpers.CreateMockedEnvironmentVariables());
    }
}
