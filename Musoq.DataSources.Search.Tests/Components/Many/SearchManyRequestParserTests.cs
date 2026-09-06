#nullable enable

using System.Linq;
using System.Text.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Musoq.DataSources.Search.Components.Contracts;

using Musoq.DataSources.Search.Components.Diagnostics;

using Musoq.DataSources.Search.Components.Text;

using Musoq.DataSources.Search.Components.Many;

namespace Musoq.DataSources.Search.Tests.Components.Many;

[TestClass]
public sealed class SearchManyRequestParserTests
{
    [TestMethod]
    public void Parse_ShouldRoundTripPatternsAndAllOptions()
    {
        var json = JsonSerializer.Serialize(new
        {
            version = 1,
            patterns = new[]
            {
                new { id = "todo-primary", pattern = "TODO", mode = "literal" },
                new { id = "todo-regex", pattern = "T[O]DO", mode = "regex" }
            },
            options = new
            {
                @case = "insensitive",
                wholeWord = true,
                encoding = "utf16-le-bom",
                selection = "leftmost-first-non-overlapping",
                take = 42,
                partialPolicy = "allow",
                validation = "observed-prefix",
                scope = new
                {
                    recursive = false,
                    include = new[] { "*.cs", "src/**" },
                    exclude = new[] { "bin/**" },
                    repositoryIgnores = "disabled",
                    globalIgnores = "configured",
                    hiddenEntries = "include",
                    followLinks = "follow",
                    inaccessibleEntries = "skip"
                }
            }
        });

        var request = SearchManyRequestParser.Parse(json);

        Assert.AreEqual(SearchManyRequest.CurrentVersion, request.Version);
        Assert.AreEqual(2, request.Patterns.Count);
        Assert.AreEqual("todo-primary", request.Patterns[0].Id);
        Assert.AreEqual("TODO", request.Patterns[0].Pattern);
        Assert.AreEqual(SearchPatternMode.Literal, request.Patterns[0].Mode);
        Assert.AreEqual("todo-regex", request.Patterns[1].Id);
        Assert.AreEqual(SearchPatternMode.Regex, request.Patterns[1].Mode);

        Assert.AreEqual(SearchCaseMode.Insensitive, request.Options.CaseMode);
        Assert.IsTrue(request.Options.WholeWord);
        Assert.AreEqual(SearchEncodingMode.Utf16LittleEndianBom, request.Options.EncodingMode);
        Assert.AreEqual(
            SearchSelectionMode.LeftmostFirstNonOverlapping,
            request.Options.Selection);
        Assert.AreEqual(42, request.Options.Take);
        Assert.AreEqual(SearchPartialPolicy.Allow, request.Options.PartialPolicy);
        Assert.AreEqual(SearchValidationMode.ObservedPrefix, request.Options.Validation);
        Assert.IsFalse(request.Options.Scope.Recursive);
        CollectionAssert.AreEqual(new[] { "*.cs", "src/**" }, request.Options.Scope.Include.ToArray());
        CollectionAssert.AreEqual(new[] { "bin/**" }, request.Options.Scope.Exclude.ToArray());
        Assert.AreEqual(RepositoryIgnorePolicy.Disabled, request.Options.Scope.RepositoryIgnores);
        Assert.AreEqual(GlobalIgnorePolicy.Configured, request.Options.Scope.GlobalIgnores);
        Assert.AreEqual(HiddenEntryPolicy.Include, request.Options.Scope.HiddenEntries);
        Assert.AreEqual(LinkTraversalPolicy.Follow, request.Options.Scope.FollowLinks);
        Assert.AreEqual(InaccessibleEntryPolicy.Skip, request.Options.Scope.InaccessibleEntries);

        var serialized = JsonSerializer.Serialize(new
        {
            version = request.Version,
            patterns = request.Patterns.Select(pattern => new
            {
                id = pattern.Id,
                pattern = pattern.Pattern,
                mode = pattern.Mode == SearchPatternMode.Literal ? "literal" : "regex"
            }),
            options = new
            {
                @case = SearchCasePolicy.ToContractValue(request.Options.CaseMode),
                wholeWord = request.Options.WholeWord,
                encoding = SearchEncodingPolicy.ToContractValue(request.Options.EncodingMode),
                selection = "leftmost-first-non-overlapping",
                take = request.Options.Take,
                partialPolicy = request.Options.PartialPolicy == SearchPartialPolicy.Allow ? "allow" : "reject",
                validation = request.Options.Validation == SearchValidationMode.ObservedPrefix
                    ? "observed-prefix"
                    : "full-input",
                scope = new
                {
                    recursive = request.Options.Scope.Recursive,
                    include = request.Options.Scope.Include,
                    exclude = request.Options.Scope.Exclude,
                    repositoryIgnores = request.Options.Scope.RepositoryIgnores == RepositoryIgnorePolicy.Disabled
                        ? "disabled"
                        : "respect",
                    globalIgnores = request.Options.Scope.GlobalIgnores == GlobalIgnorePolicy.Configured
                        ? "configured"
                        : "disabled",
                    hiddenEntries = request.Options.Scope.HiddenEntries == HiddenEntryPolicy.Include
                        ? "include"
                        : "exclude",
                    followLinks = request.Options.Scope.FollowLinks == LinkTraversalPolicy.Follow
                        ? "follow"
                        : "do not follow",
                    inaccessibleEntries = request.Options.Scope.InaccessibleEntries == InaccessibleEntryPolicy.Skip
                        ? "skip"
                        : "fail"
                }
            }
        });

        var reparsed = SearchManyRequestParser.Parse(serialized);
        Assert.AreEqual(request.Version, reparsed.Version);
        CollectionAssert.AreEqual(
            request.Patterns.Select(pattern => pattern.Id).ToArray(),
            reparsed.Patterns.Select(pattern => pattern.Id).ToArray());
        CollectionAssert.AreEqual(
            request.Patterns.Select(pattern => pattern.Pattern).ToArray(),
            reparsed.Patterns.Select(pattern => pattern.Pattern).ToArray());
        Assert.AreEqual(request.Options.CaseMode, reparsed.Options.CaseMode);
        Assert.AreEqual(request.Options.WholeWord, reparsed.Options.WholeWord);
        Assert.AreEqual(request.Options.EncodingMode, reparsed.Options.EncodingMode);
        Assert.AreEqual(request.Options.Selection, reparsed.Options.Selection);
        Assert.AreEqual(request.Options.Take, reparsed.Options.Take);
        Assert.AreEqual(request.Options.PartialPolicy, reparsed.Options.PartialPolicy);
        Assert.AreEqual(request.Options.Validation, reparsed.Options.Validation);
        Assert.AreEqual(request.Options.Scope.Recursive, reparsed.Options.Scope.Recursive);
        CollectionAssert.AreEqual(
            request.Options.Scope.Include.ToArray(),
            reparsed.Options.Scope.Include.ToArray());
        CollectionAssert.AreEqual(
            request.Options.Scope.Exclude.ToArray(),
            reparsed.Options.Scope.Exclude.ToArray());
    }

    [TestMethod]
    public void Parse_ShouldDecodeEscapesAndAcceptMaximumPatternText()
    {
        var escaped = "{\"version\":1,\"patterns\":[{\"id\":\"todo-\\u0031\",\"pattern\":\"TODO\\n\\u00e9\",\"mode\":\"literal\"}]}";

        var parsedEscaped = SearchManyRequestParser.Parse(escaped);

        Assert.AreEqual("todo-1", parsedEscaped.Patterns[0].Id);
        Assert.AreEqual("TODO\né", parsedEscaped.Patterns[0].Pattern);

        var longPattern = new string('x', SearchManyRequestParser.MaxPatternTextLength);
        var longJson = JsonSerializer.Serialize(new
        {
            version = 1,
            patterns = new[] { new { id = "long", pattern = longPattern, mode = "literal" } }
        });

        var parsedLong = SearchManyRequestParser.Parse(longJson);

        Assert.AreEqual(longPattern, parsedLong.Patterns[0].Pattern);
    }

    [TestMethod]
    public void Parse_ShouldAcceptTheDocumentedPositionalAndNamedScalarExamples()
    {
        const string positional =
            @"SELECT Path, PatternId, MatchIndex FROM search.many('./fixture', '{""version"":1,""patterns"": [{""id"": ""todo"", ""pattern"": ""TODO"", ""mode"": ""literal""}]}')";
        const string named =
            @"SELECT Path, PatternId, MatchIndex FROM search.many(root: './fixture', request: '{""version"":1,""patterns"": [{""id"": ""todo"", ""pattern"": ""TODO"", ""mode"": ""literal""}]}')";

        var positionalRequest = SearchManyRequestParser.Parse(ExtractLastSqlString(positional));
        var namedRequest = SearchManyRequestParser.Parse(ExtractLastSqlString(named));

        Assert.AreEqual(positionalRequest.Version, namedRequest.Version);
        Assert.AreEqual(positionalRequest.Patterns.Count, namedRequest.Patterns.Count);
        Assert.AreEqual(positionalRequest.Patterns[0].Id, namedRequest.Patterns[0].Id);
        Assert.AreEqual(positionalRequest.Patterns[0].Pattern, namedRequest.Patterns[0].Pattern);
        Assert.AreEqual(positionalRequest.Patterns[0].Mode, namedRequest.Patterns[0].Mode);
        Assert.AreEqual("todo", namedRequest.Patterns[0].Id);
        Assert.AreEqual("TODO", namedRequest.Patterns[0].Pattern);
    }

    [TestMethod]
    public void Parse_ShouldRejectDuplicateKeysIncludingEscapedNames()
    {
        AssertRejected(
            "{\"version\":1,\"version\":1,\"patterns\":[{\"id\":\"todo\",\"pattern\":\"TODO\",\"mode\":\"literal\"}]}",
            "duplicate JSON property");
        AssertRejected(
            "{\"version\":1,\"\\u0076ersion\":1,\"patterns\":[{\"id\":\"todo\",\"pattern\":\"TODO\",\"mode\":\"literal\"}]}",
            "duplicate JSON property");
        AssertRejected(
            "{\"version\":1,\"patterns\":[{\"id\":\"todo\",\"pattern\":\"TODO\",\"mode\":\"literal\",\"id\":\"other\"}]}",
            "duplicate JSON property");
        AssertRejected(
            "{\"version\":1,\"patterns\":[{\"id\":\"todo\",\"pattern\":\"TODO\",\"mode\":\"literal\"}],\"options\":{\"scope\":{\"recursive\":true,\"recursive\":false}}}",
            "duplicate JSON property");
        AssertRejected(
            "{\"version\":1,\"patterns\":[{\"id\":\"todo\",\"pattern\":\"TODO\",\"mode\":\"literal\"}],\"future\":{\"value\":1,\"value\":2}}",
            "duplicate JSON property");
    }

    [TestMethod]
    public void Parse_ShouldRejectUnknownPropertiesAndIncompatibleModes()
    {
        AssertRejected(
            "{\"version\":1,\"patterns\":[{\"id\":\"todo\",\"pattern\":\"TODO\",\"mode\":\"literal\"}],\"debug\":true}",
            "unknown property");
        AssertRejected(
            "{\"version\":1,\"patterns\":[{\"id\":\"todo\",\"pattern\":\"TODO\",\"mode\":\"glob\"}]}",
            "unsupported value");
        AssertRejected(
            "{\"version\":1,\"patterns\":[{\"id\":\"todo\",\"pattern\":\"TODO\"}]}",
            "'patterns[0].mode' is required");
        AssertRejected(
            "{\"version\":1,\"patterns\":[{\"id\":\"todo\",\"pattern\":\"TODO\",\"mode\":\"literal\"}],\"options\":{\"fast\":true}}",
            "unknown property");
        AssertRejected(
            "{\"version\":1,\"patterns\":[{\"id\":\"todo\",\"pattern\":\"TODO\",\"mode\":\"literal\"}],\"options\":{\"selection\":\"longest\"}}",
            "unsupported value");
    }

    [TestMethod]
    public void Parse_ShouldRejectDuplicateIdsAndInvalidBoundsWithPositions()
    {
        AssertRejected(
            "{\"version\":1,\"patterns\":[{\"id\":\"todo\",\"pattern\":\"TODO\",\"mode\":\"literal\"},{\"id\":\"todo\",\"pattern\":\"FIXME\",\"mode\":\"literal\"}]}",
            "duplicate pattern ID");
        AssertRejected(
            "{\"version\":1,\"patterns\":[{\"id\":\"_todo\",\"pattern\":\"TODO\",\"mode\":\"literal\"}]}",
            "valid ASCII identifier");
        AssertRejected(
            "{\"version\":1,\"patterns\":[{\"id\":\"todo\",\"pattern\":\"\",\"mode\":\"literal\"}]}",
            "must not be empty");
        AssertRejected(
            JsonSerializer.Serialize(new
            {
                version = 1,
                patterns = new[] { new { id = "todo", pattern = "TODO", mode = "literal" } },
                options = new { take = 0 }
            }),
            "options.take");
    }

    [TestMethod]
    public void Parse_ShouldReportMalformedJsonPositionAndRejectNonObjectRoots()
    {
        var malformed = "{\n  \"version\": 1,\n  \"patterns\": [}";
        var malformedException = Assert.ThrowsException<SearchRequestException>(
            () => SearchManyRequestParser.Parse(malformed));

        Assert.AreEqual(SearchDiagnosticPhase.Syntax, malformedException.Diagnostic.Phase);
        Assert.AreEqual("requestJson", malformedException.Diagnostic.Location?.ArgumentName);
        Assert.IsTrue(malformedException.Diagnostic.Location?.Offset >= 0);

        AssertRejected("[]", "one JSON object");
        AssertRejected("{\"version\":1,\"patterns\":[]}", "at least one pattern");
    }

    [TestMethod]
    public void Parse_ShouldRejectAnOversizedScalarBeforeJsonParsing()
    {
        var oversized = new string('x', SearchManyRequestParser.MaxRequestCharacters + 1);
        var exception = Assert.ThrowsException<SearchResourceLimitException>(
            () => SearchManyRequestParser.Parse(oversized));

        Assert.AreEqual(SearchDiagnosticCodes.ResourceLimit, exception.Diagnostic.Code);
        Assert.AreEqual(SearchDiagnosticPhase.Resource, exception.Diagnostic.Phase);
        Assert.AreEqual("requestJson", exception.Diagnostic.Location?.ArgumentName);
        StringAssert.Contains(exception.Diagnostic.Explanation, "requestJson characters");
    }

    private static void AssertRejected(string json, string expectedText)
    {
        var exception = Assert.ThrowsException<SearchRequestException>(
            () => SearchManyRequestParser.Parse(json));

        StringAssert.Contains(exception.Diagnostic.Explanation, expectedText);
        Assert.AreEqual(SearchDiagnosticCodes.InvalidArgument, exception.Diagnostic.Code);
        Assert.AreEqual("requestJson", exception.Diagnostic.Location?.ArgumentName);
        Assert.IsTrue(exception.Diagnostic.Location?.Offset >= 0);
    }

    private static string ExtractLastSqlString(string sql)
    {
        var end = sql.LastIndexOf('\'');
        var start = sql.LastIndexOf('\'', end - 1);
        Assert.IsTrue(start >= 0 && end > start);
        return sql[(start + 1)..end];
    }
}
