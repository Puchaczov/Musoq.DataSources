#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Musoq.DataSources.Tests.Common;
using Musoq.DataSources.Search.Entities;

using Musoq.DataSources.Search.Sources;

namespace Musoq.DataSources.Search.Tests.Infrastructure;

[TestClass]
public sealed class SearchReferenceScannerTests
{
    [TestMethod]
    public void ReferenceScanner_ShouldExposeOccurrenceLineFileAndCountUnits()
    {
        var scan = SearchReferenceScanner.Scan(
            [
                new SearchReferenceInput("a.txt", "TODO TODO\nDONE\n"),
                new SearchReferenceInput("b.txt", "nothing\n"),
                new SearchReferenceInput("c.txt", "TODO\n")
            ],
            "TODO");

        CollectionAssert.AreEqual(
            new[]
            {
                new SearchReferenceOccurrence("a.txt", 0, 1, 0, "TODO"),
                new SearchReferenceOccurrence("a.txt", 1, 1, 5, "TODO"),
                new SearchReferenceOccurrence("c.txt", 0, 1, 0, "TODO")
            },
            scan.Occurrences.ToArray());
        CollectionAssert.AreEqual(
            new[]
            {
                new SearchReferenceLine("a.txt", 1, 2),
                new SearchReferenceLine("c.txt", 1, 1)
            },
            scan.MatchingLines.ToArray());
        CollectionAssert.AreEquivalent(
            new[] { "a.txt", "b.txt", "c.txt" },
            scan.FileCounts.Keys.ToArray());
        Assert.AreEqual(2L, scan.FileCounts["a.txt"]);
        Assert.AreEqual(0L, scan.FileCounts["b.txt"]);
        Assert.AreEqual(1L, scan.FileCounts["c.txt"]);
        Assert.AreEqual(3L, scan.OccurrenceCount);
        Assert.AreEqual(2L, scan.MatchingLineCount);
        Assert.AreEqual(2L, scan.MatchingFileCount);
    }

    [TestMethod]
    public void Search_ShouldMatchReferenceScannerForHandEnumeratedCases()
    {
        var cases = new[]
        {
            new ReferenceCase("two-on-one-line.txt", "TODO TODO\n", "TODO"),
            new ReferenceCase("unicode-coordinate.txt", "😀 TODO\nnext TODO", "TODO"),
            new ReferenceCase("reader-boundary.txt", new string('x', 8_190) + "TODO\n", "TODO"),
            new ReferenceCase("overlap.txt", "ababa", "aba"),
            new ReferenceCase("no-match.txt", "nothing\n", "TODO")
        };

        foreach (var testCase in cases)
        {
            var root = CreateTemporaryRoot();
            var path = Path.Combine(root, testCase.Path);
            try
            {
                Directory.CreateDirectory(root);
                File.WriteAllText(path, testCase.Content, new UTF8Encoding(false));
                var expected = SearchReferenceScanner.Scan(
                    [new SearchReferenceInput(testCase.Path, testCase.Content)],
                    testCase.Literal);
                var actual = ReadSearch(path, testCase.Literal);

                AssertRowsEqual(expected.Occurrences, actual, testCase.Path);
                Assert.AreEqual(expected.OccurrenceCount, actual.LongLength, testCase.Path);
            }
            finally
            {
                DeleteTemporaryRoot(root);
            }
        }
    }

    [TestMethod]
    public void Search_ShouldMatchReferenceScannerForDeterministicRandomInputs()
    {
        const int fileCount = 128;
        const string literal = "TODO";
        var root = CreateTemporaryRoot();
        var inputs = new List<SearchReferenceInput>(fileCount);
        var random = new ReferenceXorShift32(0x51a7e11u);

        try
        {
            Directory.CreateDirectory(root);
            for (var index = 0; index < fileCount; index++)
            {
                var path = $"case-{index:000}.txt";
                var content = index switch
                {
                    0 => CreateRandomContent(random, "x _-😀\n"),
                    1 => "TODO TODO\n" + CreateRandomContent(random),
                    _ => CreateRandomContent(random, "TODOx _-😀\n")
                };
                inputs.Add(new SearchReferenceInput(path, content));
                File.WriteAllText(
                    Path.Combine(root, path),
                    content,
                    new UTF8Encoding(false));
            }

            var expected = SearchReferenceScanner.Scan(inputs, literal);
            var actual = ReadSearch(root, literal);

            AssertRowsEqual(expected.Occurrences, actual, "random corpus");
            Assert.AreEqual(expected.OccurrenceCount, actual.LongLength);
            Assert.IsTrue(expected.FileCounts.Count == fileCount);
            Assert.IsTrue(expected.FileCounts.Values.Any(count => count == 0));
            Assert.IsTrue(expected.FileCounts.Values.Any(count => count > 1));
        }
        finally
        {
            DeleteTemporaryRoot(root);
        }
    }

    private static SearchMatch[] ReadSearch(string root, string literal)
    {
        return new SearchMatchesSource(
                root,
                literal,
                RuntimeV2TestContexts.CreateExecutionContext())
            .Chunks
            .SelectMany(static chunk => chunk)
            .ToArray();
    }

    private static void AssertRowsEqual(
        IReadOnlyList<SearchReferenceOccurrence> expected,
        IReadOnlyList<SearchMatch> actual,
        string message)
    {
        CollectionAssert.AreEqual(
            expected.Select(Signature).ToArray(),
            actual.Select(Signature).ToArray(),
            message);
    }

    private static string Signature(SearchReferenceOccurrence row)
    {
        return string.Join(
            "|",
            row.Path,
            row.MatchIndex,
            row.LineNumber,
            row.Utf16Column,
            row.MatchText);
    }

    private static string Signature(SearchMatch row)
    {
        return string.Join(
            "|",
            row.Path,
            row.MatchIndex,
            row.LineNumber,
            row.Utf16Column,
            row.MatchText);
    }

    private static string CreateRandomContent(
        ReferenceXorShift32 random,
        string alphabet = "TODOx _-😀\n")
    {
        var length = random.Next(0, 96);
        var builder = new StringBuilder(length);
        for (var index = 0; index < length; index++)
            builder.Append(alphabet[random.Next(alphabet.Length)]);
        return builder.ToString();
    }

    private static string CreateTemporaryRoot()
    {
        return Path.Combine(Path.GetTempPath(), $"musoq-search-reference-{Guid.NewGuid():N}");
    }

    private static void DeleteTemporaryRoot(string root)
    {
        if (Directory.Exists(root))
            Directory.Delete(root, recursive: true);
    }

    private sealed record ReferenceCase(string Path, string Content, string Literal);

    private sealed class ReferenceXorShift32(uint seed)
    {
        private uint _state = seed == 0 ? 0x6d2b79f5u : seed;

        public int Next(int exclusiveMax)
        {
            return (int)(NextUInt() % (uint)exclusiveMax);
        }

        public int Next(int minimumInclusive, int maximumExclusive)
        {
            return minimumInclusive + Next(maximumExclusive - minimumInclusive);
        }

        private uint NextUInt()
        {
            _state ^= _state << 13;
            _state ^= _state >> 17;
            _state ^= _state << 5;
            return _state;
        }
    }
}
