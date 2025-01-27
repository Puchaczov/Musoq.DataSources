using System;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Musoq.DataSources.Archives.Tests.Components;
using Musoq.DataSources.Tests.Common;
using Musoq.Evaluator;
using Musoq.Plugins;

namespace Musoq.DataSources.Archives.Tests;

[TestClass]
public class ArchivesAndSeparatedValuesTests
{
    [TestMethod]
    public void WhenReadAllArchiveCsvFiles_ShouldPass()
    {
        const string query = 
            """
            table PeopleDetails {
                Name 'System.String',
                Surname 'System.String',
                Age 'System.Int32'
            };
            couple #separatedvalues.comma with table PeopleDetails as SourceOfPeopleDetails;
            select 
                a.Key as InZipPath, 
                b.Name, 
                b.Surname, 
                b.Age 
            from #archives.file('./Files/Example2/archive.zip') a cross apply SourceOfPeopleDetails(a.GetStreamContent(), true, 0) as b 
            where a.IsDirectory = false and a.Key like '%.csv';
            """;
        
        var vm = CreateAndRunVirtualMachine(query);
        
        var table = vm.Run();
        
        Assert.AreEqual(4, table.Columns.Count());
        
        Assert.AreEqual("InZipPath", table.Columns.ElementAt(0).ColumnName);
        Assert.AreEqual(typeof(string), table.Columns.ElementAt(0).ColumnType);
        
        Assert.AreEqual("b.Name", table.Columns.ElementAt(1).ColumnName);
        Assert.AreEqual(typeof(string), table.Columns.ElementAt(1).ColumnType);
        
        Assert.AreEqual("b.Surname", table.Columns.ElementAt(2).ColumnName);
        Assert.AreEqual(typeof(string), table.Columns.ElementAt(2).ColumnType);
        
        Assert.AreEqual("b.Age", table.Columns.ElementAt(3).ColumnName);
        Assert.AreEqual(typeof(int?), table.Columns.ElementAt(3).ColumnType);
        
        Assert.IsTrue(table.Count == 6, "Table should have 6 entries");

        Assert.IsTrue(table.Any(row => 
            (string)row.Values[0] == "Test/file3.csv" && 
            (string)row.Values[1] == "Martin" && 
            (string)row.Values[2] == "Podval" && 
            (int)row.Values[3] == 28
        ), "First row should match Test/file3.csv, Martin, Podval, 28");

        Assert.IsTrue(table.Any(row => 
            (string)row.Values[0] == "Test/file3.csv" && 
            (string)row.Values[1] == "Aneta" && 
            (string)row.Values[2] == "Podvalova" && 
            (int)row.Values[3] == 27
        ), "Second row should match Test/file3.csv, Aneta, Podvalova, 27");

        Assert.IsTrue(table.Any(row => 
            (string)row.Values[0] == "file1.csv" && 
            (string)row.Values[1] == "Joanna" && 
            (string)row.Values[2] == "Doe" && 
            (int)row.Values[3] == 23
        ), "Third row should match file1.csv, Joanna, Doe, 23");

        Assert.IsTrue(table.Any(row => 
            (string)row.Values[0] == "file1.csv" && 
            (string)row.Values[1] == "Joanna" && 
            (string)row.Values[2] == "Doe" && 
            (int)row.Values[3] == 11
        ), "Fourth row should match file1.csv, Joanna, Doe, 11");

        Assert.IsTrue(table.Any(row => 
            (string)row.Values[0] == "file2.csv" && 
            (string)row.Values[1] == "John" && 
            (string)row.Values[2] == "Doe" && 
            (int)row.Values[3] == 25
        ), "Fifth row should match file2.csv, John, Doe, 25");

        Assert.IsTrue(table.Any(row => 
            (string)row.Values[0] == "file2.csv" && 
            (string)row.Values[1] == "Jane" && 
            (string)row.Values[2] == "Doe" && 
            (int)row.Values[3] == 23
        ), "Sixth row should match file2.csv, Jane, Doe, 23");
    }
    
    [TestMethod]
    public void WhenReadOnlyRootPathCsvFiles_ShouldPass()
    {
        const string query = 
            """
            table PeopleDetails {
                Name 'System.String',
                Surname 'System.String',
                Age 'System.Int32'
            };
            couple #separatedvalues.comma with table PeopleDetails as SourceOfPeopleDetails;
            select 
                a.Key as InZipPath, 
                b.Name, 
                b.Surname, 
                b.Age 
            from #archives.file('./Files/Example2/archive.zip') a cross apply SourceOfPeopleDetails(a.GetStreamContent(), true, 0) as b 
            where a.IsDirectory = false and a.Contains(a.Key, '/') = false and a.Key like '%.csv';
            """;
        
        var vm = CreateAndRunVirtualMachine(query);
        
        var table = vm.Run();
        
        Assert.AreEqual(4, table.Columns.Count());
        
        Assert.AreEqual("InZipPath", table.Columns.ElementAt(0).ColumnName);
        Assert.AreEqual(typeof(string), table.Columns.ElementAt(0).ColumnType);
        
        Assert.AreEqual("b.Name", table.Columns.ElementAt(1).ColumnName);
        Assert.AreEqual(typeof(string), table.Columns.ElementAt(1).ColumnType);
        
        Assert.AreEqual("b.Surname", table.Columns.ElementAt(2).ColumnName);
        Assert.AreEqual(typeof(string), table.Columns.ElementAt(2).ColumnType);
        
        Assert.AreEqual("b.Age", table.Columns.ElementAt(3).ColumnName);
        Assert.AreEqual(typeof(int?), table.Columns.ElementAt(3).ColumnType);
        
        Assert.IsTrue(table.Count == 4, "Table should have 4 entries");

        Assert.IsTrue(table.Any(row => 
            (string)row.Values[0] == "file1.csv" && 
            (string)row.Values[1] == "Joanna" && 
            (string)row.Values[2] == "Doe" && 
            (int)row.Values[3] == 23
        ), "First row should match file1.csv, Joanna, Doe, 23");

        Assert.IsTrue(table.Any(row => 
            (string)row.Values[0] == "file1.csv" && 
            (string)row.Values[1] == "Joanna" && 
            (string)row.Values[2] == "Doe" && 
            (int)row.Values[3] == 11
        ), "Second row should match file1.csv, Joanna, Doe, 11");

        Assert.IsTrue(table.Any(row => 
            (string)row.Values[0] == "file2.csv" && 
            (string)row.Values[1] == "John" && 
            (string)row.Values[2] == "Doe" && 
            (int)row.Values[3] == 25
        ), "Third row should match file2.csv, John, Doe, 25");

        Assert.IsTrue(table.Any(row => 
            (string)row.Values[0] == "file2.csv" && 
            (string)row.Values[1] == "Jane" && 
            (string)row.Values[2] == "Doe" && 
            (int)row.Values[3] == 23
        ), "Fourth row should match file2.csv, Jane, Doe, 23");
    }
    
    [TestMethod]
    public void WhenFirstlyFilterEntriesToRead_ThenReadOnlyThoseEntries_ShouldPass()
    {
        const string query = 
            """
            table PeopleDetails {
                Name 'System.String',
                Surname 'System.String',
                Age 'System.Int32'
            };
            couple #separatedvalues.comma with table PeopleDetails as SourceOfPeopleDetails;
            with Files as (
                select 
                    a.Key as InZipPath
                from #archives.file('./Files/Example2/archive.zip') a
                where 
                    a.IsDirectory = false and
                    a.Contains(a.Key, '/') = false and 
                    a.Key like '%.csv'
            )
            select 
                f.InZipPath, 
                b.Name, 
                b.Surname, 
                b.Age 
            from #archives.file('./Files/Example2/archive.zip') a inner join Files f on f.InZipPath = a.Key cross apply SourceOfPeopleDetails(a.GetStreamContent(), true, 0) as b;
            """;
        
        var vm = CreateAndRunVirtualMachine(query);
        
        var table = vm.Run();
        
        Assert.AreEqual(4, table.Columns.Count());
        
        Assert.AreEqual("f.InZipPath", table.Columns.ElementAt(0).ColumnName);
        Assert.AreEqual(typeof(string), table.Columns.ElementAt(0).ColumnType);
        
        Assert.AreEqual("b.Name", table.Columns.ElementAt(1).ColumnName);
        Assert.AreEqual(typeof(string), table.Columns.ElementAt(1).ColumnType);
        
        Assert.AreEqual("b.Surname", table.Columns.ElementAt(2).ColumnName);
        Assert.AreEqual(typeof(string), table.Columns.ElementAt(2).ColumnType);
        
        Assert.AreEqual("b.Age", table.Columns.ElementAt(3).ColumnName);
        Assert.AreEqual(typeof(int?), table.Columns.ElementAt(3).ColumnType);
        
        Assert.AreEqual(4, table.Count);
        
        Assert.AreEqual("file1.csv", table[0].Values[0]);
        Assert.AreEqual("Joanna", table[0].Values[1]);
        Assert.AreEqual("Doe", table[0].Values[2]);
        Assert.AreEqual(23, table[0].Values[3]);
        
        Assert.AreEqual("file1.csv", table[1].Values[0]);
        Assert.AreEqual("Joanna", table[1].Values[1]);
        Assert.AreEqual("Doe", table[1].Values[2]);
        Assert.AreEqual(11, table[1].Values[3]);
        
        Assert.AreEqual("file2.csv", table[2].Values[0]);
        Assert.AreEqual("John", table[2].Values[1]);
        Assert.AreEqual("Doe", table[2].Values[2]);
        Assert.AreEqual(25, table[2].Values[3]);
        
        Assert.AreEqual("file2.csv", table[3].Values[0]);
        Assert.AreEqual("Jane", table[3].Values[1]);
        Assert.AreEqual("Doe", table[3].Values[2]);
        Assert.AreEqual(23, table[3].Values[3]);
    }

    private static CompiledQuery CreateAndRunVirtualMachine(string script)
    {
        return InstanceCreatorHelpers.CompileForExecution(script, Guid.NewGuid().ToString(), new ArchivesOrSeparatedValuesSchemaProvider(), EnvironmentVariablesHelpers.CreateMockedEnvironmentVariables());
    }

    static ArchivesAndSeparatedValuesTests()
    {
        new Plugins.Environment().SetValue(Constants.NetStandardDllEnvironmentVariableName, EnvironmentUtils.GetOrCreateEnvironmentVariable());

        Culture.ApplyWithDefaultCulture();
    }
}