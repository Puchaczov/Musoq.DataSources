using System.Text.RegularExpressions;
using System.Xml.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Musoq.DataSources.RepresentativeTests;

[TestClass]
public sealed class VirtualParameterDocumentationTests
{
    private static readonly Type[] SchemasWithVirtualParameters =
    [
        typeof(Musoq.DataSources.Archives.ArchivesSchema),
        typeof(Musoq.DataSources.CANBus.CANBusSchema),
        typeof(Musoq.DataSources.FlatFile.FlatFileSchema),
        typeof(Musoq.DataSources.Git.GitSchema),
        typeof(Musoq.DataSources.GitHub.GitHubSchema),
        typeof(Musoq.DataSources.Jira.JiraSchema),
        typeof(Musoq.DataSources.Json.JsonSchema),
        typeof(Musoq.DataSources.Ollama.OllamaSchema),
        typeof(Musoq.DataSources.OpenAI.OpenAiSchema),
        typeof(Musoq.DataSources.Os.OsSchema),
        typeof(Musoq.DataSources.Roslyn.CSharpSchema),
        typeof(Musoq.DataSources.Search.SearchSchema),
        typeof(Musoq.DataSources.SeparatedValues.SeparatedValuesSchema)
    ];

    private static readonly PathParameter[] FileSystemParameters =
    [
        new(typeof(Musoq.DataSources.Archives.ArchivesSchema), "archives", "file", "path"),
        new(typeof(Musoq.DataSources.FlatFile.FlatFileSchema), "flat", "file", "path"),
        new(typeof(Musoq.DataSources.Json.JsonSchema), "json", "file", "jsonFilePath"),

        new(typeof(Musoq.DataSources.SeparatedValues.SeparatedValuesSchema), "separatedvalues", "comma", "path"),
        new(typeof(Musoq.DataSources.SeparatedValues.SeparatedValuesSchema), "separatedvalues", "tab", "path"),
        new(typeof(Musoq.DataSources.SeparatedValues.SeparatedValuesSchema), "separatedvalues", "semicolon", "path"),
        new(typeof(Musoq.DataSources.SeparatedValues.SeparatedValuesSchema), "separatedvalues", "delimited", "path"),

        new(typeof(Musoq.DataSources.Search.SearchSchema), "search", "matches", "root"),
        new(typeof(Musoq.DataSources.Search.SearchSchema), "search", "many", "root"),
        new(typeof(Musoq.DataSources.Search.SearchSchema), "search", "bytes", "root"),
        new(typeof(Musoq.DataSources.Search.SearchSchema), "search", "lines", "root"),
        new(typeof(Musoq.DataSources.Search.SearchSchema), "search", "files", "root"),
        new(typeof(Musoq.DataSources.Search.SearchSchema), "search", "counts", "root"),
        new(typeof(Musoq.DataSources.Search.SearchSchema), "search", "paths", "root"),
        new(typeof(Musoq.DataSources.Search.SearchSchema), "search", "audit", "root"),

        new(typeof(Musoq.DataSources.Os.OsSchema), "os", "dirscompare", "sourceDirectory"),
        new(typeof(Musoq.DataSources.Os.OsSchema), "os", "dirscompare", "destinationDirectory"),
        new(typeof(Musoq.DataSources.Os.OsSchema), "os", "directories", "directory"),
        new(typeof(Musoq.DataSources.Os.OsSchema), "os", "dlls", "path"),
        new(typeof(Musoq.DataSources.Os.OsSchema), "os", "file", "path"),
        new(typeof(Musoq.DataSources.Os.OsSchema), "os", "files", "directory"),
        new(typeof(Musoq.DataSources.Os.OsSchema), "os", "zip", "path"),
        new(typeof(Musoq.DataSources.Os.OsSchema), "os", "metadata", "directoryOrFile"),
        new(typeof(Musoq.DataSources.Os.OsSchema), "os", "metadata", "pathDirectoryOrFile"),
        new(typeof(Musoq.DataSources.Os.OsSchema), "os", "metadata", "directory"),
        new(typeof(Musoq.DataSources.Os.OsSchema), "os", "pathinfo", "path"),

        new(typeof(Musoq.DataSources.Git.GitSchema), "git", "repository", "path"),
        new(typeof(Musoq.DataSources.Git.GitSchema), "git", "tags", "path"),
        new(typeof(Musoq.DataSources.Git.GitSchema), "git", "stashes", "path"),
        new(typeof(Musoq.DataSources.Git.GitSchema), "git", "remotetags", "path"),
        new(typeof(Musoq.DataSources.Git.GitSchema), "git", "commits", "path"),
        new(typeof(Musoq.DataSources.Git.GitSchema), "git", "branches", "path"),
        new(typeof(Musoq.DataSources.Git.GitSchema), "git", "filehistory", "path"),
        new(typeof(Musoq.DataSources.Git.GitSchema), "git", "status", "path"),
        new(typeof(Musoq.DataSources.Git.GitSchema), "git", "remotes", "path"),
        new(typeof(Musoq.DataSources.Git.GitSchema), "git", "blame", "repositoryPath"),

        new(typeof(Musoq.DataSources.Roslyn.CSharpSchema), "csharp", "solution", "path"),

        new(typeof(Musoq.DataSources.CANBus.CANBusSchema), "can", "separatedvalues", "csvData"),
        new(typeof(Musoq.DataSources.CANBus.CANBusSchema), "can", "separatedvalues", "dbcData"),
        new(typeof(Musoq.DataSources.CANBus.CANBusSchema), "can", "messages", "dbc"),
        new(typeof(Musoq.DataSources.CANBus.CANBusSchema), "can", "signals", "dbc")
    ];

    private static readonly NonPathParameter[] LogicalPathsAndPatterns =
    [
        new(typeof(Musoq.DataSources.Git.GitSchema), "git", "blame", "filePath"),
        new(typeof(Musoq.DataSources.Git.GitSchema), "git", "filehistory", "filePattern"),
        new(typeof(Musoq.DataSources.Search.SearchSchema), "search", "matches", "pattern"),
        new(typeof(Musoq.DataSources.Search.SearchSchema), "search", "many", "patterns"),
        new(typeof(Musoq.DataSources.Search.SearchSchema), "search", "bytes", "patternHex"),
        new(typeof(Musoq.DataSources.Search.SearchSchema), "search", "lines", "pattern"),
        new(typeof(Musoq.DataSources.Search.SearchSchema), "search", "files", "pattern"),
        new(typeof(Musoq.DataSources.Search.SearchSchema), "search", "counts", "pattern"),
        new(typeof(Musoq.DataSources.Search.SearchSchema), "search", "audit", "pattern")
    ];

    [TestMethod]
    public void GeneratedPluginXmlDocumentation_NamesEveryVirtualParameter()
    {
        foreach (var schemaType in SchemasWithVirtualParameters)
        {
            var document = LoadDocumentation(schemaType);
            var unnamedParameters = document.Descendants("virtual-param")
                .Where(parameter => string.IsNullOrWhiteSpace(parameter.Attribute("name")?.Value))
                .ToArray();

            Assert.AreEqual(
                0,
                unnamedParameters.Length,
                $"Every virtual-param in '{schemaType.FullName}' XML documentation must have a name.");
        }
    }

    [TestMethod]
    public void FilesystemParameters_AreMarkedAsInvocationDirectoryPaths()
    {
        foreach (var expected in FileSystemParameters)
        {
            var matchingConstructors = FindConstructors(expected.SchemaType, expected.SchemaName, expected.MethodName);
            Assert.IsTrue(
                matchingConstructors.Length > 0,
                $"No XML example was found for '{expected.SchemaName}.{expected.MethodName}'.");

            var constructorsWithParameter = matchingConstructors
                .Where(constructor => HasParameterSignature(constructor, expected.ParameterName))
                .ToArray();
            Assert.IsTrue(
                constructorsWithParameter.Length > 0,
                $"No '{expected.ParameterName}' argument was found in '{expected.SchemaName}.{expected.MethodName}'.");

            foreach (var constructor in constructorsWithParameter)
            {
                var parameter = constructor.Elements("virtual-param")
                    .FirstOrDefault(candidate =>
                        string.Equals(candidate.Attribute("name")?.Value, expected.ParameterName, StringComparison.Ordinal));

                Assert.IsNotNull(
                    parameter,
                    $"'{expected.SchemaName}.{expected.MethodName}' parameter '{expected.ParameterName}' must be documented.");
                Assert.AreEqual(
                    "path",
                    parameter!.Attribute("kind")?.Value,
                    $"'{expected.SchemaName}.{expected.MethodName}' parameter '{expected.ParameterName}' must have kind='path'.");
                Assert.AreEqual(
                    "invocation-directory",
                    parameter.Attribute("relative-to")?.Value,
                    $"'{expected.SchemaName}.{expected.MethodName}' parameter '{expected.ParameterName}' must resolve relative to the invocation directory.");
                Assert.IsNull(
                    parameter.Attribute("absolute"),
                    $"'{expected.SchemaName}.{expected.MethodName}' should express path resolution with relative-to, not an absolute attribute.");
            }
        }
    }

    [TestMethod]
    public void RepositoryRelativePathsAndSearchPatterns_AreNotMarkedAsFilesystemPaths()
    {
        foreach (var excluded in LogicalPathsAndPatterns)
        {
            var matchingConstructors = FindConstructors(excluded.SchemaType, excluded.SchemaName, excluded.MethodName);
            Assert.IsTrue(
                matchingConstructors.Length > 0,
                $"No XML example was found for '{excluded.SchemaName}.{excluded.MethodName}'.");

            var constructorsWithParameter = matchingConstructors
                .Where(constructor => HasParameterSignature(constructor, excluded.ParameterName))
                .ToArray();
            Assert.IsTrue(
                constructorsWithParameter.Length > 0,
                $"No '{excluded.ParameterName}' argument was found in '{excluded.SchemaName}.{excluded.MethodName}'.");

            var incorrectlyMarked = constructorsWithParameter
                .SelectMany(constructor => constructor.Elements("virtual-param"))
                .Any(parameter =>
                    string.Equals(parameter.Attribute("name")?.Value, excluded.ParameterName, StringComparison.Ordinal) &&
                    string.Equals(parameter.Attribute("kind")?.Value, "path", StringComparison.Ordinal));

            Assert.IsFalse(
                incorrectlyMarked,
                $"'{excluded.SchemaName}.{excluded.MethodName}' parameter '{excluded.ParameterName}' is a logical path or pattern, not a filesystem path.");
        }
    }

    private static bool HasParameterSignature(XElement constructor, string parameterName)
    {
        var expectedParameter = new Regex($@"\b{Regex.Escape(parameterName)}\b", RegexOptions.CultureInvariant);
        return constructor.Elements("examples")
            .SelectMany(examples => examples.Elements("example"))
            .SelectMany(example => example.Elements("from"))
            .Any(from => expectedParameter.IsMatch(Regex.Replace(from.Value, @"\s+", " ")));
    }

    private static XElement[] FindConstructors(Type schemaType, string schemaName, string methodName)
    {
        var document = LoadDocumentation(schemaType);
        var members = document.Root?.Element("members")?.Elements("member").ToArray();
        Assert.IsNotNull(members, $"Generated XML documentation for '{schemaType.FullName}' has no members.");

        var expectedCall = new Regex(
            $@"\b{Regex.Escape(schemaName)}\.{Regex.Escape(methodName)}\s*\(",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        return members!
            .SelectMany(member => member.Descendants("virtual-constructor"))
            .Where(constructor => constructor.Descendants("from")
                .Any(from => expectedCall.IsMatch(Regex.Replace(from.Value, @"\s+", " "))))
            .ToArray();
    }

    private static XDocument LoadDocumentation(Type schemaType)
    {
        var xmlPath = Path.ChangeExtension(schemaType.Assembly.Location, ".xml");
        Assert.IsTrue(File.Exists(xmlPath), $"Generated XML documentation was not found at '{xmlPath}'.");
        return XDocument.Load(xmlPath);
    }

    private sealed record PathParameter(Type SchemaType, string SchemaName, string MethodName, string ParameterName);

    private sealed record NonPathParameter(Type SchemaType, string SchemaName, string MethodName, string ParameterName);
}
