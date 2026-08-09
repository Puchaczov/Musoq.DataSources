#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using Musoq.DataSources.Structured;
using Musoq.Schema;
using Musoq.Schema.DataSources;
using Musoq.Schema.Optimization;

namespace Musoq.DataSources.Json.Tests;

[TestClass]
public class JsonSchemaDiscoveryTests
{
    [TestInitialize]
    public void ClearCache()
    {
        JsonSchemaDiscovery.ClearCache();
    }

    [TestMethod]
    public void Discover_WhenRootIsObject_ProducesOneRowAndFirstSeenColumns()
    {
        WithJson("{\"Second\":2,\"First\":1}", path =>
        {
            var snapshot = JsonSchemaDiscovery.GetSnapshot(path);

            Assert.AreEqual(1L, snapshot.RowCount);
            CollectionAssert.AreEqual(
                new[] { "Second", "First" },
                snapshot.Columns.Select(column => column.Name).ToArray());
            Assert.AreEqual(1L, snapshot.Partitions.Sum(partition => partition.RowCount));
        });
    }

    [TestMethod]
    public void Discover_WhenColumnsAreSparseOrLate_UsesCompleteUnionAndNullableTypes()
    {
        WithJson("[{\"First\":1},{\"Late\":true},{\"First\":3,\"Late\":null}]", path =>
        {
            var snapshot = JsonSchemaDiscovery.GetSnapshot(path);

            Assert.AreEqual(3L, snapshot.RowCount);
            CollectionAssert.AreEqual(
                new[] { "First", "Late" },
                snapshot.Columns.Select(column => column.Name).ToArray());
            Assert.AreEqual(typeof(long?), snapshot.Columns[0].ClrType);
            Assert.AreEqual(typeof(bool?), snapshot.Columns[1].ClrType);
            Assert.AreEqual(2L, snapshot.Columns[0].PresentValueCount);
            Assert.AreEqual(2L, snapshot.Columns[1].PresentValueCount);
        });
    }

    [TestMethod]
    public void Discover_WhenScalarKindsArePresent_UsesInvariantTypeContract()
    {
        WithJson(
            "{\"Boolean\":true,\"Integer\":1,\"Fraction\":1.25,\"Exponent\":1e2," +
            "\"Text\":\"2026-08-05\",\"Nested\":{},\"Nothing\":null}",
            path =>
            {
                var snapshot = JsonSchemaDiscovery.GetSnapshot(path);

                AssertColumnType(snapshot, "Boolean", typeof(bool));
                AssertColumnType(snapshot, "Integer", typeof(long));
                AssertColumnType(snapshot, "Fraction", typeof(decimal));
                AssertColumnType(snapshot, "Exponent", typeof(double));
                AssertColumnType(snapshot, "Text", typeof(string));
                AssertColumnType(snapshot, "Nested", typeof(object));
                AssertColumnType(snapshot, "Nothing", typeof(object));
                Assert.IsTrue(snapshot.Columns.Single(column => column.Name == "Nothing").TypeState.IsNullable);
            });
    }

    [TestMethod]
    public void Discover_WhenJsonKindsConflict_WidensToObject()
    {
        WithJson("[{\"Value\":1},{\"Value\":\"one\"}]", path =>
        {
            var snapshot = JsonSchemaDiscovery.GetSnapshot(path);

            AssertColumnType(snapshot, "Value", typeof(object));
        });
    }

    [TestMethod]
    public void Discover_WhenDecimalOverflows_UsesDouble()
    {
        WithJson("{\"Value\":792281625142643375935439503350.1}", path =>
        {
            AssertColumnType(JsonSchemaDiscovery.GetSnapshot(path), "Value", typeof(double));
        });
    }

    [TestMethod]
    public void Discover_WhenArrayIsEmpty_ProducesEmptySchema()
    {
        WithJson("[]", path =>
        {
            var snapshot = JsonSchemaDiscovery.GetSnapshot(path);

            Assert.AreEqual(0L, snapshot.RowCount);
            Assert.AreEqual(0, snapshot.Columns.Length);
            Assert.AreEqual(0, snapshot.Partitions.Length);
        });
    }

    [TestMethod]
    public void Discover_WhenUtf8BomIsPresent_AcceptsItAndKeepsFileOffsets()
    {
        var json = Encoding.UTF8.GetBytes("[{\"Value\":1}]");
        var bytes = Encoding.UTF8.GetPreamble().Concat(json).ToArray();
        WithBytes(bytes, path =>
        {
            var snapshot = JsonSchemaDiscovery.GetSnapshot(path);

            Assert.AreEqual(1L, snapshot.RowCount);
            Assert.AreEqual(3L + 1L, snapshot.Partitions[0].StartOffset);
        });
    }

    [TestMethod]
    public void Discover_WhenTokenCrossesInitialBufferBoundary_CompletesExactly()
    {
        var longValue = new string('x', 96 * 1024);
        WithJson($"[{{\"Long\":\"{longValue}\"}},{{\"Late\":7}}]", path =>
        {
            var snapshot = JsonSchemaDiscovery.GetSnapshot(path);

            Assert.AreEqual(2L, snapshot.RowCount);
            CollectionAssert.AreEqual(
                new[] { "Long", "Late" },
                snapshot.Columns.Select(column => column.Name).ToArray());
        });
    }

    [DataTestMethod]
    [DataRow("1")]
    [DataRow("[1]")]
    [DataRow("[{} , null]")]
    [DataRow("{} {}")]
    [DataRow("{/*comment*/\"Value\":1}")]
    [DataRow("{\"Value\":1,}")]
    [DataRow("{\"Value\":9223372036854775808}")]
    [DataRow("")]
    public void Discover_WhenRootOrSyntaxViolatesContract_RejectsInput(string json)
    {
        WithJson(json, path => AssertFormatFailure(() => JsonSchemaDiscovery.GetSnapshot(path)));
    }

    [DataTestMethod]
    [DataRow("{\"Value\":1,\"Value\":2}")]
    [DataRow("{\"Nested\":{\"Value\":1,\"Value\":2}}")]
    public void Discover_WhenObjectContainsDuplicateProperties_RejectsInput(string json)
    {
        WithJson(json, path =>
            Assert.ThrowsExactly<StructuredDuplicateFieldException>(() =>
                JsonSchemaDiscovery.GetSnapshot(path)));
    }

    [TestMethod]
    public void Discover_WhenUtf8IsInvalid_RejectsInput()
    {
        var prefix = Encoding.UTF8.GetBytes("{\"Value\":\"");
        var suffix = Encoding.UTF8.GetBytes("\"}");
        var bytes = prefix.Concat(new byte[] { 0xff }).Concat(suffix).ToArray();

        WithBytes(bytes, path => AssertFormatFailure(() => JsonSchemaDiscovery.GetSnapshot(path)));
    }

    [TestMethod]
    public void Discover_WhenCancellationWasRequested_DoesNotReadFile()
    {
        WithJson("{\"Value\":1}", path =>
        {
            using var cancellation = new CancellationTokenSource();
            cancellation.Cancel();

            Assert.ThrowsExactly<OperationCanceledException>(() =>
                JsonSchemaDiscovery.GetSnapshot(path, cancellation.Token));
        });
    }

    [TestMethod]
    public void Discover_WhenFileIsMissing_FailsDuringMetadataDiscovery()
    {
        var missingPath = Path.Combine(Path.GetTempPath(), $"missing-{Guid.NewGuid():N}.json");

        Assert.ThrowsExactly<FileNotFoundException>(() =>
            JsonSchemaDiscovery.GetSnapshot(missingPath));
    }

    [TestMethod]
    public void Discover_WhenIdentityIsUnchanged_ReturnsProcessCacheHit()
    {
        WithJson("{\"Value\":1}", path =>
        {
            var cold = JsonSchemaDiscovery.GetSnapshotWithAccess(path);
            var cached = JsonSchemaDiscovery.GetSnapshotWithAccess(path);

            Assert.AreEqual(StructuredSnapshotCacheAccess.Discovered, cold.Access);
            Assert.AreEqual(StructuredSnapshotCacheAccess.Hit, cached.Access);
            Assert.AreSame(cold.Snapshot, cached.Snapshot);
        });
    }

    [TestMethod]
    public void Discover_WhenEdgeFingerprintChanges_RediscoversSchema()
    {
        WithJson("{\"First\":1}", path =>
        {
            var before = JsonSchemaDiscovery.GetSnapshotWithAccess(path);
            File.WriteAllText(path, "{\"Other\":1}", new UTF8Encoding(false));
            var after = JsonSchemaDiscovery.GetSnapshotWithAccess(path);

            Assert.AreEqual(StructuredSnapshotCacheAccess.Discovered, before.Access);
            Assert.AreEqual(StructuredSnapshotCacheAccess.Discovered, after.Access);
            Assert.AreEqual("First", before.Snapshot.Columns[0].Name);
            Assert.AreEqual("Other", after.Snapshot.Columns[0].Name);
        });
    }

    [TestMethod]
    public void JsonTable_WhenExplicitColumnUsesDifferentCase_RejectsUnknownColumn()
    {
        WithJson("{\"Name\":\"Ada\"}", path =>
        {
            var snapshot = JsonSchemaDiscovery.GetSnapshot(path);
            var context = MetadataContext([new SchemaColumn("name", 0, typeof(object))]);

            Assert.ThrowsExactly<StructuredUnknownColumnException>(() =>
                new JsonTable(snapshot, context));
        });
    }

    [TestMethod]
    public void JsonTable_WhenExplicitTypeIsProvided_UsesItWithDenseIndex()
    {
        WithJson("{\"Value\":1}", path =>
        {
            var snapshot = JsonSchemaDiscovery.GetSnapshot(path);
            var context = MetadataContext([new SchemaColumn("Value", 7, typeof(decimal))]);

            var table = new JsonTable(snapshot, context);

            Assert.AreEqual(1, table.Columns.Length);
            Assert.AreEqual(0, table.Columns[0].ColumnIndex);
            Assert.AreEqual(typeof(decimal), table.Columns[0].ColumnType);
        });
    }

    private static SourceMetadataContext MetadataContext(IReadOnlyCollection<ISchemaColumn> columns)
    {
        return new SourceMetadataContext(
            "json-discovery-test",
            CancellationToken.None,
            columns,
            new Dictionary<string, string>(),
            new Mock<ILogger>().Object);
    }

    private static void AssertColumnType(StructuredSchemaSnapshot snapshot, string name, Type expectedType)
    {
        Assert.AreEqual(expectedType, snapshot.Columns.Single(column => column.Name == name).ClrType);
    }

    private static void AssertFormatFailure(Action action)
    {
        try
        {
            action();
            Assert.Fail("Invalid JSON should have been rejected.");
        }
        catch (Exception exception) when (exception is JsonException or FormatException)
        {
        }
    }

    private static void WithJson(string json, Action<string> assertion)
    {
        WithBytes(new UTF8Encoding(false).GetBytes(json), assertion);
    }

    private static void WithBytes(byte[] bytes, Action<string> assertion)
    {
        var path = Path.Combine(Path.GetTempPath(), $"musoq-json-{Guid.NewGuid():N}.json");
        File.WriteAllBytes(path, bytes);

        try
        {
            assertion(path);
        }
        finally
        {
            File.Delete(path);
        }
    }
}
