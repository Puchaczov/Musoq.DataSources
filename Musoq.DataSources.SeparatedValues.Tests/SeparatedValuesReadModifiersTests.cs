using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using Musoq.DataSources.Tests.Common;
using Musoq.Schema;
using Musoq.Schema.DataSources;
using Musoq.Schema.Optimization;

namespace Musoq.DataSources.SeparatedValues.Tests;

[TestClass]
public class SeparatedValuesReadModifiersTests
{
    static SeparatedValuesReadModifiersTests()
    {
        _ = SeparatedValuesReadModifiers.ResolveFileEncodingOrDefault([]);
    }

    [TestMethod]
    public void FileAndStreamSources_WhenNoModifiers_ShouldKeepDefaultUtf8Behavior()
    {
        var columns = Columns(Column("Name", 0, typeof(string)));
        var fileRows = ReadFileRows("Name\r\nAlice\r\n", Encoding.UTF8, columns);
        var streamRows = ReadStreamRows("Name\r\nAlice\r\n", Encoding.UTF8, columns);

        Assert.AreEqual("Alice", fileRows.Single()[0]);
        Assert.AreEqual("Alice", streamRows.Single()[0]);
    }

    [TestMethod]
    public void FileRowsSource_WhenUtf8HasBomOrNoBom_ShouldReadRows()
    {
        var columns = Columns(Column("Name", 0, typeof(string)));
        var bomRows = ReadFileRows("Name\r\nAlice\r\n", new UTF8Encoding(true), columns);
        var noBomRows = ReadFileRows("Name\r\nBob\r\n", new UTF8Encoding(false), columns);

        Assert.AreEqual("Alice", bomRows.Single()[0]);
        Assert.AreEqual("Bob", noBomRows.Single()[0]);
    }

    [TestMethod]
    public void FileRowsSource_WhenFileWideUtf16LeEncodingIsDeclared_ShouldReadRows()
    {
        var columns = Columns(Column("Name", 0, typeof(string), EncodingModifier("utf-16le")));
        var rows = ReadFileRows("Name\r\nLodz\r\n", new UnicodeEncoding(false, false), columns);

        Assert.AreEqual("Lodz", rows.Single()[0]);
    }

    [TestMethod]
    public void TableColumns_WhenHeaderIsUtf16Le_ShouldInferHeaderWithDeclaredEncoding()
    {
        var path = WriteTempFile("Name\r\nAlice\r\n", new UnicodeEncoding(false, false));

        try
        {
            var table = new SeparatedValuesTable(path, ",", true, 0)
            {
                InferredColumns = Columns(Column("Name", 0, typeof(string), EncodingModifier("utf-16le")))
            };

            Assert.AreEqual("Name", table.Columns.Single().ColumnName);
            Assert.AreEqual("utf-16le", table.Columns.Single().ReadModifiers[ColumnReadModifiers.Encoding]);
        }
        finally
        {
            DeleteIfExists(path);
        }
    }

    [TestMethod]
    public void FileRowsSource_WhenWindows1250EncodingIsDeclared_ShouldReadRows()
    {
        var encoding = Encoding.GetEncoding("windows-1250");
        var columns = Columns(Column("Name", 0, typeof(string), EncodingModifier("windows-1250")));
        var rows = ReadFileRows("Name\r\nZażółć\r\n", encoding, columns);

        Assert.AreEqual("Zażółć", rows.Single()[0]);
    }

    [TestMethod]
    public void FileRowsSource_WhenNoEncodingModifierAndUtf8IsInvalid_ShouldUseReplacementFallback()
    {
        var path = WriteTempFileBytes([.. Encoding.ASCII.GetBytes("Name\r\nA"), 0xFF, .. Encoding.ASCII.GetBytes("\r\n")]);

        try
        {
            var columns = Columns(Column("Name", 0, typeof(string)));
            var context = RuntimeV2TestContexts.CreateExecutionContext(
                CancellationToken.None,
                columns,
                executionPlan: SourceExecutionPlan.Empty(SourceIdentity.Empty));
            var source = new SeparatedValuesFromFileRowsSource(path, ",", true, 0, context);

            var rows = source.Chunks.SelectMany(static chunk => chunk).ToArray();

            Assert.AreEqual($"A{'\uFFFD'}", rows.Single()[0]);
        }
        finally
        {
            DeleteIfExists(path);
        }
    }

    [TestMethod]
    public void FileRowsSource_WhenExplicitUtf8EncodingIsInvalid_ShouldThrowDecoderFallbackException()
    {
        var path = WriteTempFileBytes([.. Encoding.ASCII.GetBytes("Name\r\nA"), 0xFF, .. Encoding.ASCII.GetBytes("\r\n")]);

        try
        {
            var columns = Columns(Column("Name", 0, typeof(string), EncodingModifier("utf-8")));
            var context = RuntimeV2TestContexts.CreateExecutionContext(
                CancellationToken.None,
                columns,
                executionPlan: SourceExecutionPlan.Empty(SourceIdentity.Empty));
            var source = new SeparatedValuesFromFileRowsSource(path, ",", true, 0, context);

            Assert.ThrowsException<DecoderFallbackException>(() =>
                source.Chunks.SelectMany(static chunk => chunk).ToArray());
        }
        finally
        {
            DeleteIfExists(path);
        }
    }

    [TestMethod]
    public void DescribeAndPlan_WhenNormalColumnsUseSameEncoding_ShouldNotReportError()
    {
        var path = WriteTempFile("Name,Other\r\nAlice,Bob\r\n", Encoding.UTF8);

        try
        {
            var columns = Columns(
                Column("Name", 0, typeof(string), EncodingModifier("utf8")),
                Column("Other", 1, typeof(string), EncodingModifier("utf-8")));
            var descriptor = Describe(path, columns);
            var plan = Plan(columns.Select(ToColumnRef).ToArray());

            Assert.IsFalse(descriptor.ContractDiagnostics.Any(IsError));
            Assert.IsFalse(plan.ContractDiagnostics.Any(IsError));
        }
        finally
        {
            DeleteIfExists(path);
        }
    }

    [TestMethod]
    public void DescribeAndPlan_WhenNormalColumnsUseConflictingEncodings_ShouldReportError()
    {
        var path = WriteTempFile("Name,Other\r\nAlice,Bob\r\n", Encoding.UTF8);

        try
        {
            var columns = Columns(
                Column("Name", 0, typeof(string), EncodingModifier("utf-8")),
                Column("Other", 1, typeof(string), EncodingModifier("utf-16le")));
            var descriptor = Describe(path, columns);
            var plan = Plan(columns.Select(ToColumnRef).ToArray());

            AssertDiagnostic(
                descriptor.ContractDiagnostics,
                "SeparatedValuesInconsistentEncoding",
                "Other",
                ColumnReadModifiers.Encoding);
            AssertDiagnostic(
                plan.ContractDiagnostics,
                "SeparatedValuesInconsistentEncoding",
                "Other",
                ColumnReadModifiers.Encoding);
        }
        finally
        {
            DeleteIfExists(path);
        }
    }

    [TestMethod]
    public void DescribeAndPlan_WhenEncodingIsUnsupported_ShouldReportError()
    {
        var path = WriteTempFile("Name\r\nAlice\r\n", Encoding.UTF8);

        try
        {
            var columns = Columns(Column("Name", 0, typeof(string), EncodingModifier("not-a-real-encoding")));
            var descriptor = Describe(path, columns);
            var plan = Plan(columns.Select(ToColumnRef).ToArray());

            AssertDiagnostic(
                descriptor.ContractDiagnostics,
                "SeparatedValuesUnsupportedEncoding",
                "Name",
                ColumnReadModifiers.Encoding);
            AssertDiagnostic(
                plan.ContractDiagnostics,
                "SeparatedValuesUnsupportedEncoding",
                "Name",
                ColumnReadModifiers.Encoding);
        }
        finally
        {
            DeleteIfExists(path);
        }
    }

    [TestMethod]
    public void DescribeAndPlan_WhenModifierIsUnsupported_ShouldReportWarningWithColumnAndModifier()
    {
        var path = WriteTempFile("Name\r\nAlice\r\n", Encoding.UTF8);

        try
        {
            var columns = Columns(Column("Name", 0, typeof(string), new Dictionary<string, string>
            {
                ["pad"] = "left"
            }));
            var descriptor = Describe(path, columns);
            var plan = Plan(columns.Select(ToColumnRef).ToArray());

            AssertDiagnostic(
                descriptor.ContractDiagnostics,
                "SeparatedValuesUnsupportedModifier",
                "Name",
                "pad",
                SourceContractDiagnosticSeverity.Warning);
            AssertDiagnostic(
                plan.ContractDiagnostics,
                "SeparatedValuesUnsupportedModifier",
                "Name",
                "pad",
                SourceContractDiagnosticSeverity.Warning);
        }
        finally
        {
            DeleteIfExists(path);
        }
    }

    [TestMethod]
    public void DescribeAndPlan_WhenCultureIsUnsupported_ShouldReportError()
    {
        var path = WriteTempFile("Amount\r\n1.23\r\n", Encoding.UTF8);

        try
        {
            var columns = Columns(Column("Amount", 0, typeof(decimal), new Dictionary<string, string>
            {
                [ColumnReadModifiers.Culture] = "invalid_culture"
            }));
            var descriptor = Describe(path, columns);
            var plan = Plan(columns.Select(ToColumnRef).ToArray());

            AssertDiagnostic(
                descriptor.ContractDiagnostics,
                "SeparatedValuesUnsupportedCulture",
                "Amount",
                ColumnReadModifiers.Culture);
            AssertDiagnostic(
                plan.ContractDiagnostics,
                "SeparatedValuesUnsupportedCulture",
                "Amount",
                ColumnReadModifiers.Culture);
        }
        finally
        {
            DeleteIfExists(path);
        }
    }

    [TestMethod]
    public void Describe_WhenUnsupportedEncodingIsDeclaredForMissingFile_ShouldReturnContractDiagnostic()
    {
        var path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.csv");
        var columns = Columns(Column("Name", 0, typeof(string), EncodingModifier("not-a-real-encoding")));

        var descriptor = Describe(path, columns);

        AssertDiagnostic(
            descriptor.ContractDiagnostics,
            "SeparatedValuesUnsupportedEncoding",
            "Name",
            ColumnReadModifiers.Encoding);
    }

    [TestMethod]
    public void Describe_WhenConflictingEncodingsAreDeclaredForMissingFile_ShouldReturnContractDiagnostic()
    {
        var path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.csv");
        var columns = Columns(
            Column("Name", 0, typeof(string), EncodingModifier("utf-8")),
            Column("Other", 1, typeof(string), EncodingModifier("utf-16le")));

        var descriptor = Describe(path, columns);

        AssertDiagnostic(
            descriptor.ContractDiagnostics,
            "SeparatedValuesInconsistentEncoding",
            "Other",
            ColumnReadModifiers.Encoding);
    }

    [TestMethod]
    public void RowParser_WhenTrimIsDeclared_ShouldTrimBeforeStringNullHandling()
    {
        var columns = Columns(Column("Name", 0, typeof(string), TrimModifier()));
        var rows = ReadFileRows("Name\r\n   \r\n", Encoding.UTF8, columns);

        Assert.IsNull(rows.Single()[0]);
    }

    [TestMethod]
    public void RowParser_WhenTrimIsDeclared_ShouldTrimBeforeNumericConversion()
    {
        var columns = Columns(Column("Age", 0, typeof(int), TrimModifier()));
        var rows = ReadFileRows("Age\r\n 42 \r\n", Encoding.UTF8, columns);

        Assert.AreEqual(42, rows.Single()[0]);
    }

    [TestMethod]
    public void RowParser_WhenCultureIsDeclared_ShouldParseDecimalUsingThatCulture()
    {
        var columns = Columns(Column("Amount", 0, typeof(decimal), new Dictionary<string, string>
        {
            [ColumnReadModifiers.Culture] = "pl-PL"
        }));
        var rows = ReadFileRows("Amount\r\n\"1234,56\"\r\n", Encoding.UTF8, columns);

        Assert.AreEqual(1234.56m, rows.Single()[0]);
    }

    [TestMethod]
    public void RowParser_WhenNoCultureIsDeclared_ShouldKeepCurrentCultureParsingBehavior()
    {
        var originalCulture = CultureInfo.CurrentCulture;

        try
        {
            Culture.Apply(CultureInfo.GetCultureInfo("en-US"));
            var columns = Columns(Column("Amount", 0, typeof(decimal)));
            var rows = ReadFileRows("Amount\r\n1234.56\r\n", Encoding.UTF8, columns);

            Assert.AreEqual(1234.56m, rows.Single()[0]);
        }
        finally
        {
            Culture.Apply(originalCulture);
        }
    }

    [TestMethod]
    public void RowParser_WhenDateFormatIsDeclared_ShouldParseDateTimeExactly()
    {
        var columns = Columns(Column("When", 0, typeof(DateTime), new Dictionary<string, string>
        {
            [ColumnReadModifiers.Culture] = "pl-PL",
            [ColumnReadModifiers.Format] = "dd.MM.yyyy"
        }));
        var rows = ReadFileRows("When\r\n31.12.2023\r\n", Encoding.UTF8, columns);

        Assert.AreEqual(new DateTime(2023, 12, 31), rows.Single()[0]);
    }

    [TestMethod]
    public void RowParser_WhenDateTimeOffsetFormatIsDeclared_ShouldParseDateTimeOffsetExactly()
    {
        var columns = Columns(Column("When", 0, typeof(DateTimeOffset), new Dictionary<string, string>
        {
            [ColumnReadModifiers.Format] = "O"
        }));
        var rows = ReadFileRows("When\r\n2023-12-31T10:15:30.0000000+02:00\r\n", Encoding.UTF8, columns);

        Assert.AreEqual(new DateTimeOffset(2023, 12, 31, 10, 15, 30, TimeSpan.FromHours(2)), rows.Single()[0]);
    }

    [TestMethod]
    public void RowParser_WhenTimeSpanFormatIsDeclared_ShouldParseTimeSpanExactly()
    {
        var columns = Columns(Column("Duration", 0, typeof(TimeSpan), new Dictionary<string, string>
        {
            [ColumnReadModifiers.Format] = "c"
        }));
        var rows = ReadFileRows("Duration\r\n1.02:03:04\r\n", Encoding.UTF8, columns);

        Assert.AreEqual(new TimeSpan(1, 2, 3, 4), rows.Single()[0]);
    }

    [TestMethod]
    public void RowParser_WhenObjectShapedTypeIsUnsupported_ShouldNotReturnString()
    {
        var columns = Columns(Column("Uri", 0, typeof(Uri)));

        Assert.ThrowsException<NotSupportedException>(() =>
            ReadFileRows("Uri\r\nhttps://example.com\r\n", Encoding.UTF8, columns));
    }

    [TestMethod]
    public void Describe_WhenNumericFormatIsDeclared_ShouldReportUnsupportedFormat()
    {
        var path = WriteTempFile("Amount\r\n12.5\r\n", Encoding.UTF8);

        try
        {
            var columns = Columns(Column("Amount", 0, typeof(decimal), new Dictionary<string, string>
            {
                [ColumnReadModifiers.Format] = "#,##0.00"
            }));
            var descriptor = Describe(path, columns);

            AssertDiagnostic(
                descriptor.ContractDiagnostics,
                "SeparatedValuesUnsupportedFormat",
                "Amount",
                ColumnReadModifiers.Format);
        }
        finally
        {
            DeleteIfExists(path);
        }
    }

    [TestMethod]
    public void FileRowsSource_WhenPredicateUsesCultureConversion_ShouldMatchProjectionConversion()
    {
        var columns = Columns(Column("Amount", 0, typeof(decimal), new Dictionary<string, string>
        {
            [ColumnReadModifiers.Culture] = "pl-PL"
        }));
        var predicate = new SourcePredicateComparison(
            SourcePredicateComparisonOperator.GreaterThan,
            new SourcePredicateColumn(new SourceColumnRef("Amount")),
            new SourcePredicateLiteral(2m));
        var plan = new SourceExecutionPlan
        {
            Identity = SourceIdentity.Empty,
            AcceptedColumns = [new SourceColumnRef("Amount")],
            AcceptedPredicate = predicate,
            AcceptedOrderBy = []
        };
        var rows = ReadFileRows("Amount\r\n\"1,23\"\r\n\"2,34\"\r\n", Encoding.UTF8, columns, plan);

        Assert.AreEqual(1, rows.Length);
        Assert.AreEqual(2.34m, rows.Single()[0]);
    }

    [TestMethod]
    public void TryPlanSource_WhenProjectionIsAccepted_ShouldPreserveReadModifiers()
    {
        var requiredColumns = new[]
        {
            new SourceColumnRef("Name", new Dictionary<string, string>
            {
                [ColumnReadModifiers.Encoding] = "utf-8",
                [ColumnReadModifiers.Trim] = "true"
            })
        };
        var result = Plan(requiredColumns);

        Assert.AreEqual("utf-8", result.AcceptedColumns.Single().ReadModifiers[ColumnReadModifiers.Encoding]);
        Assert.AreEqual("true", result.ExecutionPlan.AcceptedColumns.Single().ReadModifiers[ColumnReadModifiers.Trim]);
    }

    [TestMethod]
    public void FileRowsSource_WhenZeroColumnFastPathUsesModifierColumns_ShouldEmitEmptyRows()
    {
        var columns = Columns(Column("Name", 0, typeof(string), TrimModifier()));
        var plan = Plan([]).ExecutionPlan;
        var rows = ReadFileRows("Name\r\n Alice \r\n", Encoding.UTF8, columns, plan);

        Assert.AreEqual(1, rows.Length);
        Assert.AreEqual(0, rows.Single().Length);
    }

    [TestMethod]
    public void FileRowsSource_WhenSkipAndTakeAreAcceptedWithModifiers_ShouldKeepSliceBehavior()
    {
        var columns = Columns(Column("Name", 0, typeof(string), TrimModifier()));
        var plan = Plan([new SourceColumnRef("Name")], skip: 1, take: 1).ExecutionPlan;
        var rows = ReadFileRows("Name\r\n Alice \r\n Bob \r\n Carol \r\n", Encoding.UTF8, columns, plan);

        Assert.AreEqual(1, rows.Length);
        Assert.AreEqual("Bob", rows.Single()[0]);
    }

    [TestMethod]
    public void FileRowsSource_WhenHeaderlessFileUsesAutoColumnName_ShouldHonorModifiers()
    {
        var columns = Columns(Column("Column1", 0, typeof(string), TrimModifier()));
        var rows = ReadFileRows(" Alice \r\n", Encoding.UTF8, columns, hasHeader: false);

        Assert.AreEqual("Alice", rows.Single()[0]);
    }

    [TestMethod]
    public void FileRowsSource_WhenFileIsMissing_ShouldKeepExistingNoRowsBehavior()
    {
        var path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.csv");
        var columns = Columns(Column("Name", 0, typeof(string), TrimModifier()));
        var context = RuntimeV2TestContexts.CreateExecutionContext(
            CancellationToken.None,
            columns,
            executionPlan: SourceExecutionPlan.Empty(SourceIdentity.Empty));
        var source = new SeparatedValuesFromFileRowsSource(path, ",", true, 0, context);

        var rows = source.Chunks.SelectMany(static chunk => chunk).ToArray();

        Assert.AreEqual(0, rows.Length);
    }

    [TestMethod]
    public void FileRowsSource_WhenConversionFails_ShouldReturnNull()
    {
        var columns = Columns(Column("Age", 0, typeof(int)));
        var rows = ReadFileRows("Age\r\nnot-number\r\n", Encoding.UTF8, columns);

        Assert.IsNull(rows.Single()[0]);
    }

    [TestMethod]
    public void RowParser_WhenBase64Utf8CodecIsDeclared_ShouldDecodePayload()
    {
        var payload = Convert.ToBase64String(Encoding.UTF8.GetBytes("Hello"));
        var columns = Columns(Column("Payload", 0, typeof(string), new Dictionary<string, string>
        {
            [SeparatedValuesReadModifiers.SourceCodec] = "base64",
            [ColumnReadModifiers.Encoding] = "utf-8"
        }));
        var rows = ReadFileRows($"Payload\r\n{payload}\r\n", Encoding.UTF8, columns);

        Assert.AreEqual("Hello", rows.Single()[0]);
    }

    [TestMethod]
    public void RowParser_WhenBase64Windows1250CodecIsDeclared_ShouldDecodePayload()
    {
        var payload = Convert.ToBase64String(Encoding.GetEncoding("windows-1250").GetBytes("Zażółć"));
        var columns = Columns(Column("Payload", 0, typeof(string), new Dictionary<string, string>
        {
            [SeparatedValuesReadModifiers.SourceCodec] = "base64",
            [ColumnReadModifiers.Encoding] = "windows-1250"
        }));
        var rows = ReadFileRows($"Payload\r\n{payload}\r\n", Encoding.UTF8, columns);

        Assert.AreEqual("Zażółć", rows.Single()[0]);
    }

    [TestMethod]
    public void RowParser_WhenHexUtf8CodecIsDeclared_ShouldDecodePayload()
    {
        var payload = Convert.ToHexString(Encoding.UTF8.GetBytes("Hello"));
        var columns = Columns(Column("Payload", 0, typeof(string), new Dictionary<string, string>
        {
            [SeparatedValuesReadModifiers.SourceCodec] = "hex",
            [ColumnReadModifiers.Encoding] = "utf-8"
        }));
        var rows = ReadFileRows($"Payload\r\n{payload}\r\n", Encoding.UTF8, columns);

        Assert.AreEqual("Hello", rows.Single()[0]);
    }

    [TestMethod]
    public void RowParser_WhenSourceCodecPayloadIsInvalid_ShouldReturnNull()
    {
        var base64Columns = Columns(Column("Payload", 0, typeof(string), new Dictionary<string, string>
        {
            [SeparatedValuesReadModifiers.SourceCodec] = "base64"
        }));
        var hexColumns = Columns(Column("Payload", 0, typeof(string), new Dictionary<string, string>
        {
            [SeparatedValuesReadModifiers.SourceCodec] = "hex"
        }));

        var base64Rows = ReadFileRows("Payload\r\nnot-base64\r\n", Encoding.UTF8, base64Columns);
        var hexRows = ReadFileRows("Payload\r\nnot-hex\r\n", Encoding.UTF8, hexColumns);

        Assert.IsNull(base64Rows.Single()[0]);
        Assert.IsNull(hexRows.Single()[0]);
    }

    [TestMethod]
    public void DescribeAndPlan_WhenSourceCodecIsUnknown_ShouldReportError()
    {
        var path = WriteTempFile("Payload\r\nabc\r\n", Encoding.UTF8);

        try
        {
            var columns = Columns(Column("Payload", 0, typeof(string), new Dictionary<string, string>
            {
                [SeparatedValuesReadModifiers.SourceCodec] = "gzip"
            }));
            var descriptor = Describe(path, columns);
            var plan = Plan(columns.Select(ToColumnRef).ToArray());

            AssertDiagnostic(
                descriptor.ContractDiagnostics,
                "SeparatedValuesUnsupportedSourceCodec",
                "Payload",
                SeparatedValuesReadModifiers.SourceCodec);
            AssertDiagnostic(
                plan.ContractDiagnostics,
                "SeparatedValuesUnsupportedSourceCodec",
                "Payload",
                SeparatedValuesReadModifiers.SourceCodec);
        }
        finally
        {
            DeleteIfExists(path);
        }
    }

    [TestMethod]
    public void DescribeAndPlan_WhenAllConflictingEncodingColumnsUseSourceCodec_ShouldAllowConflict()
    {
        var path = WriteTempFile("Payload1,Payload2\r\nabc,def\r\n", Encoding.UTF8);

        try
        {
            var columns = Columns(
                Column("Payload1", 0, typeof(string), new Dictionary<string, string>
                {
                    [SeparatedValuesReadModifiers.SourceCodec] = "base64",
                    [ColumnReadModifiers.Encoding] = "utf-8"
                }),
                Column("Payload2", 1, typeof(string), new Dictionary<string, string>
                {
                    [SeparatedValuesReadModifiers.SourceCodec] = "hex",
                    [ColumnReadModifiers.Encoding] = "utf-16le"
                }));
            var descriptor = Describe(path, columns);
            var plan = Plan(columns.Select(ToColumnRef).ToArray());

            Assert.IsFalse(descriptor.ContractDiagnostics.Any(IsError));
            Assert.IsFalse(plan.ContractDiagnostics.Any(IsError));
        }
        finally
        {
            DeleteIfExists(path);
        }
    }

    [TestMethod]
    public void FileAndStreamSources_WhenModifiersAreShared_ShouldProduceSameRows()
    {
        var columns = Columns(
            Column("Name", 0, typeof(string), TrimModifier()),
            Column("Amount", 1, typeof(decimal), new Dictionary<string, string>
            {
                [ColumnReadModifiers.Culture] = "pl-PL"
            }));
        const string content = "Name,Amount\r\n Alice ,\"1,23\"\r\n";
        var fileRows = ReadFileRows(content, Encoding.UTF8, columns);
        var streamRows = ReadStreamRows(content, Encoding.UTF8, columns);

        CollectionAssert.AreEqual(fileRows.Single(), streamRows.Single());
    }

    private static object[][] ReadFileRows(
        string content,
        Encoding encoding,
        IReadOnlyCollection<ISchemaColumn> columns,
        SourceExecutionPlan plan = null,
        bool hasHeader = true)
    {
        var path = WriteTempFile(content, encoding);

        try
        {
            var context = RuntimeV2TestContexts.CreateExecutionContext(
                CancellationToken.None,
                columns,
                executionPlan: plan ?? SourceExecutionPlan.Empty(SourceIdentity.Empty));
            var source = new SeparatedValuesFromFileRowsSource(path, ",", hasHeader, 0, context);

            return source.Chunks.SelectMany(static chunk => chunk).ToArray();
        }
        finally
        {
            DeleteIfExists(path);
        }
    }

    private static object[][] ReadStreamRows(
        string content,
        Encoding encoding,
        IReadOnlyCollection<ISchemaColumn> columns,
        SourceExecutionPlan plan = null)
    {
        using var stream = new MemoryStream(encoding.GetBytes(content));
        var context = RuntimeV2TestContexts.CreateExecutionContext(
            CancellationToken.None,
            columns,
            executionPlan: plan ?? SourceExecutionPlan.Empty(SourceIdentity.Empty));
        var source = new SeparatedValuesFromStreamRowsSource(stream, ",", true, 0, context);

        return source.Chunks.SelectMany(static chunk => chunk).ToArray();
    }

    private static SourceDescriptor Describe(string path, IReadOnlyCollection<ISchemaColumn> columns)
    {
        var metadataContext = new SourceMetadataContext(
            "test-query",
            CancellationToken.None,
            columns,
            new Dictionary<string, string>(),
            new Mock<ILogger>().Object);
        var describeContext = new SourceDescribeContext(SourceIdentity.Empty, metadataContext);

        return new SeparatedValuesSchema().DescribeSource("comma", describeContext, path, true, 0);
    }

    private static SourcePlanResult Plan(
        IReadOnlyList<SourceColumnRef> requiredColumns,
        long? skip = null,
        long? take = null)
    {
        var request = new SourcePlanRequest
        {
            Identity = SourceIdentity.Empty,
            RequiredColumns = requiredColumns,
            SourceRuntimeSettings = new Dictionary<string, string>(),
            Predicate = null,
            OrderBy = [],
            Skip = skip,
            Take = take
        };

        return new SeparatedValuesSchema().TryPlanSource("comma", request, "data.csv", true, 0);
    }

    private static ISchemaColumn[] Columns(params ISchemaColumn[] columns)
    {
        return columns;
    }

    private static ISchemaColumn Column(
        string name,
        int index,
        Type type,
        IReadOnlyDictionary<string, string> modifiers = null)
    {
        return new SchemaColumn(name, index, type, modifiers);
    }

    private static Dictionary<string, string> EncodingModifier(string encoding)
    {
        return new Dictionary<string, string>
        {
            [ColumnReadModifiers.Encoding] = encoding
        };
    }

    private static Dictionary<string, string> TrimModifier()
    {
        return new Dictionary<string, string>
        {
            [ColumnReadModifiers.Trim] = "true"
        };
    }

    private static SourceColumnRef ToColumnRef(ISchemaColumn column)
    {
        return new SourceColumnRef(column.ColumnName, column.ReadModifiers);
    }

    private static string WriteTempFile(string content, Encoding encoding)
    {
        var path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.csv");
        File.WriteAllText(path, content, encoding);
        return path;
    }

    private static string WriteTempFileBytes(byte[] content)
    {
        var path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.csv");
        File.WriteAllBytes(path, content);
        return path;
    }

    private static void DeleteIfExists(string path)
    {
        if (File.Exists(path))
            File.Delete(path);
    }

    private static bool IsError(SourceContractDiagnostic diagnostic)
    {
        return diagnostic.Severity == SourceContractDiagnosticSeverity.Error;
    }

    private static void AssertDiagnostic(
        IReadOnlyList<SourceContractDiagnostic> diagnostics,
        string code,
        string columnName,
        string modifierKey,
        SourceContractDiagnosticSeverity severity = SourceContractDiagnosticSeverity.Error)
    {
        var diagnostic = diagnostics.SingleOrDefault(item =>
            item.Code == code &&
            item.Severity == severity &&
            item.ColumnName == columnName &&
            item.ModifierKey == modifierKey);

        Assert.IsNotNull(
            diagnostic,
            $"Expected {severity} diagnostic {code} for {columnName}/{modifierKey}. Actual: {string.Join(", ", diagnostics.Select(item => $"{item.Severity}:{item.Code}:{item.ColumnName}:{item.ModifierKey}"))}");
    }
}
