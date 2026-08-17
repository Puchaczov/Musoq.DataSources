#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using Musoq.DataSources.Structured;
using Musoq.Schema;
using Musoq.Schema.DataSources;
using Musoq.Schema.Optimization;

namespace Musoq.DataSources.SeparatedValues.Tests;

[TestClass]
public class SeparatedValuesSchemaHeaderTests
{
    [TestMethod]
    public void Columns_WhenHeaderContainsQuotedSeparator_PreservesExactName()
    {
        WithCsv("\"First,Name\",Age\r\nAlice,31\r\n", path =>
        {
            var snapshot = Snapshot(path);
            var table = new SeparatedValuesTable(snapshot, MetadataContext([]));

            Assert.AreEqual(2, table.Columns.Length);
            Assert.AreEqual("First,Name", table.Columns[0].ColumnName);
            Assert.AreEqual("Age", table.Columns[1].ColumnName);
            Assert.AreEqual(typeof(string), table.Columns[0].ColumnType);
            Assert.AreEqual(typeof(long?), table.Columns[1].ColumnType);
        });
    }

    [TestMethod]
    public void Columns_WhenExplicitTypeIsProvided_UsesItWithDenseIndex()
    {
        WithCsv("Name,Age\nAda,36\n", path =>
        {
            var snapshot = Snapshot(path);
            var table = new SeparatedValuesTable(
                snapshot,
                MetadataContext([new SchemaColumn("Age", 7, typeof(decimal))]));

            Assert.AreEqual(1, table.Columns.Length);
            Assert.AreEqual("Age", table.Columns[0].ColumnName);
            Assert.AreEqual(0, table.Columns[0].ColumnIndex);
            Assert.AreEqual(typeof(decimal), table.Columns[0].ColumnType);
        });
    }

    [TestMethod]
    public void Columns_WhenRequestedColumnDoesNotExist_DoesNotExposeIt()
    {
        WithCsv("Name\nAda\n", path =>
        {
            var snapshot = Snapshot(path);

            var table = new SeparatedValuesTable(
                snapshot,
                MetadataContext([new SchemaColumn("Missing", 0, typeof(object))]));

            Assert.AreEqual(0, table.Columns.Length);
        });
    }

    private static SourceMetadataContext MetadataContext(IReadOnlyCollection<ISchemaColumn> columns)
    {
        return new SourceMetadataContext(
            "separated-values-header-test",
            CancellationToken.None,
            columns,
            new Dictionary<string, string>(),
            new Mock<ILogger>().Object);
    }

    private static StructuredSchemaSnapshot Snapshot(string path)
    {
        return new BoundedSeparatedValuesSchemaResolver().Resolve(
            new SeparatedValuesSchemaResolutionRequest(
                path,
                ",",
                true,
                0,
                [],
                new Dictionary<string, string>
                {
                    [SeparatedValuesInferenceOptions.MaximumTimeMillisecondsSettingName] = "1000"
                },
                CancellationToken.None)).Snapshot;
    }

    private static void WithCsv(string contents, Action<string> assertion)
    {
        var path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.csv");
        File.WriteAllText(path, contents, new UTF8Encoding(false, true));

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
