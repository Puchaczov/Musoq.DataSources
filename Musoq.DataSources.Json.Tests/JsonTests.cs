using System;
using System.Linq;
using System.Threading;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using Musoq.DataSources.Tests.Common;
using Musoq.Evaluator;
using Musoq.Schema;

namespace Musoq.DataSources.Json.Tests;

[TestClass]
public class JsonTests
{
    static JsonTests()
    {
        Culture.ApplyWithDefaultCulture();
    }

    [TestMethod]
    public void SimpleSelectTest()
    {
        var query =
            @"select Name, Age from json.file('./JsonTestFile_First.json')";

        var vm = CreateAndRunVirtualMachine(query);
        var table = vm.Run();

        Assert.AreEqual(2, table.Columns.Count());
        Assert.AreEqual("Name", table.Columns.ElementAt(0).ColumnName);
        Assert.AreEqual(typeof(string), table.Columns.ElementAt(0).ColumnType);
        Assert.AreEqual("Age", table.Columns.ElementAt(1).ColumnName);
        Assert.AreEqual(typeof(long), table.Columns.ElementAt(1).ColumnType);

        Assert.IsTrue(table.Count == 3, "Table should have 3 entries");

        Assert.IsTrue(table.Any(row =>
            (string)row.Values[0] == "Aleksander" &&
            (long)row.Values[1] == 24L
        ), "First entry should be Aleksander, 24");

        Assert.IsTrue(table.Any(row =>
            (string)row.Values[0] == "Mikolaj" &&
            (long)row.Values[1] == 11L
        ), "Second entry should be Mikolaj, 11");

        Assert.IsTrue(table.Any(row =>
            (string)row.Values[0] == "Marek" &&
            (long)row.Values[1] == 45L
        ), "Third entry should be Marek, 45");
    }

    [TestMethod]
    public void SelectWithNestedArrayTest()
    {
        var query =
            @"select Name, Books from json.file('./JsonTestFile_First.json')";

        var vm = CreateAndRunVirtualMachine(query);
        var table = vm.Run();

        Assert.AreEqual(2, table.Columns.Count());
        Assert.AreEqual("Name", table.Columns.ElementAt(0).ColumnName);
        Assert.AreEqual(typeof(string), table.Columns.ElementAt(0).ColumnType);
        Assert.AreEqual("Books", table.Columns.ElementAt(1).ColumnName);
        Assert.AreEqual(typeof(object), table.Columns.ElementAt(1).ColumnType);

        Assert.IsTrue(table.Count == 3, "Table should contain exactly 3 records");

        Assert.IsTrue(table.Any(r =>
                (string)r.Values[0] == "Aleksander" && ((System.Collections.Generic.List<object>)r.Values[1]).Count == 2),
            "Missing record for Aleksander with value 2");

        Assert.IsTrue(table.Any(r =>
                (string)r.Values[0] == "Mikolaj" && ((System.Collections.Generic.List<object>)r.Values[1]).Count == 0),
            "Missing record for Mikolaj with value 0");

        Assert.IsTrue(table.Any(r =>
                (string)r.Values[0] == "Marek" && ((System.Collections.Generic.List<object>)r.Values[1]).Count == 0),
            "Missing record for Marek with value 0");
    }

    [TestMethod]
    public void SelectNestedArrayTest()
    {
        var query =
            @"select Array from json.file('./JsonTestFile_MakeFlatArray.json')";

        var vm = CreateAndRunVirtualMachine(query);
        var table = vm.Run();

        Assert.AreEqual(1, table.Columns.Count());
        Assert.AreEqual("Array", table.Columns.ElementAt(0).ColumnName);
        Assert.AreEqual(typeof(object), table.Columns.ElementAt(0).ColumnType);

        Assert.IsTrue(table.Count == 2, "Table should have 2 entries");

        Assert.IsTrue(table.Any(row =>
            ((System.Collections.Generic.List<object>)row.Values[0]).Count == 3
        ), "First entry should contain three values");

        Assert.IsTrue(table.Any(row =>
            ((System.Collections.Generic.List<object>)row.Values[0]).Count == 0
        ), "Second entry should be an empty array");
    }

    [TestMethod]
    public void JsonSource_Cancelled_ShouldBeEmpty()
    {
        var mockLogger = new Mock<ILogger>();
        using var tokenSource = new CancellationTokenSource();
        tokenSource.Cancel();
        var executionContext = RuntimeV2TestContexts.CreateExecutionContext(tokenSource.Token, logger: mockLogger.Object);
        var source = new JsonSource("./JsonTestFile_First.json", executionContext);

        var fired = source.Chunks.SelectMany(chunk => chunk).Count();

        Assert.AreEqual(0, fired);
    }

    [TestMethod]
    public void JsonSource_FullLoadTest()
    {
        var mockLogger = new Mock<ILogger>();
        var capture = new DataSourceProgressCapture();
        var executionContext = RuntimeV2TestContexts.CreateExecutionContext(
            CancellationToken.None,
            logger: mockLogger.Object,
            dataSourceProgressCallback: capture.Handler);
        var source = new JsonSource("./JsonTestFile_First.json", executionContext);

        var fired = source.Chunks.SelectMany(chunk => chunk).Count();

        Assert.AreEqual(3, fired);
        Assert.AreEqual(1, capture.For("json", DataSourcePhase.Begin).Count);
        Assert.AreEqual(3L, capture.For("json", DataSourcePhase.RowsKnown).Single().TotalRows);
        Assert.AreEqual(3L, capture.For("json", DataSourcePhase.RowsRead).Single().RowsProcessed);
        Assert.AreEqual(3L, capture.For("json", DataSourcePhase.End).Single().RowsProcessed);
    }

    private CompiledQuery CreateAndRunVirtualMachine(string script)
    {
        return InstanceCreatorHelpers.CompileForExecution(script, Guid.NewGuid().ToString(), new JsonSchemaProvider(),
            EnvironmentVariablesHelpers.CreateMockedEnvironmentVariables());
    }
}
