using System.Globalization;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Musoq.DataSources.RepresentativeTests.Components;
using Musoq.DataSources.Tests.Common;
using Musoq.Evaluator;

namespace Musoq.DataSources.RepresentativeTests;

/// <summary>
/// Representative query tests showcasing the power and versatility of Musoq.
/// These tests serve as working examples for documentation and can be used in README files.
/// </summary>
[TestClass]
public class RepresentativeQueryTests
{
    #region File System Queries (#os)

    /// <summary>
    /// Demonstrates listing files with their sizes.
    /// Query: SELECT files from a directory with size information.
    /// </summary>
    [TestMethod]
    public void FileSystem_ListFilesWithSize_ShouldReturnFilesWithSizeInformation()
    {
        var query = """
            select 
                Name,
                Length as SizeInBytes
            from #os.files('./Files', false)
            """;

        var vm = CreateAndRunVirtualMachine(query);
        var table = vm.Run();

        Assert.IsTrue(table.Count > 0, "Should return at least one file");
        Assert.AreEqual(2, table.Columns.Count());
        Assert.AreEqual("Name", table.Columns.ElementAt(0).ColumnName);
        Assert.AreEqual("SizeInBytes", table.Columns.ElementAt(1).ColumnName);
    }

    /// <summary>
    /// Demonstrates filtering files by extension.
    /// Query: Find all CSV files in a directory.
    /// </summary>
    [TestMethod]
    public void FileSystem_FilterByExtension_ShouldReturnOnlyMatchingFiles()
    {
        var query = """
            select Name, Extension
            from #os.files('./Files', false)
            where Extension = '.csv'
            """;

        var vm = CreateAndRunVirtualMachine(query);
        var table = vm.Run();

        Assert.IsTrue(table.Count > 0, "Should find CSV files");
        Assert.IsTrue(table.All(row => (string)row.Values[1] == ".csv"), "All files should be CSV");
    }

    /// <summary>
    /// Demonstrates calculating SHA256 hash of files.
    /// Query: Compute file hashes for integrity verification.
    /// </summary>
    [TestMethod]
    public void FileSystem_Sha256Hash_ShouldComputeFileHashes()
    {
        var query = """
            select 
                Name,
                Sha256File() as Hash
            from #os.files('./Files', false)
            where Name = 'Transactions.csv'
            """;

        var vm = CreateAndRunVirtualMachine(query);
        var table = vm.Run();

        Assert.AreEqual(1, table.Count);
        Assert.IsNotNull(table[0][1], "Hash should not be null");
        Assert.AreEqual(64, ((string)table[0][1]).Length, "SHA256 hash should be 64 characters");
    }

    /// <summary>
    /// Demonstrates listing directories.
    /// Query: Find all subdirectories.
    /// </summary>
    [TestMethod]
    public void FileSystem_ListDirectories_ShouldReturnDirectories()
    {
        var query = """
            select Name
            from #os.directories('./', false)
            """;

        var vm = CreateAndRunVirtualMachine(query);
        var table = vm.Run();

        Assert.IsTrue(table.Count > 0, "Should find directories");
    }

    #endregion

    #region CSV/Separated Values Queries (#separatedvalues)

    /// <summary>
    /// Demonstrates basic CSV querying.
    /// Query: Select specific columns from a CSV file.
    /// </summary>
    [TestMethod]
    public void Csv_BasicSelect_ShouldReturnColumns()
    {
        var query = """
            select Date, Description, Amount
            from #separatedvalues.comma('./Files/Transactions.csv', true, 0)
            """;

        var vm = CreateAndRunVirtualMachine(query);
        var table = vm.Run();

        Assert.AreEqual(10, table.Count, "Should have 10 transactions");
        Assert.AreEqual(3, table.Columns.Count());
    }

    /// <summary>
    /// Demonstrates aggregation on CSV data.
    /// Query: Calculate total income and expenses.
    /// </summary>
    [TestMethod]
    public void Csv_Aggregation_ShouldCalculateTotals()
    {
        var query = """
            select 
                SumIncome(ToDecimal(Amount)) as TotalIncome,
                SumOutcome(ToDecimal(Amount)) as TotalExpenses,
                SumIncome(ToDecimal(Amount)) + SumOutcome(ToDecimal(Amount)) as NetBalance
            from #separatedvalues.comma('./Files/Transactions.csv', true, 0)
            """;

        var vm = CreateAndRunVirtualMachine(query);
        var table = vm.Run();

        Assert.AreEqual(1, table.Count);
        Assert.AreEqual(8000m, table[0][0], "Total income should be 8000");
        Assert.AreEqual(-472.50m, table[0][1], "Total expenses should be -472.50");
        Assert.AreEqual(7527.50m, table[0][2], "Net balance should be 7527.50");
    }

    /// <summary>
    /// Demonstrates filtering on CSV data.
    /// Query: Find transactions above a certain amount.
    /// </summary>
    [TestMethod]
    public void Csv_Filtering_ShouldReturnMatchingRows()
    {
        var query = """
            select Description, ToDecimal(Amount) as Amount
            from #separatedvalues.comma('./Files/Transactions.csv', true, 0)
            where ToDecimal(Amount) > 0
            """;

        var vm = CreateAndRunVirtualMachine(query);
        var table = vm.Run();

        Assert.AreEqual(2, table.Count, "Should have 2 salary payments");
        Assert.IsTrue(table.All(row => (decimal)row.Values[1] > 0), "All amounts should be positive");
    }

    /// <summary>
    /// Demonstrates joining two CSV files.
    /// Query: Join employees with their project assignments.
    /// </summary>
    [TestMethod]
    public void Csv_JoinTables_ShouldCombineData()
    {
        var query = """
            select 
                emp.Name,
                emp.Department,
                proj.ProjectName,
                proj.Hours
            from #separatedvalues.comma('./Files/Employees.csv', true, 0) emp
            inner join #separatedvalues.comma('./Files/Projects.csv', true, 0) proj 
                on emp.Id = proj.EmployeeId
            order by emp.Name
            """;

        var vm = CreateAndRunVirtualMachine(query);
        var table = vm.Run();

        Assert.IsTrue(table.Count >= 5, "Should have at least 5 project assignments");
        Assert.AreEqual(4, table.Columns.Count());
    }

    /// <summary>
    /// Demonstrates grouping and counting.
    /// Query: Count employees per department.
    /// </summary>
    [TestMethod]
    public void Csv_GroupByCount_ShouldCountPerGroup()
    {
        var query = """
            select 
                Department,
                Count(Name) as EmployeeCount
            from #separatedvalues.comma('./Files/Employees.csv', true, 0)
            group by Department
            """;

        var vm = CreateAndRunVirtualMachine(query);
        var table = vm.Run();

        Assert.AreEqual(3, table.Count, "Should have 3 departments");
        
        var engineeringRow = table.First(r => (string)r[0] == "Engineering");
        Assert.AreEqual(3, engineeringRow[1], "Engineering should have 3 employees");
    }

    /// <summary>
    /// Demonstrates typed CSV with schema definition.
    /// Query: Read CSV with explicit column types.
    /// </summary>
    [TestMethod]
    public void Csv_TypedQuery_ShouldUseCorrectTypes()
    {
        var query = """
            table Employees {
               Id 'System.Int32',
               Name 'System.String',
               Department 'System.String'
            };
            couple #separatedvalues.comma with table Employees as SourceOfEmployees;
            select Id, Name, Department 
            from SourceOfEmployees('./Files/Employees.csv', true, 0)
            where Id > 2
            """;

        var vm = CreateAndRunVirtualMachine(query);
        var table = vm.Run();

        Assert.AreEqual(3, table.Count, "Should have 3 employees with Id > 2");
        Assert.AreEqual(typeof(int?), table.Columns.ElementAt(0).ColumnType, "Id should be nullable int");
    }

    #endregion

    #region Time Queries (#time)

    /// <summary>
    /// Demonstrates generating a date range.
    /// Query: Generate all days in a month.
    /// </summary>
    [TestMethod]
    public void Time_GenerateDateRange_ShouldCreateSequence()
    {
        // Using ISO 8601 format which works across cultures
        var query = """
            select Day, Month, Year
            from #time.interval('2024-01-01 00:00:00', '2024-01-31 00:00:00', 'days')
            order by Day
            """;

        var vm = CreateAndRunVirtualMachine(query);
        var table = vm.Run();

        Assert.AreEqual(31, table.Count, "January has 31 days");
        Assert.AreEqual(1, table[0][0], "First day should be 1");
        Assert.AreEqual(31, table[30][0], "Last day should be 31");
    }

    /// <summary>
    /// Demonstrates filtering time intervals.
    /// Query: Find weekend days (Saturday=6, Sunday=0 in DayOfWeek enum).
    /// </summary>
    [TestMethod]
    public void Time_FilterWeekends_ShouldReturnOnlySaturdaysAndSundays()
    {
        // DayOfWeek is an int: Sunday=0, Monday=1, ..., Saturday=6
        // Using ISO 8601 format which works across cultures
        // January 1, 2024 was a Monday, so we get: Mon(1), Tue(2), Wed(3), Thu(4), Fri(5), Sat(6), Sun(7)
        var query = """
            select Day, DayOfWeek
            from #time.interval('2024-01-01 00:00:00', '2024-01-07 00:00:00', 'days')
            where DayOfWeek = 0 or DayOfWeek = 6
            """;

        var vm = CreateAndRunVirtualMachine(query);
        var table = vm.Run();

        Assert.AreEqual(2, table.Count, "First week of 2024 has 2 weekend days");
        Assert.IsTrue(table.Any(r => (int)r[1] == 6), "Should include Saturday (6)");
        Assert.IsTrue(table.Any(r => (int)r[1] == 0), "Should include Sunday (0)");
    }

    #endregion

    #region System Queries (#system)

    /// <summary>
    /// Demonstrates number range generation.
    /// Query: Generate sequence of numbers.
    /// </summary>
    [TestMethod]
    public void System_Range_ShouldGenerateSequence()
    {
        var query = """
            select Value 
            from #system.range(1, 11)
            where Value % 2 = 0
            """;

        var vm = CreateAndRunVirtualMachine(query);
        var table = vm.Run();

        Assert.AreEqual(5, table.Count, "Should have 5 even numbers from 1-10");
        Assert.IsTrue(table.All(r => (long)r[0] % 2 == 0), "All values should be even");
    }

    /// <summary>
    /// Demonstrates dual table for calculations.
    /// Query: Perform single-row calculations.
    /// </summary>
    [TestMethod]
    public void System_Dual_ShouldPerformCalculations()
    {
        var query = """
            select 
                2 + 2 as Addition,
                10 * 5 as Multiplication,
                ToDecimal(7) / 3 as Division
            from #system.dual()
            """;

        var vm = CreateAndRunVirtualMachine(query);
        var table = vm.Run();

        Assert.AreEqual(1, table.Count);
        Assert.AreEqual(4, table[0][0], "2+2 should equal 4");
        Assert.AreEqual(50, table[0][1], "10*5 should equal 50");
        Assert.IsTrue((decimal)table[0][2] > 2.33m && (decimal)table[0][2] < 2.34m, "7/3 should be approximately 2.333");
    }

    /// <summary>
    /// Demonstrates union with dual table.
    /// Query: Combine multiple single-row results.
    /// </summary>
    [TestMethod]
    public void System_DualUnion_ShouldCombineRows()
    {
        var query = """
            select 'Option A' as Option, 100 as Value from #system.dual()
            union (Option)
            select 'Option B' as Option, 200 as Value from #system.dual()
            union (Option)
            select 'Option C' as Option, 300 as Value from #system.dual()
            """;

        var vm = CreateAndRunVirtualMachine(query);
        var table = vm.Run();

        Assert.AreEqual(3, table.Count, "Should have 3 options");
    }

    #endregion

    #region JSON Queries (#json)

    /// <summary>
    /// Demonstrates basic JSON querying.
    /// Query: Extract data from JSON array.
    /// </summary>
    [TestMethod]
    public void Json_BasicQuery_ShouldExtractData()
    {
        var query = """
            select Name, Age
            from #json.file('./Files/People.json', './Files/People.schema.json')
            """;

        var vm = CreateAndRunVirtualMachine(query);
        var table = vm.Run();

        Assert.AreEqual(3, table.Count, "Should have 3 people");
        Assert.IsTrue(table.Any(r => (string)r[0] == "Alice" && (long)r[1] == 28), "Should find Alice, age 28");
    }

    /// <summary>
    /// Demonstrates filtering JSON data.
    /// Query: Find people above a certain age.
    /// </summary>
    [TestMethod]
    public void Json_Filtering_ShouldReturnMatchingRecords()
    {
        var query = """
            select Name, Age
            from #json.file('./Files/People.json', './Files/People.schema.json')
            where Age > 30
            """;

        var vm = CreateAndRunVirtualMachine(query);
        var table = vm.Run();

        Assert.AreEqual(2, table.Count, "Should find 2 people over 30");
        Assert.IsTrue(table.All(r => (long)r[1] > 30), "All ages should be over 30");
    }

    /// <summary>
    /// Demonstrates array length in JSON.
    /// Query: Count skills per person.
    /// </summary>
    [TestMethod]
    public void Json_ArrayLength_ShouldCountItems()
    {
        var query = """
            select Name, Length(Skills) as SkillCount
            from #json.file('./Files/People.json', './Files/People.schema.json')
            """;

        var vm = CreateAndRunVirtualMachine(query);
        var table = vm.Run();

        Assert.AreEqual(3, table.Count);
        
        var bobRow = table.First(r => (string)r[0] == "Bob");
        Assert.AreEqual(3, bobRow[1], "Bob should have 3 skills");
        
        var charlieRow = table.First(r => (string)r[0] == "Charlie");
        Assert.AreEqual(0, charlieRow[1], "Charlie should have 0 skills");
    }

    #endregion

    #region Combined Queries (Multiple Data Sources)

    /// <summary>
    /// Demonstrates combining file system with CSV.
    /// Query: Read CSV files discovered in a directory.
    /// </summary>
    [TestMethod]
    public void Combined_FileSystemWithCsv_ShouldWorkTogether()
    {
        var query = """
            select 
                Name as FileName,
                Extension
            from #os.files('./Files', false)
            where Extension = '.csv'
            order by Name
            """;

        var vm = CreateAndRunVirtualMachine(query);
        var table = vm.Run();

        Assert.IsTrue(table.Count >= 3, "Should find at least 3 CSV files");
    }

    /// <summary>
    /// Demonstrates Common Table Expression (CTE).
    /// Query: Use CTE for data transformation.
    /// </summary>
    [TestMethod]
    public void Combined_CteQuery_ShouldWorkCorrectly()
    {
        var query = """
            with DepartmentStats as (
                select 
                    Department,
                    Count(Name) as EmpCount
                from #separatedvalues.comma('./Files/Employees.csv', true, 0)
                group by Department
            )
            select Department, EmpCount
            from DepartmentStats
            where EmpCount > 1
            order by EmpCount desc
            """;

        var vm = CreateAndRunVirtualMachine(query);
        var table = vm.Run();

        Assert.IsTrue(table.Count > 0, "Should have departments with more than 1 employee");
        Assert.IsTrue(table.All(r => (int)r[1] > 1), "All counts should be > 1");
    }

    /// <summary>
    /// Demonstrates nested CTEs with aggregation.
    /// Query: Multi-level data aggregation.
    /// </summary>
    [TestMethod]
    public void Combined_NestedCte_ShouldPerformMultiLevelAggregation()
    {
        var query = """
            with ProjectHours as (
                select 
                    EmployeeId,
                    Sum(ToDecimal(Hours)) as TotalHours
                from #separatedvalues.comma('./Files/Projects.csv', true, 0)
                group by EmployeeId
            ), EmployeeProjectSummary as (
                select 
                    e.Name as EmpName,
                    e.Department as EmpDepartment,
                    p.TotalHours as EmpTotalHours
                from #separatedvalues.comma('./Files/Employees.csv', true, 0) e
                inner join ProjectHours p on e.Id = p.EmployeeId
            )
            select EmpName, EmpDepartment, EmpTotalHours
            from EmployeeProjectSummary
            """;

        var vm = CreateAndRunVirtualMachine(query);
        var table = vm.Run();

        Assert.IsTrue(table.Count > 0, "Should have employee summaries");
        Assert.AreEqual(3, table.Columns.Count(), "Should have 3 columns");
    }

    #endregion

    #region Helper Methods

    private CompiledQuery CreateAndRunVirtualMachine(string script)
    {
        return InstanceCreatorHelpers.CompileForExecution(
            script, 
            Guid.NewGuid().ToString(), 
            new RepresentativeSchemaProvider(),
            EnvironmentVariablesHelpers.CreateMockedEnvironmentVariables());
    }

    static RepresentativeQueryTests()
    {
        Culture.Apply(CultureInfo.GetCultureInfo("en-EN"));
    }

    private static string StartDirectory
    {
        get
        {
            var filePath = typeof(RepresentativeQueryTests).Assembly.Location;
            var directory = Path.GetDirectoryName(filePath);

            if (string.IsNullOrEmpty(directory))
                throw new InvalidOperationException("Directory is empty.");

            return directory;
        }
    }

    #endregion
}
