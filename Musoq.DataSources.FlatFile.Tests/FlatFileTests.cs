using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using Musoq.DataSources.FlatFile;
using Musoq.DataSources.Tests.Common;
using Musoq.Evaluator;

namespace Musoq.Schema.FlatFile.Tests;

[TestClass]
public class FlatFileTests
{
    static FlatFileTests()
    {
        Culture.ApplyWithDefaultCulture();
    }

    [TestMethod]
    public void HasSelectedAllLinesTest()
    {
        var query = @"select LineNumber, Line from #FlatFile.file('./TestMultilineFile.txt')";

        var vm = CreateAndRunVirtualMachine(query);
        var table = vm.Run();

        Assert.AreEqual(2, table.Columns.Count());
        Assert.AreEqual("LineNumber", table.Columns.ElementAt(0).ColumnName);
        Assert.AreEqual(typeof(int), table.Columns.ElementAt(0).ColumnType);
        Assert.AreEqual("Line", table.Columns.ElementAt(1).ColumnName);
        Assert.AreEqual(typeof(string), table.Columns.ElementAt(1).ColumnType);

        Assert.IsTrue(table.Count == 6, "Table should have 6 entries");

        Assert.IsTrue(table.Any(row =>
            (int)row.Values[0] == 1 &&
            (string)row.Values[1] == string.Empty
        ), "First entry should be 1 with empty string");

        Assert.IsTrue(table.Any(row =>
            (int)row.Values[0] == 2 &&
            (string)row.Values[1] == "line 2"
        ), "Second entry should be 2 with 'line 2'");

        Assert.IsTrue(table.Any(row =>
            (int)row.Values[0] == 3 &&
            (string)row.Values[1] == "line3"
        ), "Third entry should be 3 with 'line3'");

        Assert.IsTrue(table.Any(row =>
            (int)row.Values[0] == 4 &&
            (string)row.Values[1] == "line"
        ), "Fourth entry should be 4 with 'line'");

        Assert.IsTrue(table.Any(row =>
            (int)row.Values[0] == 5 &&
            (string)row.Values[1] == string.Empty
        ), "Fifth entry should be 5 with empty string");

        Assert.IsTrue(table.Any(row =>
            (int)row.Values[0] == 6 &&
            (string)row.Values[1] == "linexx"
        ), "Sixth entry should be 6 with 'linexx'");
    }

    [TestMethod]
    public void FlatFileSource_CancelledLoadTest()
    {
        var mockLogger = new Mock<ILogger>();
        var endWorkTokenSource = new CancellationTokenSource();
        endWorkTokenSource.Cancel();
        var schema = new FlatFileSource("./TestMultilineFile.txt",
            new RuntimeContext(
                "test",
                endWorkTokenSource.Token,
                Array.Empty<ISchemaColumn>(),
                new Dictionary<string, string>(),
                QuerySourceInfo.Empty,
                mockLogger.Object));

        var fires = schema.Rows.Count();

        Assert.AreEqual(0, fires);
    }

    [TestMethod]
    public void FlatFileSource_FullLoadTest()
    {
        var mockLogger = new Mock<ILogger>();
        var schema = new FlatFileSource("./TestMultilineFile.txt",
            new RuntimeContext(
                "test",
                CancellationToken.None,
                Array.Empty<ISchemaColumn>(),
                new Dictionary<string, string>(),
                QuerySourceInfo.Empty,
                mockLogger.Object));

        var fires = schema.Rows.Count();

        Assert.AreEqual(6, fires);
    }

    private CompiledQuery CreateAndRunVirtualMachine(string script)
    {
        return InstanceCreatorHelpers.CompileForExecution(script, Guid.NewGuid().ToString(),
            new FlatFileSchemaProvider(), EnvironmentVariablesHelpers.CreateMockedEnvironmentVariables());
    }
}