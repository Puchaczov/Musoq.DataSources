using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using Musoq.DataSources.Tests.Common;
using Musoq.Evaluator;
using Musoq.Schema;
using Musoq.Schema.DataSources;
using Musoq.Schema.Optimization;
using Musoq.DataSources.SeparatedValues.Tests.Infrastructure;

namespace Musoq.DataSources.SeparatedValues.Tests.Sources;

[TestClass]
public class CsvTests
{
    static CsvTests()
    {
        Culture.Apply(CultureInfo.GetCultureInfo("en-US"));
    }

    [TestInitialize]
    public void SetCulture()
    {
        Culture.Apply(CultureInfo.GetCultureInfo("en-US"));
    }

    [TestMethod]
    public void SimpleSelectWithSkipLinesTest()
    {
        var query =
            "SELECT Name FROM separatedvalues.comma('./TestData/Files/BankingTransactionsWithSkippedLines.csv', true, 2)";

        var vm = CreateAndRunVirtualMachine(query);
        var table = vm.Run();

        Assert.AreEqual(1, table.Columns.Count());
        Assert.AreEqual("Name", table.Columns.ElementAt(0).ColumnName);
        Assert.AreEqual(typeof(string), table.Columns.ElementAt(0).ColumnType);

        Assert.IsTrue(table.Count == 11, "Table should have 11 entries");

        Assert.IsTrue(table.Count(row => (string)row.ReadValues()[0] == "Salary") == 2, "Should have 2 'Salary' entries");
        Assert.IsTrue(table.Count(row => (string)row.ReadValues()[0] == "Restaurant A") == 2,
            "Should have 2 'Restaurant A' entries");
        Assert.IsTrue(table.Count(row => (string)row.ReadValues()[0] == "Bus ticket") == 2,
            "Should have 2 'Bus ticket' entries");
        Assert.IsTrue(table.Count(row => (string)row.ReadValues()[0] == "Tesco") == 2, "Should have 2 'Tesco' entries");
        Assert.IsTrue(table.Count(row => (string)row.ReadValues()[0] == "Restaurant B") == 2,
            "Should have 2 'Restaurant B' entries");
        Assert.IsTrue(table.Count(row => (string)row.ReadValues()[0] == "Service") == 1, "Should have 1 'Service' entry");
    }

    [TestMethod]
    public void SimpleSelectWithCouplingTableSyntaxSkipLinesTest()
    {
        var query = "" +
                    "table CsvFile {" +
                    "   Name: string" +
                    "};" +
                    "couple separatedvalues.comma with table CsvFile as SourceCsvFile;" +
                    "select Name from SourceCsvFile('./TestData/Files/BankingTransactionsWithSkippedLines.csv', true, 2);";

        var vm = CreateAndRunVirtualMachine(query);
        var table = vm.Run();

        Assert.AreEqual(1, table.Columns.Count());
        Assert.AreEqual("Name", table.Columns.ElementAt(0).ColumnName);
        Assert.AreEqual(typeof(string), table.Columns.ElementAt(0).ColumnType);

        Assert.IsTrue(table.Count == 11, "Table should have 11 entries");

        Assert.IsTrue(table.Count(row => (string)row.ReadValues()[0] == "Salary") == 2, "Should have 2 'Salary' entries");
        Assert.IsTrue(table.Count(row => (string)row.ReadValues()[0] == "Restaurant A") == 2,
            "Should have 2 'Restaurant A' entries");
        Assert.IsTrue(table.Count(row => (string)row.ReadValues()[0] == "Bus ticket") == 2,
            "Should have 2 'Bus ticket' entries");
        Assert.IsTrue(table.Count(row => (string)row.ReadValues()[0] == "Tesco") == 2, "Should have 2 'Tesco' entries");
        Assert.IsTrue(table.Count(row => (string)row.ReadValues()[0] == "Restaurant B") == 2,
            "Should have 2 'Restaurant B' entries");
        Assert.IsTrue(table.Count(row => (string)row.ReadValues()[0] == "Service") == 1, "Should have 1 'Service' entry");
    }

    [TestMethod]
    public void CheckTypesTest()
    {
        var query = "" +
                    "table Persons {" +
                    "   Id: int," +
                    "   Name: string" +
                    "};" +
                    "couple separatedvalues.comma with table Persons as SourceOfPersons;" +
                    "select Id, Name from SourceOfPersons('./TestData/Files/Persons.csv', true, 0)";

        var vm = CreateAndRunVirtualMachine(query);
        var table = vm.Run();

        Assert.AreEqual(2, table.Columns.Count());
        Assert.AreEqual("Id", table.Columns.ElementAt(0).ColumnName);
        Assert.AreEqual(typeof(int?), table.Columns.ElementAt(0).ColumnType);
        Assert.AreEqual("Name", table.Columns.ElementAt(1).ColumnName);
        Assert.AreEqual(typeof(string), table.Columns.ElementAt(1).ColumnType);

        Assert.IsTrue(table.Count == 5, "Table should have 5 entries");

        Assert.IsTrue(table.Any(row =>
            (int)row.ReadValues()[0] == 1 &&
            (string)row.ReadValues()[1] == "Jan"
        ), "First entry should be 1, Jan");

        Assert.IsTrue(table.Any(row =>
            (int)row.ReadValues()[0] == 2 &&
            (string)row.ReadValues()[1] == "Marek"
        ), "Second entry should be 2, Marek");

        Assert.IsTrue(table.Any(row =>
            (int)row.ReadValues()[0] == 3 &&
            (string)row.ReadValues()[1] == "Witek"
        ), "Third entry should be 3, Witek");

        Assert.IsTrue(table.Any(row =>
            (int)row.ReadValues()[0] == 4 &&
            (string)row.ReadValues()[1] == "Anna"
        ), "Fourth entry should be 4, Anna");

        Assert.IsTrue(table.Any(row =>
            (int)row.ReadValues()[0] == 5 &&
            (string)row.ReadValues()[1] == "Anna"
        ), "Fifth entry should be 5, Anna");
    }

    [TestMethod]
    public void CheckNullValues()
    {
        var query = "" +
                    "table BankingTransactions {" +
                    "   Category: string," +
                    "   Money: decimal" +
                    "};" +
                    "couple separatedvalues.comma with table BankingTransactions as SourceOfBankingTransactions;" +
                    "select Category, Money from SourceOfBankingTransactions('./TestData/Files/BankingTransactionsNullValues.csv', true, 0) where (Category is null) or (Money is null)";

        var vm = CreateAndRunVirtualMachine(query);
        var table = vm.Run();

        Assert.AreEqual(2, table.Columns.Count());
        Assert.AreEqual("Category", table.Columns.ElementAt(0).ColumnName);
        Assert.AreEqual(typeof(string), table.Columns.ElementAt(0).ColumnType);
        Assert.AreEqual("Money", table.Columns.ElementAt(1).ColumnName);
        Assert.AreEqual(typeof(decimal?), table.Columns.ElementAt(1).ColumnType);

        Assert.IsTrue(table.Count == 4, "Table should have 4 entries");

        Assert.IsTrue(table.Any(row =>
            (string)row.ReadValues()[0] == "Life" &&
            row.ReadValues()[1] == null
        ), "First entry should be Life, null");

        Assert.IsTrue(table.Any(row =>
            row.ReadValues()[0] == null &&
            (decimal?)row.ReadValues()[1] == -1m
        ), "Second entry should be null, -1");

        Assert.IsTrue(table.Any(row =>
            row.ReadValues()[0] == null &&
            (decimal?)row.ReadValues()[1] == -121.95m
        ), "Third entry should be null, -121.95");

        Assert.IsTrue(table.Any(row =>
            row.ReadValues()[0] == null &&
            row.ReadValues()[1] == null
        ), "Fourth entry should be null, null");
    }

    [TestMethod]
    public void CsvSource_FromTableRows_IsRejectedByMandatoryQueryRowGuard()
    {
        var schema = new SeparatedValuesSchema();
        var context = RuntimeV2TestContexts.CreateExecutionContext(CancellationToken.None);

        var exception = Assert.ThrowsExactly<InvalidOperationException>(() =>
            global::Musoq.DataSources.Tests.Common.NativeSourceCalls.Source(schema, global::Musoq.Schema.SourceArguments.Create(new TestReadOnlyTable([["./TestData/Files/BankingTransactionsWithSkippedLines.csv", true, 2]]), global::Musoq.Schema.SourceArguments.Create(true, global::Musoq.Schema.SourceArguments.Create(0, global::Musoq.Schema.SourceArguments.Empty)))).Read<object[]>("comma", context));

        StringAssert.Contains(exception.Message, "unsupported legacy row transfer");
    }

    [TestMethod]
    public void SimpleSelectTest()
    {
        var query = "SELECT Name FROM separatedvalues.comma('./TestData/Files/BankingTransactions.csv', true, 0)";

        var vm = CreateAndRunVirtualMachine(query);
        var table = vm.Run();

        Assert.AreEqual(1, table.Columns.Count());
        Assert.AreEqual("Name", table.Columns.ElementAt(0).ColumnName);
        Assert.AreEqual(typeof(string), table.Columns.ElementAt(0).ColumnType);

        Assert.IsTrue(table.Count == 11, "Table should have 11 entries");

        Assert.IsTrue(table.Count(row => (string)row.ReadValues()[0] == "Salary") == 2, "Should have 2 'Salary' entries");
        Assert.IsTrue(table.Count(row => (string)row.ReadValues()[0] == "Restaurant A") == 2,
            "Should have 2 'Restaurant A' entries");
        Assert.IsTrue(table.Count(row => (string)row.ReadValues()[0] == "Bus ticket") == 2,
            "Should have 2 'Bus ticket' entries");
        Assert.IsTrue(table.Count(row => (string)row.ReadValues()[0] == "Tesco") == 2, "Should have 2 'Tesco' entries");
        Assert.IsTrue(table.Count(row => (string)row.ReadValues()[0] == "Restaurant B") == 2,
            "Should have 2 'Restaurant B' entries");
        Assert.IsTrue(table.Count(row => (string)row.ReadValues()[0] == "Service") == 1, "Should have 1 'Service' entry");
    }

    [TestMethod]
    public void SimpleSelectNoHeaderTest()
    {
        var query = "SELECT Column3 FROM separatedvalues.comma('./TestData/Files/BankingTransactionsNoHeader.csv', false, 0)";

        var vm = CreateAndRunVirtualMachine(query);
        var table = vm.Run();

        Assert.AreEqual(1, table.Columns.Count());
        Assert.AreEqual("Column3", table.Columns.ElementAt(0).ColumnName);
        Assert.AreEqual(typeof(string), table.Columns.ElementAt(0).ColumnType);

        Assert.IsTrue(table.Count == 11, "Table should have 11 entries");

        Assert.IsTrue(table.Count(row => (string)row.ReadValues()[0] == "Salary") == 2, "Should have 2 'Salary' entries");
        Assert.IsTrue(table.Count(row => (string)row.ReadValues()[0] == "Restaurant A") == 2,
            "Should have 2 'Restaurant A' entries");
        Assert.IsTrue(table.Count(row => (string)row.ReadValues()[0] == "Bus ticket") == 2,
            "Should have 2 'Bus ticket' entries");
        Assert.IsTrue(table.Count(row => (string)row.ReadValues()[0] == "Tesco") == 2, "Should have 2 'Tesco' entries");
        Assert.IsTrue(table.Count(row => (string)row.ReadValues()[0] == "Restaurant B") == 2,
            "Should have 2 'Restaurant B' entries");
        Assert.IsTrue(table.Count(row => (string)row.ReadValues()[0] == "Service") == 1, "Should have 1 'Service' entry");
    }

    [TestMethod]
    public void SimpleCountTest()
    {
        var query =
            "SELECT Count(OperationDate) FROM separatedvalues.comma('./TestData/Files/BankingTransactions.csv', true, 0)";

        var vm = CreateAndRunVirtualMachine(query);
        var table = vm.Run();

        Assert.AreEqual(1, table.Columns.Count());
        Assert.AreEqual("Count(OperationDate)", table.Columns.ElementAt(0).ColumnName);
        Assert.AreEqual(typeof(long), table.Columns.ElementAt(0).ColumnType);

        Assert.AreEqual(1, table.Count);
        Assert.AreEqual(11L, table[0].ReadValues()[0]);
    }

    [TestMethod]
    public void SimpleGroupByWithSum()
    {
        var query =
            @"
select 
    Count(OperationDate, 1), 
    ExtractFromDate(OperationDate, 'month'), 
    Count(OperationDate), 
    SumIncome(ToDecimal(Money)), 
    SumOutcome(ToDecimal(Money)), 
    SumIncome(ToDecimal(Money)) - Abs(SumOutcome(ToDecimal(Money))) 
from separatedvalues.comma('./TestData/Files/BankingTransactions.csv', true, 0)
group by ExtractFromDate(OperationDate, 'month')";

        var vm = CreateAndRunVirtualMachine(query);
        var table = vm.Run();

        Assert.AreEqual(6, table.Columns.Count());
        Assert.AreEqual("Count(OperationDate, 1)", table.Columns.ElementAt(0).ColumnName);
        Assert.AreEqual(typeof(long), table.Columns.ElementAt(0).ColumnType);
        Assert.AreEqual("ExtractFromDate(OperationDate, month)", table.Columns.ElementAt(1).ColumnName);
        Assert.AreEqual(typeof(int), table.Columns.ElementAt(1).ColumnType);
        Assert.AreEqual("Count(OperationDate)", table.Columns.ElementAt(2).ColumnName);
        Assert.AreEqual(typeof(long), table.Columns.ElementAt(2).ColumnType);
        Assert.AreEqual("SumIncome(ToDecimal(Money))", table.Columns.ElementAt(3).ColumnName);
        Assert.AreEqual(typeof(decimal?), table.Columns.ElementAt(3).ColumnType);
        Assert.AreEqual("SumOutcome(ToDecimal(Money))", table.Columns.ElementAt(4).ColumnName);
        Assert.AreEqual(typeof(decimal?), table.Columns.ElementAt(4).ColumnType);
        Assert.AreEqual("SumIncome(ToDecimal(Money)) - Abs(SumOutcome(ToDecimal(Money)))",
            table.Columns.ElementAt(5).ColumnName);
        Assert.AreEqual(typeof(decimal?), table.Columns.ElementAt(5).ColumnType);

        Assert.IsTrue(table.Count == 2, "Table should have 2 entries");

        Assert.IsTrue(table.Any(row =>
            (long)row.ReadValues()[0] == 11L &&
            (int)row.ReadValues()[1] == 1 &&
            (long)row.ReadValues()[2] == 6L &&
            (decimal)row.ReadValues()[3] == 4199m &&
            (decimal)row.ReadValues()[4] == -157.15m &&
            (decimal)row.ReadValues()[5] == 4041.85m
        ), "First entry does not match expected details");

        Assert.IsTrue(table.Any(row =>
            (long)row.ReadValues()[0] == 11L &&
            (int)row.ReadValues()[1] == 2 &&
            (long)row.ReadValues()[2] == 5L &&
            (decimal)row.ReadValues()[3] == 4000m &&
            (decimal)row.ReadValues()[4] == -157.15m &&
            (decimal)row.ReadValues()[5] == 3842.85m
        ), "Second entry does not match expected details");
    }

    [TestMethod]
    public void InnerJoinTest()
    {
        var query = @"
select 
    persons.Name, 
    persons.Surname, 
    grades.Subject, 
    grades.ToDecimal(grades.Grade) 
from separatedvalues.comma('./TestData/Files/Persons.csv', true, 0) persons
inner join separatedvalues.comma('./TestData/Files/Gradebook.csv', true, 0) grades on persons.Id = grades.PersonId";

        var vm = CreateAndRunVirtualMachine(query);
        var table = vm.Run();

        Assert.AreEqual(4, table.Columns.Count());

        Assert.AreEqual("Name", table.Columns.ElementAt(0).ColumnName);
        Assert.AreEqual(typeof(string), table.Columns.ElementAt(0).ColumnType);
        Assert.AreEqual("Surname", table.Columns.ElementAt(1).ColumnName);
        Assert.AreEqual(typeof(string), table.Columns.ElementAt(1).ColumnType);
        Assert.AreEqual("Subject", table.Columns.ElementAt(2).ColumnName);
        Assert.AreEqual(typeof(string), table.Columns.ElementAt(2).ColumnType);
        Assert.AreEqual("grades.ToDecimal(grades.Grade)", table.Columns.ElementAt(3).ColumnName);
        Assert.AreEqual(typeof(decimal?), table.Columns.ElementAt(3).ColumnType);

        Assert.IsTrue(table.Count == 24, "Table should contain exactly 24 records");


        Assert.IsTrue(table.Count(r =>
                (string)r.ReadValue(0) == "Jan" && (string)r.ReadValue(1) == "Grzyb" && (string)r.ReadValue(2) == "Math" && (decimal)r.ReadValue(3) == 5m) == 1,
            "Jan Grzyb should have exactly one Math grade of 5");
        Assert.IsTrue(table.Count(r =>
                          (string)r.ReadValue(0) == "Jan" && (string)r.ReadValue(1) == "Grzyb" && (string)r.ReadValue(2) == "English" &&
                          (decimal)r.ReadValue(3) == 2m) ==
                      1,
            "Jan Grzyb should have exactly one English grade of 2");
        Assert.IsTrue(table.Count(r =>
                (string)r.ReadValue(0) == "Jan" && (string)r.ReadValue(1) == "Grzyb" && (string)r.ReadValue(2) == "Math" && (decimal)r.ReadValue(3) == 4m) == 2,
            "Jan Grzyb should have exactly two Math grades of 4");
        Assert.IsTrue(table.Count(r =>
                          (string)r.ReadValue(0) == "Jan" && (string)r.ReadValue(1) == "Grzyb" && (string)r.ReadValue(2) == "Biology" &&
                          (decimal)r.ReadValue(3) == 4m) ==
                      1,
            "Jan Grzyb should have exactly one Biology grade of 4");
        Assert.IsTrue(table.Count(r =>
                          (string)r.ReadValue(0) == "Jan" && (string)r.ReadValue(1) == "Grzyb" && (string)r.ReadValue(2) == "Biology" &&
                          (decimal)r.ReadValue(3) == 3m) ==
                      1,
            "Jan Grzyb should have exactly one Biology grade of 3");


        Assert.IsTrue(table.Count(r =>
                (string)r.ReadValue(0) == "Marek" && (string)r.ReadValue(1) == "Tarczynski" && (string)r.ReadValue(2) == "Math" &&
                (decimal)r.ReadValue(3) == 5m) == 1,
            "Marek Tarczynski should have exactly one Math grade of 5");
        Assert.IsTrue(table.Count(r =>
                (string)r.ReadValue(0) == "Marek" && (string)r.ReadValue(1) == "Tarczynski" && (string)r.ReadValue(2) == "English" &&
                (decimal)r.ReadValue(3) == 2m) == 1,
            "Marek Tarczynski should have exactly one English grade of 2");


        Assert.IsTrue(table.Count(r =>
                          (string)r.ReadValue(0) == "Witek" && (string)r.ReadValue(1) == "Lechoń" && (string)r.ReadValue(2) == "Math" &&
                          (decimal)r.ReadValue(3) == 4m) ==
                      2,
            "Witek Lechoń should have exactly two Math grades of 4");
        Assert.IsTrue(table.Count(r =>
                (string)r.ReadValue(0) == "Witek" && (string)r.ReadValue(1) == "Lechoń" && (string)r.ReadValue(2) == "Biology" &&
                (decimal)r.ReadValue(3) == 4m) == 1,
            "Witek Lechoń should have exactly one Biology grade of 4");
        Assert.IsTrue(table.Count(r =>
                (string)r.ReadValue(0) == "Witek" && (string)r.ReadValue(1) == "Lechoń" && (string)r.ReadValue(2) == "Biology" &&
                (decimal)r.ReadValue(3) == 3m) == 1,
            "Witek Lechoń should have exactly one Biology grade of 3");


        Assert.IsTrue(table.Count(r =>
                (string)r.ReadValue(0) == "Anna" && (string)r.ReadValue(1) == "Rozmaryn" && (string)r.ReadValue(2) == "Math" &&
                (decimal)r.ReadValue(3) == 5m) == 1,
            "Anna Rozmaryn should have exactly one Math grade of 5");
        Assert.IsTrue(table.Count(r =>
                (string)r.ReadValue(0) == "Anna" && (string)r.ReadValue(1) == "Rozmaryn" && (string)r.ReadValue(2) == "English" &&
                (decimal)r.ReadValue(3) == 2m) == 1,
            "Anna Rozmaryn should have exactly one English grade of 2");
        Assert.IsTrue(table.Count(r =>
                (string)r.ReadValue(0) == "Anna" && (string)r.ReadValue(1) == "Rozmaryn" && (string)r.ReadValue(2) == "Math" &&
                (decimal)r.ReadValue(3) == 4m) == 2,
            "Anna Rozmaryn should have exactly two Math grades of 4");
        Assert.IsTrue(table.Count(r =>
                (string)r.ReadValue(0) == "Anna" && (string)r.ReadValue(1) == "Rozmaryn" && (string)r.ReadValue(2) == "Biology" &&
                (decimal)r.ReadValue(3) == 4m) == 1,
            "Anna Rozmaryn should have exactly one Biology grade of 4");
        Assert.IsTrue(table.Count(r =>
                (string)r.ReadValue(0) == "Anna" && (string)r.ReadValue(1) == "Rozmaryn" && (string)r.ReadValue(2) == "Biology" &&
                (decimal)r.ReadValue(3) == 3m) == 1,
            "Anna Rozmaryn should have exactly one Biology grade of 3");


        Assert.IsTrue(table.Count(r =>
                          (string)r.ReadValue(0) == "Anna" && (string)r.ReadValue(1) == "Trzpień" && (string)r.ReadValue(2) == "Math" &&
                          (decimal)r.ReadValue(3) == 5m) ==
                      1,
            "Anna Trzpień should have exactly one Math grade of 5");
        Assert.IsTrue(table.Count(r =>
                (string)r.ReadValue(0) == "Anna" && (string)r.ReadValue(1) == "Trzpień" && (string)r.ReadValue(2) == "English" &&
                (decimal)r.ReadValue(3) == 2m) == 1,
            "Anna Trzpień should have exactly one English grade of 2");
        Assert.IsTrue(table.Count(r =>
                          (string)r.ReadValue(0) == "Anna" && (string)r.ReadValue(1) == "Trzpień" && (string)r.ReadValue(2) == "Math" &&
                          (decimal)r.ReadValue(3) == 4m) ==
                      2,
            "Anna Trzpień should have exactly two Math grades of 4");
        Assert.IsTrue(table.Count(r =>
                (string)r.ReadValue(0) == "Anna" && (string)r.ReadValue(1) == "Trzpień" && (string)r.ReadValue(2) == "Biology" &&
                (decimal)r.ReadValue(3) == 4m) == 1,
            "Anna Trzpień should have exactly one Biology grade of 4");
        Assert.IsTrue(table.Count(r =>
                (string)r.ReadValue(0) == "Anna" && (string)r.ReadValue(1) == "Trzpień" && (string)r.ReadValue(2) == "Biology" &&
                (decimal)r.ReadValue(3) == 3m) == 1,
            "Anna Trzpień should have exactly one Biology grade of 3");
    }

    [TestMethod]
    public void CoupledQueryInsideCteTest()
    {
        var query = File.ReadAllText("./TestData/Test1/Query.txt");

        var vm = CreateAndRunVirtualMachine(query);
        var table = vm.Run();

        Assert.IsTrue(table.Count == 6, "Table should contain exactly 6 records");


        Assert.IsTrue(table.Any(r =>
                (string)r.ReadValue(0) == "file1" && (decimal)r.ReadValue(1) == 5m && (string)r.ReadValue(2) == "1 row of file1"),
            "Missing first row of file1 with value 5");

        Assert.IsTrue(table.Any(r =>
                (string)r.ReadValue(0) == "file1" && (decimal)r.ReadValue(1) == 3m && (string)r.ReadValue(2) == "2 row of file1"),
            "Missing second row of file1 with value 3");

        Assert.IsTrue(table.Any(r =>
                (string)r.ReadValue(0) == "file1" && (decimal)r.ReadValue(1) == 16m && (string)r.ReadValue(2) == "3 row of file1"),
            "Missing third row of file1 with value 16");


        Assert.IsTrue(table.Any(r =>
                (string)r.ReadValue(0) == "file2" && (decimal)r.ReadValue(1) == 11m && (string)r.ReadValue(2) == "1 row of file2"),
            "Missing first row of file2 with value 11");

        Assert.IsTrue(table.Any(r =>
                (string)r.ReadValue(0) == "file2" && (decimal)r.ReadValue(1) == 12m && (string)r.ReadValue(2) == "2 row of file2"),
            "Missing second row of file2 with value 12");

        Assert.IsTrue(table.Any(r =>
                (string)r.ReadValue(0) == "file2" && (decimal)r.ReadValue(1) == 15m && (string)r.ReadValue(2) == "3 row of file2"),
            "Missing third row of file2 with value 15");
    }

    [TestMethod]
    public void CsvSource_CancelledLoadTest()
    {
        var mockLogger = new Mock<ILogger>();
        using var tokenSource = new CancellationTokenSource();
        const string path = "./TestData/Files/BankingTransactionsWithSkippedLines.csv";
        var request = new SourcePlanRequest
        {
            Identity = SourceIdentity.Empty,
            RequiredColumns = [new SourceColumnRef("Name")],
            SourceRuntimeSettings = new Dictionary<string, string>(),
            Predicate = null,
            OrderBy = [],
            Skip = null,
            Take = null
        };
        var plan = new SeparatedValuesSchema()
            .TryPlanSource("comma", request, path, true, 2)
            .ExecutionPlan;
        tokenSource.Cancel();
        var source = SeparatedValuesNativeTestSource.Create<string>(
            path,
            ",",
            true,
            2,
            RuntimeV2TestContexts.CreateExecutionContext(
                tokenSource.Token,
                [new SchemaColumn("Name", 0, typeof(string))],
                logger: mockLogger.Object,
                executionPlan: plan));

        var fired = source.Chunks.SelectMany(chunk => chunk).Count();

        Assert.AreEqual(0, fired);
    }

    [TestMethod]
    public void DescSchemaTest()
    {
        var query = "desc separatedvalues";

        var vm = CreateAndRunVirtualMachine(query);
        var table = vm.Run();

        Assert.AreEqual(4, table.Columns.Count());
        Assert.AreEqual(3, table.Count);
    }

    [TestMethod]
    public void DescMethodTest()
    {
        var query = "desc separatedvalues.comma";

        var vm = CreateAndRunVirtualMachine(query);
        var table = vm.Run();

        Assert.AreEqual(4, table.Columns.Count());
        Assert.AreEqual(1, table.Count);
    }

    [TestMethod]
    [DataRow("int")]
    [DataRow("int?")]
    public void TableDefinedSource_ReorderedSubset_ShouldRetainNullableDeclarationTypes(string integerType)
    {
        var path = Path.GetTempFileName();
        try
        {
            File.WriteAllText(path, "Id,Name,Ignored\n7, Ada ,x\n, Mira ,y\n");
            var query = $"table Contract {{ Name: string, Id: {integerType} }}; " +
                        "couple separatedvalues.comma with table Contract as People; " +
                        $"select Name as Name, Id as Id from People('{path.Replace("\\", "/").Replace("'", "''")}', true, 0) order by Name";
            using var compiled = CreateAndRunVirtualMachine(query);
            using var result = compiled.Run();
            Assert.AreEqual(typeof(string), result.Columns.ElementAt(0).ColumnType);
            Assert.AreEqual(typeof(int?), result.Columns.ElementAt(1).ColumnType);
            Assert.AreEqual("Name", result.Columns.ElementAt(0).ColumnName);
            Assert.AreEqual("Id", result.Columns.ElementAt(1).ColumnName);
            Assert.AreEqual(2, result.Count);
            Assert.AreEqual(" Ada ", result[0].ReadValues()[0]);
            Assert.AreEqual(7, result[0].ReadValues()[1]);
            Assert.AreEqual(" Mira ", result[1].ReadValues()[0]);
            Assert.IsNull(result[1].ReadValues()[1]);
        }
        finally
        {
            File.Delete(path);
        }
    }

    private CompiledQuery CreateAndRunVirtualMachine(string script)
    {
        return InstanceCreatorHelpers.CompileForExecution(script, Guid.NewGuid().ToString(), new CsvSchemaProvider(),
            EnvironmentVariablesHelpers.CreateMockedEnvironmentVariables());
    }

    private sealed class TestReadOnlyTable : IReadOnlyTable
    {
        private readonly IReadOnlyList<IReadOnlyRow> _rows;

        public TestReadOnlyTable(IEnumerable<object[]> rows)
        {
            _rows = rows.Select(row => new TestReadOnlyRow(row)).ToArray();
        }

        public int Count => _rows.Count;

        public IReadOnlyList<IReadOnlyRow> Rows => _rows;
    }

    private sealed class TestReadOnlyRow(object[] values) : IReadOnlyRow
    {
        public int Count => values.Length;
        public T GetValue<T>(int columnIndex) => (T)values[columnIndex];

        public void VisitColumns<TVisitor>(ref TVisitor visitor)
            where TVisitor : IColumnValueVisitor, allows ref struct
        {
            for (var index = 0; index < values.Length; index++)
            {
                switch (values[index])
                {
                    case string text: visitor.Visit(index, in text); break;
                    case bool flag: visitor.Visit(index, in flag); break;
                    case int number: visitor.Visit(index, in number); break;
                    default: throw new InvalidOperationException("Unsupported fixture column type.");
                }
            }
        }
    }
}
