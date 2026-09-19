#nullable enable

namespace Musoq.DataSources.Search.Testing;

/// <summary>
///     Manifest-backed expectations for fixed scope cases. This oracle records
///     declared fixture facts; it intentionally does not implement Search's
///     ignore or glob parser.
/// </summary>
public static class SearchScopeOracle
{
    public static IReadOnlyList<string> EligibleTextPaths(
        ContractCorpusFixture fixture,
        bool includeHidden = false,
        bool respectRepositoryIgnores = true,
        bool recursive = true)
    {
        ArgumentNullException.ThrowIfNull(fixture);

        return fixture.Files
            .Where(file => file.IsText)
            .Where(file => IsUnderScopeRoot(file.RelativePath))
            .Where(file => recursive || RelativeToScope(file.RelativePath).IndexOf('/') < 0)
            .Where(file => includeHidden || !file.Specification.Hidden)
            .Where(file => !respectRepositoryIgnores || !file.Specification.Ignored)
            .Select(file => RelativeToScope(file.RelativePath))
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToArray();
    }

    public static IReadOnlyList<ContractScopeExpectation> DeclaredFacts(
        ContractCorpusFixture fixture)
    {
        ArgumentNullException.ThrowIfNull(fixture);

        return fixture.Files
            .Where(file => file.IsText && IsUnderScopeRoot(file.RelativePath))
            .Select(file => new ContractScopeExpectation(
                RelativeToScope(file.RelativePath),
                file.Specification.Hidden,
                file.Specification.Ignored,
                file.Specification.Hidden
                    ? "hidden entry"
                    : file.Specification.Ignored
                        ? "manifest-declared ignore"
                        : "manifest-declared eligible"))
            .OrderBy(fact => fact.RelativePath, StringComparer.Ordinal)
            .ToArray();
    }

    private static bool IsUnderScopeRoot(string path)
    {
        return path.StartsWith("scope/", StringComparison.Ordinal);
    }

    private static string RelativeToScope(string path)
    {
        return path["scope/".Length..];
    }
}

public sealed record ContractScopeExpectation(
    string RelativePath,
    bool Hidden,
    bool Ignored,
    string Reason);
