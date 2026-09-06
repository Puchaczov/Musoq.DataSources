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
using Musoq.Schema.Optimization;

using Musoq.DataSources.Search.Entities;

using Musoq.DataSources.Search.Sources;

using Musoq.DataSources.Search.Components.Contracts;

using Musoq.DataSources.Search.Components.Diagnostics;

using Musoq.DataSources.Search.Components.Text;

using Musoq.DataSources.Search.Components.Traversal;

namespace Musoq.DataSources.Search.Tests.Components.Text;

[TestClass]
public sealed class SearchContextTests
{
    [TestMethod]
    public void Context_ShouldEnrichOccurrencesWithoutChangingCardinality()
    {
        var root = CreateTemporaryRoot();
        const string content = "zero\none TODO\nsame TODO TODO\nthree\nfour TODO\nfive\n";

        try
        {
            Write(root, "fixture.txt", content);
            var rows = ReadMatches(
                SearchRequest.Create(
                    root,
                    "TODO",
                    context: new SearchContextOptions(
                        beforeLines: 2,
                        afterLines: 2,
                        maxBytes: 256)),
                CreateContext(
                    nameof(SearchMatch.Path),
                    nameof(SearchMatch.MatchIndex),
                    nameof(SearchMatch.LineNumber),
                    nameof(SearchMatch.Context)));
            var baseline = ReadMatches(
                SearchRequest.Create(root, "TODO"),
                CreateContext(
                    nameof(SearchMatch.Path),
                    nameof(SearchMatch.MatchIndex),
                    nameof(SearchMatch.LineNumber)));

            Assert.AreEqual(4, rows.Length);
            Assert.AreEqual(baseline.Length, rows.Length);
            CollectionAssert.AreEqual(
                new long[] { 0, 1, 2, 3 },
                rows.Select(static row => row.MatchIndex).ToArray());
            CollectionAssert.AreEqual(
                baseline.Select(static row => row.MatchIndex).ToArray(),
                rows.Select(static row => row.MatchIndex).ToArray());
            CollectionAssert.AreEqual(
                new long[] { 2, 3, 3, 5 },
                rows.Select(static row => row.LineNumber).ToArray());
            CollectionAssert.AreEqual(
                baseline.Select(static row => row.LineNumber).ToArray(),
                rows.Select(static row => row.LineNumber).ToArray());

            CollectionAssert.AreEqual(
                new[] { "-1:1:zero\n", "+1:3:same TODO TODO\n", "+2:4:three\n" },
                FormatContext(rows[0]));
            CollectionAssert.AreEqual(
                new[] { "-2:1:zero\n", "-1:2:one TODO\n", "+1:4:three\n", "+2:5:four TODO\n" },
                FormatContext(rows[1]));
            CollectionAssert.AreEqual(FormatContext(rows[1]), FormatContext(rows[2]));
            CollectionAssert.AreEqual(
                new[] { "-2:3:same TODO TODO\n", "-1:4:three\n", "+1:6:five\n" },
                FormatContext(rows[3]));
        }
        finally
        {
            DeleteTemporaryRoot(root);
        }
    }

    [TestMethod]
    public void Context_ShouldPreserveCrLfAcrossReaderBlocksAndBoundaries()
    {
        var root = CreateTemporaryRoot();
        const string content = "before\r\nhit TODO\r\nafter\r\n";

        try
        {
            Write(root, "fixture.txt", "reader content is supplied by the test");
            var rows = ReadMatches(
                SearchRequest.Create(
                    root,
                    "TODO",
                    context: new SearchContextOptions(
                        beforeLines: 1,
                        afterLines: 1,
                        maxBytes: 64)),
                CreateContext(
                    nameof(SearchMatch.Path),
                    nameof(SearchMatch.LineNumber),
                    nameof(SearchMatch.Context)),
                _ => new ChunkedTextReader(content, chunkSize: 3));

            Assert.AreEqual(1, rows.Length);
            Assert.AreEqual(2L, rows[0].LineNumber);
            CollectionAssert.AreEqual(
                new[] { "-1:1:before\r\n", "+1:3:after\r\n" },
                FormatContext(rows[0]));
        }
        finally
        {
            DeleteTemporaryRoot(root);
        }
    }

    [TestMethod]
    public void Context_ShouldAlsoCompletePhysicalRegexRecords()
    {
        var root = CreateTemporaryRoot();

        try
        {
            Write(root, "fixture.txt", "before\nTODO\nafter\n");
            var rows = ReadMatches(
                SearchRequest.CreateRegex(
                    root,
                    "TODO",
                    context: new SearchContextOptions(
                        beforeLines: 1,
                        afterLines: 1,
                        maxBytes: 64)),
                CreateContext(
                    nameof(SearchMatch.Path),
                    nameof(SearchMatch.LineNumber),
                    nameof(SearchMatch.Context)));

            Assert.AreEqual(1, rows.Length);
            CollectionAssert.AreEqual(
                new[] { "-1:1:before\n", "+1:3:after\n" },
                FormatContext(rows[0]));
        }
        finally
        {
            DeleteTemporaryRoot(root);
        }
    }

    [TestMethod]
    public void Context_ShouldHonorPerWindowUtf8BudgetForLongLines()
    {
        var root = CreateTemporaryRoot();
        const string content = "0123456789\nTODO\nabcdefghij\n";

        try
        {
            Write(root, "fixture.txt", content);
            var rows = ReadMatches(
                SearchRequest.Create(
                    root,
                    "TODO",
                    context: new SearchContextOptions(
                        beforeLines: 1,
                        afterLines: 1,
                        maxBytes: 10)),
                CreateContext(
                    nameof(SearchMatch.Path),
                    nameof(SearchMatch.LineNumber),
                    nameof(SearchMatch.Context)));

            Assert.AreEqual(1, rows.Length);
            Assert.AreEqual(2, rows[0].Context.Count);
            Assert.IsTrue(rows[0].Context.All(
                static line => line.LineText is null || Encoding.UTF8.GetByteCount(line.LineText) <= 5));
            Assert.IsTrue(rows[0].Context.Sum(
                static line => line.LineText is null ? 0 : Encoding.UTF8.GetByteCount(line.LineText)) <= 10);
            CollectionAssert.AreEqual(
                new[] { "01234", "abcde" },
                rows[0].Context.Select(static line => line.LineText).ToArray());
        }
        finally
        {
            DeleteTemporaryRoot(root);
        }
    }

    [TestMethod]
    public void Context_ShouldRemainEmptyWhenTheCollectionIsNotProjected()
    {
        var root = CreateTemporaryRoot();

        try
        {
            Write(root, "fixture.txt", "before\nTODO\nafter\n");
            var rows = ReadMatches(
                SearchRequest.Create(
                    root,
                    "TODO",
                    context: new SearchContextOptions(
                        beforeLines: 1,
                        afterLines: 1,
                        maxBytes: 64)),
                CreateContext(
                    nameof(SearchMatch.Path),
                    nameof(SearchMatch.MatchIndex),
                    nameof(SearchMatch.LineNumber)));

            Assert.AreEqual(1, rows.Length);
            Assert.AreEqual(0, rows[0].Context.Count);
        }
        finally
        {
            DeleteTemporaryRoot(root);
        }
    }

    [TestMethod]
    public void ContextOptions_ShouldRejectRequestsBeyondDeclaredLimits()
    {
        Assert.ThrowsException<SearchResourceLimitException>(() =>
            new SearchContextOptions(
                beforeLines: SearchContextOptions.MaxContextLines,
                afterLines: 1));
        Assert.ThrowsException<SearchResourceLimitException>(() =>
            new SearchContextOptions(maxBytes: SearchContextOptions.MaxContextBytes + 1));
    }

    [TestMethod]
    public void Context_ShouldShareLineTextAcrossOccurrencesButMaterializeImmutableRows()
    {
        var root = CreateTemporaryRoot();
        const string content = "before\nTODO TODO\nafter\n";

        try
        {
            Write(root, "fixture.txt", content);
            var rows = ReadMatches(
                SearchRequest.Create(
                    root,
                    "TODO",
                    context: new SearchContextOptions(
                        beforeLines: 1,
                        afterLines: 1,
                        maxBytes: 64)),
                CreateContext(
                    nameof(SearchMatch.MatchIndex),
                    nameof(SearchMatch.LineNumber),
                    nameof(SearchMatch.Context)));

            Assert.AreEqual(2, rows.Length);
            Assert.AreNotSame(rows[0].Context, rows[1].Context);
            Assert.AreSame(rows[0].Context[0].LineText, rows[1].Context[0].LineText);
            Assert.AreSame(rows[0].Context[1].LineText, rows[1].Context[1].LineText);
            Assert.AreEqual(-1, rows[0].Context[0].RelativeLine);
            Assert.AreEqual(1, rows[0].Context[1].RelativeLine);
            Assert.AreEqual(-1, rows[1].Context[0].RelativeLine);
            Assert.AreEqual(1, rows[1].Context[1].RelativeLine);

            var originalBefore = rows[0].Context[0].LineText;
            File.WriteAllText(Path.Combine(root, "fixture.txt"), "changed\nTODO TODO\nreplacement\n");

            Assert.AreEqual(originalBefore, rows[0].Context[0].LineText);
            Assert.AreEqual("before\n", rows[1].Context[0].LineText);
        }
        finally
        {
            DeleteTemporaryRoot(root);
        }
    }

    [TestMethod]
    public void ContextExpansion_ShouldReadBoundedEvidenceAndRejectStaleSource()
    {
        var root = CreateTemporaryRoot();
        const string content = "zero\none\nhit TODO\nthree\nfour\nfive\n";

        try
        {
            Write(root, "fixture.txt", content);
            var rows = ReadMatches(
                SearchRequest.Create(
                    root,
                    "TODO",
                    context: new SearchContextOptions(
                        beforeLines: 1,
                        afterLines: 1,
                        maxBytes: 64)),
                CreateContext(
                    nameof(SearchMatch.LineNumber),
                    nameof(SearchMatch.Context)));

            Assert.AreEqual(1, rows.Length);
            CollectionAssert.AreEqual(
                new[] { "-2:1:zero\n", "-1:2:one\n", "+1:4:three\n", "+2:5:four\n" },
                FormatContext(rows[0].ExpandContext(
                    new SearchContextOptions(
                        beforeLines: 2,
                        afterLines: 2,
                        maxBytes: 128))));

            File.WriteAllText(Path.Combine(root, "fixture.txt"), "zero\none\nhit TODO\nchanged\nfour\nfive\n");

            Assert.ThrowsException<SearchEvidenceStaleException>(() =>
                rows[0].ExpandContext(
                    new SearchContextOptions(
                        beforeLines: 2,
                        afterLines: 2,
                        maxBytes: 128)));
            Assert.AreEqual("one\n", rows[0].Context[0].LineText);
            Assert.AreEqual("three\n", rows[0].Context[1].LineText);
        }
        finally
        {
            DeleteTemporaryRoot(root);
        }
    }

    private static SearchMatch[] ReadMatches(
        SearchRequest request,
        SourceExecutionContext context,
        Func<string, TextReader>? readerFactory = null)
    {
        return new SearchMatchesSource(request, context, readerFactory)
            .Chunks
            .SelectMany(static chunk => chunk)
            .ToArray();
    }

    private static SourceExecutionContext CreateContext(params string[] columns)
    {
        return RuntimeV2TestContexts.CreateExecutionContext(
            allColumns: columns
                .Select((name, index) => (ISchemaColumn)new SchemaColumn(name, index, typeof(object)))
                .ToArray());
    }

    private static string[] FormatContext(SearchMatch row)
    {
        return FormatContext(row.Context);
    }

    private static string[] FormatContext(IReadOnlyList<SearchContextLine> context)
    {
        return context
            .Select(static line =>
                $"{(line.RelativeLine > 0 ? "+" : string.Empty)}{line.RelativeLine}:" +
                $"{line.LineNumber}:{line.LineText}")
            .ToArray();
    }

    private static void Write(string root, string relativePath, string content)
    {
        Directory.CreateDirectory(root);
        File.WriteAllText(Path.Combine(root, relativePath), content);
    }

    private static string CreateTemporaryRoot()
    {
        return Path.Combine(Path.GetTempPath(), $"musoq-search-context-{Guid.NewGuid():N}");
    }

    private static void DeleteTemporaryRoot(string root)
    {
        if (Directory.Exists(root))
            Directory.Delete(root, recursive: true);
    }

    private sealed class ChunkedTextReader(string content, int chunkSize) : TextReader
    {
        private int _position;

        public override int Read(char[] buffer, int index, int count)
        {
            var remaining = content.Length - _position;
            if (remaining == 0)
                return 0;

            var length = Math.Min(Math.Min(count, chunkSize), remaining);
            content.CopyTo(_position, buffer, index, length);
            _position += length;
            return length;
        }
    }
}
