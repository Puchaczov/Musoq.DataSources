using System;
using System.Linq;
using System.IO;
using System.Threading;
using Musoq.Converter.Exceptions;
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
            (string)row.ReadValues()[0] == "Aleksander" &&
            (long)row.ReadValues()[1] == 24L
        ), "First entry should be Aleksander, 24");

        Assert.IsTrue(table.Any(row =>
            (string)row.ReadValues()[0] == "Mikolaj" &&
            (long)row.ReadValues()[1] == 11L
        ), "Second entry should be Mikolaj, 11");

        Assert.IsTrue(table.Any(row =>
            (string)row.ReadValues()[0] == "Marek" &&
            (long)row.ReadValues()[1] == 45L
        ), "Third entry should be Marek, 45");
    }

    [TestMethod]
    public void SelectWithNestedArrayTest()
    {
        var query =
            @"select Name, Books from json.file('./JsonTestFile_First.json')";

        var exception = Assert.ThrowsExactly<MusoqQueryException>(() => CreateAndRunVirtualMachine(query));

        StringAssert.Contains(exception.Message, "MQ3027");
    }

    [TestMethod]
    public void SelectNestedArrayTest()
    {
        var query =
            @"select Array from json.file('./JsonTestFile_MakeFlatArray.json')";

        var exception = Assert.ThrowsExactly<MusoqQueryException>(() => CreateAndRunVirtualMachine(query));

        StringAssert.Contains(exception.Message, "MQ3027");
    }

    [TestMethod]
    public void JsonSource_Cancelled_ShouldBeEmpty()
    {
        var mockLogger = new Mock<ILogger>();
        using var tokenSource = new CancellationTokenSource();
        tokenSource.Cancel();
        var executionContext = RuntimeV2TestContexts.CreateExecutionContext(tokenSource.Token, logger: mockLogger.Object);
        var source = new NativeJsonFixtureSource("./JsonTestFile_First.json", executionContext);

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
        var source = new NativeJsonFixtureSource("./JsonTestFile_First.json", executionContext);

        var fired = source.Chunks.SelectMany(chunk => chunk).Count();

        Assert.AreEqual(3, fired);
        Assert.AreEqual(1, capture.For("json", DataSourcePhase.Begin).Count);
        Assert.AreEqual(3L, capture.For("json", DataSourcePhase.RowsKnown).Single().TotalRows);
        Assert.AreEqual(3L, capture.For("json", DataSourcePhase.RowsRead).Single().RowsProcessed);
        Assert.AreEqual(3L, capture.For("json", DataSourcePhase.End).Single().RowsProcessed);
    }

    [TestMethod]
    [DataRow("long")]
    [DataRow("long?")]
    public void TableDefinedSource_ReorderedSubset_ShouldRetainNullableDeclarationTypes(string integerType)
    {
        var path = Path.GetTempFileName();
        try
        {
            File.WriteAllText(path, "[{\"Id\":7,\"Name\":\"Ada\",\"Ignored\":\"x\"},{\"Id\":null,\"Name\":\"Mira\",\"Ignored\":\"y\"}]");
            var query = $"table Contract {{ Name: string, Id: {integerType} }}; " +
                        "couple json.file with table Contract as People; " +
                        $"select Name as Name, Id as Id from People('{path.Replace("\\", "/").Replace("'", "''")}') order by Name";
            using var compiled = CreateAndRunVirtualMachine(query);
            using var result = compiled.Run();
            Assert.AreEqual(typeof(string), result.Columns.ElementAt(0).ColumnType);
            Assert.AreEqual(typeof(long?), result.Columns.ElementAt(1).ColumnType);
            Assert.AreEqual("Name", result.Columns.ElementAt(0).ColumnName);
            Assert.AreEqual("Id", result.Columns.ElementAt(1).ColumnName);
            Assert.AreEqual(2, result.Count);
            Assert.AreEqual("Ada", result[0].ReadValues()[0]);
            Assert.AreEqual(7L, result[0].ReadValues()[1]);
            Assert.AreEqual("Mira", result[1].ReadValues()[0]);
            Assert.IsNull(result[1].ReadValues()[1]);
        }
        finally
        {
            File.Delete(path);
        }
    }

    private CompiledQuery CreateAndRunVirtualMachine(string script)
    {
        return InstanceCreatorHelpers.CompileForExecution(script, Guid.NewGuid().ToString(), new JsonSchemaProvider(),
            EnvironmentVariablesHelpers.CreateMockedEnvironmentVariables());
    }
}
