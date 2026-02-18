using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using Musoq.DataSources.Airtable.Components;
using Musoq.DataSources.Airtable.Sources.Bases;
using Musoq.DataSources.Tests.Common;
using Musoq.Evaluator;
using Musoq.Schema;

namespace Musoq.DataSources.Airtable.Tests;

[TestClass]
public class AirtableSchemaDescribeTests
{
    static AirtableSchemaDescribeTests()
    {
        Culture.ApplyWithDefaultCulture();
    }

    private CompiledQuery CreateAndRunVirtualMachine(string script, IAirtableApi api)
    {
        var mockSchemaProvider = new Mock<ISchemaProvider>();

        mockSchemaProvider.Setup(f => f.GetSchema(It.IsAny<string>())).Returns(
            new AirtableSchema(api));

        return InstanceCreatorHelpers.CompileForExecution(
            script,
            Guid.NewGuid().ToString(),
            mockSchemaProvider.Object,
            new Dictionary<uint, IReadOnlyDictionary<string, string>>
            {
                {
                    0, new Dictionary<string, string>
                    {
                        { "MUSOQ_AIRTABLE_API_KEY", "test_key" },
                        { "MUSOQ_AIRTABLE_BASE_ID", "test_base_id" }
                    }
                }
            });
    }

    [TestMethod]
    public void DescSchema_ShouldListAllAvailableMethods()
    {
        var api = new Mock<IAirtableApi>();
        var query = "desc #airtable";

        var vm = CreateAndRunVirtualMachine(query, api.Object);
        var table = vm.Run();

        Assert.AreEqual(2, table.Columns.Count(), "Should have 2 columns: Name and Param 0");
        Assert.AreEqual("Name", table.Columns.ElementAt(0).ColumnName);
        Assert.AreEqual("Param 0", table.Columns.ElementAt(1).ColumnName);

        Assert.AreEqual(3, table.Count, "Should have 3 rows (bases, base, records)");

        var methodNames = table.Select(row => (string)row[0]).ToList();

        Assert.AreEqual(1, methodNames.Count(m => m == "bases"), "Should contain 'bases' method once");
        Assert.AreEqual(1, methodNames.Count(m => m == "base"), "Should contain 'base' method once");
        Assert.AreEqual(1, methodNames.Count(m => m == "records"), "Should contain 'records' method once");

        var basesRow = table.First(row => (string)row[0] == "bases");
        Assert.IsNull(basesRow[1], "bases method should have no parameters");

        var baseRow = table.First(row => (string)row[0] == "base");
        Assert.IsNull(baseRow[1], "base method should have no parameters");

        var recordsRow = table.First(row => (string)row[0] == "records");
        Assert.AreEqual("tableName: System.String", (string)recordsRow[1]);
    }

    [TestMethod]
    public void DescBases_ShouldReturnMethodSignature()
    {
        var api = new Mock<IAirtableApi>();
        var query = "desc #airtable.bases";

        var vm = CreateAndRunVirtualMachine(query, api.Object);
        var table = vm.Run();

        Assert.AreEqual(1, table.Columns.Count(), "Should have 1 column: Name");
        Assert.AreEqual("Name", table.Columns.ElementAt(0).ColumnName);

        Assert.AreEqual(1, table.Count, "Should have exactly 1 row");

        var row = table.First();
        Assert.AreEqual("bases", (string)row[0]);
    }

    [TestMethod]
    public void DescBase_ShouldReturnMethodSignature()
    {
        var api = new Mock<IAirtableApi>();
        var query = "desc #airtable.base";

        var vm = CreateAndRunVirtualMachine(query, api.Object);
        var table = vm.Run();

        Assert.AreEqual(1, table.Columns.Count(), "Should have 1 column: Name");
        Assert.AreEqual("Name", table.Columns.ElementAt(0).ColumnName);

        Assert.AreEqual(1, table.Count, "Should have exactly 1 row");

        var row = table.First();
        Assert.AreEqual("base", (string)row[0]);
    }

    [TestMethod]
    public void DescRecords_ShouldReturnMethodSignature()
    {
        var api = new Mock<IAirtableApi>();
        var query = "desc #airtable.records";

        var vm = CreateAndRunVirtualMachine(query, api.Object);
        var table = vm.Run();

        Assert.AreEqual(2, table.Columns.Count(), "Should have 2 columns: Name and Param 0");
        Assert.AreEqual("Name", table.Columns.ElementAt(0).ColumnName);
        Assert.AreEqual("Param 0", table.Columns.ElementAt(1).ColumnName);

        Assert.AreEqual(1, table.Count, "Should have exactly 1 row");

        var row = table.First();
        Assert.AreEqual("records", (string)row[0]);
        Assert.AreEqual("tableName: System.String", (string)row[1]);
    }

    [TestMethod]
    public void DescBasesWithArgs_ShouldReturnTableSchema()
    {
        var api = new Mock<IAirtableApi>();

        api.Setup(f => f.GetBases(It.IsAny<IEnumerable<string>>()))
            .Returns(new List<List<AirtableBase>>
            {
                new()
                {
                    new AirtableBase("id1", "name1", "owner")
                }
            });

        var query = "desc #airtable.bases()";

        var vm = CreateAndRunVirtualMachine(query, api.Object);
        var table = vm.Run();

        Assert.AreEqual(3, table.Columns.Count());
        Assert.IsTrue(table.Count > 0, "Should have rows describing the table columns");

        var columnNames = table.Select(row => (string)row[0]).ToList();
        var expectedColumns = new[]
        {
            nameof(AirtableBase.Id),
            nameof(AirtableBase.Name),
            nameof(AirtableBase.PermissionLevel)
        };

        foreach (var expectedColumn in expectedColumns)
            Assert.IsTrue(columnNames.Contains(expectedColumn),
                $"Should have '{expectedColumn}' column");
    }

    [TestMethod]
    public void DescBaseWithArgs_ShouldReturnTableSchema()
    {
        var api = new Mock<IAirtableApi>();

        api.Setup(f => f.GetTables(It.IsAny<IEnumerable<string>>()))
            .Returns(new List<List<AirtableTable>>
            {
                new()
                {
                    new AirtableTable("id1", "name1", "primaryFieldId1", "desc1")
                }
            });

        var query = "desc #airtable.base()";

        var vm = CreateAndRunVirtualMachine(query, api.Object);
        var table = vm.Run();

        Assert.AreEqual(3, table.Columns.Count());
        Assert.IsTrue(table.Count > 0, "Should have rows describing the table columns");

        var columnNames = table.Select(row => (string)row[0]).ToList();
        var expectedColumns = new[]
        {
            nameof(AirtableTable.Id),
            nameof(AirtableTable.Name),
            nameof(AirtableTable.PrimaryFieldId)
        };

        foreach (var expectedColumn in expectedColumns)
            Assert.IsTrue(columnNames.Contains(expectedColumn),
                $"Should have '{expectedColumn}' column");
    }

    [TestMethod]
    public void DescUnknownMethod_ShouldThrowException()
    {
        var api = new Mock<IAirtableApi>();
        var query = "desc #airtable.unknownmethod";

        try
        {
            var vm = CreateAndRunVirtualMachine(query, api.Object);
            var table = vm.Run();
            Assert.Fail("Should have thrown an exception for unknown method");
        }
        catch (Exception ex)
        {
            var message = ex.InnerException?.Message ?? ex.Message;
            Assert.IsTrue(
                message.Contains("unknownmethod", StringComparison.OrdinalIgnoreCase),
                $"Error message should mention the unknown method. Got: {message}");
            Assert.IsTrue(
                message.Contains("not supported", StringComparison.OrdinalIgnoreCase) ||
                message.Contains("Available data sources", StringComparison.OrdinalIgnoreCase),
                $"Error message should be helpful. Got: {message}");
        }
    }

    [TestMethod]
    public void DescSchema_ShouldHaveConsistentColumnTypes()
    {
        var api = new Mock<IAirtableApi>();
        var query = "desc #airtable";

        var vm = CreateAndRunVirtualMachine(query, api.Object);
        var table = vm.Run();

        foreach (var column in table.Columns)
            Assert.AreEqual(typeof(string), column.ColumnType,
                $"Column '{column.ColumnName}' should be of type string");
    }

    [TestMethod]
    public void DescBasesNoArgs_VsWithArgs_ShouldReturnDifferentResults()
    {
        var api = new Mock<IAirtableApi>();

        api.Setup(f => f.GetBases(It.IsAny<IEnumerable<string>>()))
            .Returns(new List<List<AirtableBase>>
            {
                new()
                {
                    new AirtableBase("id1", "name1", "owner")
                }
            });

        var queryNoArgs = "desc #airtable.bases";
        var vmNoArgs = CreateAndRunVirtualMachine(queryNoArgs, api.Object);
        var tableNoArgs = vmNoArgs.Run();

        var queryWithArgs = "desc #airtable.bases()";
        var vmWithArgs = CreateAndRunVirtualMachine(queryWithArgs, api.Object);
        var tableWithArgs = vmWithArgs.Run();

        Assert.AreNotEqual(tableNoArgs.Count, tableWithArgs.Count,
            "Method signature vs table schema should have different row counts");

        Assert.AreEqual(1, tableNoArgs.Count);
        Assert.AreEqual("bases", (string)tableNoArgs.First()[0]);

        Assert.IsTrue(tableWithArgs.Count > 1);
        var columnNames = tableWithArgs.Select(row => (string)row[0]).ToList();
        Assert.IsTrue(columnNames.Contains(nameof(AirtableBase.Id)));
        Assert.IsTrue(columnNames.Contains(nameof(AirtableBase.Name)));
    }
}