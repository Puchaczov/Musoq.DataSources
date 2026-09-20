#nullable enable

using Microsoft.VisualStudio.TestTools.UnitTesting;
using Musoq.DataSources.Search.Components.Diagnostics;
using Musoq.DataSources.Search.ContractTests.Infrastructure;
using Musoq.DataSources.Search.Testing;
using Musoq.Evaluator.Exceptions;

namespace Musoq.DataSources.Search.ContractTests.Sources;

[TestClass]
public sealed class SearchTextContractTests : SearchContractTestBase
{
    [TestMethod]
    [TestCategory("SearchContractFast")]
    public void AutoEncoding_ShouldReadSupportedBomFamilies()
    {
        WithCorpus(ContractCorpusProfile.Encoding, fixture =>
        {
            foreach (var (relativePath, encoding) in new[]
                     {
                         ("encoding/utf8-bom.txt", "auto"),
                         ("encoding/utf16-le.txt", "auto"),
                         ("encoding/utf16-be.txt", "auto")
                     })
            {
                var result = Sql.ExecuteForRoot(
                    fixture.PathFor(relativePath),
                    root => $"select m.Path, m.MatchText from search.matches('{root}', 'TODO', " +
                            $"(Text: (Encoding: '{encoding}'))) m");

                Assert.AreEqual(1, result.Count, relativePath);
                Assert.AreEqual("TODO", result.Value<string>(0, 1), relativePath);
            }
        });
    }

    [TestMethod]
    [TestCategory("SearchContractFast")]
    public void ExplicitEncodingModes_ShouldDecodeTheDeclaredFiles()
    {
        WithCorpus(ContractCorpusProfile.Encoding, fixture =>
        {
            var cases = new[]
            {
                ("encoding/utf8-bom.txt", "utf8-bom"),
                ("encoding/utf16-le.txt", "utf16-le-bom"),
                ("encoding/utf16-be.txt", "utf16-be-bom")
            };

            foreach (var (relativePath, encoding) in cases)
            {
                var result = Sql.ExecuteForRoot(
                    fixture.PathFor(relativePath),
                    root => $"select m.MatchText from search.matches('{root}', 'TODO', " +
                            $"(Text: (Encoding: '{encoding}'))) m");

                Assert.AreEqual(1, result.Count, relativePath);
                Assert.AreEqual("TODO", result.Value<string>(0, 0), relativePath);
            }
        });
    }

    [TestMethod]
    [TestCategory("SearchContractFast")]
    public void BinaryPolicy_ShouldSkipLateNulFilesAndDiagnoseMalformedText()
    {
        WithCorpus(ContractCorpusProfile.Encoding, fixture =>
        {
            var result = Sql.ExecuteForRoot(
                fixture.PathFor("encoding/late-nul.txt"),
                root => $"select m.Path from search.matches('{root}', 'TODO') m");

            Assert.AreEqual(0, result.Count);

            var exception = Assert.ThrowsException<QueryExecutionException>(() =>
                Sql.ExecuteForRoot(
                    fixture.PathFor("encoding/invalid.bin"),
                    root => $"select m.Path from search.matches('{root}', 'TODO') m"));

            var readFailure = FindInner<SearchSourceReadException>(exception);
            Assert.IsNotNull(readFailure, exception.ToString());
            Assert.AreEqual(SearchDiagnosticCodes.SourceReadFailed, readFailure!.Diagnostic.Code);
        });
    }

    [TestMethod]
    [TestCategory("SearchContractFast")]
    public void UnicodeCoordinates_ShouldUseUtf16ColumnsAndOriginalByteOffsets()
    {
        WithCorpus(ContractCorpusProfile.Minimal, fixture =>
        {
            var result = Sql.ExecuteForRoot(
                fixture.PathFor("nested/unicode.txt"),
                root => $"select m.ByteOffset, m.ByteLength, m.Utf16Column, m.Utf16Length " +
                        $"from search.matches('{root}', 'TODO') m");

            Assert.AreEqual(2, result.Count);
            Assert.AreEqual(5L, result.Value<long>(0, 0));
            Assert.AreEqual(4L, result.Value<long>(0, 1));
            Assert.AreEqual(3L, result.Value<long>(0, 2));
            Assert.AreEqual(4L, result.Value<long>(0, 3));
        });
    }

    [TestMethod]
    [TestCategory("SearchContractFast")]
    public void WholeWord_ShouldExcludeEmbeddedAndUnderscoreMatches()
    {
        WithCorpus(ContractCorpusProfile.Text, fixture =>
        {
            var result = Sql.ExecuteForRoot(
                fixture.Root,
                root => $"select m.MatchText from search.matches('{root}', 'todo', " +
                        "(Text: (CaseMode: 'insensitive', WholeWord: true))) m " +
                        "where m.Path = 'text/case.txt' order by m.MatchIndex");

            CollectionAssert.AreEqual(
                new[] { "todo", "TODO", "ToDo" },
                result.Rows.Select(row => (string)row[0]!).ToArray());
        });
    }

    [TestMethod]
    [TestCategory("SearchContractFast")]
    public void RegexQuery_ShouldReturnMatchesFromRealFiles()
    {
        WithCorpus(ContractCorpusProfile.Regex, fixture =>
        {
            var result = Sql.ExecuteForRoot(
                fixture.PathFor("regex/issues.txt"),
                root => $"select m.MatchText, m.LineNumber from search.matches('{root}', " +
                        "'ISSUE-([0-9]+)', (Text: (Mode: 'regex'), " +
                        "Records: (Mode: 'physical-line', MaxBytes: 1024))) m order by m.MatchIndex");

            Assert.AreEqual(2, result.Count);
            CollectionAssert.AreEqual(
                new[] { "ISSUE-42", "ISSUE-7" },
                result.Rows.Select(row => (string)row[0]!).ToArray());
            CollectionAssert.AreEqual(
                new long[] { 1, 2 },
                result.Rows.Select(row => (long)row[1]!).ToArray());
        });
    }

    [TestMethod]
    [TestCategory("SearchContractFast")]
    public void RegexQuery_ShouldExposeCapturesWhenProjected()
    {
        WithCorpus(ContractCorpusProfile.Regex, fixture =>
        {
            var result = Sql.ExecuteForRoot(
                fixture.PathFor("regex/captures.txt"),
                root => $"select c.GroupName, c.Text from search.matches('{root}', " +
                        "'name=(?<name>[a-z]+) id=(?<id>[0-9]+)', " +
                        "(Text: (Mode: 'regex'), Records: (Mode: 'physical-line', MaxBytes: 1024))) m " +
                        "cross apply m.Captures c " +
                        "order by m.MatchIndex, c.GroupIndex");

            Assert.AreEqual(4, result.Count);
            CollectionAssert.AreEqual(
                new[] { "name", "id", "name", "id" },
                result.Rows.Select(row => (string)row[0]!).ToArray());
            CollectionAssert.AreEqual(
                new[] { "alpha", "42", "beta", "7" },
                result.Rows.Select(row => (string)row[1]!).ToArray());
        });
    }

    [TestMethod]
    [TestCategory("SearchContractFast")]
    public void RegexQuery_ShouldDistinguishUnmatchedAndSuccessfulEmptyCaptures()
    {
        WithCorpus(ContractCorpusProfile.Regex, fixture =>
        {
            var result = Sql.ExecuteForRoot(
                fixture.PathFor("regex/empty-group.txt"),
                root => $"select c.GroupName, c.Success, c.Text, c.Utf16Length " +
                        $"from search.matches('{root}', '\\A(?<optional>z)?(?<empty>)', " +
                        "(Text: (Mode: 'regex'), Records: (Mode: 'physical-line', MaxBytes: 1024))) m " +
                        "cross apply m.Captures c order by c.GroupIndex");

            Assert.AreEqual(2, result.Count);
            Assert.AreEqual("optional", result.Value<string>(0, 0));
            Assert.IsFalse(result.Value<bool>(0, 1));
            Assert.IsNull(result.Value(0, 2));
            Assert.IsNull(result.Value(0, 3));
            Assert.AreEqual("empty", result.Value<string>(1, 0));
            Assert.IsTrue(result.Value<bool>(1, 1));
            Assert.AreEqual(string.Empty, result.Value<string>(1, 2));
            Assert.AreEqual(0L, result.Value<long>(1, 3));
        });
    }

    [TestMethod]
    [TestCategory("SearchContractFast")]
    public void RegexRecordLimit_ShouldRejectAnOverlongPhysicalRecord()
    {
        WithCorpus(ContractCorpusProfile.Text, fixture =>
        {
            var exception = Assert.ThrowsException<QueryExecutionException>(() =>
                Sql.ExecuteForRoot(
                    fixture.PathFor("text/long-literal.txt"),
                    root => $"select m.MatchText from search.matches('{root}', 'TODO', " +
                            "(Text: (Mode: 'regex'), Records: (Mode: 'physical-line', MaxBytes: 1048576))) m"));

            var resourceLimit = FindInner<SearchResourceLimitException>(exception);
            if (resourceLimit is null)
                Assert.Fail(exception.ToString());
            Assert.AreEqual("record", resourceLimit!.Diagnostic.Location?.ArgumentName);
        });
    }

    private static T? FindInner<T>(Exception exception)
        where T : Exception
    {
        for (var current = exception; current is not null; current = current.InnerException)
        {
            if (current is T typed)
                return typed;
        }

        return null;
    }
}
