using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Musoq.DataSources.Archives;
using Musoq.DataSources.CANBus;
using Musoq.DataSources.Common;
using Musoq.DataSources.FlatFile;
using Musoq.DataSources.Git;
using Musoq.DataSources.Json;
using Musoq.DataSources.Os;
using Musoq.DataSources.RepresentativeTests.Components;
using Musoq.DataSources.Roslyn;
using Musoq.DataSources.Search;
using Musoq.DataSources.SeparatedValues;
using Musoq.DataSources.Tests.Common;
using Musoq.Schema;
using Musoq.Schema.DataSources;
using Musoq.Schema.Optimization;

namespace Musoq.DataSources.RepresentativeTests;

[TestClass]
public sealed class FileSystemPathValidationTests
{
    private static readonly HashSet<string> PathNames = new(StringComparer.Ordinal)
    {
        "path", "jsonFilePath", "csvData", "dbcData", "dbc", "root", "directory",
        "sourceDirectory", "destinationDirectory", "directoryOrFile", "pathDirectoryOrFile",
        "repositoryPath"
    };

    public static IEnumerable<object[]> Constructors()
    {
        foreach (var provider in new[] { "archives", "can", "flatfile", "git", "json", "os", "roslyn", "search", "separatedvalues" })
        {
            foreach (var method in Schema(provider).GetRawConstructors(MetadataContext()))
            {
                var arguments = method.ConstructorInfo.Arguments;
                for (var index = 0; index < arguments.Length; index++)
                {
                    if (!PathNames.Contains(arguments[index].Name))
                        continue;
                    var values = arguments.Select(argument => argument.Type == typeof(string) ? (object)"valid-path" :
                        argument.Type == typeof(bool) ? false : argument.Type == typeof(int) ? 0 : null!).ToArray();
                    yield return [provider, method.MethodName, values, index, arguments[index].Name];
                }
            }
        }
    }

    [DataTestMethod]
    [DynamicData(nameof(Constructors), DynamicDataSourceType.Method)]
    public void EveryPathArgument_IsRejectedBeforeProviderWork(
        string provider, string method, object[] values, int pathIndex, string parameterName)
    {
        var schema = Schema(provider);
        var identity = new SourceIdentity(provider, "s", "s", method);
        var request = new SourcePlanRequest { Identity = identity };
        var describe = new SourceDescribeContext(identity, MetadataContext());
        // Include all Unicode control characters, even those accepted by Unix filesystems.
        var invalidCharacters = Enumerable.Range(0, char.MaxValue + 1).Select(value => (char)value)
            .Where(char.IsControl).Concat(Path.GetInvalidPathChars()).Distinct();
        foreach (var character in invalidCharacters)
        {
            foreach (var position in new[] { 0, 2, 4 })
            {
                values[pathIndex] = "abcd".Insert(position, character.ToString());
                AssertInvalid(() => schema.GetTableByName(method, MetadataContext(), values), parameterName, character, position);
                AssertInvalid(() => schema.DescribeSource(method, describe, values), parameterName, character, position);
                AssertInvalid(() => schema.TryPlanSource(method, request, values), parameterName, character, position);
                var native = new TestArguments(values);
                AssertInvalid(() => schema.GetRowSource<object, TestArguments>(method, null!, native), parameterName, character, position);
                if (schema is IQueryScopedRowSourceSchema queryScoped)
                    AssertInvalid(() => queryScoped.GetQueryScopedRowSource<object, UnusedMaterializer, TestArguments>(method, null!, native),
                        parameterName, character, position);
            }
        }
    }

    [TestMethod]
    public void Validator_PreservesValidValuesAndIgnoresNonPathArguments()
    {
        var validator = new FileSystemPathValidator(
            new Dictionary<string, FileSystemPathValidator.Argument[]>(StringComparer.OrdinalIgnoreCase)
            {
                ["file"] = [new(0, "path")]
            });
        foreach (var path in new string?[] { null, "", " ", "relative/file.txt", "../café 🚀/file name.txt",
                     @"C:\folder\file.txt", @"\\server\share\file.txt", "/tmp/file.txt", @"literal\t.txt" })
        {
            object?[] arguments = [path, "\t\n\0"];
            validator.Validate("FILE", arguments);
            validator.Validate("file", new TestArguments(arguments));
            Assert.AreSame(path, arguments[0]);
        }
        validator.Validate("file", []);
        validator.Validate("file", SourceArguments.Empty);
        validator.Validate("file", SourceArguments.Create(42, SourceArguments.Empty));
        validator.Validate("unknown", ["\t"]);
    }

    [TestMethod]
    public void StaticMetadataWithoutValues_RemainsAvailable()
    {
        foreach (var (provider, method) in new[] { ("archives", "file"), ("flatfile", "file"), ("git", "blame"),
                     ("os", "metadata"), ("roslyn", "solution"), ("search", "paths"), ("can", "messages") })
            Assert.IsNotNull(Schema(provider).GetTableByName(method, MetadataContext()));
    }

    [DataTestMethod]
    [DataRow("os.file('bad\tpath')")]
    [DataRow("os.dirscompare('valid', 'bad\tpath')")]
    [DataRow("json.file('bad\tpath')")]
    [DataRow("separatedvalues.comma('bad\tpath', true, 0)")]
    [DataRow("archives.file('bad\tpath')")]
    [DataRow("flat.file('bad\tpath')")]
    [DataRow("can.messages('bad\tpath')")]
    [DataRow("can.signals('bad\tpath')")]
    [DataRow("can.separatedvalues('bad\tpath', 'valid')")]
    [DataRow("can.separatedvalues('valid', 'bad\tpath')")]
    [DataRow("git.blame('bad\tpath', 'valid')")]
    [DataRow("search.paths('bad\tpath')")]
    [DataRow("search.matches('bad\tpath', 'text')")]
    [DataRow("search.lines('bad\tpath', 'text')")]
    [DataRow("search.files('bad\tpath', 'text')")]
    [DataRow("search.counts('bad\tpath', 'text')")]
    [DataRow("search.audit('bad\tpath', 'text')")]
    [DataRow("search.bytes('bad\tpath', '00')")]
    [DataRow("search.many('bad\tpath', array { (Id: 'p', Pattern: 'text') })")]
    [DataRow("csharp.solution('bad\tpath')")]
    public void CompiledSql_ReportsInvalidPaths(string source)
    {
        try
        {
            var table = InstanceCreatorHelpers.CompileForExecution("select * from " + source + " s", Guid.NewGuid().ToString(),
                new ValidationSchemaProvider(), EnvironmentVariablesHelpers.CreateMockedEnvironmentVariables()).Run();
            _ = table.Count;
            Assert.Fail("The query should reject its path.");
        }
        catch (Exception exception) when (exception is not AssertFailedException)
        {
            var messages = exception.ToString();
            StringAssert.Contains(messages, "U+0009");
            StringAssert.Contains(messages, "position 3");
        }
    }

    [TestMethod]
    public void Search_InvalidPathRetainsDiagnosticContract()
    {
        ArgumentException? exception = null;
        try { new SearchSchema().GetTableByName("paths", MetadataContext(), "bad\npath"); }
        catch (ArgumentException failure) { exception = failure; }
        Assert.IsNotNull(exception);
        Assert.AreEqual("SearchRequestException", exception.GetType().Name);
        Assert.IsInstanceOfType<ArgumentException>(exception.InnerException);
        Assert.AreEqual("root", exception.ParamName);
        StringAssert.Contains(exception.Message, "U+000A");
    }

    [TestMethod]
    public void CompiledSql_ValidPathsAndNonPathControlCharactersRemainUsable()
    {
        var root = Path.Combine(AppContext.BaseDirectory, "musoq-paths-" + Guid.NewGuid().ToString("N"), "café folder");
        Directory.CreateDirectory(root);
        try
        {
            var path = Path.Combine(root, "sample file.txt");
            File.WriteAllText(path, "first\tsecond\n");
            foreach (var value in new[] { path, Path.GetRelativePath(Environment.CurrentDirectory, path) })
            {
                var escaped = EscapeSql(value);
                var result = Compile($"select p.InputPath from os.pathinfo('{escaped}') p").Run();
                Assert.AreEqual(value, result.Rows.Single().ReadValue(0));
                Assert.AreEqual(1, Compile($"select * from flat.file('{escaped}') f").Run().Count);
                Assert.AreEqual(1, Compile($"select * from search.matches('{escaped}', 'first\tsecond') m").Run().Count);
            }

            var csv = Path.Combine(root, "sample file.csv");
            File.WriteAllText(csv, "Name|Value\nAlice|first\tsecond\n");
            Assert.AreEqual(1, Compile($"select * from separatedvalues.delimited('{EscapeSql(csv)}', '|', true, 0) r").Run().Count);
        }
        finally
        {
            Directory.Delete(Path.GetDirectoryName(root)!, recursive: true);
        }
    }

    [TestMethod]
    public void GitRepositoryRelativeNamesPatternsAndRevisions_RetainTheirOwnRules()
    {
        // These are logical Git names, not filesystem arguments (as declared in the provider metadata).
        var schema = new GitSchema();
        Assert.IsNotNull(schema.GetTableByName("filehistory", MetadataContext(), "valid", "*\t*.cs"));
        Assert.IsNotNull(schema.GetTableByName("blame", MetadataContext(), "valid", "logical\tname", "revision\n"));
    }

    private static string EscapeSql(string value) => value.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("'", "''", StringComparison.Ordinal);

    private static Musoq.Evaluator.CompiledQuery Compile(string query) => InstanceCreatorHelpers.CompileForExecution(
        query, Guid.NewGuid().ToString(), new ValidationSchemaProvider(), EnvironmentVariablesHelpers.CreateMockedEnvironmentVariables());

    private sealed class ValidationSchemaProvider : ISchemaProvider
    {
        public ISchema GetSchema(string schema) => schema.ToLowerInvariant() switch
        {
            "#flat" => new FlatFileSchema(),
            "#can" => new CANBusSchema(),
            _ => new RepresentativeSchemaProvider().GetSchema(schema)
        };
    }

    private static void AssertInvalid(Action action, string name, char character, int position)
    {
        // Search uses its established ArgumentException subclass.
        ArgumentException? failure = null;
        try { action(); }
        catch (ArgumentException exception) { failure = exception; }
        Assert.IsNotNull(failure, $"Expected invalid path argument {name}.");
        Assert.AreEqual(name, failure.ParamName);
        StringAssert.Contains(failure.Message, $"U+{(int)character:X4}");
        StringAssert.Contains(failure.Message, $"position {position} (zero-based)");
        Assert.IsFalse(failure.Message.Any(char.IsControl));
        Assert.IsFalse(failure.Message.Contains("abcd", StringComparison.Ordinal));
    }

    private static SourceMetadataContext MetadataContext() => new(
        "path-validation", CancellationToken.None, [], new Dictionary<string, string>(), NullLogger.Instance);

    private static SchemaBase Schema(string provider) => provider switch
    {
        "archives" => new ArchivesSchema(), "can" => new CANBusSchema(), "flatfile" => new FlatFileSchema(),
        "git" => new GitSchema(), "json" => new JsonSchema(), "os" => new OsSchema(),
        "roslyn" => new CSharpSchema(), "search" => new SearchSchema(), "separatedvalues" => new SeparatedValuesSchema(),
        _ => throw new ArgumentOutOfRangeException(nameof(provider))
    };

    private readonly struct TestArguments(object?[] values) : ISourceArguments
    {
        public int Count => values.Length;
        public T GetValue<T>(int index) => (T)values[index]!;
        public void Visit<TVisitor>(ref TVisitor visitor, int offset = 0)
            where TVisitor : IColumnValueVisitor, allows ref struct => throw new AssertFailedException("Validation must precede argument transfer.");
    }

    private readonly struct UnusedMaterializer : IQueryRowMaterializer<object>
    {
        public static object Materialize<TReader>(scoped ref TReader reader)
            where TReader : IQuerySourceFieldReader, allows ref struct => throw new AssertFailedException("Validation must precede materialization.");
    }
}
