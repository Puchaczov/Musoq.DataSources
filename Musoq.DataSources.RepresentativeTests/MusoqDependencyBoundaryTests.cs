using System.Xml.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Musoq.DataSources.RepresentativeTests;

[TestClass]
public sealed class MusoqDependencyBoundaryTests
{
    private static readonly IReadOnlyDictionary<string, string> VersionProperties =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["Musoq.Converter"] = "$(MusoqConverterVersion)",
            ["Musoq.Evaluator"] = "$(MusoqEvaluatorVersion)",
            ["Musoq.Parser"] = "$(MusoqParserVersion)",
            ["Musoq.Plugins"] = "$(MusoqPluginsVersion)",
            ["Musoq.Schema"] = "$(MusoqSchemaVersion)"
        };

    [TestMethod]
    public void MusoqPackageVersions_ShouldBeCentralized()
    {
        var root = FindSolutionRoot();
        var properties = XDocument.Load(Path.Combine(root, "Directory.Build.props"));

        foreach (var propertyName in VersionProperties.Values.Select(GetPropertyName).Distinct(StringComparer.Ordinal))
        {
            var value = properties.Descendants(propertyName).SingleOrDefault()?.Value;
            Assert.AreEqual("17.0.8-alpha.1", value, $"Unexpected value for {propertyName}.");
        }

        foreach (var projectPath in EnumerateProjectPaths(root))
        {
            var project = XDocument.Load(projectPath);
            foreach (var reference in GetMusoqPackageReferences(project))
            {
                var packageId = reference.Attribute("Include")!.Value;
                Assert.AreEqual(
                    VersionProperties[packageId],
                    reference.Attribute("Version")?.Value,
                    $"{Path.GetRelativePath(root, projectPath)} must use the centralized {packageId} version property.");
            }
        }
    }

    [TestMethod]
    public void ProductionProjects_ShouldReferenceOnlyHostProvidedAbiPackages()
    {
        var root = FindSolutionRoot();

        foreach (var projectPath in EnumerateProjectPaths(root).Where(IsProductionProject))
        {
            var relativePath = Path.GetRelativePath(root, projectPath);
            var project = XDocument.Load(projectPath);
            foreach (var reference in GetMusoqPackageReferences(project))
            {
                var packageId = reference.Attribute("Include")!.Value;
                Assert.IsTrue(
                    packageId is "Musoq.Schema" or "Musoq.Plugins",
                    $"Production project {relativePath} must not reference test/runtime package {packageId}.");

                var excludeAssets = reference.Attribute("ExcludeAssets")?.Value
                                    ?? reference.Element("ExcludeAssets")?.Value;
                Assert.AreEqual(
                    "runtime",
                    excludeAssets,
                    $"Production project {relativePath} must exclude runtime assets for {packageId}.");
            }
        }
    }

    private static IEnumerable<string> EnumerateProjectPaths(string root)
    {
        return Directory.EnumerateFiles(root, "*.csproj", SearchOption.AllDirectories)
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase));
    }

    private static IEnumerable<XElement> GetMusoqPackageReferences(XContainer project)
    {
        return project.Descendants("PackageReference")
            .Where(reference => VersionProperties.ContainsKey(reference.Attribute("Include")?.Value ?? string.Empty));
    }

    private static bool IsProductionProject(string path)
    {
        var name = Path.GetFileNameWithoutExtension(path);
        return !path.Contains($"{Path.DirectorySeparatorChar}TestsSolutions{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase)
               && !name.Contains(".Tests", StringComparison.OrdinalIgnoreCase)
               && !name.EndsWith("Tests", StringComparison.OrdinalIgnoreCase)
               && !name.Contains(".Playground", StringComparison.OrdinalIgnoreCase)
               && !name.EndsWith("Benchmarks", StringComparison.OrdinalIgnoreCase);
    }

    private static string GetPropertyName(string expression)
    {
        return expression[2..^1];
    }

    private static string FindSolutionRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Musoq.DataSources.sln")))
                return directory.FullName;

            directory = directory.Parent;
        }

        throw new InvalidOperationException("Could not find solution root.");
    }
}
