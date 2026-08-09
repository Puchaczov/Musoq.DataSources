#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Musoq.DataSources.Structured;

namespace Musoq.DataSources.SeparatedValues.Tests;

[TestClass]
public class SeparatedValuesSchemaDiscoveryTests
{
    [TestMethod]
    public void Discover_HeaderedFile_InfersEverySupportedTypeAndNullability()
    {
        WithCsv(
            "Boolean,Integer,Fraction,Exponent,Text,Optional\n" +
            "true,1,1.25,1e2,alpha,\n" +
            "FALSE,-2,2.50,3E-2,beta,value\n",
            path =>
            {
                var snapshot = Discover(path);

                Assert.AreEqual(2L, snapshot.RowCount);
                AssertColumn(snapshot, "Boolean", typeof(bool), false);
                AssertColumn(snapshot, "Integer", typeof(long), false);
                AssertColumn(snapshot, "Fraction", typeof(decimal), false);
                AssertColumn(snapshot, "Exponent", typeof(double), false);
                AssertColumn(snapshot, "Text", typeof(string), false);
                AssertColumn(snapshot, "Optional", typeof(string), true);
            });
    }

    [TestMethod]
    public void Discover_NumericKinds_WidenWithoutSampling()
    {
        WithCsv("Value\n1\n1.5\n1e2\n", path =>
        {
            var snapshot = Discover(path);

            AssertColumn(snapshot, "Value", typeof(double), false);
            Assert.AreEqual(3L, snapshot.RowCount);
        });
    }

    [TestMethod]
    public void Discover_DecimalOverflow_WidensToDouble()
    {
        WithCsv("Value\n79228162514264337593543950336.0\n", path =>
            AssertColumn(Discover(path), "Value", typeof(double), false));
    }

    [TestMethod]
    public void Discover_IntegerOverflow_FallsBackToString()
    {
        WithCsv("Value\n9223372036854775808\n", path =>
            AssertColumn(Discover(path), "Value", typeof(string), false));
    }

    [TestMethod]
    public void Discover_ConflictingCsvKinds_WidenToString()
    {
        WithCsv("Value\n1\ntext\n", path =>
            AssertColumn(Discover(path), "Value", typeof(string), false));
    }

    [TestMethod]
    public void Discover_QuotedAndUnquotedEmptyFields_RemainDistinct()
    {
        WithCsv("NullValue,EmptyString\n,\"\"\n", path =>
        {
            var snapshot = Discover(path);
            var nullValue = snapshot.Columns.Single(column => column.Name == "NullValue");
            var emptyString = snapshot.Columns.Single(column => column.Name == "EmptyString");

            Assert.AreEqual(StructuredValueKind.String, nullValue.TypeState.Kind);
            Assert.IsTrue(nullValue.TypeState.IsNullable);
            Assert.AreEqual(StructuredValueKind.String, emptyString.TypeState.Kind);
            Assert.IsFalse(emptyString.TypeState.IsNullable);
        });
    }

    [TestMethod]
    public void Discover_HeaderedShortRows_MakeMissingColumnsNullable()
    {
        WithCsv("A,B,C\n1,2,3\n4\n", path =>
        {
            var snapshot = Discover(path);

            AssertColumn(snapshot, "A", typeof(long), false);
            AssertColumn(snapshot, "B", typeof(long?), true);
            AssertColumn(snapshot, "C", typeof(long?), true);
            Assert.AreEqual(2L, snapshot.RowCount);
        });
    }

    [TestMethod]
    public void Discover_HeaderedOverflowRow_IsMalformed()
    {
        WithCsv("A,B\n1,2,3\n", path =>
            Assert.ThrowsExactly<InvalidDataException>(() => Discover(path)));
    }

    [TestMethod]
    public void Discover_HeaderlessLateWidth_UsesMaximumWidthAndNullableMissingFields()
    {
        WithCsv("1\n2,3,4\n5,6\n", path =>
        {
            var snapshot = SeparatedValuesSchemaDiscovery.GetSnapshot(path, ",", false, 0);

            CollectionAssert.AreEqual(
                new[] { "Column1", "Column2", "Column3" },
                snapshot.Columns.Select(column => column.Name).ToArray());
            AssertColumn(snapshot, "Column1", typeof(long), false);
            AssertColumn(snapshot, "Column2", typeof(long?), true);
            AssertColumn(snapshot, "Column3", typeof(long?), true);
            Assert.AreEqual(3L, snapshot.RowCount);
        });
    }

    [TestMethod]
    public void Discover_Headers_AreExactOrdinalAndCaseSensitive()
    {
        WithCsv("First Name,[Odd],a.b,A,a\ntext,text,text,text,text\n", path =>
        {
            var snapshot = Discover(path);

            CollectionAssert.AreEqual(
                new[] { "First Name", "[Odd]", "a.b", "A", "a" },
                snapshot.Columns.Select(column => column.Name).ToArray());
        });
    }

    [TestMethod]
    public void Discover_EmptyOrDuplicateHeader_IsRejected()
    {
        WithCsv(",B\n1,2\n", path =>
            Assert.ThrowsExactly<InvalidDataException>(() => Discover(path)));
        WithCsv("A,A\n1,2\n", path =>
            Assert.ThrowsExactly<InvalidDataException>(() => Discover(path)));
        WithCsv("\"\"\nvalue\n", path =>
            Assert.ThrowsExactly<InvalidDataException>(() => Discover(path)));
    }

    [TestMethod]
    public void Discover_BlankLinesAreSkippedAndMultilineFieldsAreOneRow()
    {
        WithCsv("Id,Notes\r\n\r\n1,\"line one\r\nline \"\"two\"\"\"\r\n\n2,end\n", path =>
        {
            var snapshot = Discover(path);

            Assert.AreEqual(2L, snapshot.RowCount);
            Assert.AreEqual(2, snapshot.Partitions.Sum(partition => checked((int)partition.RowCount)));
            AssertColumn(snapshot, "Notes", typeof(string), false);
        });
    }

    [TestMethod]
    public void Discover_SkipLinesSkipsPhysicalUtf8PreambleBeforeParsing()
    {
        WithCsv("ignored \" unmatched quote\nName,Value\nAda,1\n", path =>
        {
            var snapshot = SeparatedValuesSchemaDiscovery.GetSnapshot(path, ",", true, 1);

            Assert.AreEqual(1L, snapshot.RowCount);
            CollectionAssert.AreEqual(
                new[] { "Name", "Value" },
                snapshot.Columns.Select(column => column.Name).ToArray());
        });
    }

    [TestMethod]
    public void Discover_Utf8BomIsAllowed()
    {
        WithCsv("Name\nZażółć\n", path =>
            AssertColumn(Discover(path), "Name", typeof(string), false), true);
    }

    [TestMethod]
    public void Discover_HeaderlessEmptyFile_ReturnsEmptySnapshot()
    {
        WithCsv(string.Empty, path =>
        {
            var snapshot = SeparatedValuesSchemaDiscovery.GetSnapshot(path, ",", false, 0);

            Assert.AreEqual(0L, snapshot.RowCount);
            Assert.AreEqual(0, snapshot.Columns.Length);
            Assert.AreEqual(0, snapshot.Partitions.Length);
        });
    }

    [TestMethod]
    public void Discover_HeaderedEmptyFile_IsRejected()
    {
        WithCsv(string.Empty, path =>
            Assert.ThrowsExactly<InvalidDataException>(() => Discover(path)));
    }

    [TestMethod]
    public void Discover_HeaderOnly_DefaultsUnresolvedColumnsToString()
    {
        WithCsv("A,B\n", path =>
        {
            var snapshot = Discover(path);

            AssertColumn(snapshot, "A", typeof(string), false);
            AssertColumn(snapshot, "B", typeof(string), false);
            Assert.AreEqual(0L, snapshot.RowCount);
        });
    }

    [TestMethod]
    public void Discover_StrictGrammar_RejectsMalformedRecords()
    {
        string[] malformed =
        [
            "A\n\"unterminated\n",
            "A,B\nab\"cd,value\n",
            "A,B\n\"value\"x,next\n",
            "A,B\nfirst\rsecond,next\n"
        ];

        foreach (var contents in malformed)
        {
            WithCsv(contents, path =>
                Assert.ThrowsExactly<InvalidDataException>(() => Discover(path)));
        }
    }

    [TestMethod]
    public void Discover_InvalidUtf8_IsRejected()
    {
        var path = TempPath();
        File.WriteAllBytes(path, [.. "Name\n"u8, 0xff, (byte)'\n']);

        try
        {
            Assert.ThrowsExactly<InvalidDataException>(() => Discover(path));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [TestMethod]
    public void Discover_MissingFile_FailsDuringDiscovery()
    {
        Assert.ThrowsExactly<FileNotFoundException>(() =>
            Discover(Path.Combine(Path.GetTempPath(), $"missing-{Guid.NewGuid():N}.csv")));
    }

    [TestMethod]
    public void Discover_CancelledRequest_StopsDiscovery()
    {
        WithCsv("Name\nAda\n", path =>
        {
            using var cancellation = new CancellationTokenSource();
            cancellation.Cancel();

            Assert.ThrowsExactly<OperationCanceledException>(() =>
                SeparatedValuesSchemaDiscovery.GetSnapshot(path, ",", true, 0, cancellation.Token));
        });
    }

    [TestMethod]
    public void Discover_UnchangedIdentity_ReturnsProcessCacheHit()
    {
        WithCsv("Name\nAda\n", path =>
        {
            var cold = SeparatedValuesSchemaDiscovery.GetSnapshotWithAccess(path, ",", true, 0);
            var cached = SeparatedValuesSchemaDiscovery.GetSnapshotWithAccess(path, ",", true, 0);

            Assert.AreEqual(StructuredSnapshotCacheAccess.Discovered, cold.Access);
            Assert.AreEqual(StructuredSnapshotCacheAccess.Hit, cached.Access);
            Assert.AreSame(cold.Snapshot, cached.Snapshot);
        });
    }

    [TestMethod]
    public void Discover_ParserOptionsHaveIndependentCacheEntries()
    {
        WithCsv("A,B\n1,2\n", path =>
        {
            var headered = SeparatedValuesSchemaDiscovery.GetSnapshotWithAccess(path, ",", true, 0);
            var headerless = SeparatedValuesSchemaDiscovery.GetSnapshotWithAccess(path, ",", false, 0);

            Assert.AreEqual(1L, headered.Snapshot.RowCount);
            Assert.AreEqual(2L, headerless.Snapshot.RowCount);
            Assert.AreEqual(StructuredSnapshotCacheAccess.Discovered, headerless.Access);
        });
    }

    [TestMethod]
    public void Discover_ChangedFingerprint_RediscoversSchema()
    {
        WithCsv("First\nvalue\n", path =>
        {
            var before = SeparatedValuesSchemaDiscovery.GetSnapshotWithAccess(path, ",", true, 0);
            File.WriteAllText(path, "Other\nvalue\n", new UTF8Encoding(false, true));
            var after = SeparatedValuesSchemaDiscovery.GetSnapshotWithAccess(path, ",", true, 0);

            Assert.AreEqual(StructuredSnapshotCacheAccess.Discovered, before.Access);
            Assert.AreEqual(StructuredSnapshotCacheAccess.Discovered, after.Access);
            Assert.AreEqual("First", before.Snapshot.Columns[0].Name);
            Assert.AreEqual("Other", after.Snapshot.Columns[0].Name);
        });
    }

    private static StructuredSchemaSnapshot Discover(string path)
    {
        return SeparatedValuesSchemaDiscovery.GetSnapshot(path, ",", true, 0);
    }

    private static void AssertColumn(
        StructuredSchemaSnapshot snapshot,
        string name,
        Type clrType,
        bool nullable)
    {
        var column = snapshot.Columns.Single(item => item.Name == name);
        Assert.AreEqual(clrType, column.ClrType, name);
        Assert.AreEqual(nullable, column.TypeState.IsNullable, name);
    }

    private static void WithCsv(string contents, Action<string> assertion, bool bom = false)
    {
        var path = TempPath();
        File.WriteAllText(path, contents, new UTF8Encoding(bom, true));

        try
        {
            assertion(path);
        }
        finally
        {
            File.Delete(path);
        }
    }

    private static string TempPath()
    {
        return Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.csv");
    }
}
