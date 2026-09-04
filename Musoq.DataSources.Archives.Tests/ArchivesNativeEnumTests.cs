#nullable enable

using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Musoq.DataSources.Tests.Common;
using Musoq.Evaluator;
using Musoq.Schema;

namespace Musoq.DataSources.Archives.Tests;

[TestClass]
[DoNotParallelize]
public sealed class ArchivesNativeEnumTests
{
    private const string ArchivePath = "./Files/Example1/archives.zip";

    [TestMethod]
    public void CompressionType_DirectProjectionUsesIntegralCarrierAndDeclaredNames()
    {
        using var table = Run(
            $"select Key, CompressionType, EnumName(CompressionType) as CompressionName " +
            $"from archives.file('{ArchivePath}') order by Key");

        PortableEnumAssertions.AssertEnumColumn(
            table,
            "CompressionType",
            typeof(int),
            typeof(int),
            typeof(SharpCompress.Common.CompressionType),
            EnumUnderlyingKind.Int32,
            isFlags: false,
            ["None", "Deflate"]);
        PortableEnumAssertions.AssertNoClrEnumValues(table);

        CollectionAssert.AreEqual(
            new[]
            {
                "others/|0|None",
                "others/text3.txt|0|None",
                "text1.txt|0|None",
                "text2.txt|0|None"
            },
            table.Select(row =>
                    $"{row[0]}|{row[1]}|{row[2] ?? "<null>"}")
                .ToArray());
    }

    [TestMethod]
    public void CompressionType_AliasesAndSparseProjectionPreservePortableMetadata()
    {
        using var table = Run(
            $"select e.CompressionType as Kind, e.Key as Entry " +
            $"from archives.file('{ArchivePath}') e order by Entry");

        PortableEnumAssertions.AssertEnumColumn(
            table,
            "Kind",
            typeof(int),
            typeof(int),
            typeof(SharpCompress.Common.CompressionType),
            EnumUnderlyingKind.Int32,
            isFlags: false,
            ["None", "Deflate"]);
        Assert.AreEqual(typeof(string), table.Columns.Single(column => column.ColumnName == "Entry").ColumnType);
        Assert.IsNull(table.Columns.Single(column => column.ColumnName == "Entry").EnumType);
        PortableEnumAssertions.AssertNoClrEnumValues(table);

        CollectionAssert.AreEqual(
            new[] { "0|others/", "0|others/text3.txt", "0|text1.txt", "0|text2.txt" },
            table.Select(row => $"{row[0]}|{row[1]}").ToArray());
    }

    [TestMethod]
    public void CompressionType_MixedZipPreservesStoredAndDeflateBackingValues()
    {
        WithMixedZip(path =>
        {
            using var table = Run($"select Key, CompressionType, EnumName(CompressionType) " +
                                  $"from archives.file('{QueryPath(path)}') order by Key");

            PortableEnumAssertions.AssertEnumColumn(
                table,
                "CompressionType",
                typeof(int),
                typeof(int),
                typeof(SharpCompress.Common.CompressionType),
                EnumUnderlyingKind.Int32,
                isFlags: false,
                ["None", "Deflate"]);
            PortableEnumAssertions.AssertNoClrEnumValues(table);

            Assert.IsTrue(table.Any(row => (string)row[0] == "stored.txt" && (int)row[1] == 0 &&
                                           (string)row[2] == "None"));
            Assert.IsTrue(table.Any(row => (string)row[0] == "deflated.txt" && (int)row[1] == 4 &&
                                           (string)row[2] == "Deflate"));
        });
    }

    [TestMethod]
    public void CompressionType_DescriptorFingerprintAndMembersAreStableAcrossArchiveFormats()
    {
        using var zip = Run($"select CompressionType from archives.file('{ArchivePath}')");
        using var tar = Run("select CompressionType from archives.file('./Files/Example1/archives.tar')");

        var zipDescriptor = zip.Columns.Single().EnumType;
        var tarDescriptor = tar.Columns.Single().EnumType;
        Assert.IsNotNull(zipDescriptor);
        Assert.IsNotNull(tarDescriptor);
        Assert.AreEqual(zipDescriptor.Fingerprint, tarDescriptor.Fingerprint);
        CollectionAssert.AreEqual(
            zipDescriptor.Members.Select(member => member.Name).ToArray(),
            tarDescriptor.Members.Select(member => member.Name).ToArray());
        PortableEnumAssertions.AssertNoClrEnumValues(zip);
        PortableEnumAssertions.AssertNoClrEnumValues(tar);
    }

    [TestMethod]
    public void CompressionType_ResidualEqualityMembershipAndInequalityUsePrimitiveValues()
    {
        WithMixedZip(path =>
        {
            var queryPath = QueryPath(path);
            using var deflated = Run(
                $"select Key, CompressionType from archives.file('{queryPath}') e " +
                "where e.CompressionType = 'Deflate' order by e.Key");
            using var notDeflated = Run(
                $"select Key, CompressionType from archives.file('{queryPath}') e " +
                "where e.CompressionType <> 'Deflate' order by e.Key");
            using var all = Run(
                $"select Key, CompressionType from archives.file('{queryPath}') e " +
                "where e.CompressionType in ('None', 'Deflate') order by e.Key");
            using var onlyStored = Run(
                $"select Key, CompressionType from archives.file('{queryPath}') e " +
                "where e.CompressionType not in ('Deflate') order by e.Key");

            foreach (var table in new[] { deflated, notDeflated, all, onlyStored })
            {
                PortableEnumAssertions.AssertEnumColumn(
                    table,
                    "CompressionType",
                    typeof(int),
                    typeof(int),
                    typeof(SharpCompress.Common.CompressionType),
                    EnumUnderlyingKind.Int32,
                    isFlags: false);
                PortableEnumAssertions.AssertNoClrEnumValues(table);
            }

            CollectionAssert.AreEqual(
                new[] { "deflated.txt|4" },
                deflated.Select(row => $"{row[0]}|{row[1]}").ToArray());
            CollectionAssert.AreEqual(
                new[] { "stored.txt|0" },
                notDeflated.Select(row => $"{row[0]}|{row[1]}").ToArray());
            CollectionAssert.AreEqual(
                new[] { "deflated.txt|4", "stored.txt|0" },
                all.Select(row => $"{row[0]}|{row[1]}").ToArray());
            CollectionAssert.AreEqual(
                new[] { "stored.txt|0" },
                onlyStored.Select(row => $"{row[0]}|{row[1]}").ToArray());
        });
    }

    private static Musoq.Evaluator.Tables.Table Run(string query)
    {
        var compiled = InstanceCreatorHelpers.CompileForExecution(
            query,
            $"ArchivesNativeEnum_{Guid.NewGuid():N}",
            new ArchivesSchemaProvider(),
            EnvironmentVariablesHelpers.CreateMockedEnvironmentVariables());
        return compiled.Run();
    }

    private static void WithMixedZip(Action<string> assertion)
    {
        var path = Path.Combine(Path.GetTempPath(), $"musoq-enum-{Guid.NewGuid():N}.zip");
        using (var stream = File.Create(path))
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create))
        {
            var stored = archive.CreateEntry("stored.txt", CompressionLevel.NoCompression);
            using (var writer = new StreamWriter(stored.Open(), Encoding.UTF8, leaveOpen: false))
                writer.Write("stored");

            var deflated = archive.CreateEntry("deflated.txt", CompressionLevel.Optimal);
            using (var writer = new StreamWriter(deflated.Open(), Encoding.UTF8, leaveOpen: false))
                writer.Write(new string('x', 8192));
        }

        try
        {
            assertion(path);
        }
        finally
        {
            File.Delete(path);
        }
    }

    private static string QueryPath(string path)
    {
        return Path.GetFullPath(path)
            .Replace('\\', '/')
            .Replace("'", "''", StringComparison.Ordinal);
    }
}
