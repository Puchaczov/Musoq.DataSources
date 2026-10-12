#nullable enable

using Microsoft.VisualStudio.TestTools.UnitTesting;
using Musoq.DataSources.Search.ContractTests.Infrastructure;
using Musoq.DataSources.Search.Testing;

namespace Musoq.DataSources.Search.ContractTests.Sources;

[TestClass]
public sealed class SearchRegexDefaultRecordsContractTests : SearchContractTestBase
{
    private const string ImplicitOptions =
        "(Text: (Mode: 'regex'), Scope: (Include: array { 'src/*.cs' }))";

    private const string ExplicitOptions =
        "(Text: (Mode: 'regex'), Records: (Mode: 'physical-line'), " +
        "Scope: (Include: array { 'src/*.cs' }))";

    [TestMethod]
    [TestCategory("SearchContractFast")]
    public void Lines_ShouldUsePhysicalRecordsWhenRecordsAreOmitted()
    {
        WithFixture(root =>
        {
            var omitted = Sql.ExecuteForRoot(
                root,
                path => $"select s.Path, s.LineNumber from search.lines(" +
                        $"'{path}', 'TODO|FIXME', options: {ImplicitOptions}) s " +
                        "order by s.Path, s.LineNumber");
            var explicitRecords = Sql.ExecuteForRoot(
                root,
                path => $"select s.Path, s.LineNumber from search.lines(" +
                        $"'{path}', 'TODO|FIXME', options: {ExplicitOptions}) s " +
                        "order by s.Path, s.LineNumber");

            var expected = new[]
            {
                "src/Alpha.cs:1", "src/Alpha.cs:2", "src/Beta.cs:1"
            };
            CollectionAssert.AreEqual(expected, LineSignatures(omitted));
            CollectionAssert.AreEqual(expected, LineSignatures(explicitRecords));
        });
    }

    [TestMethod]
    [TestCategory("SearchContractFast")]
    public void OtherSinglePatternSources_ShouldShareImplicitPhysicalRecords()
    {
        WithFixture(root =>
        {
            var matches = Sql.ExecuteForRoot(
                root,
                path => $"select m.Path, m.LineNumber, m.MatchText from search.matches(" +
                        $"'{path}', 'TODO|FIXME', options: {ImplicitOptions}) m " +
                        "order by m.Path, m.LineNumber");
            var explicitMatches = Sql.ExecuteForRoot(
                root,
                path => $"select m.Path, m.LineNumber, m.MatchText from search.matches(" +
                        $"'{path}', 'TODO|FIXME', options: {ExplicitOptions}) m " +
                        "order by m.Path, m.LineNumber");

            var expectedMatches = new[]
            {
                "src/Alpha.cs:1:TODO", "src/Alpha.cs:2:FIXME", "src/Beta.cs:1:TODO"
            };
            CollectionAssert.AreEqual(expectedMatches, MatchSignatures(matches));
            CollectionAssert.AreEqual(expectedMatches, MatchSignatures(explicitMatches));

            var files = Sql.ExecuteForRoot(
                root,
                path => $"select f.Path from search.files(" +
                        $"'{path}', 'TODO|FIXME', options: {ImplicitOptions}) f " +
                        "order by f.Path");
            CollectionAssert.AreEqual(
                new[] { "src/Alpha.cs", "src/Beta.cs" },
                files.Rows.Select(row => (string)row[0]!).ToArray());

            var counts = Sql.ExecuteForRoot(
                root,
                path => $"select c.Path, c.OccurrenceCount from search.counts(" +
                        $"'{path}', 'TODO|FIXME', options: {ImplicitOptions}) c " +
                        "order by c.Path");
            CollectionAssert.AreEqual(
                new[] { "src/Alpha.cs:2", "src/Beta.cs:1", "src/Clean.cs:0" },
                counts.Rows.Select(row => $"{row[0]}:{row[1]}").ToArray());

            var audit = Sql.ExecuteForRoot(
                root,
                path => $"select a.Complete, a.Occurrences from search.audit(" +
                        $"'{path}', 'TODO|FIXME', options: {ImplicitOptions}) a");
            Assert.AreEqual(1, audit.Count);
            Assert.AreEqual(true, audit.Value<bool>(0, 0));
            Assert.AreEqual(3L, audit.Value<long>(0, 1));
        });
    }

    private static string[] LineSignatures(SearchQueryResult result)
    {
        return result.Rows.Select(row => $"{row[0]}:{row[1]}").ToArray();
    }

    private static string[] MatchSignatures(SearchQueryResult result)
    {
        return result.Rows.Select(row => $"{row[0]}:{row[1]}:{row[2]}").ToArray();
    }

    private static void WithFixture(Action<string> test)
    {
        var root = Path.Combine(
            Path.GetTempPath(),
            $"musoq-search-regex-defaults-{Guid.NewGuid():N}");
        var sourceDirectory = Path.Combine(root, "src");
        Directory.CreateDirectory(sourceDirectory);

        try
        {
            File.WriteAllText(Path.Combine(sourceDirectory, "Alpha.cs"),
                "// TODO implement\n// FIXME improve\n");
            File.WriteAllText(Path.Combine(sourceDirectory, "Beta.cs"),
                "// TODO document\n");
            File.WriteAllText(Path.Combine(sourceDirectory, "Clean.cs"),
                "public class Clean {}\n");
            File.WriteAllText(Path.Combine(root, "notes.txt"), "TODO outside scope\n");

            test(root);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }
}
