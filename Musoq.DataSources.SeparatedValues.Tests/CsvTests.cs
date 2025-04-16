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
using Musoq.Evaluator.Tables;
using Musoq.Schema;

namespace Musoq.DataSources.SeparatedValues.Tests
{
    [TestClass]
    public class CsvTests
    {
        [TestMethod]
        public void ReplaceNotValidCharacters()
        {
            var columnName = SeparatedValuesHelper.MakeHeaderNameValidColumnName("#Column name 123 22@");

            Assert.AreEqual("ColumnName12322", columnName);
        }

        [TestMethod]
        public void SimpleSelectWithSkipLinesTest()
        {
            var query = "SELECT Name FROM #separatedvalues.comma('./Files/BankingTransactionsWithSkippedLines.csv', true, 2)";

            var vm = CreateAndRunVirtualMachine(query);
            var table = vm.Run();

            Assert.AreEqual(1, table.Columns.Count());
            Assert.AreEqual("Name", table.Columns.ElementAt(0).ColumnName);
            Assert.AreEqual(typeof(string), table.Columns.ElementAt(0).ColumnType);

            Assert.IsTrue(table.Count == 11, "Table should have 11 entries");

            Assert.IsTrue(table.Count(row => (string)row.Values[0] == "Salary") == 2, "Should have 2 'Salary' entries");
            Assert.IsTrue(table.Count(row => (string)row.Values[0] == "Restaurant A") == 2, "Should have 2 'Restaurant A' entries");
            Assert.IsTrue(table.Count(row => (string)row.Values[0] == "Bus ticket") == 2, "Should have 2 'Bus ticket' entries");
            Assert.IsTrue(table.Count(row => (string)row.Values[0] == "Tesco") == 2, "Should have 2 'Tesco' entries");
            Assert.IsTrue(table.Count(row => (string)row.Values[0] == "Restaurant B") == 2, "Should have 2 'Restaurant B' entries");
            Assert.IsTrue(table.Count(row => (string)row.Values[0] == "Service") == 1, "Should have 1 'Service' entry");
        }

        [TestMethod]
        public void SimpleSelectWithCouplingTableSyntaxSkipLinesTest()
        {
            var query = "" +
                "table CsvFile {" +
                "   Name 'System.String'" +
                "};" +
                "couple #separatedvalues.comma with table CsvFile as SourceCsvFile;" +
                "select Name from SourceCsvFile('./Files/BankingTransactionsWithSkippedLines.csv', true, 2);";

            var vm = CreateAndRunVirtualMachine(query);
            var table = vm.Run();

            Assert.AreEqual(1, table.Columns.Count());
            Assert.AreEqual("Name", table.Columns.ElementAt(0).ColumnName);
            Assert.AreEqual(typeof(string), table.Columns.ElementAt(0).ColumnType);
            
            Assert.IsTrue(table.Count == 11, "Table should have 11 entries");

            Assert.IsTrue(table.Count(row => (string)row.Values[0] == "Salary") == 2, "Should have 2 'Salary' entries");
            Assert.IsTrue(table.Count(row => (string)row.Values[0] == "Restaurant A") == 2, "Should have 2 'Restaurant A' entries");
            Assert.IsTrue(table.Count(row => (string)row.Values[0] == "Bus ticket") == 2, "Should have 2 'Bus ticket' entries");
            Assert.IsTrue(table.Count(row => (string)row.Values[0] == "Tesco") == 2, "Should have 2 'Tesco' entries");
            Assert.IsTrue(table.Count(row => (string)row.Values[0] == "Restaurant B") == 2, "Should have 2 'Restaurant B' entries");
            Assert.IsTrue(table.Count(row => (string)row.Values[0] == "Service") == 1, "Should have 1 'Service' entry");
        }

        [TestMethod]
        public void CheckTypesTest()
        {
            var query = "" +
                "table Persons {" +
                "   Id 'System.Int32'," +
                "   Name 'System.String'" +
                "};" +
                "couple #separatedvalues.comma with table Persons as SourceOfPersons;" +
                "select Id, Name from SourceOfPersons('./Files/Persons.csv', true, 0)";

            var vm = CreateAndRunVirtualMachine(query);
            var table = vm.Run();

            Assert.AreEqual(2, table.Columns.Count());
            Assert.AreEqual("Id", table.Columns.ElementAt(0).ColumnName);
            Assert.AreEqual(typeof(int?), table.Columns.ElementAt(0).ColumnType);
            Assert.AreEqual("Name", table.Columns.ElementAt(1).ColumnName);
            Assert.AreEqual(typeof(string), table.Columns.ElementAt(1).ColumnType);
            
            Assert.IsTrue(table.Count == 5, "Table should have 5 entries");

            Assert.IsTrue(table.Any(row => 
                (int)row.Values[0] == 1 && 
                (string)row.Values[1] == "Jan"
            ), "First entry should be 1, Jan");

            Assert.IsTrue(table.Any(row => 
                (int)row.Values[0] == 2 && 
                (string)row.Values[1] == "Marek"
            ), "Second entry should be 2, Marek");

            Assert.IsTrue(table.Any(row => 
                (int)row.Values[0] == 3 && 
                (string)row.Values[1] == "Witek"
            ), "Third entry should be 3, Witek");

            Assert.IsTrue(table.Any(row => 
                (int)row.Values[0] == 4 && 
                (string)row.Values[1] == "Anna"
            ), "Fourth entry should be 4, Anna");

            Assert.IsTrue(table.Any(row => 
                (int)row.Values[0] == 5 && 
                (string)row.Values[1] == "Anna"
            ), "Fifth entry should be 5, Anna");
        }

        [TestMethod]
        public void CheckNullValues()
        {
            var query = "" +
                "table BankingTransactions {" +
                "   Category 'string'," +
                "   Money 'decimal'" +
                "};" +
                "couple #separatedvalues.comma with table BankingTransactions as SourceOfBankingTransactions;" +
                "select Category, Money from SourceOfBankingTransactions('./Files/BankingTransactionsNullValues.csv', true, 0) where (Category is null) or (Money is null)";

            var vm = CreateAndRunVirtualMachine(query);
            var table = vm.Run();

            Assert.AreEqual(2, table.Columns.Count());
            Assert.AreEqual("Category", table.Columns.ElementAt(0).ColumnName);
            Assert.AreEqual(typeof(string), table.Columns.ElementAt(0).ColumnType);
            Assert.AreEqual("Money", table.Columns.ElementAt(1).ColumnName);
            Assert.AreEqual(typeof(decimal?), table.Columns.ElementAt(1).ColumnType);
            
            Assert.IsTrue(table.Count == 4, "Table should have 4 entries");

            Assert.IsTrue(table.Any(row => 
                (string)row.Values[0] == "Life" && 
                row.Values[1] == null
            ), "First entry should be Life, null");

            Assert.IsTrue(table.Any(row => 
                row.Values[0] == null && 
                (decimal?)row.Values[1] == -1m
            ), "Second entry should be null, -1");

            Assert.IsTrue(table.Any(row => 
                row.Values[0] == null && 
                (decimal?)row.Values[1] == -121.95m
            ), "Third entry should be null, -121.95");

            Assert.IsTrue(table.Any(row => 
                row.Values[0] == null && 
                row.Values[1] == null
            ), "Fourth entry should be null, null");
        }

        [TestMethod]
        public void SimpleSelectWithCouplingTableSyntaxSkipLinesTest2()
        {
            var query = "" +
                "table CsvFile {" +
                "   Name 'System.String'" +
                "};" +
                "couple #separatedvalues.comma with table CsvFile as SourceCsvFile;" +
                "with FilesToScan as (" +
                "   select './Files/BankingTransactionsWithSkippedLines.csv', true, 2 from #separatedvalues.empty()" +
                ")" +
                "select Name from SourceCsvFile(FilesToScan);";

            var vm = CreateAndRunVirtualMachine(query);
            var table = vm.Run();

            Assert.AreEqual(1, table.Columns.Count());
            Assert.AreEqual("Name", table.Columns.ElementAt(0).ColumnName);
            Assert.AreEqual(typeof(string), table.Columns.ElementAt(0).ColumnType);

            Assert.IsTrue(table.Count == 11, "Table should have 11 entries");

            Assert.IsTrue(table.Count(row => (string)row.Values[0] == "Salary") == 2, "Should have 2 'Salary' entries");
            Assert.IsTrue(table.Count(row => (string)row.Values[0] == "Restaurant A") == 2, "Should have 2 'Restaurant A' entries");
            Assert.IsTrue(table.Count(row => (string)row.Values[0] == "Bus ticket") == 2, "Should have 2 'Bus ticket' entries");
            Assert.IsTrue(table.Count(row => (string)row.Values[0] == "Tesco") == 2, "Should have 2 'Tesco' entries");
            Assert.IsTrue(table.Count(row => (string)row.Values[0] == "Restaurant B") == 2, "Should have 2 'Restaurant B' entries");
            Assert.IsTrue(table.Count(row => (string)row.Values[0] == "Service") == 1, "Should have 1 'Service' entry");
        }

        [TestMethod]
        public void SameFileReadDoubleTimesTest()
        {
            var query = "" +
                "table CsvFile {" +
                "   Name 'System.String'" +
                "};" +
                "couple #separatedvalues.comma with table CsvFile as SourceOfCsvFile;" +
                "with FilesToScan as (" +
                "   select './Files/BankingTransactionsWithSkippedLines.csv' as FileName, true, 2 from #separatedvalues.empty()" +
                "   union all (FileName) " +
                "   select './Files/BankingTransactionsWithSkippedLines.csv' as FileName, true, 2 from #separatedvalues.empty()" +
                ")" +
                "select Name from SourceOfCsvFile(FilesToScan);";

            var vm = CreateAndRunVirtualMachine(query);
            var table = vm.Run();

            Assert.AreEqual(1, table.Columns.Count());
            Assert.AreEqual("Name", table.Columns.ElementAt(0).ColumnName);
            Assert.AreEqual(typeof(string), table.Columns.ElementAt(0).ColumnType);

            Assert.IsTrue(table.Count == 22, "Table should have 22 entries");

            Assert.IsTrue(table.Count(row => (string)row.Values[0] == "Salary") == 4, "Should have 4 'Salary' entries");
            Assert.IsTrue(table.Count(row => (string)row.Values[0] == "Restaurant A") == 4, "Should have 4 'Restaurant A' entries");
            Assert.IsTrue(table.Count(row => (string)row.Values[0] == "Bus ticket") == 4, "Should have 4 'Bus ticket' entries");
            Assert.IsTrue(table.Count(row => (string)row.Values[0] == "Tesco") == 4, "Should have 4 'Tesco' entries");
            Assert.IsTrue(table.Count(row => (string)row.Values[0] == "Restaurant B") == 4, "Should have 4 'Restaurant B' entries");
            Assert.IsTrue(table.Count(row => (string)row.Values[0] == "Service") == 2, "Should have 2 'Service' entries");
        }

        [TestMethod]
        public void SimpleSelectTest()
        {
            var query = "SELECT Name FROM #separatedvalues.comma('./Files/BankingTransactions.csv', true, 0)";

            var vm = CreateAndRunVirtualMachine(query);
            var table = vm.Run();

            Assert.AreEqual(1, table.Columns.Count());
            Assert.AreEqual("Name", table.Columns.ElementAt(0).ColumnName);
            Assert.AreEqual(typeof(string), table.Columns.ElementAt(0).ColumnType);
            
            Assert.IsTrue(table.Count == 11, "Table should have 11 entries");

            Assert.IsTrue(table.Count(row => (string)row.Values[0] == "Salary") == 2, "Should have 2 'Salary' entries");
            Assert.IsTrue(table.Count(row => (string)row.Values[0] == "Restaurant A") == 2, "Should have 2 'Restaurant A' entries");
            Assert.IsTrue(table.Count(row => (string)row.Values[0] == "Bus ticket") == 2, "Should have 2 'Bus ticket' entries");
            Assert.IsTrue(table.Count(row => (string)row.Values[0] == "Tesco") == 2, "Should have 2 'Tesco' entries");
            Assert.IsTrue(table.Count(row => (string)row.Values[0] == "Restaurant B") == 2, "Should have 2 'Restaurant B' entries");
            Assert.IsTrue(table.Count(row => (string)row.Values[0] == "Service") == 1, "Should have 1 'Service' entry");
        }

        [TestMethod]
        public void SimpleSelectNoHeaderTest()
        {
            var query = "SELECT Column3 FROM #separatedvalues.comma('./Files/BankingTransactionsNoHeader.csv', false, 0)";

            var vm = CreateAndRunVirtualMachine(query);
            var table = vm.Run();

            Assert.AreEqual(1, table.Columns.Count());
            Assert.AreEqual("Column3", table.Columns.ElementAt(0).ColumnName);
            Assert.AreEqual(typeof(string), table.Columns.ElementAt(0).ColumnType);

            Assert.IsTrue(table.Count == 11, "Table should have 11 entries");

            Assert.IsTrue(table.Count(row => (string)row.Values[0] == "Salary") == 2, "Should have 2 'Salary' entries");
            Assert.IsTrue(table.Count(row => (string)row.Values[0] == "Restaurant A") == 2, "Should have 2 'Restaurant A' entries");
            Assert.IsTrue(table.Count(row => (string)row.Values[0] == "Bus ticket") == 2, "Should have 2 'Bus ticket' entries");
            Assert.IsTrue(table.Count(row => (string)row.Values[0] == "Tesco") == 2, "Should have 2 'Tesco' entries");
            Assert.IsTrue(table.Count(row => (string)row.Values[0] == "Restaurant B") == 2, "Should have 2 'Restaurant B' entries");
            Assert.IsTrue(table.Count(row => (string)row.Values[0] == "Service") == 1, "Should have 1 'Service' entry");
        }

        [TestMethod]
        public void SimpleCountTest()
        {
            var query = "SELECT Count(OperationDate) FROM #separatedvalues.comma('./Files/BankingTransactions.csv', true, 0)";

            var vm = CreateAndRunVirtualMachine(query);
            var table = vm.Run();

            Assert.AreEqual(1, table.Columns.Count());
            Assert.AreEqual("Count(OperationDate)", table.Columns.ElementAt(0).ColumnName);
            Assert.AreEqual(typeof(int), table.Columns.ElementAt(0).ColumnType);

            Assert.AreEqual(1, table.Count);
            Assert.AreEqual(11, table[0].Values[0]);
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
from #separatedvalues.comma('./Files/BankingTransactions.csv', true, 0) 
group by ExtractFromDate(OperationDate, 'month')";

            var vm = CreateAndRunVirtualMachine(query);
            var table = vm.Run();

            Assert.AreEqual(6, table.Columns.Count());
            Assert.AreEqual("Count(OperationDate, 1)", table.Columns.ElementAt(0).ColumnName);
            Assert.AreEqual(typeof(int), table.Columns.ElementAt(0).ColumnType);
            Assert.AreEqual("ExtractFromDate(OperationDate, month)", table.Columns.ElementAt(1).ColumnName);
            Assert.AreEqual(typeof(int), table.Columns.ElementAt(1).ColumnType);
            Assert.AreEqual("Count(OperationDate)", table.Columns.ElementAt(2).ColumnName);
            Assert.AreEqual(typeof(int), table.Columns.ElementAt(2).ColumnType);
            Assert.AreEqual("SumIncome(ToDecimal(Money))", table.Columns.ElementAt(3).ColumnName);
            Assert.AreEqual(typeof(decimal), table.Columns.ElementAt(3).ColumnType);
            Assert.AreEqual("SumOutcome(ToDecimal(Money))", table.Columns.ElementAt(4).ColumnName);
            Assert.AreEqual(typeof(decimal), table.Columns.ElementAt(4).ColumnType);
            Assert.AreEqual("SumIncome(ToDecimal(Money)) - Abs(SumOutcome(ToDecimal(Money)))",
                table.Columns.ElementAt(5).ColumnName);
            Assert.AreEqual(typeof(decimal), table.Columns.ElementAt(5).ColumnType);
            
            Assert.IsTrue(table.Count == 2, "Table should have 2 entries");

            Assert.IsTrue(table.Any(row => 
                (int)row.Values[0] == 11 &&
                (int)row.Values[1] == 1 &&
                (int)row.Values[2] == 6 &&
                (decimal)row.Values[3] == 4199m &&
                (decimal)row.Values[4] == -157.15m &&
                (decimal)row.Values[5] == 4041.85m
            ), "First entry does not match expected details");

            Assert.IsTrue(table.Any(row => 
                (int)row.Values[0] == 11 &&
                (int)row.Values[1] == 2 &&
                (int)row.Values[2] == 5 &&
                (decimal)row.Values[3] == 4000m &&
                (decimal)row.Values[4] == -157.15m &&
                (decimal)row.Values[5] == 3842.85m
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
from #separatedvalues.comma('./Files/Persons.csv', true, 0) persons 
inner join #separatedvalues.comma('./Files/Gradebook.csv', true, 0) grades on persons.Id = grades.PersonId";

            var vm = CreateAndRunVirtualMachine(query);
            var table = vm.Run();

            Assert.AreEqual(4, table.Columns.Count());

            Assert.AreEqual("persons.Name", table.Columns.ElementAt(0).ColumnName);
            Assert.AreEqual(typeof(string), table.Columns.ElementAt(0).ColumnType);
            Assert.AreEqual("persons.Surname", table.Columns.ElementAt(1).ColumnName);
            Assert.AreEqual(typeof(string), table.Columns.ElementAt(1).ColumnType);
            Assert.AreEqual("grades.Subject", table.Columns.ElementAt(2).ColumnName);
            Assert.AreEqual(typeof(string), table.Columns.ElementAt(2).ColumnType);
            Assert.AreEqual("ToDecimal(grades.Grade)", table.Columns.ElementAt(3).ColumnName);
            Assert.AreEqual(typeof(decimal?), table.Columns.ElementAt(3).ColumnType);

            Assert.IsTrue(table.Count == 24, "Table should contain exactly 24 records");

            // Jan Grzyb's records
            Assert.IsTrue(table.Count(r => 
                (string) r[0] == "Jan" && (string) r[1] == "Grzyb" && (string) r[2] == "Math" && (decimal)r[3] == 5m) == 1, 
                "Jan Grzyb should have exactly one Math grade of 5");
            Assert.IsTrue(table.Count(r => 
                (string) r[0] == "Jan" && (string) r[1] == "Grzyb" && (string) r[2] == "English" && (decimal)r[3] == 2m) == 1,
                "Jan Grzyb should have exactly one English grade of 2");
            Assert.IsTrue(table.Count(r => 
                (string) r[0] == "Jan" && (string) r[1] == "Grzyb" && (string) r[2] == "Math" && (decimal)r[3] == 4m) == 2,
                "Jan Grzyb should have exactly two Math grades of 4");
            Assert.IsTrue(table.Count(r => 
                (string) r[0] == "Jan" && (string) r[1] == "Grzyb" && (string) r[2] == "Biology" && (decimal)r[3] == 4m) == 1,
                "Jan Grzyb should have exactly one Biology grade of 4");
            Assert.IsTrue(table.Count(r => 
                (string) r[0] == "Jan" && (string) r[1] == "Grzyb" && (string) r[2] == "Biology" && (decimal)r[3] == 3m) == 1,
                "Jan Grzyb should have exactly one Biology grade of 3");

            // Marek Tarczynski's records
            Assert.IsTrue(table.Count(r => 
                (string) r[0] == "Marek" && (string) r[1] == "Tarczynski" && (string) r[2] == "Math" && (decimal)r[3] == 5m) == 1,
                "Marek Tarczynski should have exactly one Math grade of 5");
            Assert.IsTrue(table.Count(r => 
                (string) r[0] == "Marek" && (string) r[1] == "Tarczynski" && (string) r[2] == "English" && (decimal)r[3] == 2m) == 1,
                "Marek Tarczynski should have exactly one English grade of 2");

            // Witek Lechoń's records
            Assert.IsTrue(table.Count(r => 
                (string) r[0] == "Witek" && (string) r[1] == "Lechoń" && (string) r[2] == "Math" && (decimal)r[3] == 4m) == 2,
                "Witek Lechoń should have exactly two Math grades of 4");
            Assert.IsTrue(table.Count(r => 
                (string) r[0] == "Witek" && (string) r[1] == "Lechoń" && (string) r[2] == "Biology" && (decimal)r[3] == 4m) == 1,
                "Witek Lechoń should have exactly one Biology grade of 4");
            Assert.IsTrue(table.Count(r => 
                (string) r[0] == "Witek" && (string) r[1] == "Lechoń" && (string) r[2] == "Biology" && (decimal)r[3] == 3m) == 1,
                "Witek Lechoń should have exactly one Biology grade of 3");

            // Anna Rozmaryn's records
            Assert.IsTrue(table.Count(r => 
                (string) r[0] == "Anna" && (string) r[1] == "Rozmaryn" && (string) r[2] == "Math" && (decimal)r[3] == 5m) == 1,
                "Anna Rozmaryn should have exactly one Math grade of 5");
            Assert.IsTrue(table.Count(r => 
                (string) r[0] == "Anna" && (string) r[1] == "Rozmaryn" && (string) r[2] == "English" && (decimal)r[3] == 2m) == 1,
                "Anna Rozmaryn should have exactly one English grade of 2");
            Assert.IsTrue(table.Count(r => 
                (string) r[0] == "Anna" && (string) r[1] == "Rozmaryn" && (string) r[2] == "Math" && (decimal)r[3] == 4m) == 2,
                "Anna Rozmaryn should have exactly two Math grades of 4");
            Assert.IsTrue(table.Count(r => 
                (string) r[0] == "Anna" && (string) r[1] == "Rozmaryn" && (string) r[2] == "Biology" && (decimal)r[3] == 4m) == 1,
                "Anna Rozmaryn should have exactly one Biology grade of 4");
            Assert.IsTrue(table.Count(r => 
                (string) r[0] == "Anna" && (string) r[1] == "Rozmaryn" && (string) r[2] == "Biology" && (decimal)r[3] == 3m) == 1,
                "Anna Rozmaryn should have exactly one Biology grade of 3");

            // Anna Trzpień's records
            Assert.IsTrue(table.Count(r => 
                (string) r[0] == "Anna" && (string) r[1] == "Trzpień" && (string) r[2] == "Math" && (decimal)r[3] == 5m) == 1,
                "Anna Trzpień should have exactly one Math grade of 5");
            Assert.IsTrue(table.Count(r => 
                (string) r[0] == "Anna" && (string) r[1] == "Trzpień" && (string) r[2] == "English" && (decimal)r[3] == 2m) == 1,
                "Anna Trzpień should have exactly one English grade of 2");
            Assert.IsTrue(table.Count(r => 
                (string) r[0] == "Anna" && (string) r[1] == "Trzpień" && (string) r[2] == "Math" && (decimal)r[3] == 4m) == 2,
                "Anna Trzpień should have exactly two Math grades of 4");
            Assert.IsTrue(table.Count(r => 
                (string) r[0] == "Anna" && (string) r[1] == "Trzpień" && (string) r[2] == "Biology" && (decimal)r[3] == 4m) == 1,
                "Anna Trzpień should have exactly one Biology grade of 4");
            Assert.IsTrue(table.Count(r => 
                (string) r[0] == "Anna" && (string) r[1] == "Trzpień" && (string) r[2] == "Biology" && (decimal)r[3] == 3m) == 1,
                "Anna Trzpień should have exactly one Biology grade of 3");
        }

        [TestMethod]
        public void RichStatsFakeBankFile1Test()
        {
            var query = @"
with BasicIndicators as (
	select 
		ExtractFromDate(DateTime, 'month') as 'Month', 
		ClusteredByContainsKey('./Files/Categories.txt', ChargeName) as 'Category', 
		SumIncome(ToDecimal(Amount)) as Income, 
		SumIncome(ToDecimal(Amount), 1) as 'MonthlyIncome',
		Round(PercentOf(Abs(SumOutcome(ToDecimal(Amount))), SumIncome(ToDecimal(Amount), 1)), 2) as 'PercOfOutForOvInc',	
		SumOutcome(ToDecimal(Amount)) as Outcome, 
		SumOutcome(ToDecimal(Amount), 1) as 'MonthlyOutcome',
		SumIncome(ToDecimal(Amount), 1) + SumOutcome(ToDecimal(Amount), 1) as 'MoneysLeft',
		SumIncome(ToDecimal(Amount), 2) + SumOutcome(ToDecimal(Amount), 2) as 'OvMoneysLeft'
	from #separatedvalues.comma('./Files/FakeBankingTransactions.csv', true, 0) as csv
	group by 
		ExtractFromDate(DateTime, 'month'), 
		ClusteredByContainsKey('./Files/Categories.txt', ChargeName)
), AggregatedCategories as (
	select Category, Sum(Outcome) as 'CategoryOutcome' from BasicIndicators group by Category
)
select
	bi.Month as Month,
	bi.Category as Category,
	bi.Income as Income,
	bi.MonthlyIncome as 'Monthly Income',
	bi.PercOfOutForOvInc as '% Of Out. for ov. inc.',
	bi.Outcome as Outcome,
	bi.MonthlyOutcome as 'Monthly Outcome',
	bi.MoneysLeft as 'Moneys Left',
	bi.OvMoneysLeft as 'Ov. Moneys Left',
	ac.CategoryOutcome as 'Ov. Categ. Outcome',
    ((bi.MonthlyIncome - bi.MonthlyOutcome) / bi.MonthlyIncome) as 'Saving Coeff'
from BasicIndicators bi inner join AggregatedCategories ac on bi.Category = ac.Category";

            var vm = CreateAndRunVirtualMachine(query);
            var table = vm.Run();

            Assert.AreEqual(11, table.Columns.Count());

            Assert.AreEqual("Month", table.Columns.ElementAt(0).ColumnName);
            Assert.AreEqual(typeof(int), table.Columns.ElementAt(0).ColumnType);
            Assert.AreEqual(0, table.Columns.ElementAt(0).ColumnIndex);

            Assert.AreEqual("Category", table.Columns.ElementAt(1).ColumnName);
            Assert.AreEqual(typeof(string), table.Columns.ElementAt(1).ColumnType);
            Assert.AreEqual(1, table.Columns.ElementAt(1).ColumnIndex);

            Assert.AreEqual("Income", table.Columns.ElementAt(2).ColumnName);
            Assert.AreEqual(typeof(decimal), table.Columns.ElementAt(2).ColumnType);
            Assert.AreEqual(2, table.Columns.ElementAt(2).ColumnIndex);

            Assert.AreEqual("Monthly Income", table.Columns.ElementAt(3).ColumnName);
            Assert.AreEqual(typeof(decimal), table.Columns.ElementAt(3).ColumnType);
            Assert.AreEqual(3, table.Columns.ElementAt(3).ColumnIndex);

            Assert.AreEqual("% Of Out. for ov. inc.", table.Columns.ElementAt(4).ColumnName);
            Assert.AreEqual(typeof(decimal?), table.Columns.ElementAt(4).ColumnType);
            Assert.AreEqual(4, table.Columns.ElementAt(4).ColumnIndex);

            Assert.AreEqual("Outcome", table.Columns.ElementAt(5).ColumnName);
            Assert.AreEqual(typeof(decimal), table.Columns.ElementAt(5).ColumnType);
            Assert.AreEqual(5, table.Columns.ElementAt(5).ColumnIndex);

            Assert.AreEqual("Monthly Outcome", table.Columns.ElementAt(6).ColumnName);
            Assert.AreEqual(typeof(decimal), table.Columns.ElementAt(6).ColumnType);
            Assert.AreEqual(6, table.Columns.ElementAt(6).ColumnIndex);

            Assert.AreEqual("Moneys Left", table.Columns.ElementAt(7).ColumnName);
            Assert.AreEqual(typeof(decimal), table.Columns.ElementAt(7).ColumnType);
            Assert.AreEqual(7, table.Columns.ElementAt(7).ColumnIndex);

            Assert.AreEqual("Ov. Moneys Left", table.Columns.ElementAt(8).ColumnName);
            Assert.AreEqual(typeof(decimal), table.Columns.ElementAt(8).ColumnType);
            Assert.AreEqual(8, table.Columns.ElementAt(8).ColumnIndex);

            Assert.AreEqual("Ov. Categ. Outcome", table.Columns.ElementAt(9).ColumnName);
            Assert.AreEqual(typeof(decimal), table.Columns.ElementAt(9).ColumnType);
            Assert.AreEqual(9, table.Columns.ElementAt(9).ColumnIndex);

            Assert.AreEqual("Saving Coeff", table.Columns.ElementAt(10).ColumnName);
            Assert.AreEqual(typeof(decimal), table.Columns.ElementAt(10).ColumnType);
            Assert.AreEqual(10, table.Columns.ElementAt(10).ColumnIndex);

            Assert.AreEqual(48, table.Count);
        }

        [TestMethod]
        public void RichStatsFakeBankFile2Test()
        {
            var query = @"
with BasicIndicators as (
	select 
		ExtractFromDate(DateTime, 'month') as 'Month', 
		ClusteredByContainsKey('./Files/Categories.txt', ChargeName) as 'Category', 
		SumIncome(ToDecimal(Amount)) as Income, 
		SumIncome(ToDecimal(Amount), 1) as 'MonthlyIncome',
		Round(PercentOf(Abs(SumOutcome(ToDecimal(Amount))), SumIncome(ToDecimal(Amount), 1)), 2) as 'PercOfOutForOvInc',	
		SumOutcome(ToDecimal(Amount)) as Outcome, 
		SumOutcome(ToDecimal(Amount), 1) as 'MonthlyOutcome',
		SumIncome(ToDecimal(Amount), 1) + SumOutcome(ToDecimal(Amount), 1) as 'MoneysLeft',
		SumIncome(ToDecimal(Amount), 2) + SumOutcome(ToDecimal(Amount), 2) as 'OvMoneysLeft'
	from #separatedvalues.comma('./Files/FakeBankingTransactions.csv', true, 0) as csv
	group by 
		ExtractFromDate(DateTime, 'month'), 
		ClusteredByContainsKey('./Files/Categories.txt', ChargeName)
), AggregatedCategories as (
	select Category, Sum(Outcome) as 'CategoryOutcome' from BasicIndicators group by Category
)
select
	BasicIndicators.Month,
	BasicIndicators.Category,
	BasicIndicators.Income,
	BasicIndicators.MonthlyIncome as 'Monthly Income',
	BasicIndicators.PercOfOutForOvInc as '% Of Out. for ov. inc.',
	BasicIndicators.Outcome,
	BasicIndicators.MonthlyOutcome as 'Monthly Outcome',
	BasicIndicators.MoneysLeft as 'Moneys Left',
	BasicIndicators.OvMoneysLeft as 'Ov. Moneys Left',
	AggregatedCategories.CategoryOutcome as 'Ov. Categ. Outcome',
    ((BasicIndicators.MonthlyIncome - BasicIndicators.MonthlyOutcome) / BasicIndicators.MonthlyIncome) as 'Saving Coeff'
from BasicIndicators inner join AggregatedCategories on BasicIndicators.Category = AggregatedCategories.Category";

            var vm = CreateAndRunVirtualMachine(query);
            var table = vm.Run();

            Assert.AreEqual(11, table.Columns.Count());

            Assert.AreEqual("BasicIndicators.Month", table.Columns.ElementAt(0).ColumnName);
            Assert.AreEqual(typeof(int), table.Columns.ElementAt(0).ColumnType);
            Assert.AreEqual(0, table.Columns.ElementAt(0).ColumnIndex);

            Assert.AreEqual("BasicIndicators.Category", table.Columns.ElementAt(1).ColumnName);
            Assert.AreEqual(typeof(string), table.Columns.ElementAt(1).ColumnType);
            Assert.AreEqual(1, table.Columns.ElementAt(1).ColumnIndex);

            Assert.AreEqual("BasicIndicators.Income", table.Columns.ElementAt(2).ColumnName);
            Assert.AreEqual(typeof(decimal), table.Columns.ElementAt(2).ColumnType);
            Assert.AreEqual(2, table.Columns.ElementAt(2).ColumnIndex);

            Assert.AreEqual("Monthly Income", table.Columns.ElementAt(3).ColumnName);
            Assert.AreEqual(typeof(decimal), table.Columns.ElementAt(3).ColumnType);
            Assert.AreEqual(3, table.Columns.ElementAt(3).ColumnIndex);

            Assert.AreEqual("% Of Out. for ov. inc.", table.Columns.ElementAt(4).ColumnName);
            Assert.AreEqual(typeof(decimal?), table.Columns.ElementAt(4).ColumnType);
            Assert.AreEqual(4, table.Columns.ElementAt(4).ColumnIndex);

            Assert.AreEqual("BasicIndicators.Outcome", table.Columns.ElementAt(5).ColumnName);
            Assert.AreEqual(typeof(decimal), table.Columns.ElementAt(5).ColumnType);
            Assert.AreEqual(5, table.Columns.ElementAt(5).ColumnIndex);

            Assert.AreEqual("Monthly Outcome", table.Columns.ElementAt(6).ColumnName);
            Assert.AreEqual(typeof(decimal), table.Columns.ElementAt(6).ColumnType);
            Assert.AreEqual(6, table.Columns.ElementAt(6).ColumnIndex);

            Assert.AreEqual("Moneys Left", table.Columns.ElementAt(7).ColumnName);
            Assert.AreEqual(typeof(decimal), table.Columns.ElementAt(7).ColumnType);
            Assert.AreEqual(7, table.Columns.ElementAt(7).ColumnIndex);

            Assert.AreEqual("Ov. Moneys Left", table.Columns.ElementAt(8).ColumnName);
            Assert.AreEqual(typeof(decimal), table.Columns.ElementAt(8).ColumnType);
            Assert.AreEqual(8, table.Columns.ElementAt(8).ColumnIndex);

            Assert.AreEqual("Ov. Categ. Outcome", table.Columns.ElementAt(9).ColumnName);
            Assert.AreEqual(typeof(decimal), table.Columns.ElementAt(9).ColumnType);
            Assert.AreEqual(9, table.Columns.ElementAt(9).ColumnIndex);

            Assert.AreEqual("Saving Coeff", table.Columns.ElementAt(10).ColumnName);
            Assert.AreEqual(typeof(decimal), table.Columns.ElementAt(10).ColumnType);
            Assert.AreEqual(10, table.Columns.ElementAt(10).ColumnIndex);

            Assert.AreEqual(48, table.Count);
        }

        [TestMethod]
        public void CoupledQueryInsideCteTest()
        {
            var query = File.ReadAllText("./Test1/Query.txt");

            var vm = CreateAndRunVirtualMachine(query);
            var table = vm.Run();
            
            Assert.IsTrue(table.Count == 6, "Table should contain exactly 6 records");

            // file1 records
            Assert.IsTrue(table.Any(r => 
                    (string)r[0] == "file1" && (decimal)r[1] == 5m && (string)r[2] == "1 row of file1"),
                "Missing first row of file1 with value 5");

            Assert.IsTrue(table.Any(r => 
                    (string)r[0] == "file1" && (decimal)r[1] == 3m && (string)r[2] == "2 row of file1"),
                "Missing second row of file1 with value 3");

            Assert.IsTrue(table.Any(r => 
                    (string)r[0] == "file1" && (decimal)r[1] == 16m && (string)r[2] == "3 row of file1"),
                "Missing third row of file1 with value 16");

            // file2 records
            Assert.IsTrue(table.Any(r => 
                    (string)r[0] == "file2" && (decimal)r[1] == 11m && (string)r[2] == "1 row of file2"),
                "Missing first row of file2 with value 11");

            Assert.IsTrue(table.Any(r => 
                    (string)r[0] == "file2" && (decimal)r[1] == 12m && (string)r[2] == "2 row of file2"),
                "Missing second row of file2 with value 12");

            Assert.IsTrue(table.Any(r => 
                    (string)r[0] == "file2" && (decimal)r[1] == 15m && (string)r[2] == "3 row of file2"),
                "Missing third row of file2 with value 15");
        }

        [TestMethod]
        public void CsvSource_CancelledLoadTest()
        {
            var mockLogger = new Mock<ILogger>();
            using var tokenSource = new CancellationTokenSource();
            tokenSource.Cancel();
            var source = new SeparatedValuesFromFileRowsSource(
                "./Files/BankingTransactionsWithSkippedLines.csv", 
                ",", 
                true, 
                2, 
                tokenSource.Token)
            {
                RuntimeContext = new RuntimeContext(
                    tokenSource.Token, 
                    Array.Empty<ISchemaColumn>(), 
                    new Dictionary<string, string>(),
                    (null, null, null, false),
                    mockLogger.Object)
            };

            var fired = source.Rows.Count();

            Assert.AreEqual(0, fired);
        }

        [TestMethod]
        public void CsvSource_AllTypesSupportedTest()
        {
            var mockLogger = new Mock<ILogger>();
            using var tokenSource = new CancellationTokenSource();
            var columns = new List<ISchemaColumn>
            {
                new Column("boolColumn", typeof(bool?), 0),
                new Column("byteColumn", typeof(byte?), 1),
                new Column("charColumn", typeof(char?), 2),
                new Column("dateTimeColumn", typeof(DateTime?), 3),
                new Column("decimalColumn", typeof(decimal?), 4),
                new Column("doubleColumn", typeof(double?), 5),
                new Column("shortColumn", typeof(short?), 6),
                new Column("intColumn", typeof(int?), 7),
                new Column("longColumn", typeof(long?), 8),
                new Column("sbyteColumn", typeof(sbyte?), 9),
                new Column("singleColumn", typeof(float?), 10),
                new Column("stringColumn", typeof(string), 11),
                new Column("ushortColumn", typeof(ushort?), 12),
                new Column("uintColumn", typeof(uint?), 13),
                new Column("ulongColumn", typeof(ulong?), 14)
            };

            var context = new RuntimeContext(
                tokenSource.Token, 
                columns, 
                new Dictionary<string, string>(),
                (null, null, null, false),
                mockLogger.Object);

            var source = new SeparatedValuesFromFileRowsSource("./Files/AllTypes.csv", ",", true, 0, tokenSource.Token)
            {
                RuntimeContext = context
            };

            var rows = source.Rows;

            var row = rows.ElementAt(0);

            Assert.AreEqual(true, row[0]);
            Assert.AreEqual((byte)48, row[1]);
            Assert.AreEqual('c', row[2]);
            Assert.AreEqual(DateTime.Parse("12/12/2012"), row[3]);
            Assert.AreEqual(10.23m, row[4]);
            Assert.AreEqual(13.111d, row[5]);
            Assert.AreEqual((short)-15, row[6]);
            Assert.AreEqual(2147483647, row[7]);
            Assert.AreEqual(9223372036854775807, row[8]);
            Assert.AreEqual((sbyte)-3, row[9]);
            Assert.AreEqual(1.11f, row[10]);
            Assert.AreEqual("some text", row[11]);
            Assert.AreEqual((ushort)256, row[12]);
            Assert.AreEqual((uint)512, row[13]);
            Assert.AreEqual((ulong)1024, row[14]);
        }

        [TestMethod]
        public void DescSchemaTest()
        {
            var query = "desc #separatedvalues";

            var vm = CreateAndRunVirtualMachine(query);
            var table = vm.Run();

            Assert.AreEqual(5, table.Columns.Count());
            Assert.AreEqual(4, table.Count);
        }

        [TestMethod]
        public void DescMethodTest()
        {
            var query = "desc #separatedvalues.comma";

            var vm = CreateAndRunVirtualMachine(query);
            var table = vm.Run();

            Assert.AreEqual(5, table.Columns.Count());
            Assert.AreEqual(1, table.Count);
        }

        [TestMethod]
        public void CsvSource_FullLoadTest()
        {
            var mockLogger = new Mock<ILogger>();
            var source = new SeparatedValuesFromFileRowsSource("./Files/BankingTransactionsWithSkippedLines.csv", ",", true, 2, CancellationToken.None)
            {
                RuntimeContext = new RuntimeContext(
                    CancellationToken.None, 
                    Array.Empty<ISchemaColumn>(), 
                    new Dictionary<string, string>(),
                    (null, null, null, false),
                    mockLogger.Object)
            };

            var fired = source.Rows.Count();

            Assert.AreEqual(11, fired);
        }

        private CompiledQuery CreateAndRunVirtualMachine(string script)
        {
            return InstanceCreatorHelpers.CompileForExecution(script, Guid.NewGuid().ToString(), new CsvSchemaProvider(), EnvironmentVariablesHelpers.CreateMockedEnvironmentVariables());
        }

        static CsvTests()
        {
            Culture.Apply(CultureInfo.GetCultureInfo("en-EN"));
        }
    }
}