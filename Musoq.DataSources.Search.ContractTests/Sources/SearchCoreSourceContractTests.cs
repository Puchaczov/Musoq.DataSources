#nullable enable

using Microsoft.VisualStudio.TestTools.UnitTesting;
using Musoq.DataSources.Search.ContractTests.Infrastructure;
using Musoq.DataSources.Search.Testing;

namespace Musoq.DataSources.Search.ContractTests.Sources;

[TestClass]
public sealed class SearchCoreSourceContractTests : SearchContractTestBase
{
    [TestMethod]
    [TestCategory("SearchContractFast")]
    public void MinimalCorpus_ShouldExposeConsistentSourceUnits()
    {
        WithCorpus(ContractCorpusProfile.Minimal, fixture =>
        {
            var matches = Sql.ExecuteForRoot(
                fixture.Root,
                root => $"select m.Path, m.MatchIndex, m.ByteOffset, m.ByteLength, " +
                        "m.LineNumber, m.Utf16Column, m.Utf16Length, m.MatchText " +
                        $"from search.matches('{root}', 'TODO') m " +
                        "order by m.Path, m.MatchIndex");
            var lines = Sql.ExecuteForRoot(
                fixture.Root,
                root => $"select l.Path, l.LineNumber, l.OccurrenceCount " +
                        $"from search.lines('{root}', 'TODO') l " +
                        "order by l.Path, l.LineNumber");
            var files = Sql.ExecuteForRoot(
                fixture.Root,
                root => $"select f.Path from search.files('{root}', 'TODO') f order by f.Path");
            var counts = Sql.ExecuteForRoot(
                fixture.Root,
                root => $"select c.Path, c.OccurrenceCount, c.MatchingLineCount, c.BytesScanned, c.Complete " +
                        $"from search.counts('{root}', 'TODO') c order by c.Path");
            var paths = Sql.ExecuteForRoot(
                fixture.Root,
                root => $"select p.Path from search.paths('{root}') p order by p.Path");

            AssertNoHashCharacter("select m.Path from search.matches('root', 'TODO') m");

            var expected = SearchReferenceOracle.Scan(Utf8TextInputs(fixture), "TODO");
            Assert.AreEqual(expected.OccurrenceCount, matches.Count);
            Assert.AreEqual(expected.MatchingLineCount, lines.Count);
            Assert.AreEqual(expected.MatchingFileCount, files.Count);
            Assert.AreEqual(fixture.Files.Count(file => file.IsText), counts.Count);
            Assert.AreEqual(fixture.Files.Count, paths.Count);

            CollectionAssert.AreEqual(
                expected.Occurrences.Select(OccurrenceSignature).ToArray(),
                matches.Rows.Select(MatchSignature).ToArray());

            CollectionAssert.AreEqual(
                expected.MatchingLines.Select(LineSignature).ToArray(),
                lines.Rows.Select(LineSignature).ToArray());

            CollectionAssert.AreEqual(
                expected.FileCounts.Where(pair => pair.Value > 0)
                    .Select(pair => pair.Key)
                    .OrderBy(path => path, StringComparer.Ordinal)
                    .ToArray(),
                Paths(files));

            var expectedCountRows = fixture.Files
                .Where(file => file.IsText)
                .Select(file =>
                {
                    var count = expected.FileCounts[file.RelativePath];
                    var matchingLines = expected.MatchingLines.Count(line =>
                        string.Equals(line.Path, file.RelativePath, StringComparison.Ordinal));
                    return string.Join(
                        "|",
                        file.RelativePath,
                        count,
                        matchingLines,
                        file.Bytes.LongLength,
                        true);
                })
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToArray();

            var actualCountRows = counts.Rows
                .Select(row => string.Join("|", row[0], row[1], row[2], row[3], row[4]))
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToArray();

            CollectionAssert.AreEqual(expectedCountRows, actualCountRows);
        });
    }

    [TestMethod]
    [TestCategory("SearchContractFast")]
    public void OmittedOptions_ShouldEqualExplicitDefaultOptions()
    {
        WithCorpus(ContractCorpusProfile.Minimal, fixture =>
        {
            var simple = Sql.ExecuteForRoot(
                fixture.Root,
                root => $"select m.Path, m.MatchIndex from search.matches('{root}', 'TODO') m " +
                        "order by m.Path, m.MatchIndex");
            var explicitDefaults = Sql.ExecuteForRoot(
                fixture.Root,
                root => $"select m.Path, m.MatchIndex from search.matches('{root}', 'TODO', " +
                        "(Text: (Mode: 'literal', CaseMode: 'sensitive', Encoding: 'auto', WholeWord: false), " +
                        "Scope: (Recursive: true, HiddenEntries: false, FollowLinks: false, " +
                        "RepositoryIgnores: 'respect', GlobalIgnores: 'disabled'))) m " +
                        "order by m.Path, m.MatchIndex");

            CollectionAssert.AreEqual(
                simple.Rows.Select(Signature).ToArray(),
                explicitDefaults.Rows.Select(Signature).ToArray());
        });
    }

    [TestMethod]
    [TestCategory("SearchContractFast")]
    public void InsensitiveDetailedMatches_ShouldRetainSourceSpelling()
    {
        WithCorpus(ContractCorpusProfile.Text, fixture =>
        {
            var result = Sql.ExecuteForRoot(
                fixture.Root,
                root => $"select m.Path, m.MatchText from search.matches('{root}', 'todo', " +
                        "(Text: (CaseMode: 'insensitive'))) m " +
                        "where m.Path = 'text/case.txt' order by m.MatchIndex");

            CollectionAssert.AreEqual(
                new[] { "todo", "TODO", "ToDo", "TODO", "TODO", "TODO" },
                result.Rows.Select(row => (string)row[1]!).ToArray());
        });
    }

    private static string OccurrenceSignature(SearchReferenceOccurrence occurrence)
    {
        return string.Join(
            "|",
            occurrence.Path,
            occurrence.MatchIndex,
            occurrence.ByteOffset,
            occurrence.ByteLength,
            occurrence.LineNumber,
            occurrence.Utf16Column,
            occurrence.Utf16Length,
            occurrence.MatchText);
    }

    private static string MatchSignature(IReadOnlyList<object?> row)
    {
        return string.Join("|", row[0], row[1], row[2], row[3], row[4], row[5], row[6], row[7]);
    }

    private static string LineSignature(SearchReferenceLine line)
    {
        return string.Join("|", line.Path, line.LineNumber, line.OccurrenceCount);
    }

    private static string LineSignature(IReadOnlyList<object?> row)
    {
        return string.Join("|", row[0], row[1], row[2]);
    }

    private static string Signature(IReadOnlyList<object?> row)
    {
        return string.Join("|", row.Select(value => value?.ToString() ?? "<null>"));
    }
}
