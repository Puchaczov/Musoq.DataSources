#nullable enable

using Microsoft.VisualStudio.TestTools.UnitTesting;
using Musoq.DataSources.Search.Components.Diagnostics;
using Musoq.DataSources.Search.ContractTests.Infrastructure;
using Musoq.DataSources.Search.Testing;
using Musoq.Evaluator.Exceptions;

namespace Musoq.DataSources.Search.ContractTests.Sources;

[TestClass]
public sealed class SearchExtendedTextContractTests : SearchContractTestBase
{
    [TestMethod]
    [TestCategory("SearchContractFast")]
    public void UnicodeCoordinates_ShouldCountCombiningMarksAndSurrogatePairsAsUtf16Units()
    {
        WithExtendedCorpus(fixture =>
        {
            var result = Sql.ExecuteForRoot(
                fixture.PathFor("extended/text/combining.txt"),
                root => $"select m.MatchIndex, m.ByteOffset, m.ByteLength, m.Utf16Column, m.Utf16Length " +
                        $"from search.matches('{root}', 'TODO') m order by m.MatchIndex");

            Assert.AreEqual(2, result.Count);
            CollectionAssert.AreEqual(new long[] { 4, 14 }, result.Rows.Select(row => (long)row[1]!).ToArray());
            CollectionAssert.AreEqual(new long[] { 4, 4 }, result.Rows.Select(row => (long)row[2]!).ToArray());
            CollectionAssert.AreEqual(new long[] { 3, 11 }, result.Rows.Select(row => (long)row[3]!).ToArray());
            Assert.IsTrue(result.Rows.All(row => (long)row[4]! == 4));
        });
    }

    [TestMethod]
    [TestCategory("SearchContractFast")]
    public void PhysicalLines_ShouldTreatLoneCarriageReturnsAsSourceText()
    {
        WithExtendedCorpus(fixture =>
        {
            var result = Sql.ExecuteForRoot(
                fixture.PathFor("extended/text/lone-cr.txt"),
                root => $"select m.LineNumber, m.Utf16Column, m.MatchText from search.matches('{root}', 'TODO') m " +
                        "order by m.MatchIndex");

            Assert.AreEqual(2, result.Count);
            Assert.IsTrue(result.Rows.All(row => (long)row[0]! == 1));
            CollectionAssert.AreEqual(new long[] { 0, 10 }, result.Rows.Select(row => (long)row[1]!).ToArray());
            CollectionAssert.AreEqual(new[] { "TODO", "TODO" }, result.Rows.Select(row => (string)row[2]!).ToArray());
        });
    }

    [TestMethod]
    [TestCategory("SearchContractFast")]
    public void BoundedMultilineRecords_ShouldRequireDelimitersAndHonorEofPolicy()
    {
        WithExtendedCorpus(fixture =>
        {
            var bounded = Sql.ExecuteForRoot(
                fixture.PathFor("extended/regex/bounded.txt"),
                root => $"select m.MatchText, m.LineNumber from search.matches('{root}', 'TODO', " +
                        "(Text: (Mode: 'regex'), Records: (Mode: 'bounded-multiline', MaxBytes: 1024, " +
                        "StartDelimiter: 'BEGIN', EndDelimiter: 'END', AllowUnterminatedEof: false))) m");
            Assert.AreEqual(1, bounded.Count);
            Assert.AreEqual("TODO", bounded.Value<string>(0, 0));
            Assert.AreEqual(2L, bounded.Value<long>(0, 1));

            var unterminated = Sql.ExecuteForRoot(
                fixture.PathFor("extended/regex/bounded-unterminated.txt"),
                root => $"select m.MatchText from search.matches('{root}', 'TODO', " +
                        "(Text: (Mode: 'regex'), Records: (Mode: 'bounded-multiline', MaxBytes: 1024, " +
                        "StartDelimiter: 'BEGIN', EndDelimiter: 'END', AllowUnterminatedEof: true))) m");
            Assert.AreEqual(1, unterminated.Count);
            Assert.AreEqual("TODO", unterminated.Value<string>(0, 0));
        });
    }

    [TestMethod]
    [TestCategory("SearchContractFast")]
    public void LiteralRecordLimit_ShouldBeOptInWhileRegexRecordsRemainBounded()
    {
        WithExtendedCorpus(fixture =>
        {
            var defaultLiteral = Sql.ExecuteForRoot(
                fixture.PathFor("extended/text/long-regex.txt"),
                root => $"select m.MatchText from search.matches('{root}', 'TODO') m");
            Assert.AreEqual(1, defaultLiteral.Count);

            var exception = Assert.ThrowsException<QueryExecutionException>(() =>
                Sql.ExecuteForRoot(
                    fixture.PathFor("extended/text/long-regex.txt"),
                    root => $"select m.MatchText from search.matches('{root}', 'TODO', " +
                            "(Limits: (MaxRecordBytes: 1024))) m"));
            var resourceLimit = FindInner<SearchResourceLimitException>(exception);
            Assert.IsNotNull(resourceLimit, exception.ToString());
            Assert.AreEqual("record-bytes", resourceLimit!.Diagnostic.Location?.ArgumentName);
        });
    }

    [TestMethod]
    [TestCategory("SearchContractFault")]
    public void MalformedExplicitEncoding_ShouldFailBeforeTrustedRows()
    {
        WithExtendedCorpus(fixture =>
        {
            var exception = Assert.ThrowsException<QueryExecutionException>(() =>
                Sql.ExecuteForRoot(
                    fixture.PathFor("extended/encoding/malformed-le.bin"),
                    root => $"select m.Path from search.matches('{root}', 'TODO', " +
                            "(Text: (Encoding: 'utf16-le-bom'))) m"));
            var readFailure = FindInner<SearchSourceReadException>(exception);
            Assert.IsNotNull(readFailure, exception.ToString());
            Assert.AreEqual(SearchDiagnosticCodes.SourceReadFailed, readFailure!.Diagnostic.Code);
        });
    }

    [TestMethod]
    [TestCategory("SearchContractFast")]
    public void OptionalRegexCaptures_ShouldDistinguishEmptyAndUnmatchedGroups()
    {
        WithExtendedCorpus(fixture =>
        {
            var result = Sql.ExecuteForRoot(
                fixture.PathFor("extended/regex/optional-captures.txt"),
                root => $"select m.MatchIndex, c.GroupIndex, c.GroupName, c.Success, c.Text, " +
                        $"c.ByteOffset, c.Utf16Column from search.matches('{root}', " +
                        "'name=(?<name>[a-z]+)(?: id=(?<id>[0-9]*))?', " +
                        "(Text: (Mode: 'regex'), Records: (Mode: 'physical-line', MaxBytes: 1024))) m " +
                        "cross apply m.Captures c order by m.MatchIndex, c.GroupIndex");

            Assert.AreEqual(4, result.Count);
            CollectionAssert.AreEqual(
                new long[] { 0, 0, 1, 1 },
                result.Rows.Select(row => (long)row[0]!).ToArray());
            CollectionAssert.AreEqual(
                new[] { "name", "id", "name", "id" },
                result.Rows.Select(row => (string)row[2]!).ToArray());
            Assert.IsTrue((bool)result.Rows[0][3]!);
            Assert.AreEqual("alpha", result.Rows[0][4]);
            Assert.IsTrue((bool)result.Rows[1][3]!);
            Assert.AreEqual(string.Empty, result.Rows[1][4]);
            Assert.IsTrue((bool)result.Rows[2][3]!);
            Assert.AreEqual("beta", result.Rows[2][4]);
            Assert.IsFalse((bool)result.Rows[3][3]!);
            Assert.IsNull(result.Rows[3][4]);
            Assert.IsTrue(result.Rows.Take(3).All(row => row[5] is long));
            Assert.IsTrue(result.Rows.Take(3).All(row => row[6] is long));
            Assert.IsNull(result.Rows[3][5]);
            Assert.IsNull(result.Rows[3][6]);
        });
    }

    private static T? FindInner<T>(Exception exception)
        where T : Exception
    {
        for (var current = exception; current is not null; current = current.InnerException)
            if (current is T match)
                return match;

        return null;
    }
}
