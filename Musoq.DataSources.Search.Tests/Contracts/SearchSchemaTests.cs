#nullable enable

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Musoq.DataSources.Tests.Common;
using Musoq.Converter;
using Musoq.Evaluator;
using Musoq.Evaluator.Exceptions;
using Musoq.Evaluator.Tables;
using Musoq.Schema;
using Musoq.Schema.DataSources;
using Musoq.Schema.Optimization;

using Musoq.DataSources.Search.Entities;

using Musoq.DataSources.Search.Sources;

using Musoq.DataSources.Search.Components.Contracts;

using Musoq.DataSources.Search.Components.Diagnostics;

using Musoq.DataSources.Search.Components.Text;

using Musoq.DataSources.Search.Tests.Infrastructure;

namespace Musoq.DataSources.Search.Tests.Contracts;

[TestClass]
public sealed class SearchSchemaTests
{
    private static readonly StarContractCase[] StarCases =
    [
        new(
            "matches",
            [typeof(string), typeof(string)],
            [FixtureRoot, "TODO"],
            $"select * from search.matches('{EscapedFixtureRoot}', 'TODO')",
            [
                Column(nameof(SearchMatch.Path), typeof(string)),
                Column(nameof(SearchMatch.PatternId), typeof(string)),
                Column(nameof(SearchMatch.MatchIndex), typeof(long)),
                Column(nameof(SearchMatch.ByteOffset), typeof(long?)),
                Column(nameof(SearchMatch.ByteLength), typeof(long?)),
                Column(nameof(SearchMatch.LineNumber), typeof(long)),
                Column(nameof(SearchMatch.Utf16Column), typeof(long)),
                Column(nameof(SearchMatch.Utf16Length), typeof(long)),
                Column(nameof(SearchMatch.MatchText), typeof(string))
            ],
            [nameof(SearchMatch.Captures), nameof(SearchMatch.Context)]),
        new(
            "many",
            [typeof(string), typeof(string)],
            [FixtureRoot, ManyRequest],
            $"select * from search.many('{EscapedFixtureRoot}', {ManyRequest})",
            [
                Column(nameof(SearchMatch.Path), typeof(string)),
                Column(nameof(SearchMatch.PatternId), typeof(string)),
                Column(nameof(SearchMatch.MatchIndex), typeof(long)),
                Column(nameof(SearchMatch.ByteOffset), typeof(long?)),
                Column(nameof(SearchMatch.ByteLength), typeof(long?)),
                Column(nameof(SearchMatch.LineNumber), typeof(long)),
                Column(nameof(SearchMatch.Utf16Column), typeof(long)),
                Column(nameof(SearchMatch.Utf16Length), typeof(long)),
                Column(nameof(SearchMatch.MatchText), typeof(string))
            ],
            [nameof(SearchMatch.Captures), nameof(SearchMatch.Context)]),
        new(
            "bytes",
            [typeof(string), typeof(string)],
            [FixtureRoot, ByteRequest],
            $"select * from search.bytes('{EscapedFixtureRoot}', '{ByteRequest}', (Window: (BeforeBytes: 1, AfterBytes: 1)))",
            [
                Column(nameof(SearchByteMatch.Path), typeof(string)),
                Column(nameof(SearchByteMatch.Origin), typeof(string)),
                Column(nameof(SearchByteMatch.PatternId), typeof(string)),
                Column(nameof(SearchByteMatch.MatchIndex), typeof(long)),
                Column(nameof(SearchByteMatch.ByteOffset), typeof(long)),
                Column(nameof(SearchByteMatch.ByteLength), typeof(long)),
                Column(nameof(SearchByteMatch.WindowStartByteOffset), typeof(long?)),
                Column(nameof(SearchByteMatch.WindowByteLength), typeof(long?)),
                Column(nameof(SearchByteMatch.WindowComplete), typeof(bool?)),
                Column(nameof(SearchByteMatch.LineNumber), typeof(long?)),
                Column(nameof(SearchByteMatch.Utf16Column), typeof(long?)),
                Column(nameof(SearchByteMatch.Utf16Length), typeof(long?)),
                Column(nameof(SearchByteMatch.MatchText), typeof(string)),
                Column(nameof(SearchByteMatch.LineText), typeof(string))
            ],
            [
                nameof(SearchByteMatch.MatchedBytes),
                nameof(SearchByteMatch.WindowBytes),
                nameof(SearchByteMatch.Captures),
                nameof(SearchByteMatch.Context)
            ]),
        new(
            "lines",
            [typeof(string), typeof(string)],
            [FixtureRoot, "TODO"],
            $"select * from search.lines('{EscapedFixtureRoot}', 'TODO')",
            [
                Column(nameof(SearchLine.Path), typeof(string)),
                Column(nameof(SearchLine.Origin), typeof(string)),
                Column(nameof(SearchLine.PatternId), typeof(string)),
                Column(nameof(SearchLine.LineNumber), typeof(long)),
                Column(nameof(SearchLine.ByteOffset), typeof(long?)),
                Column(nameof(SearchLine.LineText), typeof(string)),
                Column(nameof(SearchLine.OccurrenceCount), typeof(long))
            ],
            []),
        new(
            "files",
            [typeof(string), typeof(string)],
            [FixtureRoot, "TODO"],
            $"select * from search.files('{EscapedFixtureRoot}', 'TODO')",
            [
                Column(nameof(SearchFile.Path), typeof(string)),
                Column(nameof(SearchFile.Origin), typeof(string)),
                Column(nameof(SearchFile.PatternId), typeof(string))
            ],
            []),
        new(
            "counts",
            [typeof(string), typeof(string)],
            [FixtureRoot, "TODO"],
            $"select * from search.counts('{EscapedFixtureRoot}', 'TODO')",
            [
                Column(nameof(SearchCount.Path), typeof(string)),
                Column(nameof(SearchCount.Origin), typeof(string)),
                Column(nameof(SearchCount.PatternId), typeof(string)),
                Column(nameof(SearchCount.OccurrenceCount), typeof(long)),
                Column(nameof(SearchCount.MatchingLineCount), typeof(long)),
                Column(nameof(SearchCount.BytesScanned), typeof(long)),
                Column(nameof(SearchCount.Complete), typeof(bool))
            ],
            []),
        new(
            "paths",
            [typeof(string)],
            [FixtureRoot],
            $"select * from search.paths('{EscapedFixtureRoot}')",
            [
                Column(nameof(SearchPath.Path), typeof(string)),
                Column(nameof(SearchPath.Origin), typeof(string)),
                Column(nameof(SearchPath.EntryKind), typeof(string))
            ],
            []),
        new(
            "audit",
            [typeof(string), typeof(string)],
            [FixtureRoot, "TODO"],
            $"select * from search.audit('{EscapedFixtureRoot}', 'TODO')",
            [
                Column(nameof(SearchAudit.Root), typeof(string)),
                Column(nameof(SearchAudit.ScanId), typeof(string)),
                Column(nameof(SearchAudit.ScopeFingerprint), typeof(string)),
                Column(
                    nameof(SearchAudit.Outcome),
                    typeof(int),
                    typeof(SearchOutcome),
                    EnumTypeOrigin.NativeClr,
                    typeof(SearchOutcome).FullName!,
                    EnumUnderlyingKind.Int32,
                    false),
                Column(nameof(SearchAudit.TerminalReason), typeof(string)),
                Column(nameof(SearchAudit.Complete), typeof(bool)),
                Column(nameof(SearchAudit.ScopeExhausted), typeof(bool)),
                Column(nameof(SearchAudit.QuerySatisfied), typeof(bool)),
                Column(nameof(SearchAudit.CountsExact), typeof(bool)),
                Column(nameof(SearchAudit.ScopeResolved), typeof(bool)),
                Column(nameof(SearchAudit.VisitedFiles), typeof(long)),
                Column(nameof(SearchAudit.EligibleFiles), typeof(long)),
                Column(nameof(SearchAudit.FilesOpened), typeof(long)),
                Column(nameof(SearchAudit.FilesRead), typeof(long)),
                Column(nameof(SearchAudit.FilesCompleted), typeof(long)),
                Column(nameof(SearchAudit.FilesFailed), typeof(long)),
                Column(nameof(SearchAudit.BinaryFilesSkipped), typeof(long)),
                Column(nameof(SearchAudit.BytesScanned), typeof(long)),
                Column(nameof(SearchAudit.FilesMatched), typeof(long)),
                Column(nameof(SearchAudit.MatchingLines), typeof(long)),
                Column(nameof(SearchAudit.Occurrences), typeof(long)),
                Column(nameof(SearchAudit.ObservedRows), typeof(long)),
                Column(nameof(SearchAudit.FailureCode), typeof(string)),
                Column(nameof(SearchAudit.FailurePath), typeof(string))
            ],
            [])
    ];

    static SearchSchemaTests()
    {
        Culture.ApplyWithDefaultCulture();
    }

    private static string FixtureRoot => Path.Combine(AppContext.BaseDirectory, "TestData", "SearchFixture");

    private static string EscapedFixtureRoot => FixtureRoot.Replace("\\", "\\\\", StringComparison.Ordinal);

    private const string ManyRequest =
        "array { (Id: 'todo', Pattern: 'TODO') }";

    private const string ByteRequest = "54 4f 44 4f";

    [TestMethod]
    public void AssemblyDiscovery_ShouldDeclareSearchSchema()
    {
        var assembly = typeof(SearchSchema).Assembly;
        var attribute = assembly.GetCustomAttributesData().SingleOrDefault(candidate =>
            string.Equals(
                candidate.AttributeType.FullName,
                "Musoq.Schema.Attributes.PluginSchemasAttribute",
                StringComparison.Ordinal));

        Assert.IsNotNull(attribute, "The Search assembly must declare PluginSchemas.");
        var schemaNames = attribute.ConstructorArguments
            .SelectMany(argument => argument.Value is IEnumerable<CustomAttributeTypedArgument> values
                ? values.Select(value => value.Value?.ToString())
                : [argument.Value?.ToString()])
            .ToArray();
        CollectionAssert.Contains(schemaNames, "search");
    }

    [TestMethod]
    public void GeneratedXmlDocumentation_ShouldBeWellFormedAndCoverPublicSearchApi()
    {
        var assembly = typeof(SearchSchema).Assembly;
        var xmlPath = Path.ChangeExtension(assembly.Location, ".xml");

        Assert.IsTrue(File.Exists(xmlPath), $"Generated XML documentation was not found at '{xmlPath}'.");

        var document = XDocument.Load(xmlPath, LoadOptions.SetLineInfo);
        var members = document.Root?.Element("members")?.Elements("member")
            .ToDictionary(member => member.Attribute("name")!.Value, StringComparer.Ordinal)
            ?? throw new AssertFailedException("Generated XML documentation has no members element.");

        Assert.IsTrue(members.Count > 0, "Generated XML documentation contains no members.");

        var missing = new List<string>();
        foreach (var type in assembly.GetExportedTypes())
        {
            var typeName = XmlTypeName(type);
            var displayTypeName = type.FullName ?? type.Name;
            RequireEntry($"T:{typeName}", displayTypeName);

            foreach (var constructor in type.GetConstructors(BindingFlags.Public | BindingFlags.Instance))
                RequirePrefix($"M:{typeName}.#ctor", constructor.ToString() ?? displayTypeName);

            foreach (var property in type.GetProperties(
                         BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly))
                RequireEntry($"P:{typeName}.{property.Name}", property.ToString() ?? displayTypeName);

            foreach (var field in type.GetFields(
                         BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly))
            {
                // Every CLR enum has this compiler-generated backing field;
                // it has no source declaration that can carry XML docs.
                if (!(type.IsEnum && field.Name == "value__"))
                    RequireEntry($"F:{typeName}.{field.Name}", field.ToString() ?? displayTypeName);
            }

            foreach (var eventInfo in type.GetEvents(
                         BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly))
                RequireEntry($"E:{typeName}.{eventInfo.Name}", eventInfo.ToString() ?? displayTypeName);

            foreach (var methodGroup in type.GetMethods(
                         BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly)
                         .Where(method => !method.IsSpecialName)
                         .GroupBy(method => method.Name, StringComparer.Ordinal))
            {
                var prefix = $"M:{typeName}.{methodGroup.Key}";
                var matchingCount = members.Keys.Count(memberName =>
                    memberName.StartsWith(prefix, StringComparison.Ordinal) &&
                    (memberName.Length == prefix.Length ||
                     memberName[prefix.Length] == '(' || memberName[prefix.Length] == '`'));
                if (matchingCount < methodGroup.Count())
                    missing.Add($"{prefix} ({methodGroup.Count()} public overloads, {matchingCount} XML entries)");
            }
        }

        if (missing.Count > 0)
            Assert.Fail("Missing public Search XML documentation entries:\n" + string.Join("\n", missing));

        foreach (var member in members.Values.Where(member =>
                     member.Attribute("name")?.Value.StartsWith("T:Musoq.DataSources.Search", StringComparison.Ordinal) == true ||
                     member.Attribute("name")?.Value.StartsWith("M:Musoq.DataSources.Search", StringComparison.Ordinal) == true ||
                     member.Attribute("name")?.Value.StartsWith("P:Musoq.DataSources.Search", StringComparison.Ordinal) == true ||
                     member.Attribute("name")?.Value.StartsWith("F:Musoq.DataSources.Search", StringComparison.Ordinal) == true ||
                     member.Attribute("name")?.Value.StartsWith("E:Musoq.DataSources.Search", StringComparison.Ordinal) == true))
        {
            Assert.IsTrue(
                member.Element("summary") is not null || member.Element("inheritdoc") is not null,
                $"Search XML member '{member.Attribute("name")?.Value}' has neither a summary nor inheritdoc element.");
        }

        void RequireEntry(string exactName, string displayName)
        {
            if (!members.ContainsKey(exactName))
                missing.Add($"{exactName} ({displayName})");
        }

        void RequirePrefix(string prefix, string displayName)
        {
            if (!members.Keys.Any(memberName => memberName.StartsWith(prefix, StringComparison.Ordinal)))
                missing.Add($"{prefix} ({displayName})");
        }
    }

    [TestMethod]
    public void RawConstructors_ShouldExposeMinimalSearchSignatures()
    {
        var constructors = new SearchSchema().GetRawConstructors(CreateMetadataContext());

        Assert.AreEqual(16, constructors.Length);
        Assert.AreEqual("matches", constructors[0].MethodName);
        CollectionAssert.AreEqual(
            new[] { typeof(string), typeof(string) },
            constructors[0].ConstructorInfo.Arguments.Select(argument => argument.Type).ToArray());
        CollectionAssert.AreEqual(
            new[] { typeof(string), typeof(string), typeof(SearchMatchOptionsInput) },
            constructors[1].ConstructorInfo.Arguments.Select(argument => argument.Type).ToArray());
        Assert.AreEqual("matches", constructors[1].MethodName);
        Assert.AreEqual("many", constructors[2].MethodName);
        CollectionAssert.AreEqual(
            new[] { typeof(string), typeof(IReadOnlyList<SearchPatternInput>) },
            constructors[2].ConstructorInfo.Arguments.Select(argument => argument.Type).ToArray());
        CollectionAssert.AreEqual(
            new[] { typeof(string), typeof(IReadOnlyList<SearchPatternInput>), typeof(SearchManyOptionsInput) },
            constructors[3].ConstructorInfo.Arguments.Select(argument => argument.Type).ToArray());
        Assert.AreEqual("lines", constructors[4].MethodName);
        CollectionAssert.AreEqual(
            new[] { typeof(string), typeof(string) },
            constructors[4].ConstructorInfo.Arguments.Select(argument => argument.Type).ToArray());
        CollectionAssert.AreEqual(
            new[] { typeof(string), typeof(string), typeof(SearchScanOptionsInput) },
            constructors[5].ConstructorInfo.Arguments.Select(argument => argument.Type).ToArray());
        Assert.AreEqual("files", constructors[6].MethodName);
        CollectionAssert.AreEqual(
            new[] { typeof(string), typeof(string) },
            constructors[6].ConstructorInfo.Arguments.Select(argument => argument.Type).ToArray());
        CollectionAssert.AreEqual(
            new[] { typeof(string), typeof(string), typeof(SearchScanOptionsInput) },
            constructors[7].ConstructorInfo.Arguments.Select(argument => argument.Type).ToArray());
        Assert.AreEqual("counts", constructors[8].MethodName);
        CollectionAssert.AreEqual(
            new[] { typeof(string), typeof(string) },
            constructors[8].ConstructorInfo.Arguments.Select(argument => argument.Type).ToArray());
        CollectionAssert.AreEqual(
            new[] { typeof(string), typeof(string), typeof(SearchScanOptionsInput) },
            constructors[9].ConstructorInfo.Arguments.Select(argument => argument.Type).ToArray());
        Assert.AreEqual("paths", constructors[10].MethodName);
        CollectionAssert.AreEqual(
            new[] { typeof(string) },
            constructors[10].ConstructorInfo.Arguments.Select(argument => argument.Type).ToArray());
        CollectionAssert.AreEqual(
            new[] { typeof(string), typeof(SearchPathsOptionsInput) },
            constructors[11].ConstructorInfo.Arguments.Select(argument => argument.Type).ToArray());
        Assert.AreEqual("bytes", constructors[12].MethodName);
        CollectionAssert.AreEqual(
            new[] { typeof(string), typeof(string) },
            constructors[12].ConstructorInfo.Arguments.Select(argument => argument.Type).ToArray());
        CollectionAssert.AreEqual(
            new[] { typeof(string), typeof(string), typeof(SearchBytesOptionsInput) },
            constructors[13].ConstructorInfo.Arguments.Select(argument => argument.Type).ToArray());
        Assert.AreEqual("audit", constructors[14].MethodName);
        CollectionAssert.AreEqual(
            new[] { typeof(string), typeof(string) },
            constructors[14].ConstructorInfo.Arguments.Select(argument => argument.Type).ToArray());
        CollectionAssert.AreEqual(
            new[] { typeof(string), typeof(string), typeof(SearchScanOptionsInput) },
            constructors[15].ConstructorInfo.Arguments.Select(argument => argument.Type).ToArray());
    }

    [TestMethod]
    public void DescribeSource_ShouldAlignRowTypeAndColumns()
    {
        var schema = new SearchSchema();
        var metadataContext = CreateMetadataContext();
        var identity = new SourceIdentity("search", "matches", "search-tests", "fixture");
        var descriptor = schema.DescribeSource(
            "matches",
            new SourceDescribeContext(identity, metadataContext),
            FixtureRoot,
            "TODO");

        Assert.AreEqual(typeof(SearchMatch), descriptor.RowType);
        CollectionAssert.AreEqual(
            new[]
            {
                "Path", "PatternId", "MatchIndex", "ByteOffset", "ByteLength",
                "LineNumber", "Utf16Column", "Utf16Length", "MatchText", "Captures", "Context"
            },
            descriptor.Columns.Select(column => column.ColumnName).ToArray());
        CollectionAssert.AreEqual(
            new[]
            {
                typeof(string), typeof(string), typeof(long), typeof(long?), typeof(long?),
                typeof(long), typeof(long), typeof(long), typeof(string),
                typeof(IReadOnlyList<SearchCapture>),
                typeof(IReadOnlyList<SearchContextLine>)
            },
            descriptor.Columns.Select(column => column.ColumnType).ToArray());
        Assert.AreEqual(typeof(SearchMatch), schema.GetTableByName("matches", metadataContext).Metadata.TableEntityType);
    }

    [TestMethod]
    public void Captures_ShouldBeExpandableWithCrossApply()
    {
        var result = Compile(
                $"select c.GroupName as GroupName, c.GroupIndex as GroupIndex, " +
                "c.CaptureIndex as CaptureIndex, c.Success as Success, c.Text as Text " +
                $"from search.matches('{EscapedFixtureRoot}', 'TODO') m " +
                "cross apply m.Captures c")
            .Run();

        CollectionAssert.AreEqual(
            new[] { "GroupName", "GroupIndex", "CaptureIndex", "Success", "Text" },
            result.Columns.Select(column => column.ColumnName).ToArray());
        Assert.AreEqual(0, result.Count);
    }

    [TestMethod]
    public void Context_ShouldBeExpandableWithCrossApply()
    {
        var result = Compile(
                $"select c.RelativeLine as RelativeLine, c.LineNumber as LineNumber, " +
                "c.LineText as LineText " +
                $"from search.matches('{EscapedFixtureRoot}', 'TODO') m " +
                "cross apply m.Context c")
            .Run();

        CollectionAssert.AreEqual(
            new[] { "RelativeLine", "LineNumber", "LineText" },
            result.Columns.Select(column => column.ColumnName).ToArray());
        Assert.AreEqual(0, result.Count);
    }

    [TestMethod]
    public void DescribePathsSource_ShouldAlignRowTypeAndColumns()
    {
        var schema = new SearchSchema();
        var metadataContext = CreateMetadataContext();
        var identity = new SourceIdentity("search", "paths", "search-tests", "fixture");
        var descriptor = schema.DescribeSource(
            "paths",
            new SourceDescribeContext(identity, metadataContext),
            FixtureRoot);

        Assert.AreEqual(typeof(SearchPath), descriptor.RowType);
        CollectionAssert.AreEqual(
            new[] { "Path", "Origin", "EntryKind" },
            descriptor.Columns.Select(column => column.ColumnName).ToArray());
        CollectionAssert.AreEqual(
            new[] { typeof(string), typeof(string), typeof(string) },
            descriptor.Columns.Select(column => column.ColumnType).ToArray());
        Assert.AreEqual(typeof(SearchPath), schema.GetTableByName("paths", metadataContext).Metadata.TableEntityType);
    }

    [TestMethod]
    public void CompiledPathsQuery_ShouldReturnEligiblePathsWithoutContentColumns()
    {
        var result = Compile(
                $"select Path, Origin, EntryKind from search.paths('{EscapedFixtureRoot}') " +
                "order by Path")
            .Run();

        CollectionAssert.AreEqual(
            new[] { "Path", "Origin", "EntryKind" },
            result.Columns.Select(column => column.ColumnName).ToArray());
        Assert.AreEqual(2, result.Count);
        Assert.AreEqual("first.txt", result[0][0]);
        Assert.IsNull(result[0][1]);
        Assert.AreEqual("file", result[0][2]);
        Assert.AreEqual("nested/other.txt", result[1][0]);
        Assert.IsNull(result[1][1]);
        Assert.AreEqual("file", result[1][2]);
    }

    [TestMethod]
    public void DescribeSource_ShouldRemainStaticWithoutResolvingTheRoot()
    {
        var descriptor = new SearchSchema().DescribeSource(
            "matches",
            new SourceDescribeContext(
                new SourceIdentity("search", "matches", "search-tests", "invalid-root"),
                CreateMetadataContext()),
            "\0",
            string.Empty);

        Assert.AreEqual(typeof(SearchMatch), descriptor.RowType);
    }

    [TestMethod]
    public void InvalidRequest_ShouldBeRejectedBeforeRootResolution()
    {
        var exception = Assert.ThrowsException<SearchRequestException>(() =>
            new SearchMatchesSource(
                "\0",
                string.Empty,
                RuntimeV2TestContexts.CreateExecutionContext()));

        Assert.AreEqual(SearchDiagnosticCodes.InvalidArgument, exception.Diagnostic.Code);
        Assert.AreEqual(SearchDiagnosticPhase.Argument, exception.Diagnostic.Phase);
        Assert.AreEqual("literal", exception.Diagnostic.Location?.ArgumentName);
    }

    [TestMethod]
    public void SearchDiagnostics_ShouldUseStablePluginCodesAndBoundedGuidance()
    {
        var diagnostics = new[]
        {
            SearchDiagnosticCatalog.InvalidRegex(),
            SearchDiagnosticCatalog.InvalidRecordFraming("an open record reached end-of-file"),
            SearchDiagnosticCatalog.InvalidBytePattern("odd hex digit"),
            SearchDiagnosticCatalog.InvalidArgument("root"),
            SearchDiagnosticCatalog.MissingRoot(new string('x', 512)),
            SearchDiagnosticCatalog.SourceOpenFailed("fixture.txt"),
            SearchDiagnosticCatalog.SourceReadFailed("fixture.txt"),
            SearchDiagnosticCatalog.UnsupportedEncoding(),
            SearchDiagnosticCatalog.SourceChanged("fixture.txt"),
            SearchDiagnosticCatalog.ResourceLimit("include", 128),
            SearchDiagnosticCatalog.OutputFailed()
        };

        CollectionAssert.AreEquivalent(
            SearchDiagnosticCodes.All.ToArray(),
            diagnostics.Select(diagnostic => diagnostic.Code).ToArray());
        Assert.AreEqual(5, diagnostics.Select(diagnostic => diagnostic.Phase).Distinct().Count());

        foreach (var diagnostic in diagnostics)
        {
            StringAssert.StartsWith(diagnostic.Code, "SEARCH-");
            Assert.IsFalse(diagnostic.Code.StartsWith("MQ", StringComparison.Ordinal));
            Assert.IsTrue(diagnostic.Message.Length <= SearchDiagnostic.MaxTextLength);
            Assert.IsTrue(diagnostic.Explanation.Length <= SearchDiagnostic.MaxTextLength);
            Assert.IsTrue(diagnostic.SuggestedFix.Length <= SearchDiagnostic.MaxTextLength);
        }

        Assert.AreEqual(
            256,
            diagnostics.Single(diagnostic => diagnostic.Code == SearchDiagnosticCodes.MissingRoot)
                .Location?.Path?.Length);
    }

    [TestMethod]
    public void BadRegex_ShouldProduceTypedSyntaxDiagnostic()
    {
        var exception = Assert.ThrowsException<SearchPatternException>(() =>
            SearchDiagnosticValidation.ValidateRegex("["));

        Assert.AreEqual(SearchDiagnosticCodes.InvalidRegex, exception.Diagnostic.Code);
        Assert.AreEqual(SearchDiagnosticPhase.Syntax, exception.Diagnostic.Phase);
        Assert.AreEqual("pattern", exception.Diagnostic.Location?.ArgumentName);
        Assert.IsNotNull(exception.InnerException);
        Assert.IsFalse(string.IsNullOrWhiteSpace(exception.Diagnostic.Explanation));
        Assert.IsFalse(string.IsNullOrWhiteSpace(exception.Diagnostic.SuggestedFix));
    }

    [TestMethod]
    public void UnsupportedEncoding_ShouldProduceTypedSourceDiagnostic()
    {
        var exception = Assert.ThrowsException<SearchEncodingException>(() =>
            SearchDiagnosticValidation.ValidateEncoding("windows-1252"));

        Assert.AreEqual(SearchDiagnosticCodes.UnsupportedEncoding, exception.Diagnostic.Code);
        Assert.AreEqual(SearchDiagnosticPhase.SourceAccess, exception.Diagnostic.Phase);
        Assert.AreEqual("encoding", exception.Diagnostic.Location?.ArgumentName);
    }

    [TestMethod]
    public void OversizedScope_ShouldProduceTypedResourceDiagnostic()
    {
        var exception = Assert.ThrowsException<SearchResourceLimitException>(() =>
            new ScopePolicy(include: Enumerable.Repeat("*.cs", 129)));

        Assert.AreEqual(SearchDiagnosticCodes.ResourceLimit, exception.Diagnostic.Code);
        Assert.AreEqual(SearchDiagnosticPhase.Resource, exception.Diagnostic.Phase);
        Assert.AreEqual("include", exception.Diagnostic.Location?.ArgumentName);
    }

    [TestMethod]
    public void MalformedSourceArguments_ShouldProduceTypedArgumentDiagnostic()
    {
        var exception = Assert.ThrowsException<SearchRequestException>(() =>
            new SearchSchema().GetRowSource<SearchMatch>(
                "matches",
                RuntimeV2TestContexts.CreateExecutionContext(),
                FixtureRoot));

        Assert.AreEqual(SearchDiagnosticCodes.InvalidArgument, exception.Diagnostic.Code);
        Assert.AreEqual(SearchDiagnosticPhase.Argument, exception.Diagnostic.Phase);
        Assert.AreEqual("arguments", exception.Diagnostic.Location?.ArgumentName);
    }

    [TestMethod]
    public void MissingRoot_ShouldProduceTypedSourceAccessDiagnostic()
    {
        var missingRoot = Path.Combine(Path.GetTempPath(), $"musoq-search-missing-{Guid.NewGuid():N}");
        var source = new SearchMatchesSource(
            missingRoot,
            "TODO",
            RuntimeV2TestContexts.CreateExecutionContext());

        var exception = Assert.ThrowsException<SearchSourceAccessException>(() => source.Chunks.ToArray());

        Assert.AreEqual(SearchDiagnosticCodes.MissingRoot, exception.Diagnostic.Code);
        Assert.AreEqual(SearchDiagnosticPhase.SourceAccess, exception.Diagnostic.Phase);
        Assert.AreEqual(missingRoot, exception.Diagnostic.Location?.Path);
        Assert.IsFalse(exception.Message.Contains(missingRoot, StringComparison.Ordinal));
    }

    [TestMethod]
    public void CompiledMissingRoot_ShouldRetainSearchDiagnosticInsideEngineBoundary()
    {
        var missingRoot = Path.Combine(Path.GetTempPath(), $"musoq-search-missing-{Guid.NewGuid():N}");
        var escapedRoot = missingRoot.Replace("\\", "\\\\", StringComparison.Ordinal);
        var exception = Assert.ThrowsException<QueryExecutionException>(() =>
        {
            var result = Compile($"select * from search.matches('{escapedRoot}', 'TODO')").Run();
            _ = result.Count;
        });

        var sourceException = FindInner<SearchSourceAccessException>(exception);
        Assert.IsNotNull(sourceException);
        Assert.AreEqual(SearchDiagnosticCodes.MissingRoot, sourceException!.Diagnostic.Code);
        StringAssert.Contains(exception.FormatText(), "MQ7011");
        Assert.IsFalse(exception.FormatText().Contains(missingRoot, StringComparison.Ordinal));
    }

    [TestMethod]
    public void CompiledNullRoot_ShouldRetainSearchArgumentDiagnostic()
    {
        var exception = Assert.ThrowsException<QueryExecutionException>(() =>
        {
            var result = Compile("select * from search.matches(NULL, 'TODO')").Run();
            _ = result.Count;
        });

        var requestException = FindInner<SearchRequestException>(exception);
        Assert.IsNotNull(requestException);
        Assert.AreEqual(SearchDiagnosticCodes.InvalidArgument, requestException!.Diagnostic.Code);
        Assert.AreEqual("root", requestException.Diagnostic.Location?.ArgumentName);
    }

    [TestMethod]
    public void ScopePolicy_ShouldCopyAndFreezePatternCollections()
    {
        var include = new[] { "*.cs" };
        var policy = new ScopePolicy(include: include);
        include[0] = "*.json";

        Assert.AreEqual("*.cs", policy.Include[0]);
        Assert.IsTrue(policy.Recursive);
        Assert.AreEqual(RepositoryIgnorePolicy.Respect, policy.RepositoryIgnores);
        Assert.AreEqual(GlobalIgnorePolicy.Disabled, policy.GlobalIgnores);
        Assert.AreEqual(HiddenEntryPolicy.Exclude, policy.HiddenEntries);
        Assert.AreEqual(LinkTraversalPolicy.DoNotFollow, policy.FollowLinks);
        Assert.AreEqual(InaccessibleEntryPolicy.Fail, policy.InaccessibleEntries);

        var readOnly = (IList<string>)policy.Include;
        Assert.ThrowsException<NotSupportedException>(() => readOnly[0] = "*.json");
    }

    [TestMethod]
    public void LiteralMatcher_ShouldEmitNonOverlappingSpans()
    {
        var matcher = new LiteralMatcher("aba");
        var spans = new List<MatchSpan>();

        for (var index = 0; index < "ababa".Length; index++)
            if (matcher.TryConsume("ababa"[index], 1, index, out var span))
                spans.Add(span);

        Assert.AreEqual(1, spans.Count);
        Assert.AreEqual(
            new MatchSpan(0, 3, 1, 0) { MatchText = "aba" },
            spans[0]);
        Assert.AreEqual(3L, spans[0].EndExclusive);
    }

    [TestMethod]
    public void SearchOutcome_ShouldMatchThePublishedTerminalContract()
    {
        CollectionAssert.AreEqual(
            new[]
            {
                SearchOutcome.QuerySatisfied,
                SearchOutcome.ScopeExhausted,
                SearchOutcome.Failed,
                SearchOutcome.Partial
            },
            Enum.GetValues<SearchOutcome>());
    }

    [TestMethod]
    public void CompiledLiteralQuery_ShouldReturnKnownTypedRows()
    {
        var query =
            $"select Path, MatchIndex, LineNumber, Utf16Column, MatchText " +
            $"from search.matches('{EscapedFixtureRoot}', 'TODO') order by MatchIndex";
        var result = Compile(query).Run();

        CollectionAssert.AreEqual(
            new[] { "Path", "MatchIndex", "LineNumber", "Utf16Column", "MatchText" },
            result.Columns.Select(column => column.ColumnName).ToArray());
        CollectionAssert.AreEqual(
            new[] { typeof(string), typeof(long), typeof(long), typeof(long), typeof(string) },
            result.Columns.Select(column => column.ColumnType).ToArray());

        Assert.AreEqual(2, result.Count);
        Assert.AreEqual("first.txt", result[0][0]);
        Assert.AreEqual(0L, result[0][1]);
        Assert.AreEqual(1L, result[0][2]);
        Assert.AreEqual(0L, result[0][3]);
        Assert.AreEqual("TODO", result[0][4]);
        Assert.AreEqual("first.txt", result[1][0]);
        Assert.AreEqual(1L, result[1][1]);
        Assert.AreEqual(1L, result[1][2]);
        Assert.AreEqual(5L, result[1][3]);
        Assert.AreEqual("TODO", result[1][4]);
    }

    [TestMethod]
    public void CompiledSearch_ShouldReturnRowsAndEmitLifecycleTrace()
    {
        using var compiled = Compile(
            $"select Path, MatchIndex from search.matches('{EscapedFixtureRoot}', 'TODO') " +
            "order by MatchIndex");
        var capture = new DataSourceProgressCapture();
        compiled.DataSourceProgress += capture.Handler;

        var result = compiled.Run();

        Assert.AreEqual(2, result.Count);
        CollectionAssert.AreEqual(
            new[] { DataSourcePhase.Begin, DataSourcePhase.RowsRead, DataSourcePhase.End },
            capture.Events.Select(args => args.Phase).ToArray());
        Assert.AreEqual(1, capture.For("search", DataSourcePhase.Begin).Count);
        var rowsRead = capture.For("search", DataSourcePhase.RowsRead).Single();
        Assert.AreEqual(2L, rowsRead.RowsProcessed);
        var end = capture.For("search", DataSourcePhase.End).Single();
        Assert.AreEqual(2L, end.TotalRows);
        Assert.AreEqual(2L, end.RowsProcessed);
    }

    [TestMethod]
    public void CompiledSearch_ShouldRepeatWithoutCrossRunRowsOrState()
    {
        using var compiled = Compile(
            $"select Path, MatchIndex, MatchText from search.matches('{EscapedFixtureRoot}', 'TODO') " +
            "order by MatchIndex");

        var first = Snapshot(compiled.Run());
        var second = Snapshot(compiled.Run());

        CollectionAssert.AreEqual(first, second);
        Assert.AreEqual(2, first.Length);
    }

    [TestMethod]
    public void CompiledSearch_WithSettingsProfiles_UsesDistinctSourceContexts()
    {
        var resolver = new ContextAwareSettingsResolver();
        var options = new CompilationOptions(sourceRuntimeSettingsResolver: resolver);
        var query =
            $"couple #search.matches with settings prod as Prod; " +
            $"couple #search.matches with settings staging as Stage; " +
            $"select p.Path, s.Path from Prod('{EscapedFixtureRoot}', 'TODO') p " +
            $"inner join Stage('{EscapedFixtureRoot}', 'TODO') s on 1 = 1";

        using var compiled = CompileWithOptions(query, options);
        var result = compiled.Run();

        Assert.AreEqual(4, result.Count);
        var requests = resolver.Requests.ToArray();
        Assert.AreEqual(2, requests.Length);
        CollectionAssert.AreEquivalent(
            new[] { "prod", "staging" },
            requests.Select(request => request.ProfileName).Distinct(StringComparer.Ordinal).ToArray());
        Assert.AreEqual(
            2,
            requests.Select(request => request.Identity.SourceContextId).Distinct(StringComparer.Ordinal).Count());
        Assert.AreEqual("prod", requests.Single(request => request.Identity.Alias == "p").ProfileName);
        Assert.AreEqual("staging", requests.Single(request => request.Identity.Alias == "s").ProfileName);
        Assert.IsTrue(requests.All(request =>
            request.Identity.SchemaName == "#search" &&
            request.Identity.MethodName == "matches" &&
            request.Parameters.Length == 0));
    }

    [TestMethod]
    public void DirectSearch_WithSlowConsumer_EmitsEveryRowAndCoarseProgress()
    {
        var path = Path.Combine(Path.GetTempPath(), $"musoq-search-{Guid.NewGuid():N}.txt");
        var expectedCount = RowChunking.DefaultChunkSize * 5 + 1;
        File.WriteAllText(path, string.Concat(Enumerable.Repeat("TODO", expectedCount)));
        var progress = new DataSourceProgressCapture();

        try
        {
            var source = new SearchMatchesSource(
                path,
                "TODO",
                RuntimeV2TestContexts.CreateExecutionContext(
                    dataSourceProgressCallback: progress.Handler));
            var rows = new List<SearchMatch>();

            foreach (var chunk in source.Chunks)
            {
                rows.AddRange(chunk);
                Thread.Sleep(2);
            }

            Assert.AreEqual(expectedCount, rows.Count);
            Assert.AreEqual(expectedCount - 1L, rows[^1].MatchIndex);
            Assert.AreEqual(1, progress.For("search", DataSourcePhase.Begin).Count);
            Assert.AreEqual(1, progress.For("search", DataSourcePhase.RowsRead).Count);
            Assert.AreEqual(expectedCount, progress.For("search", DataSourcePhase.RowsRead).Last().RowsProcessed);
            Assert.AreEqual(expectedCount, progress.For("search", DataSourcePhase.End).Single().RowsProcessed);
        }
        finally
        {
            if (File.Exists(path))
                File.Delete(path);
        }
    }

    [TestMethod]
    public void DirectSearch_WithSeparateContextsAndLoggers_PreservesRowsWithoutStdoutTelemetry()
    {
        lock (ConsoleOutputLock)
        {
            var previousOutput = Console.Out;
            using var output = new StringWriter();
            Console.SetOut(output);

            try
            {
                var firstProgress = new DataSourceProgressCapture();
                var secondProgress = new DataSourceProgressCapture();
                var recordingLogger = new RecordingLogger();
                var firstRows = ReadDirect(
                    RuntimeV2TestContexts.CreateExecutionContext(
                        sourceRuntimeSettings: new Dictionary<string, string>
                        {
                            ["profile"] = "prod"
                        },
                        logger: NullLogger.Instance,
                        dataSourceProgressCallback: firstProgress.Handler));
                var secondRows = ReadDirect(
                    RuntimeV2TestContexts.CreateExecutionContext(
                        sourceRuntimeSettings: new Dictionary<string, string>
                        {
                            ["profile"] = "staging"
                        },
                        logger: recordingLogger,
                        dataSourceProgressCallback: secondProgress.Handler));

                CollectionAssert.AreEqual(
                    firstRows.Select(RowSignature).ToArray(),
                    secondRows.Select(RowSignature).ToArray());
                Assert.AreEqual(0, recordingLogger.Messages.Count);
                Assert.AreEqual(string.Empty, output.ToString());
                Assert.AreEqual(1, firstProgress.For("search", DataSourcePhase.End).Count);
                Assert.AreEqual(1, secondProgress.For("search", DataSourcePhase.End).Count);
            }
            finally
            {
                Console.SetOut(previousOutput);
            }
        }
    }

    [TestMethod]
    public void DirectSource_ShouldFlushAtChunkBoundaryWithoutChangingMultiplicity()
    {
        var path = Path.Combine(Path.GetTempPath(), $"musoq-search-{Guid.NewGuid():N}.txt");
        var expectedRows = RowChunking.DefaultChunkSize + 5;
        File.WriteAllText(path, string.Concat(Enumerable.Repeat("TODO", expectedRows)));

        try
        {
            var source = new SearchMatchesSource(
                path,
                "TODO",
                RuntimeV2TestContexts.CreateExecutionContext());
            var chunks = source.Chunks.ToArray();
            var rows = chunks.SelectMany(chunk => chunk).ToArray();

            Assert.IsTrue(chunks.Length >= 2);
            Assert.AreEqual(RowChunking.DefaultChunkSize, chunks[0].Count());
            Assert.AreEqual(expectedRows, rows.Length);
            Assert.AreEqual(expectedRows - 1L, rows[^1].MatchIndex);
            Assert.AreEqual(Path.GetFileName(path), rows[0].Path);
        }
        finally
        {
            if (File.Exists(path))
                File.Delete(path);
        }
    }

    [TestMethod]
    public void DirectSource_ShouldMatchLiteralAcrossReaderBufferBoundaries()
    {
        var path = Path.Combine(Path.GetTempPath(), $"musoq-search-{Guid.NewGuid():N}.txt");
        File.WriteAllText(path, new string('x', 8191) + "TODO");

        try
        {
            var source = new SearchMatchesSource(
                path,
                "TODO",
                RuntimeV2TestContexts.CreateExecutionContext());
            var rows = source.Chunks.SelectMany(chunk => chunk).ToArray();

            Assert.AreEqual(1, rows.Length);
            Assert.AreEqual(Path.GetFileName(path), rows[0].Path);
            Assert.AreEqual(8191L, rows[0].Utf16Column);
            Assert.AreEqual("TODO", rows[0].MatchText);
        }
        finally
        {
            if (File.Exists(path))
                File.Delete(path);
        }
    }

    [TestMethod]
    public void CancellationBeforeStart_ShouldNotOpenReaderOrEmitRows()
    {
        var path = Path.Combine(Path.GetTempPath(), $"musoq-search-{Guid.NewGuid():N}.txt");
        File.WriteAllText(path, "TODO");
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var readersCreated = 0;

        try
        {
            var source = new SearchMatchesSource(
                path,
                "TODO",
                RuntimeV2TestContexts.CreateExecutionContext(cancellation.Token),
                _ =>
                {
                    readersCreated++;
                    return new TrackingTextReader("TODO");
                });

            OperationCanceledException? cancellationException = null;
            try
            {
                _ = source.Chunks.ToArray();
            }
            catch (OperationCanceledException exception)
            {
                cancellationException = exception;
            }

            Assert.IsNotNull(cancellationException);
            Assert.AreEqual(0, readersCreated);
        }
        finally
        {
            if (File.Exists(path))
                File.Delete(path);
        }
    }

    [TestMethod]
    public void CancellationDuringRead_ShouldDisposeReaderAndNotReturnRows()
    {
        var path = Path.Combine(Path.GetTempPath(), $"musoq-search-{Guid.NewGuid():N}.txt");
        File.WriteAllText(path, "TODO");
        using var cancellation = new CancellationTokenSource();
        var reader = new BlockingTextReader("TODO");

        try
        {
            var source = new SearchMatchesSource(
                path,
                "TODO",
                RuntimeV2TestContexts.CreateExecutionContext(cancellation.Token),
                _ => reader);
            var collection = Task.Run(() => source.Chunks.ToArray());

            Assert.IsTrue(reader.ReadStarted.Wait(TimeSpan.FromSeconds(5)));
            cancellation.Cancel();
            reader.Release.Set();

            IReadOnlyList<IReadOnlyList<SearchMatch>> chunks = [];
            OperationCanceledException? cancellationException = null;
            try
            {
                chunks = collection.GetAwaiter().GetResult();
            }
            catch (OperationCanceledException exception)
            {
                cancellationException = exception;
            }

            Assert.IsNotNull(cancellationException);
            Assert.AreEqual(0, chunks.Count);
            Assert.IsTrue(reader.Disposed.Wait(TimeSpan.FromSeconds(5)));
        }
        finally
        {
            reader.Release.Set();
            if (File.Exists(path))
                File.Delete(path);
        }
    }

    [TestMethod]
    public void CancellationDuringScan_ShouldObserveTheTokenBeforeCompleting()
    {
        var path = Path.Combine(Path.GetTempPath(), $"musoq-search-{Guid.NewGuid():N}.txt");
        File.WriteAllText(path, "fixture");
        using var cancellation = new CancellationTokenSource();
        var reader = new SlowTextReader(new string('x', 100_000));

        try
        {
            var source = new SearchMatchesSource(
                path,
                "TODO",
                RuntimeV2TestContexts.CreateExecutionContext(cancellation.Token),
                _ => reader);
            var collection = Task.Run(() => source.Chunks.ToArray());

            Assert.IsTrue(reader.ReadStarted.Wait(TimeSpan.FromSeconds(5)));
            cancellation.CancelAfter(TimeSpan.FromMilliseconds(25));

            IReadOnlyList<IReadOnlyList<SearchMatch>> chunks = [];
            OperationCanceledException? cancellationException = null;
            try
            {
                chunks = collection.GetAwaiter().GetResult();
            }
            catch (OperationCanceledException exception)
            {
                cancellationException = exception;
            }

            Assert.IsTrue(reader.ReadCount > 1);
            Assert.IsNotNull(cancellationException);
            Assert.AreEqual(0, chunks.Count);
            Assert.IsTrue(reader.Disposed.Wait(TimeSpan.FromSeconds(5)));
        }
        finally
        {
            if (File.Exists(path))
                File.Delete(path);
        }
    }

    [TestMethod]
    public void CancellationBetweenChunks_ShouldNotExposeACompletedScan()
    {
        var path = Path.Combine(Path.GetTempPath(), $"musoq-search-{Guid.NewGuid():N}.txt");
        File.WriteAllText(path, "fixture");
        using var cancellation = new CancellationTokenSource();
        var reader = new EndOfInputBlockingTextReader(
            string.Concat(Enumerable.Repeat("TODO", RowChunking.DefaultChunkSize)));

        try
        {
            var source = new SearchMatchesSource(
                path,
                "TODO",
                RuntimeV2TestContexts.CreateExecutionContext(cancellation.Token),
                _ => reader);
            using var enumerator = source.Chunks.GetEnumerator();

            Assert.IsTrue(enumerator.MoveNext());
            Assert.AreEqual(RowChunking.DefaultChunkSize, enumerator.Current.Count);
            Assert.IsTrue(reader.EndReadStarted.Wait(TimeSpan.FromSeconds(5)));

            cancellation.Cancel();
            reader.Release.Set();

            var producedAnotherChunk = false;
            var cancellationObserved = false;
            try
            {
                producedAnotherChunk = enumerator.MoveNext();
            }
            catch (OperationCanceledException)
            {
                cancellationObserved = true;
            }

            Assert.IsFalse(producedAnotherChunk);
            Assert.IsTrue(cancellationObserved);
            Assert.IsTrue(reader.Disposed.Wait(TimeSpan.FromSeconds(5)));
        }
        finally
        {
            reader.Release.Set();
            if (File.Exists(path))
                File.Delete(path);
        }
    }

    [TestMethod]
    public void EarlyDisposal_ShouldCancelTheProducerAndDisposeItsReader()
    {
        var path = Path.Combine(Path.GetTempPath(), $"musoq-search-{Guid.NewGuid():N}.txt");
        File.WriteAllText(path, "fixture");
        var reader = new EarlyDisposalTextReader(
            string.Concat(Enumerable.Repeat("TODO", RowChunking.DefaultChunkSize)));

        try
        {
            var source = new SearchMatchesSource(
                path,
                "TODO",
                RuntimeV2TestContexts.CreateExecutionContext(),
                _ => reader);
            var enumerator = source.Chunks.GetEnumerator();

            Assert.IsTrue(enumerator.MoveNext());
            Assert.IsTrue(reader.BlockedReadStarted.Wait(TimeSpan.FromSeconds(5)));

            var disposal = Task.Run(() => enumerator.Dispose());
            Thread.Sleep(100);
            reader.Release.Set();

            Assert.IsTrue(reader.Disposed.Wait(TimeSpan.FromSeconds(5)));
            Assert.IsTrue(disposal.Wait(TimeSpan.FromSeconds(5)));
            Assert.IsFalse(reader.UnexpectedReadStarted.IsSet);
        }
        finally
        {
            reader.Release.Set();
            if (File.Exists(path))
                File.Delete(path);
        }
    }

    [TestMethod]
    public void ReaderFailure_ShouldPropagateAndDisposeTheReader()
    {
        var path = Path.Combine(Path.GetTempPath(), $"musoq-search-{Guid.NewGuid():N}.txt");
        File.WriteAllText(path, "fixture");
        var reader = new ThrowingTextReader();

        try
        {
            var source = new SearchMatchesSource(
                path,
                "TODO",
                RuntimeV2TestContexts.CreateExecutionContext(),
                _ => reader);

            var exception = Assert.ThrowsException<SearchSourceReadException>(() => source.Chunks.ToArray());
            Assert.AreEqual(SearchDiagnosticCodes.SourceReadFailed, exception.Diagnostic.Code);
            Assert.AreEqual(SearchDiagnosticPhase.SourceAccess, exception.Diagnostic.Phase);
            Assert.IsTrue(reader.Disposed.Wait(TimeSpan.FromSeconds(5)));
        }
        finally
        {
            if (File.Exists(path))
                File.Delete(path);
        }
    }

    [TestMethod]
    public void EverySearchConstructor_HasExactStarContract()
    {
        var schema = new SearchSchema();
        var context = CreateMetadataContext();

        var constructorSignatures = schema.GetRawConstructors(context)
            .Select(constructor =>
                $"{constructor.MethodName}({string.Join(", ", constructor.ConstructorInfo.Arguments.Select(argument => argument.Type.FullName))})")
            .ToArray();
        CollectionAssert.AreEquivalent(
            new[]
            {
                $"matches({typeof(string).FullName}, {typeof(string).FullName})",
                $"matches({typeof(string).FullName}, {typeof(string).FullName}, {typeof(SearchMatchOptionsInput).FullName})",
                $"many({typeof(string).FullName}, {typeof(IReadOnlyList<SearchPatternInput>).FullName})",
                $"many({typeof(string).FullName}, {typeof(IReadOnlyList<SearchPatternInput>).FullName}, {typeof(SearchManyOptionsInput).FullName})",
                $"lines({typeof(string).FullName}, {typeof(string).FullName})",
                $"lines({typeof(string).FullName}, {typeof(string).FullName}, {typeof(SearchScanOptionsInput).FullName})",
                $"files({typeof(string).FullName}, {typeof(string).FullName})",
                $"files({typeof(string).FullName}, {typeof(string).FullName}, {typeof(SearchScanOptionsInput).FullName})",
                $"counts({typeof(string).FullName}, {typeof(string).FullName})",
                $"counts({typeof(string).FullName}, {typeof(string).FullName}, {typeof(SearchScanOptionsInput).FullName})",
                $"paths({typeof(string).FullName})",
                $"paths({typeof(string).FullName}, {typeof(SearchPathsOptionsInput).FullName})",
                $"bytes({typeof(string).FullName}, {typeof(string).FullName})",
                $"bytes({typeof(string).FullName}, {typeof(string).FullName}, {typeof(SearchBytesOptionsInput).FullName})",
                $"audit({typeof(string).FullName}, {typeof(string).FullName})",
                $"audit({typeof(string).FullName}, {typeof(string).FullName}, {typeof(SearchScanOptionsInput).FullName})"
            },
            constructorSignatures);

        foreach (var contract in StarCases)
        {
            var table = schema.GetTableByName(contract.MethodName, context, contract.Arguments.ToArray());
            StarContractAssertions.AssertExcludedColumnsRemainInSchema(table, contract);
            StarContractAssertions.AssertResult(Compile(contract.Query).Run(), contract);
        }
    }

    private static CompiledQuery Compile(string query)
    {
        return InstanceCreatorHelpers.CompileForExecution(
            query,
            Guid.NewGuid().ToString(),
            new SearchSchemaProvider(),
            EnvironmentVariablesHelpers.CreateMockedEnvironmentVariables());
    }

    private static CompiledQuery CompileWithOptions(
        string query,
        CompilationOptions options,
        ILoggerResolver? loggerResolver = null)
    {
        return InstanceCreator.CompileForExecution(
            query,
            Guid.NewGuid().ToString(),
            new SearchSchemaProvider(),
            loggerResolver ?? new NullSearchLoggerResolver(),
            options);
    }

    private static SearchMatch[] ReadDirect(SourceExecutionContext context)
    {
        return new SearchMatchesSource(FixtureRoot, "TODO", context)
            .Chunks
            .SelectMany(static chunk => chunk)
            .ToArray();
    }

    private static string RowSignature(SearchMatch row)
    {
        return string.Join(
            "|",
            row.Path,
            row.MatchIndex,
            row.LineNumber,
            row.Utf16Column,
            row.MatchText);
    }

    private static string[] Snapshot(Table table)
    {
        return table.Rows
            .Select(row => string.Join(
                "\u001f",
                row.Values.Select(value => value?.ToString() ?? string.Empty)))
            .ToArray();
    }

    private static SourceMetadataContext CreateMetadataContext()
    {
        return new SourceMetadataContext(
            "search-tests",
            CancellationToken.None,
            [],
            new Dictionary<string, string>(),
            NullLogger.Instance);
    }

    private abstract class ObservedTextReader : TextReader
    {
        protected ObservedTextReader()
        {
            Disposed = new ManualResetEventSlim();
        }

        public ManualResetEventSlim Disposed { get; }

        protected override void Dispose(bool disposing)
        {
            Disposed.Set();
            base.Dispose(disposing);
        }
    }

    private sealed class TrackingTextReader(string content) : ObservedTextReader
    {
        private int _position;

        public override int Read(char[] buffer, int index, int count)
        {
            var length = Math.Min(count, content.Length - _position);
            if (length == 0)
                return 0;

            content.CopyTo(_position, buffer, index, length);
            _position += length;
            return length;
        }
    }

    private sealed class BlockingTextReader(string content) : ObservedTextReader
    {
        private int _position;

        public ManualResetEventSlim ReadStarted { get; } = new();

        public ManualResetEventSlim Release { get; } = new();

        public override int Read(char[] buffer, int index, int count)
        {
            ReadStarted.Set();
            Release.Wait();
            var length = Math.Min(count, content.Length - _position);
            if (length == 0)
                return 0;

            content.CopyTo(_position, buffer, index, length);
            _position += length;
            return length;
        }
    }

    private sealed class SlowTextReader(string content) : ObservedTextReader
    {
        private int _position;

        public int ReadCount { get; private set; }

        public ManualResetEventSlim ReadStarted { get; } = new();

        public override int Read(char[] buffer, int index, int count)
        {
            ReadStarted.Set();
            Thread.Sleep(1);
            ReadCount++;
            var length = Math.Min(1, content.Length - _position);
            if (length == 0)
                return 0;

            content.CopyTo(_position, buffer, index, length);
            _position += length;
            return length;
        }
    }

    private sealed class EndOfInputBlockingTextReader(string content) : ObservedTextReader
    {
        private int _position;
        private bool _endReadStarted;

        public ManualResetEventSlim EndReadStarted { get; } = new();

        public ManualResetEventSlim Release { get; } = new();

        public override int Read(char[] buffer, int index, int count)
        {
            if (_position < content.Length)
            {
                var length = Math.Min(count, content.Length - _position);
                content.CopyTo(_position, buffer, index, length);
                _position += length;
                return length;
            }

            if (!_endReadStarted)
            {
                _endReadStarted = true;
                EndReadStarted.Set();
                Release.Wait();
            }

            return 0;
        }
    }

    private sealed class EarlyDisposalTextReader(string content) : ObservedTextReader
    {
        private int _position;
        private int _readPhase;

        public ManualResetEventSlim BlockedReadStarted { get; } = new();

        public ManualResetEventSlim Release { get; } = new();

        public ManualResetEventSlim UnexpectedReadStarted { get; } = new();

        public override int Read(char[] buffer, int index, int count)
        {
            if (_position < content.Length)
            {
                var length = Math.Min(count, content.Length - _position);
                content.CopyTo(_position, buffer, index, length);
                _position += length;
                return length;
            }

            if (_readPhase == 0)
            {
                _readPhase = 1;
                BlockedReadStarted.Set();
                Release.Wait();
                buffer[index] = 'T';
                buffer[index + 1] = 'O';
                buffer[index + 2] = 'D';
                buffer[index + 3] = 'O';
                return 4;
            }

            UnexpectedReadStarted.Set();
            return 0;
        }
    }

    private sealed class ThrowingTextReader : ObservedTextReader
    {
        public override int Read(char[] buffer, int index, int count)
        {
            throw new IOException("synthetic reader failure");
        }
    }

    private static StarContractColumn Column(string name, Type type) => new(name, type);

    private static StarContractColumn Column(
        string name,
        Type type,
        Type schemaSourceReadType,
        EnumTypeOrigin enumOrigin,
        string enumDisplayName,
        EnumUnderlyingKind enumUnderlyingKind,
        bool enumIsFlags) =>
        new(
            name,
            type,
            schemaSourceReadType,
            enumOrigin,
            enumDisplayName,
            enumUnderlyingKind,
            enumIsFlags);

    private static TException? FindInner<TException>(Exception exception)
        where TException : Exception
    {
        for (var current = exception; current is not null; current = current.InnerException)
            if (current is TException match)
                return match;

        return null;
    }

    private static string XmlTypeName(Type type) => (type.FullName ?? type.Name).Replace('+', '.');

    private static readonly object ConsoleOutputLock = new();

    private sealed class NullSearchLoggerResolver : ILoggerResolver
    {
        public ILogger ResolveLogger() => NullLogger.Instance;

        public ILogger<T> ResolveLogger<T>() => NullLogger<T>.Instance;
    }

    private sealed class ContextAwareSettingsResolver : ISourceRuntimeSettingsResolver
    {
        public ConcurrentQueue<SourceRuntimeSettingsResolutionRequest> Requests { get; } = new();

        public IReadOnlyDictionary<string, string> Resolve(SourceRuntimeSettingsResolutionRequest request)
        {
            Requests.Enqueue(request);
            return new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["profile"] = request.ProfileName ?? string.Empty
            };
        }
    }

    private sealed class RecordingLogger : ILogger
    {
        public ConcurrentQueue<string> Messages { get; } = new();

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull
        {
            return NullLogger.Instance.BeginScope(state);
        }

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            Messages.Enqueue(formatter(state, exception));
        }
    }
}
