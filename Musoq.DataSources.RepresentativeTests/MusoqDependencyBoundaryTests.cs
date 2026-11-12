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

    private static readonly IReadOnlyDictionary<string, string> ExpectedVersions =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["MusoqConverterVersion"] = "$(MusoqTypedCoreVersion)",
            ["MusoqEvaluatorVersion"] = "$(MusoqTypedCoreVersion)",
            ["MusoqParserVersion"] = "$(MusoqTypedCoreVersion)",
            ["MusoqPluginsVersion"] = "$(MusoqTypedCoreVersion)",
            ["MusoqSchemaVersion"] = "$(MusoqTypedCoreVersion)"
        };

    [TestMethod]
    public void MusoqPackageVersions_ShouldBeCentralized()
    {
        var root = FindSolutionRoot();
        var properties = XDocument.Load(Path.Combine(root, "Directory.Build.props"));
        Assert.AreEqual("17.0.18",
            properties.Descendants("MusoqTypedCoreVersion").Single().Value, "Musoq runtime package versions must remain aligned.");

        foreach (var (propertyName, expectedVersion) in ExpectedVersions)
        {
            var value = properties.Descendants(propertyName).SingleOrDefault()?.Value;
            Assert.AreEqual(expectedVersion, value, $"Unexpected value for {propertyName}.");
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
        var excluded = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".git", ".artifacts", "artifacts", "bin", "obj"
        };
        var pending = new Stack<string>();
        pending.Push(root);
        while (pending.TryPop(out var directory))
        {
            foreach (var project in Directory.EnumerateFiles(directory, "*.csproj"))
                yield return project;
            foreach (var child in Directory.EnumerateDirectories(directory))
                if (!excluded.Contains(Path.GetFileName(child)))
                    pending.Push(child);
        }
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
               && !name.EndsWith(".Testing", StringComparison.OrdinalIgnoreCase)
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
