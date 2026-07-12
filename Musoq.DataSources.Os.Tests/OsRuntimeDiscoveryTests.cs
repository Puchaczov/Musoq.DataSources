using System;
using System.Globalization;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Musoq.DataSources.Tests.Common;
using Musoq.Evaluator;

namespace Musoq.DataSources.Os.Tests;

[TestClass]
public class OsRuntimeDiscoveryTests
{
    static OsRuntimeDiscoveryTests()
    {
        Culture.ApplyWithDefaultCulture();
    }

    [DataTestMethod]
    [DataRow("cultures")]
    [DataRow("currentculture")]
    [DataRow("encodings")]
    [DataRow("timezones")]
    [DataRow("runtime")]
    [DataRow("drives")]
    [DataRow("specialfolders")]
    [DataRow("fileattributes")]
    [DataRow("environmentvariables")]
    public void DescNoParameterRuntimeDiscoverySource_ShouldReturnNoParameterSignature(string methodName)
    {
        var table = CreateAndRunVirtualMachine($"desc #os.{methodName}").Run();

        Assert.AreEqual(1, table.Columns.Count());
        Assert.AreEqual(1, table.Count);
        Assert.AreEqual(methodName, (string)table[0][0]);
    }

    [TestMethod]
    public void DescPathInfo_ShouldReturnPathParameterSignature()
    {
        var table = CreateAndRunVirtualMachine("desc #os.pathinfo").Run();

        Assert.AreEqual(2, table.Columns.Count());
        Assert.AreEqual(1, table.Count);
        Assert.AreEqual("pathinfo", (string)table[0][0]);
        Assert.AreEqual("path: System.String", (string)table[0][1]);
    }

    [TestMethod]
    public void Cultures_ShouldExposeCurrentRuntimeCulturesAndFormattingColumns()
    {
        var table = CreateAndRunVirtualMachine("select Name, DecimalSeparator, ShortDatePattern from #os.cultures()").Run();

        Assert.IsTrue(table.Count > 0);
        Assert.IsTrue(table.Any(row => (string)row[0] == CultureInfo.CurrentCulture.Name));
        Assert.IsTrue(table.Columns.Any(column => column.ColumnName == "DecimalSeparator"));
        Assert.IsTrue(table.Columns.Any(column => column.ColumnName == "ShortDatePattern"));
    }

    [TestMethod]
    public void CurrentCulture_ShouldReturnSingleRow()
    {
        var table = CreateAndRunVirtualMachine("select CurrentCulture, CurrentUICulture, DecimalSeparator from #os.currentculture()").Run();

        Assert.AreEqual(1, table.Count);
        Assert.AreEqual(CultureInfo.CurrentCulture.Name, (string)table[0][0]);
        Assert.AreEqual(CultureInfo.CurrentUICulture.Name, (string)table[0][1]);
        Assert.IsFalse(string.IsNullOrEmpty((string)table[0][2]));
    }

    [TestMethod]
    public void Encodings_ShouldExposeUtf8AndCodePageEncodings()
    {
        var table = CreateAndRunVirtualMachine("select WebName, CodePage from #os.encodings()").Run();

        Assert.IsTrue(table.Any(row => string.Equals((string)row[0], "utf-8", StringComparison.OrdinalIgnoreCase)));
        Assert.IsTrue(table.Any(row => (int)row[1] == 1250), "Code page provider should expose windows-1250.");
    }

    [TestMethod]
    public void TimeZones_ShouldReturnSystemTimeZones()
    {
        var table = CreateAndRunVirtualMachine("select Id, BaseUtcOffset from #os.timezones()").Run();

        Assert.IsTrue(table.Count > 0);
        Assert.AreEqual("Id", table.Columns.ElementAt(0).ColumnName);
        Assert.AreEqual("BaseUtcOffset", table.Columns.ElementAt(1).ColumnName);
    }

    [TestMethod]
    public void Runtime_ShouldReturnSingleSafeRowWithoutIdentityColumns()
    {
        var table = CreateAndRunVirtualMachine("select * from #os.runtime()").Run();

        Assert.AreEqual(1, table.Count);
        Assert.IsFalse(table.Columns.Any(column => column.ColumnName == "UserName"));
        Assert.IsFalse(table.Columns.Any(column => column.ColumnName == "MachineName"));
        Assert.IsFalse(table.Columns.Any(column => column.ColumnName == "CommandLine"));
        Assert.IsFalse(string.IsNullOrEmpty((string)table[0][0]));
    }

    [TestMethod]
    public void Drives_ShouldReturnAtLeastOneDriveWithoutAssumingDriveLetters()
    {
        var table = CreateAndRunVirtualMachine("select Name, IsReady from #os.drives()").Run();

        Assert.IsTrue(table.Count > 0);
        Assert.AreEqual("Name", table.Columns.ElementAt(0).ColumnName);
        Assert.AreEqual("IsReady", table.Columns.ElementAt(1).ColumnName);
    }

    [TestMethod]
    public void SpecialFolders_ShouldExposeKnownFolderNames()
    {
        var table = CreateAndRunVirtualMachine("select s.Name, s.Path, s.Exists from #os.specialfolders() s").Run();

        Assert.IsTrue(table.Any(row => (string)row[0] == nameof(Environment.SpecialFolder.Desktop)));
    }

    [TestMethod]
    public void FileAttributes_ShouldExposeCommonFileAttributeValues()
    {
        var table = CreateAndRunVirtualMachine("select Name, Value from #os.fileattributes()").Run();
        var names = table.Select(row => (string)row[0]).ToArray();

        CollectionAssert.Contains(names, "Directory");
        CollectionAssert.Contains(names, "Hidden");
        CollectionAssert.Contains(names, "ReadOnly");
    }

    [TestMethod]
    public void EnvironmentVariables_ShouldExposeNamesAndTargetsOnly()
    {
        var table = CreateAndRunVirtualMachine("select * from #os.environmentvariables()").Run();

        Assert.IsTrue(table.Columns.Any(column => column.ColumnName == "Name"));
        Assert.IsTrue(table.Columns.Any(column => column.ColumnName == "Target"));
        Assert.IsFalse(table.Columns.Any(column => column.ColumnName.Contains("Value", StringComparison.OrdinalIgnoreCase)));
    }

    [TestMethod]
    public void PathInfo_ShouldReturnSingleRowForExistingAndMissingPaths()
    {
        var existing = CreateAndRunVirtualMachine("select p.Exists, p.IsDirectory, p.FileName from #os.pathinfo('./Files') p").Run();
        var missing = CreateAndRunVirtualMachine("select p.Exists, p.IsFile, p.IsDirectory from #os.pathinfo('./MissingPathForRuntimeDiscoveryTests') p").Run();

        Assert.AreEqual(1, existing.Count);
        Assert.AreEqual(true, existing[0][0]);
        Assert.AreEqual(true, existing[0][1]);

        Assert.AreEqual(1, missing.Count);
        Assert.AreEqual(false, missing[0][0]);
        Assert.AreEqual(false, missing[0][1]);
        Assert.AreEqual(false, missing[0][2]);
    }

    private static CompiledQuery CreateAndRunVirtualMachine(string script)
    {
        return InstanceCreatorHelpers.CompileForExecution(
            script,
            Guid.NewGuid().ToString(),
            new OsSchemaProvider(),
            EnvironmentVariablesHelpers.CreateMockedEnvironmentVariables());
    }
}
