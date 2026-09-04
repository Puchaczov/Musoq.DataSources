#nullable enable

using System;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Musoq.DataSources.Os.Compare.Directories;
using Musoq.DataSources.Tests.Common;
using Musoq.Evaluator;
using Musoq.Schema;

namespace Musoq.DataSources.Os.Tests;

[TestClass]
[DoNotParallelize]
public sealed class OsNativeEnumTests
{
    [TestMethod]
    public void Directories_AttributesUsesDirectoryInfoCarrierAndPortableNames()
    {
        using var table = Run(
            "select Name, FullName, Attributes, EnumName(Attributes) as AttributeName " +
            "from os.directories('./Directories', false) order by FullName");

        PortableEnumAssertions.AssertEnumColumn(
            table,
            "Attributes",
            typeof(int),
            typeof(int),
            typeof(FileAttributes),
            EnumUnderlyingKind.Int32,
            isFlags: true,
            ["Directory", "Hidden", "ReadOnly"]);
        PortableEnumAssertions.AssertNoClrEnumValues(table);

        var expectedDirectories = Directory.GetDirectories("./Directories")
            .Select(path => new DirectoryInfo(path))
            .OrderBy(info => info.FullName, StringComparer.Ordinal)
            .ToArray();
        Assert.AreEqual(expectedDirectories.Length, table.Count);

        for (var index = 0; index < expectedDirectories.Length; index++)
        {
            var expected = expectedDirectories[index];
            var row = table[index];
            Assert.AreEqual(expected.Name, row[0]);
            Assert.AreEqual(expected.FullName, row[1]);
            Assert.AreEqual((int)expected.Attributes, row[2]);
            Assert.AreEqual(Enum.GetName(typeof(FileAttributes), expected.Attributes), row[3]);
        }
    }

    [TestMethod]
    public void Directories_AttributesSupportsFlagsPredicatesWithoutEnumMaterialization()
    {
        using var table = Run(
            "select Name, Attributes, " +
            "HasAnyFlags(Attributes, 'Directory') as HasDirectory, " +
            "HasAllFlags(Attributes, 'Directory') as AllDirectory " +
            "from os.directories('./Directories', false) order by Name");

        PortableEnumAssertions.AssertEnumColumn(
            table,
            "Attributes",
            typeof(int),
            typeof(int),
            typeof(FileAttributes),
            EnumUnderlyingKind.Int32,
            isFlags: true);
        Assert.IsTrue(table.All(row => (bool)row[2]));
        Assert.IsTrue(table.All(row => (bool)row[3]));
        Assert.IsFalse(table.SelectMany(static row => row.Values).Any(static value => value is Enum));
    }

    [TestMethod]
    public void Directories_AttributesReorderedAndAliasedProjectionRetainsDescriptor()
    {
        using var table = Run(
            "select d.Attributes as Flags, d.Name as DirectoryName " +
            "from os.directories('./Directories', false) d order by DirectoryName");

        PortableEnumAssertions.AssertEnumColumn(
            table,
            "Flags",
            typeof(int),
            typeof(int),
            typeof(FileAttributes),
            EnumUnderlyingKind.Int32,
            isFlags: true,
            ["Directory"]);
        Assert.IsNull(table.Columns.Single(column => column.ColumnName == "DirectoryName").EnumType);
        PortableEnumAssertions.AssertNoClrEnumValues(table);
    }

    [TestMethod]
    public void DirectoriesCompare_StateUsesPrimitiveValuesAndDeclaredNames()
    {
        using var table = Run(
            "select State, EnumName(State), SourceFileRelative, DestinationFileRelative " +
            "from os.dirscompare('./Directories/Directory1', './Directories/Directory2')");

        PortableEnumAssertions.AssertEnumColumn(
            table,
            "State",
            typeof(int),
            typeof(int),
            typeof(State),
            EnumUnderlyingKind.Int32,
            isFlags: false,
            ["TheSame", "Modified", "Added", "Removed"]);
        PortableEnumAssertions.AssertNoClrEnumValues(table);
        Assert.IsFalse(table.Columns.Any(column =>
            column.ColumnName is "SourceFile" or "DestinationFile" or "SourceRoot" or "DestinationRoot"));

        Assert.IsTrue(table.Any(row => (int)row[0] == (int)State.Removed &&
                                       (string)row[1] == nameof(State.Removed) &&
                                       ((string?)row[2])?.EndsWith("TextFile1.txt", StringComparison.Ordinal) == true &&
                                       row[3] is null));
        Assert.IsTrue(table.Count(row => (int)row[0] == (int)State.Added) >= 2);
        Assert.IsTrue(table.Where(row => (int)row[0] == (int)State.Added)
            .All(row => (string)row[1] == nameof(State.Added) && row[2] is null && row[3] is string));
    }

    [TestMethod]
    public void DirectoriesCompare_TheSameStateAndSparseProjectionRemainPortable()
    {
        using var table = Run(
            "select c.State as ComparisonState, c.DestinationFileRelative " +
            "from os.dirscompare('./Directories/Directory1', './Directories/Directory1') c");

        PortableEnumAssertions.AssertEnumColumn(
            table,
            "ComparisonState",
            typeof(int),
            typeof(int),
            typeof(State),
            EnumUnderlyingKind.Int32,
            isFlags: false,
            ["TheSame"]);
        PortableEnumAssertions.AssertNoClrEnumValues(table);
        Assert.IsTrue(table.Count > 0);
        Assert.IsTrue(table.All(row => (int)row[0] == (int)State.TheSame));
    }

    [TestMethod]
    public void Directories_AttributesResidualEqualityAndFlagsKeepPrimitiveCarrier()
    {
        var expected = Directory.GetDirectories("./Directories")
            .Select(path => new DirectoryInfo(path))
            .Where(info => info.Attributes == FileAttributes.Directory)
            .Select(info => info.Name)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();

        using var equality = Run(
            "select Name, Attributes from os.directories('./Directories', false) d " +
            "where d.Attributes = 'Directory' order by d.Name");
        using var flags = Run(
            "select Name, Attributes from os.directories('./Directories', false) d " +
            "where HasAnyFlags(d.Attributes, 'Directory') and HasAllFlags(d.Attributes, 'Directory') " +
            "order by d.Name");

        PortableEnumAssertions.AssertEnumColumn(
            equality,
            "Attributes",
            typeof(int),
            typeof(int),
            typeof(FileAttributes),
            EnumUnderlyingKind.Int32,
            isFlags: true);
        PortableEnumAssertions.AssertEnumColumn(
            flags,
            "Attributes",
            typeof(int),
            typeof(int),
            typeof(FileAttributes),
            EnumUnderlyingKind.Int32,
            isFlags: true);
        PortableEnumAssertions.AssertNoClrEnumValues(equality);
        PortableEnumAssertions.AssertNoClrEnumValues(flags);

        CollectionAssert.AreEqual(expected, equality.Select(row => (string)row[0]).ToArray());
        Assert.IsTrue(flags.All(row => ((int)row[1] & (int)FileAttributes.Directory) != 0));
    }

    [TestMethod]
    public void DirectoriesCompare_StateResidualMembershipNullChecksAndNamesRemainPortable()
    {
        const string source = "os.dirscompare('./Directories/Directory1', './Directories/Directory2')";
        using var added = Run($"select State, EnumName(State) from {source} c where c.State = 'Added'");
        using var notAdded = Run($"select State from {source} c where c.State <> 'Added'");
        using var membership = Run($"select State from {source} c where c.State in ('Added', 'Removed')");
        using var notMembership = Run($"select State from {source} c where c.State not in ('Added')");
        using var isNull = Run($"select State from {source} c where c.State is null");
        using var isNotNull = Run($"select State from {source} c where c.State is not null");

        foreach (var table in new[] { added, notAdded, membership, notMembership, isNull, isNotNull })
        {
            PortableEnumAssertions.AssertEnumColumn(
                table,
                "State",
                typeof(int),
                typeof(int),
                typeof(State),
                EnumUnderlyingKind.Int32,
                isFlags: false);
            PortableEnumAssertions.AssertNoClrEnumValues(table);
        }

        Assert.AreEqual(2, added.Count);
        Assert.IsTrue(added.All(row => (int)row[0] == (int)State.Added && (string)row[1] == nameof(State.Added)));
        Assert.AreEqual(1, notAdded.Count);
        Assert.AreEqual((int)State.Removed, notAdded[0][0]);
        Assert.AreEqual(3, membership.Count);
        Assert.AreEqual(1, notMembership.Count);
        Assert.AreEqual((int)State.Removed, notMembership[0][0]);
        Assert.AreEqual(0, isNull.Count);
        Assert.AreEqual(3, isNotNull.Count);
    }

    private static Musoq.Evaluator.Tables.Table Run(string query)
    {
        var compiled = InstanceCreatorHelpers.CompileForExecution(
            query,
            $"OsNativeEnum_{Guid.NewGuid():N}",
            new OsSchemaProvider(),
            EnvironmentVariablesHelpers.CreateMockedEnvironmentVariables());
        return compiled.Run();
    }
}
