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
public class SeparatedValuesSchemaInferenceTests
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
                AssertColumn(snapshot, "Boolean", typeof(bool?), true);
                AssertColumn(snapshot, "Integer", typeof(long?), true);
                AssertColumn(snapshot, "Fraction", typeof(decimal?), true);
                AssertColumn(snapshot, "Exponent", typeof(double?), true);
                AssertColumn(snapshot, "Text", typeof(string), true);
                AssertColumn(snapshot, "Optional", typeof(string), true);
            });
    }

    [TestMethod]
    public void Discover_NumericKinds_WidenWithinBoundedSample()
    {
        WithCsv("Value\n1\n1.5\n1e2\n", path =>
        {
            var snapshot = Discover(path);

            AssertColumn(snapshot, "Value", typeof(double?), true);
            Assert.AreEqual(3L, snapshot.RowCount);
        });
    }

    [TestMethod]
    public void Discover_DecimalOverflow_WidensToDouble()
    {
        WithCsv("Value\n79228162514264337593543950336.0\n", path =>
            AssertColumn(Discover(path), "Value", typeof(double?), true));
    }

    [TestMethod]
    public void Discover_IntegerOverflow_FallsBackToString()
    {
        WithCsv("Value\n9223372036854775808\n", path =>
            AssertColumn(Discover(path), "Value", typeof(string), true));
    }

    [TestMethod]
    public void Discover_ConflictingCsvKinds_WidenToString()
    {
        WithCsv("Value\n1\ntext\n", path =>
            AssertColumn(Discover(path), "Value", typeof(string), true));
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
            Assert.IsTrue(emptyString.TypeState.IsNullable);
        });
    }

    [TestMethod]
    public void Discover_HeaderedShortRows_MakeMissingColumnsNullable()
    {
        WithCsv("A,B,C\n1,2,3\n4\n", path =>
        {
            var snapshot = Discover(path);

            AssertColumn(snapshot, "A", typeof(long?), true);
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
            var snapshot = Discover(path, false);

            CollectionAssert.AreEqual(
                new[] { "Column1", "Column2", "Column3" },
                snapshot.Columns.Select(column => column.Name).ToArray());
            AssertColumn(snapshot, "Column1", typeof(long?), true);
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
            Assert.IsTrue(snapshot.Partitions.IsEmpty);
            AssertColumn(snapshot, "Notes", typeof(string), true);
        });
    }

    [TestMethod]
    public void Discover_SkipLinesSkipsPhysicalUtf8PreambleBeforeParsing()
    {
        WithCsv("ignored \" unmatched quote\nName,Value\nAda,1\n", path =>
        {
            var snapshot = Discover(path, true, 1);

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
            AssertColumn(Discover(path), "Name", typeof(string), true), true);
    }

    [TestMethod]
    public void Discover_HeaderlessEmptyFile_ReturnsEmptySnapshot()
    {
        WithCsv(string.Empty, path =>
        {
            var snapshot = Discover(path, false);

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

            AssertColumn(snapshot, "A", typeof(string), true);
            AssertColumn(snapshot, "B", typeof(string), true);
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
                Resolve(path, true, 0, cancellation.Token));
        });
    }

    [TestMethod]
    public void Discover_UnchangedIdentity_ProducesEquivalentBoundedContracts()
    {
        WithCsv("Name\nAda\n", path =>
        {
            var first = Resolve(path);
            var second = Resolve(path);

            Assert.AreEqual(first.Snapshot.Identity, second.Snapshot.Identity);
            Assert.AreEqual(first.InspectedRows, second.InspectedRows);
            Assert.AreNotSame(first.Snapshot, second.Snapshot);
        });
    }

    [TestMethod]
    public void Discover_ParserOptionsProduceDistinctIdentities()
    {
        WithCsv("A,B\n1,2\n", path =>
        {
            var headered = Resolve(path, true);
            var headerless = Resolve(path, false);

            Assert.AreEqual(1L, headered.Snapshot.RowCount);
            Assert.AreEqual(2L, headerless.Snapshot.RowCount);
            Assert.AreNotEqual(
                headered.Snapshot.Identity.ParserOptions,
                headerless.Snapshot.Identity.ParserOptions);
        });
    }

    [TestMethod]
    public void Discover_ChangedFingerprint_ProducesANewContract()
    {
        WithCsv("First\nvalue\n", path =>
        {
            var before = Resolve(path);
            File.WriteAllText(path, "Other\nvalue\n", new UTF8Encoding(false, true));
            var after = Resolve(path);

            Assert.AreNotEqual(before.Snapshot.Identity.Fingerprint, after.Snapshot.Identity.Fingerprint);
            Assert.AreEqual("First", before.Snapshot.Columns[0].Name);
            Assert.AreEqual("Other", after.Snapshot.Columns[0].Name);
        });
    }

    private static StructuredSchemaSnapshot Discover(
        string path,
        bool hasHeader = true,
        int skipLines = 0)
    {
        return Resolve(path, hasHeader, skipLines).Snapshot;
    }

    private static SeparatedValuesSourceContract Resolve(
        string path,
        bool hasHeader = true,
        int skipLines = 0,
        CancellationToken cancellationToken = default)
    {
        return new BoundedSeparatedValuesSchemaResolver().Resolve(
            new SeparatedValuesSchemaResolutionRequest(
                path,
                ",",
                hasHeader,
                skipLines,
                [],
                new Dictionary<string, string>
                {
                    [SeparatedValuesInferenceOptions.MaximumTimeMillisecondsSettingName] = "1000"
                },
                cancellationToken));
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
