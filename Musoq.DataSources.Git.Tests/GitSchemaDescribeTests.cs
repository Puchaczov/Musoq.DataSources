using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Musoq.DataSources.Git.Entities;
using Musoq.DataSources.Git.Tests.Components;
using Musoq.DataSources.Tests.Common;
using Musoq.Evaluator;

namespace Musoq.DataSources.Git.Tests;

[TestClass]
public class GitSchemaDescribeTests
{
    private CompiledQuery CreateAndRunVirtualMachine(string script)
    {
        return InstanceCreatorHelpers.CompileForExecution(
            script,
            Guid.NewGuid().ToString(),
            new GitSchemaProvider(),
            EnvironmentVariablesHelpers.CreateMockedEnvironmentVariables());
    }

    static GitSchemaDescribeTests()
    {
        Culture.ApplyWithDefaultCulture();
    }

    [TestMethod]
    public void DescSchema_ShouldListAllAvailableMethods()
    {
        var query = "desc #git";

        var vm = CreateAndRunVirtualMachine(query);
        var table = vm.Run();

        Assert.IsTrue(table.Columns.Count() >= 2, "Should have at least 2 columns");
        Assert.AreEqual("Name", table.Columns.ElementAt(0).ColumnName);

        Assert.AreEqual(11, table.Count, "Should have 11 rows (repository, tags, commits, branches, filehistory x3, status, remotes, blame x2)");

        var repositoryRow = table.FirstOrDefault(r => (string)r[0] == "repository");
        Assert.IsNotNull(repositoryRow);

        var tagsRow = table.FirstOrDefault(r => (string)r[0] == "tags");
        Assert.IsNotNull(tagsRow);
        
        var commitsRow = table.FirstOrDefault(r => (string)r[0] == "commits");
        Assert.IsNotNull(commitsRow);
        
        var branchesRow = table.FirstOrDefault(r => (string)r[0] == "branches");
        Assert.IsNotNull(branchesRow);
        
        var remotesRow = table.FirstOrDefault(r => (string)r[0] == "remotes");
        Assert.IsNotNull(remotesRow);
        
        var blameRows = table.Where(r => ((string)r[0]).StartsWith("blame")).ToList();
        Assert.AreEqual(2, blameRows.Count, "Should have 2 blame method signatures");
    }

    [TestMethod]
    public void DescRepository_ShouldReturnMethodSignature()
    {
        var query = "desc #git.repository";

        var vm = CreateAndRunVirtualMachine(query);
        var table = vm.Run();

        Assert.AreEqual(2, table.Columns.Count(), "Should have 2 columns: Name and Param 0");
        Assert.AreEqual("Name", table.Columns.ElementAt(0).ColumnName);
        Assert.AreEqual("Param 0", table.Columns.ElementAt(1).ColumnName);

        Assert.AreEqual(1, table.Count, "Should have exactly 1 row");

        var row = table.First();
        Assert.AreEqual("repository", (string)row[0]);
        Assert.AreEqual("path: System.String", (string)row[1]);
    }

    [TestMethod]
    public void DescRepositoryWithArgs_ShouldReturnTableSchema()
    {
        var repositoryPath = UnpackRepository(Repository1ZipPath);

        try
        {
            var query = $"desc #git.repository('{repositoryPath}')";

            var vm = CreateAndRunVirtualMachine(query);
            var table = vm.Run();

            Assert.AreEqual(3, table.Columns.Count());
            Assert.IsTrue(table.Count > 0, "Should have rows describing the table columns");

            var columnNames = table.Select(row => (string)row[0]).ToList();
            var expectedColumns = new[]
            {
                nameof(RepositoryEntity.Path),
                nameof(RepositoryEntity.WorkingDirectory),
                nameof(RepositoryEntity.Branches),
                nameof(RepositoryEntity.Tags),
                nameof(RepositoryEntity.Commits),
                nameof(RepositoryEntity.Head),
                nameof(RepositoryEntity.Configuration),
                nameof(RepositoryEntity.Information),
                nameof(RepositoryEntity.Stashes),
                nameof(RepositoryEntity.Self)
            };

            foreach (var expectedColumn in expectedColumns)
            {
                Assert.IsTrue(columnNames.Contains(expectedColumn),
                    $"Should have '{expectedColumn}' column");
            }
        }
        finally
        {
            if (Directory.Exists(repositoryPath))
            {
                Directory.Delete(repositoryPath, true);
            }
        }
    }

    [TestMethod]
    public void DescUnknownMethod_ShouldThrowException()
    {
        var query = "desc #git.unknownmethod";

        try
        {
            var vm = CreateAndRunVirtualMachine(query);
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
        var query = "desc #git";

        var vm = CreateAndRunVirtualMachine(query);
        var table = vm.Run();

        foreach (var column in table.Columns)
        {
            Assert.AreEqual(typeof(string), column.ColumnType,
                $"Column '{column.ColumnName}' should be of type string");
        }
    }

    [TestMethod]
    public void DescRepositoryNoArgs_VsWithArgs_ShouldReturnDifferentResults()
    {
        var repositoryPath = UnpackRepository(Repository1ZipPath);

        try
        {
            var queryNoArgs = "desc #git.repository";
            var vmNoArgs = CreateAndRunVirtualMachine(queryNoArgs);
            var tableNoArgs = vmNoArgs.Run();

            var queryWithArgs = $"desc #git.repository('{repositoryPath}')";
            var vmWithArgs = CreateAndRunVirtualMachine(queryWithArgs);
            var tableWithArgs = vmWithArgs.Run();

            Assert.AreNotEqual(tableNoArgs.Count, tableWithArgs.Count,
                "Method signature vs table schema should have different row counts");

            Assert.AreEqual(1, tableNoArgs.Count);
            Assert.AreEqual("repository", (string)tableNoArgs.First()[0]);

            Assert.IsTrue(tableWithArgs.Count > 1);
            var columnNames = tableWithArgs.Select(row => (string)row[0]).ToList();
            Assert.IsTrue(columnNames.Contains(nameof(RepositoryEntity.Path)));
            Assert.IsTrue(columnNames.Contains(nameof(RepositoryEntity.WorkingDirectory)));
        }
        finally
        {
            if (Directory.Exists(repositoryPath))
            {
                Directory.Delete(repositoryPath, true);
            }
        }
    }

    private static string Repository1ZipPath => Path.Combine(StartDirectory, "Repositories", "Repository1.zip");

    private static string StartDirectory
    {
        get
        {
            var filePath = typeof(GitSchemaDescribeTests).Assembly.Location;
            var directory = Path.GetDirectoryName(filePath);

            if (string.IsNullOrEmpty(directory))
                throw new InvalidOperationException("Directory is empty.");

            return directory;
        }
    }

    private static string UnpackRepository(string zipPath)
    {
        var tempPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        ZipFile.ExtractToDirectory(zipPath, tempPath);
        return tempPath;
    }
}
