#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Musoq.DataSources.Tests.Common;
using Musoq.Schema;
using Musoq.Schema.DataSources;
using Musoq.DataSources.Search.Entities;

using Musoq.DataSources.Search.Sources;

using Musoq.DataSources.Search.Components.Contracts;

namespace Musoq.DataSources.Search.Tests.Components.Text;

[TestClass]
public sealed class SearchRegexCaptureTests
{
    [TestMethod]
    public void OptionalUnmatchedGroup_ShouldRemainDistinctFromSuccessfulEmptyCapture()
    {
        var rows = ReadMatches(
            "b",
            "\\A(?<optional>a)?(?<empty>)",
            nameof(SearchMatch.Captures));

        Assert.AreEqual(1, rows.Length);
        Assert.AreEqual(2, rows[0].Captures.Count);

        var unmatched = rows[0].Captures[0];
        Assert.AreEqual("optional", unmatched.GroupName);
        Assert.AreEqual(1, unmatched.GroupIndex);
        Assert.AreEqual(0, unmatched.CaptureIndex);
        Assert.IsFalse(unmatched.Success);
        Assert.IsNull(unmatched.Text);
        Assert.IsNull(unmatched.Utf16Column);
        Assert.IsNull(unmatched.Utf16Length);

        var empty = rows[0].Captures[1];
        Assert.AreEqual("empty", empty.GroupName);
        Assert.AreEqual(2, empty.GroupIndex);
        Assert.AreEqual(0, empty.CaptureIndex);
        Assert.IsTrue(empty.Success);
        Assert.AreEqual(string.Empty, empty.Text);
        Assert.AreEqual(0L, empty.Utf16Column);
        Assert.AreEqual(0L, empty.Utf16Length);
    }

    [TestMethod]
    public void MultipleGroups_ShouldExposeEveryCaptureInSourceOrder()
    {
        var rows = ReadMatches(
            "123",
            "(?<first>1)(?<second>2)(?<third>3)",
            nameof(SearchMatch.Captures));

        var captures = rows.Single().Captures;
        Assert.AreEqual(3, captures.Count);
        CollectionAssert.AreEqual(
            new[] { "1", "2", "3" },
            captures.Select(static capture => capture.Text).ToArray());
        CollectionAssert.AreEqual(
            new[] { "first", "second", "third" },
            captures.Select(static capture => capture.GroupName).ToArray());
        CollectionAssert.AreEqual(
            new[] { 1, 2, 3 },
            captures.Select(static capture => capture.GroupIndex).ToArray());
        CollectionAssert.AreEqual(
            new[] { 0, 0, 0 },
            captures.Select(static capture => capture.CaptureIndex).ToArray());
        Assert.IsTrue(captures.All(static capture =>
            capture.Success));
        CollectionAssert.AreEqual(
            new long?[] { 0, 1, 2 },
            captures.Select(static capture => capture.Utf16Column).ToArray());
    }

    [TestMethod]
    public void RepeatedGroup_ShouldExposeThePortableProfileFinalCapture()
    {
        var rows = ReadMatches(
            "123",
            "(?<digit>\\d)+",
            nameof(SearchMatch.Captures));

        var capture = rows.Single().Captures.Single();
        Assert.AreEqual("digit", capture.GroupName);
        Assert.AreEqual(1, capture.GroupIndex);
        Assert.AreEqual(0, capture.CaptureIndex);
        Assert.IsTrue(capture.Success);
        Assert.AreEqual("3", capture.Text);
        Assert.AreEqual(2L, capture.Utf16Column);
        Assert.AreEqual(1L, capture.Utf16Length);
    }

    [TestMethod]
    public void DuplicateNamedGroups_ShouldUseTheSharedEngineGroupNumber()
    {
        var rows = ReadMatches(
            "ab",
            "(?<tag>a)(?<tag>b)",
            nameof(SearchMatch.Captures));

        var captures = rows.Single().Captures;
        Assert.AreEqual(1, captures.Count);
        Assert.AreEqual("tag", captures[0].GroupName);
        Assert.AreEqual(1, captures[0].GroupIndex);
        Assert.AreEqual(0, captures[0].CaptureIndex);
        Assert.AreEqual("b", captures[0].Text);
    }

    [TestMethod]
    public void CaptureProjection_ShouldMaterializeCapturesOnlyWhenRequested()
    {
        var compact = ReadMatches("TODO", "(?<word>TODO)", nameof(SearchMatch.Path));
        var requested = ReadMatches("TODO", "(?<word>TODO)", nameof(SearchMatch.Captures));

        Assert.AreEqual(1, compact.Length);
        Assert.AreEqual(1, requested.Length);
        Assert.AreEqual(0, compact[0].Captures.Count);
        Assert.AreEqual(1, requested[0].Captures.Count);
        Assert.AreEqual("word", requested[0].Captures[0].GroupName);
        Assert.IsNull(compact[0].MatchText);
        Assert.IsNull(requested[0].MatchText);
    }

    [TestMethod]
    public void Capture_ShouldMapUtf8BytesAndUtf16Coordinates()
    {
        var rows = ReadMatchesFromFile("😀 TODO", "(?<word>TODO)", nameof(SearchMatch.Captures));

        var capture = rows.Single().Captures.Single();
        Assert.AreEqual("word", capture.GroupName);
        Assert.AreEqual(1, capture.GroupIndex);
        Assert.AreEqual(0, capture.CaptureIndex);
        Assert.IsTrue(capture.Success);
        Assert.AreEqual("TODO", capture.Text);
        Assert.AreEqual(5L, capture.ByteOffset);
        Assert.AreEqual(4L, capture.ByteLength);
        Assert.AreEqual(3L, capture.Utf16Column);
        Assert.AreEqual(4L, capture.Utf16Length);
    }

    private static SearchMatch[] ReadMatches(
        string content,
        string pattern,
        params string[] columns)
    {
        return ReadMatchesCore(content, pattern, columns, useFileReader: false);
    }

    private static SearchMatch[] ReadMatchesFromFile(
        string content,
        string pattern,
        params string[] columns)
    {
        return ReadMatchesCore(content, pattern, columns, useFileReader: true);
    }

    private static SearchMatch[] ReadMatchesCore(
        string content,
        string pattern,
        IReadOnlyList<string> columns,
        bool useFileReader)
    {
        var root = CreateTemporaryRoot();
        var path = Path.Combine(root, "input.txt");
        try
        {
            Directory.CreateDirectory(root);
            File.WriteAllText(path, content, new UTF8Encoding(false));
            var request = SearchRequest.CreateRegex(root, pattern);
            var context = RuntimeV2TestContexts.CreateExecutionContext(
                allColumns: columns
                    .Select((name, index) => (ISchemaColumn)new SchemaColumn(name, index, typeof(object)))
                    .ToArray());
            Func<string, TextReader>? readerFactory = useFileReader
                ? null
                : _ => new StringReader(content);

            return new SearchMatchesSource(request, context, readerFactory)
                .Chunks
                .SelectMany(static chunk => chunk)
                .ToArray();
        }
        finally
        {
            DeleteTemporaryRoot(root);
        }
    }

    private static string CreateTemporaryRoot()
    {
        return Path.Combine(Path.GetTempPath(), $"musoq-search-regex-captures-{Guid.NewGuid():N}");
    }

    private static void DeleteTemporaryRoot(string root)
    {
        if (Directory.Exists(root))
            Directory.Delete(root, recursive: true);
    }
}
