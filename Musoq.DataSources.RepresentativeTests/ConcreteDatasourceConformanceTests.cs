using System.Globalization;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Musoq.DataSources.Archives;
using Musoq.DataSources.CANBus;
using Musoq.DataSources.Docker;
using Musoq.DataSources.FlatFile;
using Musoq.DataSources.Git;
using Musoq.DataSources.GitHub;
using Musoq.DataSources.Jira;
using Musoq.DataSources.Json;
using Musoq.DataSources.Ollama;
using Musoq.DataSources.OpenAI;
using Musoq.DataSources.Os;
using Musoq.DataSources.Roslyn;
using Musoq.DataSources.SeparatedValues;
using Musoq.DataSources.Tests.Common;
using Musoq.DataSources.Time;
using Musoq.Schema;
using Musoq.Schema.DataSources;
using Musoq.Schema.Optimization;
using Musoq.Schema.Reflection;

namespace Musoq.DataSources.RepresentativeTests;

[TestClass]
public sealed class ConcreteDatasourceConformanceTests
{
    private static readonly ConstructorCase[] ConcreteCases =
    [
        Concrete("archives", "file", ["./Files/Example1/archives.zip"], typeof(string)),

        Concrete("can", "messages", ["./Files/example.dbc"], typeof(string)),
        Concrete("can", "signals", ["./Files/example.dbc"], typeof(string)),

        Concrete("docker", "containers", []),
        Concrete("docker", "images", []),
        Concrete("docker", "networks", []),
        Concrete("docker", "volumes", []),

        Concrete("flatfile", "file", ["./Files/example.txt"], typeof(string)),

        Concrete("git", "repository", ["./Repositories/Repository5"], typeof(string)),
        Concrete("git", "tags", ["./Repositories/Repository5"], typeof(string)),
        Concrete("git", "commits", ["./Repositories/Repository5"], typeof(string)),
        Concrete("git", "branches", ["./Repositories/Repository5"], typeof(string)),
        Concrete("git", "filehistory", ["./Repositories/Repository5", "*.cs"], typeof(string), typeof(string)),
        Concrete("git", "filehistory", ["./Repositories/Repository5", "*.cs", 1], typeof(string), typeof(string), typeof(int)),
        Concrete("git", "filehistory", ["./Repositories/Repository5", "*.cs", 0, 1], typeof(string), typeof(string), typeof(int), typeof(int)),
        Concrete("git", "status", ["./Repositories/Repository5"], typeof(string)),
        Concrete("git", "remotes", ["./Repositories/Repository5"], typeof(string)),
        Concrete("git", "blame", ["./Repositories/BlameTestRepo", "File1.txt"], typeof(string), typeof(string)),
        Concrete("git", "blame", ["./Repositories/BlameTestRepo", "File1.txt", "HEAD"], typeof(string), typeof(string), typeof(string)),

        Concrete("github", "repositories", []),
        Concrete("github", "repositories", ["owner"], typeof(string)),
        Concrete("github", "issues", ["owner", "repo"], typeof(string), typeof(string)),
        Concrete("github", "pullrequests", ["owner", "repo"], typeof(string), typeof(string)),
        Concrete("github", "commits", ["owner", "repo"], typeof(string), typeof(string)),
        Concrete("github", "commits", ["owner", "repo", "main"], typeof(string), typeof(string), typeof(string)),
        Concrete("github", "branchcommits", ["owner", "repo", "base", "head"], typeof(string), typeof(string), typeof(string), typeof(string)),
        Concrete("github", "branches", ["owner", "repo"], typeof(string), typeof(string)),
        Concrete("github", "releases", ["owner", "repo"], typeof(string), typeof(string)),

        Concrete("jira", "issues", ["PROJ"], typeof(string)),
        Concrete("jira", "projects", []),
        Concrete("jira", "comments", ["PROJ-1"], typeof(string)),

        Concrete("os", "file", ["./Files/example.txt"], typeof(string)),
        Concrete("os", "files", ["./Files", false], typeof(string), typeof(bool)),
        Concrete("os", "directories", ["./Files", false], typeof(string), typeof(bool)),
        Concrete("os", "zip", ["./Files/example.zip"], typeof(string)),
        Concrete("os", "processes", []),
        Concrete("os", "dlls", ["./Files", false], typeof(string), typeof(bool)),
        Concrete("os", "dirscompare", ["./Files", "./Files"], typeof(string), typeof(string)),
        Concrete("os", "cultures", []),
        Concrete("os", "currentculture", []),
        Concrete("os", "encodings", []),
        Concrete("os", "timezones", []),
        Concrete("os", "runtime", []),
        Concrete("os", "drives", []),
        Concrete("os", "specialfolders", []),
        Concrete("os", "fileattributes", []),
        Concrete("os", "environmentvariables", []),
        Concrete("os", "pathinfo", ["./Files/example.txt"], typeof(string)),
        Concrete("os", "metadata", ["./Files/example.txt"], typeof(string)),
        Concrete("os", "metadata", ["./Files/example.txt", false], typeof(string), typeof(bool)),
        Concrete("os", "metadata", ["./Files", false, true], typeof(string), typeof(bool), typeof(bool)),

        Concrete("roslyn", "solution", ["./TestsSolutions/Solution1/Solution1.sln"], typeof(string)),

        Concrete("system", "dual", []),
        Concrete("system", "range", [3L], typeof(long)),
        Concrete("system", "range", [1L, 3L], typeof(long), typeof(long)),

        Concrete("time", "interval", ["2024-01-01", "2024-01-02", "1h"], typeof(string), typeof(string), typeof(string))
    ];

    private static readonly DynamicExclusion[] DynamicCases =
    [
        Dynamic("json", "file", ["./Files/example.json"], "The top-level JSON shape is discovered from the input file."),

        Dynamic("separatedvalues", "comma", ["./Files/example.csv", true, 0],
            "Column types are resolved from the selected file and query-scoped shape."),
        Dynamic("separatedvalues", "tab", ["./Files/example.tsv", true, 0],
            "Column types are resolved from the selected file and query-scoped shape."),
        Dynamic("separatedvalues", "semicolon", ["./Files/example.csv", true, 0],
            "Column types are resolved from the selected file and query-scoped shape."),
        Dynamic("separatedvalues", "delimited", ["./Files/example.csv", ",", true, 0],
            "Column types are resolved from the selected file and query-scoped shape."),

        Dynamic("can", "separatedvalues", ["timestamp,id", "BO_ 1 Message: 1 Node"],
            "CAN message and signal columns are discovered from CSV and DBC content."),
        Dynamic("can", "separatedvalues", ["timestamp,id", "BO_ 1 Message: 1 Node", "dec"],
            "CAN message and signal columns are discovered from CSV and DBC content."),
        Dynamic("can", "separatedvalues", ["timestamp,id", "BO_ 1 Message: 1 Node", "dec", "little"],
            "CAN message and signal columns are discovered from CSV and DBC content."),

        Dynamic("openai", "gpt", [], "The API response determines the entity columns at runtime."),
        Dynamic("openai", "gpt", ["model"], "The API response determines the entity columns at runtime."),
        Dynamic("openai", "gpt", ["model", 4000], "The API response determines the entity columns at runtime."),
        Dynamic("openai", "gpt", ["model", 4000, 0.5f], "The API response determines the entity columns at runtime."),
        Dynamic("openai", "gpt", ["model", 4000, 0.5f, 0.1f, 0.2f], "The API response determines the entity columns at runtime."),

        Dynamic("ollama", "llm", ["model"], "The model response determines the entity columns at runtime."),
        Dynamic("ollama", "llm", ["model", 0.5m], "The model response determines the entity columns at runtime.")
    ];

    [TestMethod]
    public void RegisteredConstructors_HaveExactly56ConcreteAnd15DynamicCases()
    {
        Assert.AreEqual(56, ConcreteCases.Length);
        Assert.AreEqual(15, DynamicCases.Length);
        Assert.IsTrue(ConcreteCases.All(item =>
            item.Query.StartsWith("select * from ", StringComparison.OrdinalIgnoreCase)));
        Assert.IsTrue(DynamicCases.All(item => !string.IsNullOrWhiteSpace(item.Reason)));

        var schemas = CreateSchemas();
        var context = CreateMetadataContext();
        var expectedConcrete = ConcreteCases.Select(item => item.Signature).ToArray();
        var expectedDynamic = DynamicCases.Select(item => item.Signature).ToArray();
        var expectedClassifications = expectedConcrete.Concat(expectedDynamic).ToArray();
        var expectedAll = expectedClassifications.ToHashSet(StringComparer.Ordinal);
        var dynamicMethods = DynamicCases.Select(item =>
                (SchemaName: item.SchemaName, MethodName: item.MethodName))
            .Distinct()
            .ToArray();
        var actual = schemas
            .SelectMany(pair => pair.Value.GetRawConstructors(context)
                .Select(constructor => FormatSignature(pair.Key, constructor)))
            .Concat(dynamicMethods
                .SelectMany(method => schemas[method.SchemaName]
                    .GetRawConstructors(method.MethodName, context)
                    .Select(constructor => FormatSignature(method.SchemaName, constructor))))
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        var duplicateExpected = expectedClassifications
            .GroupBy(signature => signature, StringComparer.Ordinal)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .ToArray();
        Assert.AreEqual(0, duplicateExpected.Length,
            $"Expected constructor classifications must be unique: {string.Join(", ", duplicateExpected)}");

        var unclassified = actual.Where(signature => !expectedAll.Contains(signature)).OrderBy(signature => signature).ToArray();
        var missing = expectedAll.Except(actual, StringComparer.Ordinal).OrderBy(signature => signature).ToArray();
        Assert.IsTrue(unclassified.Length == 0,
            $"Unclassified registered constructors: {string.Join(", ", unclassified)}");
        Assert.IsTrue(missing.Length == 0,
            $"Missing registered constructors: {string.Join(", ", missing)}");

        var actualConcrete = actual.Where(signature => expectedConcrete.Contains(signature, StringComparer.Ordinal)).ToArray();
        var actualDynamic = actual.Where(signature => expectedDynamic.Contains(signature, StringComparer.Ordinal)).ToArray();
        Assert.AreEqual(56, actualConcrete.Length);
        Assert.AreEqual(15, actualDynamic.Length);
    }

    [TestMethod]
    public void IncludedSchemaCollections_AreMarkedForCrossApply()
    {
        var schemas = CreateSchemas();
        var context = CreateMetadataContext();
        var failures = new List<string>();

        foreach (var item in ConcreteCases)
        {
            var table = schemas[item.SchemaName].GetTableByName(
                item.MethodName,
                context,
                item.Arguments.ToArray());

            var exposedColumns = table.Columns
                .Select(column => column.ColumnName)
                .ToHashSet(StringComparer.Ordinal);

            foreach (var property in table.Metadata.TableEntityType.GetProperties(BindingFlags.Instance | BindingFlags.Public))
            {
                if (!exposedColumns.Contains(property.Name) ||
                    !IsApplyEligibleCollection(property.PropertyType))
                    continue;

                if (property.GetCustomAttribute<Musoq.Plugins.Attributes.BindablePropertyAsTableAttribute>(true) is null)
                    failures.Add($"{table.Metadata.TableEntityType.FullName}.{property.Name}");
            }
        }

        Assert.IsTrue(failures.Count == 0,
            $"Unmarked schema collection properties: {string.Join(", ", failures.OrderBy(value => value))}");
    }

    [TestMethod]
    public void PrimitiveValidation_IsEnabledAtEveryExplicitCallSite()
    {
        var root = FindRepositoryRoot();
        var matches = new List<string>();

        foreach (var path in Directory.EnumerateFiles(root, "*.cs", SearchOption.AllDirectories)
                     .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))
                     .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase)))
        {
            var content = File.ReadAllText(path);
            foreach (Match match in Regex.Matches(
                         content,
                         @"usePrimitiveTypeValidation\s*:\s*(?<value>true|false)",
                         RegexOptions.IgnoreCase))
            {
                matches.Add($"{Path.GetRelativePath(root, path)}: {match.Groups["value"].Value}");
                Assert.AreEqual("true", match.Groups["value"].Value.ToLowerInvariant(),
                    $"Primitive validation must be enabled at {Path.GetRelativePath(root, path)}.");
            }
        }

        Assert.IsTrue(matches.Count > 0, "No explicit primitive validation call sites were found.");
    }

    [TestMethod]
    public void RegisteredSchemaTables_PreservePortableEnumMetadataAndOrdinaryScalarTypes()
    {
        var schemas = CreateSchemas();
        var context = CreateMetadataContext();
        var enumColumns = new List<string>();

        foreach (var item in ConcreteCases)
        {
            var table = schemas[item.SchemaName].GetTableByName(
                item.MethodName,
                context,
                item.Arguments.ToArray());

            foreach (var column in table.Columns)
            {
                var location = $"{item.SchemaName}.{item.MethodName}.{column.ColumnName}";
                if (column.EnumType is { } descriptor)
                {
                    enumColumns.Add(location);
                    var carrier = Nullable.GetUnderlyingType(column.ColumnType) ?? column.ColumnType;

                    Assert.IsFalse(
                        column.ColumnType.IsEnum,
                        $"{location} must expose a primitive carrier rather than a CLR enum.");
                    Assert.AreEqual(
                        EnumScalarTypeFacts.GetCarrierType(descriptor.UnderlyingKind),
                        carrier,
                        $"{location} carrier must match the descriptor backing kind.");
                    Assert.IsTrue(
                        column.SourceReadType.IsEnum,
                        $"{location} schema source-read type must retain the native CLR enum.");
                    Assert.AreEqual(
                        EnumTypeOrigin.NativeClr,
                        descriptor.Origin,
                        $"{location} must use native CLR enum metadata.");
                    Assert.IsFalse(
                        string.IsNullOrWhiteSpace(descriptor.DisplayName),
                        $"{location} enum display name must be present.");
                    Assert.IsFalse(
                        string.IsNullOrWhiteSpace(descriptor.Fingerprint),
                        $"{location} enum fingerprint must be present.");
                    Assert.IsTrue(
                        descriptor.Members.Count > 0,
                        $"{location} enum descriptor must contain members.");
                }
                else
                {
                    Assert.IsFalse(
                        column.ColumnType.IsEnum,
                        $"{location} ordinary column unexpectedly exposes a CLR enum.");
                    if (IsPrimitive(column.ColumnType))
                    {
                        Assert.AreEqual(
                            column.ColumnType,
                            column.SourceReadType,
                            $"{location} ordinary scalar carrier/source-read types must match.");
                    }
                }
            }
        }

        CollectionAssert.AreEquivalent(
            new[]
            {
                "archives.file.CompressionType",
                "os.directories.Attributes",
                "os.dirscompare.State"
            },
            enumColumns,
            "The native enum producer inventory must remain explicit and complete.");
    }

    private static IReadOnlyDictionary<string, SchemaBase> CreateSchemas()
    {
        return new Dictionary<string, SchemaBase>(StringComparer.Ordinal)
        {
            ["archives"] = new ArchivesSchema(),
            ["can"] = new CANBusSchema(),
            ["docker"] = new DockerSchema(),
            ["flatfile"] = new FlatFileSchema(),
            ["git"] = new GitSchema(),
            ["github"] = new GitHubSchema(),
            ["jira"] = new JiraSchema(),
            ["json"] = new JsonSchema(),
            ["ollama"] = new OllamaSchema(),
            ["openai"] = new OpenAiSchema(),
            ["os"] = new OsSchema(),
            ["roslyn"] = new CSharpSchema(),
            ["separatedvalues"] = new SeparatedValuesSchema(),
            ["system"] = new Musoq.DataSources.System.SystemSchema(),
            ["time"] = new TimeSchema()
        };
    }

    private static string FormatSignature(string schemaName, SchemaMethodInfo constructor)
    {
        var parameterTypes = constructor.ConstructorInfo.Arguments.Select(argument => argument.Type);
        return FormatSignature(schemaName, constructor.MethodName, parameterTypes);
    }

    private static string FormatSignature(string schemaName, string methodName, IEnumerable<Type> parameterTypes)
    {
        return $"{schemaName}.{methodName}({string.Join(", ", parameterTypes.Select(type => type.FullName ?? type.Name))})";
    }

    private static ConstructorCase Concrete(
        string schemaName,
        string methodName,
        IReadOnlyList<object> arguments,
        params Type[] parameterTypes)
    {
        return new ConstructorCase(
            schemaName,
            methodName,
            parameterTypes,
            arguments,
            $"select * from {schemaName}.{methodName}({string.Join(", ", arguments.Select(FormatQueryArgument))})");
    }

    private static DynamicExclusion Dynamic(
        string schemaName,
        string methodName,
        IReadOnlyList<object> arguments,
        string reason,
        params Type[] parameterTypes)
    {
        return new DynamicExclusion(
            schemaName,
            methodName,
            parameterTypes.Length == 0 ? InferParameterTypes(arguments) : parameterTypes,
            arguments,
            reason);
    }

    private static Type[] InferParameterTypes(IReadOnlyList<object> arguments) =>
        arguments.Select(argument => argument.GetType()).ToArray();

    private static string FormatQueryArgument(object argument)
    {
        return argument switch
        {
            string value => $"'{value.Replace("'", "''", StringComparison.Ordinal)}'",
            bool value => value ? "true" : "false",
            long value => $"{value.ToString(CultureInfo.InvariantCulture)}l",
            int value => value.ToString(CultureInfo.InvariantCulture),
            decimal value => value.ToString(CultureInfo.InvariantCulture),
            _ => throw new InvalidOperationException($"Unsupported inventory argument type: {argument.GetType()}")
        };
    }

    private static bool IsApplyEligibleCollection(Type type)
    {
        if (type == typeof(string))
            return false;

        if (type.IsArray)
            return true;

        return type.GetInterfaces()
            .Append(type)
            .Any(candidate => candidate.IsGenericType &&
                             candidate.GetGenericTypeDefinition() == typeof(IEnumerable<>));
    }

    private static bool TryGetCollectionElementType(Type type, out Type? elementType)
    {
        if (type.IsArray)
        {
            elementType = type.GetElementType();
            return elementType is not null;
        }

        foreach (var candidate in type.GetInterfaces().Append(type))
        {
            if (candidate.IsGenericType && candidate.GetGenericTypeDefinition() == typeof(IEnumerable<>))
            {
                elementType = candidate.GetGenericArguments()[0];
                return true;
            }
        }

        elementType = null;
        return false;
    }

    private static bool IsPrimitive(Type type)
    {
        var unwrapped = Nullable.GetUnderlyingType(type) ?? type;
        return unwrapped.IsPrimitive ||
               unwrapped == typeof(string) ||
               unwrapped == typeof(decimal) ||
               unwrapped == typeof(DateTime) ||
               unwrapped == typeof(DateTimeOffset) ||
               unwrapped == typeof(Guid) ||
               unwrapped == typeof(TimeSpan);
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null &&
               !File.Exists(Path.Combine(directory.FullName, "Musoq.DataSources.sln")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new InvalidOperationException("Could not locate the datasource repository root.");
    }

    private static SourceMetadataContext CreateMetadataContext()
    {
        return new SourceMetadataContext(
            "datasource-conformance",
            CancellationToken.None,
            [],
            new Dictionary<string, string>(),
            NullLogger.Instance);
    }

    private sealed record ConstructorCase(
        string SchemaName,
        string MethodName,
        IReadOnlyList<Type> ParameterTypes,
        IReadOnlyList<object> Arguments,
        string Query)
    {
        public string Signature => FormatSignature(SchemaName, MethodName, ParameterTypes);
    }

    private sealed record DynamicExclusion(
        string SchemaName,
        string MethodName,
        IReadOnlyList<Type> ParameterTypes,
        IReadOnlyList<object> Arguments,
        string Reason)
    {
        public string Signature => FormatSignature(SchemaName, MethodName, ParameterTypes);
    }
}
