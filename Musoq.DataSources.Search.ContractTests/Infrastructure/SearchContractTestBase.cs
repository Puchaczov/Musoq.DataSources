#nullable enable

using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Musoq.DataSources.Search.Testing;

namespace Musoq.DataSources.Search.ContractTests.Infrastructure;

public abstract class SearchContractTestBase
{
    protected static string ManifestPath => Path.Combine(
        AppContext.BaseDirectory,
        "TestData",
        "SearchContractCorpus",
        "manifest.json");

    protected static string ExtendedManifestPath => Path.Combine(
        AppContext.BaseDirectory,
        "TestData",
        "SearchContractCorpus",
        "manifest-extended.json");

    protected static SearchSqlHarness Sql { get; } = new();

    protected static void WithCorpus(
        ContractCorpusProfile profile,
        Action<ContractCorpusFixture> test)
    {
        ArgumentNullException.ThrowIfNull(test);

        using var fixture = ContractCorpusBuilder.Create(ManifestPath, profile);
        try
        {
            test(fixture);
        }
        catch (Exception exception)
        {
            fixture.KeepOnDispose = true;
            fixture.WriteFailureReport(exception);
            throw;
        }
    }

    protected static void WithExtendedCorpus(Action<ContractCorpusFixture> test)
    {
        ArgumentNullException.ThrowIfNull(test);

        using var fixture = ContractCorpusBuilder.Create(
            ExtendedManifestPath,
            ContractCorpusProfile.Extended);
        try
        {
            test(fixture);
        }
        catch (Exception exception)
        {
            fixture.KeepOnDispose = true;
            fixture.WriteFailureReport(exception);
            throw;
        }
    }

    protected static IReadOnlyList<SearchReferenceInput> Utf8TextInputs(
        ContractCorpusFixture fixture)
    {
        return fixture.Files
            .Where(file => file.IsText && string.Equals(
                file.Specification.Encoding,
                "utf8",
                StringComparison.Ordinal))
            .Select(file => new SearchReferenceInput(
                file.RelativePath,
                new UTF8Encoding(false).GetString(file.Bytes)))
            .ToArray();
    }

    protected static string[] Paths(SearchQueryResult result)
    {
        return result.Rows
            .Select(row => (string)row[0]!)
            .ToArray();
    }

    protected static string[] SortedPaths(SearchQueryResult result)
    {
        return Paths(result).OrderBy(path => path, StringComparer.Ordinal).ToArray();
    }

    protected static string[] RowSignatures(
        SearchQueryResult result,
        Func<IReadOnlyList<object?>, string> formatter)
    {
        return result.Rows.Select(formatter).OrderBy(value => value, StringComparer.Ordinal).ToArray();
    }

    protected static void AssertNoHashCharacter(string query)
    {
        Assert.IsFalse(query.Contains('#', StringComparison.Ordinal), query);
    }
}
