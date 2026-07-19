using System.IO.Compression;
using System.Text.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Musoq.DataSources.RepresentativeTests;

[TestClass]
public class PluginPackageSmokeTests
{
    private const string SemVerPattern =
        @"^(0|[1-9]\d*)\.(0|[1-9]\d*)\.(0|[1-9]\d*)(-[0-9A-Za-z-]+(\.[0-9A-Za-z-]+)*)?$";

    private static readonly string[] HostProvidedAssemblies =
    [
        "Musoq.Schema.dll",
        "Musoq.Plugins.dll",
        "Musoq.Parser.dll",
        "Musoq.Converter.dll",
        "Musoq.Evaluator.dll",
        "Musoq.CommandLine.dll"
    ];

    [TestMethod]
    public void PluginPackages_WhenArtifactsExist_ShouldMatchRuntimeV2PackageContract()
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

            var libraryNamePath = Path.Combine(outerDirectory, "LibraryName.txt");
            Assert.IsTrue(File.Exists(libraryNamePath), $"Package is missing LibraryName.txt: {packagePath}");
            Assert.IsFalse(
                string.IsNullOrWhiteSpace(File.ReadAllText(libraryNamePath)),
                $"LibraryName.txt is empty: {packagePath}");

            var versionPath = Path.Combine(outerDirectory, "Version.txt");
            Assert.IsTrue(File.Exists(versionPath), $"Package is missing Version.txt: {packagePath}");
            var packageVersion = File.ReadAllText(versionPath).Trim();
            Assert.IsFalse(string.IsNullOrWhiteSpace(packageVersion), $"Version.txt is empty: {packagePath}");
            StringAssert.Matches(
                packageVersion,
                new global::System.Text.RegularExpressions.Regex(SemVerPattern),
                $"Version.txt must contain a stable or prerelease SemVer value: {packagePath}");

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

            foreach (var assemblyName in HostProvidedAssemblies)
            {
                Assert.IsFalse(
                    Directory.GetFiles(pluginDirectory, assemblyName, SearchOption.AllDirectories).Any(),
                    $"Plugin.zip should not include host-provided assembly '{assemblyName}': {packagePath}");
            }

            Assert.IsFalse(
                Directory.GetFiles(pluginDirectory, "Musoq.Targets.*.dll", SearchOption.AllDirectories).Any(),
                $"Plugin.zip should not include host-provided Musoq.Targets assemblies: {packagePath}");
            Assert.IsFalse(
                Directory.GetFiles(pluginDirectory, "*.CommandLineArguments.*", SearchOption.AllDirectories).Any(),
                $"Plugin.zip should not include datasource command modules: {packagePath}");

            var libraryName = File.ReadAllText(libraryNamePath).Trim();
            if (libraryName == "Musoq.DataSources.Roslyn")
                ValidateRoslynCommandLineModule(outerDirectory, packagePath);

            ValidateCompatibilityManifest(pluginDirectory, packagePath);
        }
        finally
        {
            if (Directory.Exists(tempDirectory))
                Directory.Delete(tempDirectory, true);
        }
    }

    private static void ValidateRoslynCommandLineModule(string outerDirectory, string packagePath)
    {
        var moduleDirectory = Path.Combine(outerDirectory, "CommandLineModules", "musoq.datasource.roslyn");
        var manifestPath = Path.Combine(moduleDirectory, "CommandLineModule.json");
        Assert.IsTrue(File.Exists(manifestPath), $"Roslyn package is missing its command module manifest: {packagePath}");

        using var manifest = JsonDocument.Parse(File.ReadAllText(manifestPath));
        var root = manifest.RootElement;
        Assert.AreEqual(1, root.GetProperty("formatVersion").GetInt32());
        Assert.AreEqual("musoq.datasource.roslyn", root.GetProperty("moduleId").GetString());
        Assert.AreEqual(
            "Musoq.DataSources.Roslyn.CommandLineArguments.dll",
            root.GetProperty("entryAssembly").GetString());
        Assert.AreEqual("Musoq.CommandLine", root.GetProperty("framework").GetProperty("packageId").GetString());
        Assert.AreEqual("[0.0.1,0.1.0)", root.GetProperty("framework").GetProperty("versionRange").GetString());

        var requirements = root.GetProperty("requiredInvocationItems").EnumerateArray().ToArray();
        Assert.HasCount(1, requirements);
        Assert.AreEqual("musoq.datasource.http-request.v1", requirements[0].GetProperty("name").GetString());
        Assert.AreEqual("http-request-v1", requirements[0].GetProperty("contract").GetString());

        var declaredFiles = root.GetProperty("files").EnumerateArray().ToArray();
        Assert.IsTrue(declaredFiles.Length > 0, "Command module manifest must declare its file closure.");
        foreach (var file in declaredFiles)
        {
            var relativePath = file.GetProperty("path").GetString()!;
            var filePath = Path.Combine(moduleDirectory, relativePath);
            Assert.IsTrue(File.Exists(filePath), $"Declared command module file is missing: {relativePath}");
            Assert.AreEqual(new FileInfo(filePath).Length, file.GetProperty("sizeBytes").GetInt64());
            Assert.AreEqual(
                Convert.ToHexStringLower(global::System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(filePath))),
                file.GetProperty("sha256").GetString());
        }
    }

    private static void ValidateCompatibilityManifest(string pluginDirectory, string packagePath)
    {
        var manifestPath = Path.Combine(pluginDirectory, "MusoqPluginCompatibility.json");
        Assert.IsTrue(File.Exists(manifestPath), $"Plugin.zip is missing MusoqPluginCompatibility.json: {packagePath}");

        using var manifest = JsonDocument.Parse(File.ReadAllText(manifestPath));
        var root = manifest.RootElement;
        Assert.AreEqual(1, root.GetProperty("formatVersion").GetInt32(), $"Unexpected compatibility format: {packagePath}");
        Assert.AreEqual("musoq-runtime-v2", root.GetProperty("runtimeFamily").GetString(), $"Unexpected runtime family: {packagePath}");
        Assert.AreEqual("net10.0", root.GetProperty("targetFramework").GetString(), $"Unexpected target framework: {packagePath}");

        var hostPackages = root.GetProperty("hostPackages");
        ValidateHostPackage(hostPackages, "Musoq.Schema", packagePath);
        ValidateHostPackage(hostPackages, "Musoq.Plugins", packagePath);
    }

    private static void ValidateHostPackage(JsonElement hostPackages, string packageName, string packagePath)
    {
        var package = hostPackages.GetProperty(packageName);
        Assert.AreEqual(
            "17.0.2-alpha.4",
            package.GetProperty("minimumVersionInclusive").GetString(),
            $"Unexpected {packageName} minimum: {packagePath}");
        Assert.AreEqual(
            "18.0.0",
            package.GetProperty("maximumVersionExclusive").GetString(),
            $"Unexpected {packageName} maximum: {packagePath}");
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
