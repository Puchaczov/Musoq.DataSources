using System.IO.Compression;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Musoq.DataSources.RepresentativeTests;

[TestClass]
public class PluginPackageSmokeTests
{
    [TestMethod]
    public void PluginPackages_WhenArtifactsExist_ShouldContainEntryPointDllAndXmlDocs()
    {
        var artifactsDirectory = Environment.GetEnvironmentVariable("MUSOQ_PLUGIN_ARTIFACTS_DIR")
                                 ?? Path.Combine(FindSolutionRoot(), "artifacts");

        if (!Directory.Exists(artifactsDirectory))
            Assert.Inconclusive($"Plugin artifact directory does not exist: {artifactsDirectory}");

        var packages = Directory.GetFiles(artifactsDirectory, "Musoq.DataSources.*-*.zip", SearchOption.TopDirectoryOnly);

        if (packages.Length == 0)
            Assert.Inconclusive($"No plugin packages found in: {artifactsDirectory}");

        foreach (var packagePath in packages)
            ValidatePackage(packagePath);
    }

    private static void ValidatePackage(string packagePath)
    {
        var tempDirectory = Path.Combine(Path.GetTempPath(), "musoq-package-smoke", Guid.NewGuid().ToString("N"));
        var outerDirectory = Path.Combine(tempDirectory, "outer");
        var pluginDirectory = Path.Combine(tempDirectory, "plugin");

        try
        {
            ZipFile.ExtractToDirectory(packagePath, outerDirectory);

            var entryPointPath = Path.Combine(outerDirectory, "EntryPoint.txt");
            Assert.IsTrue(File.Exists(entryPointPath), $"Package is missing EntryPoint.txt: {packagePath}");

            var entryPointDll = File.ReadAllText(entryPointPath).Trim();
            Assert.IsFalse(string.IsNullOrWhiteSpace(entryPointDll), $"EntryPoint.txt is empty: {packagePath}");

            var pluginZipPath = Path.Combine(outerDirectory, "Plugin.zip");
            Assert.IsTrue(File.Exists(pluginZipPath), $"Package is missing Plugin.zip: {packagePath}");

            ZipFile.ExtractToDirectory(pluginZipPath, pluginDirectory);

            Assert.IsTrue(
                File.Exists(Path.Combine(pluginDirectory, entryPointDll)),
                $"Plugin.zip is missing entry point DLL '{entryPointDll}': {packagePath}");

            var xmlDocumentationFile = Path.ChangeExtension(entryPointDll, ".xml");
            Assert.IsTrue(
                File.Exists(Path.Combine(pluginDirectory, xmlDocumentationFile)),
                $"Plugin.zip is missing XML documentation '{xmlDocumentationFile}': {packagePath}");
        }
        finally
        {
            if (Directory.Exists(tempDirectory))
                Directory.Delete(tempDirectory, true);
        }
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
