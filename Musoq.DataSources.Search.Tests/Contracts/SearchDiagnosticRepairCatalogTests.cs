#nullable enable

using System;
using System.IO;
using System.Linq;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Musoq.Converter;
using Musoq.DataSources.Tests.Common;
using Musoq.Evaluator;
using Musoq.Schema.Exceptions;

using Musoq.DataSources.Search.Entities;

using Musoq.DataSources.Search.Sources;

using Musoq.DataSources.Search.Components.Contracts;

using Musoq.DataSources.Search.Components.Diagnostics;

using Musoq.DataSources.Search.Components.Text;

using Musoq.DataSources.Search.Components.Many;

using Musoq.DataSources.Search.Tests.Infrastructure;

namespace Musoq.DataSources.Search.Tests.Contracts;

[TestClass]
public sealed class SearchDiagnosticRepairCatalogTests
{
    [TestMethod]
    public void UnknownSourceName_ShouldUseMetadataInsteadOfInventingARepairMethod()
    {
        var schema = new SearchSchema();
        var exception = Assert.ThrowsException<SourceNotFoundException>(() =>
            schema.GetRowSource<SearchMatch>(
                "matchz",
                RuntimeV2TestContexts.CreateExecutionContext(),
                "fixture",
                "TODO"));

        Assert.IsNotNull(exception);
        var repaired = schema.GetRowSource<SearchMatch>(
            "matches",
            RuntimeV2TestContexts.CreateExecutionContext(),
            "fixture",
            "TODO");
        Assert.IsInstanceOfType<SearchMatchesTypedSource>(repaired);
    }

    [TestMethod]
    public void MissingArgument_ShouldRepairToTheReflectedConstructorWithoutChangingResultUnit()
    {
        var schema = new SearchSchema();
        var exception = Assert.ThrowsException<SearchRequestException>(() =>
            schema.GetRowSource<SearchMatch>(
                "matches",
                RuntimeV2TestContexts.CreateExecutionContext(),
                "fixture"));

        Assert.AreEqual(SearchDiagnosticCodes.InvalidArgument, exception.Diagnostic.Code);
        Assert.AreEqual("arguments", exception.Diagnostic.Location?.ArgumentName);

        var repaired = schema.GetRowSource<SearchMatch>(
            "matches",
            RuntimeV2TestContexts.CreateExecutionContext(),
            "fixture",
            "TODO");
        Assert.IsInstanceOfType<SearchMatchesTypedSource>(repaired);
    }

    [TestMethod]
    public void MissingAlias_ShouldRepairTheBindingWithoutChangingThePathResultUnit()
    {
        var root = CreateTemporaryRoot();

        try
        {
            Directory.CreateDirectory(root);
            File.WriteAllText(Path.Combine(root, "source.txt"), "TODO\n");
            var escapedRoot = EscapeSql(root);
            Assert.ThrowsException<InvalidOperationException>(() =>
                InstanceCreator.CompileForInspection(
                    $"select m.Path from search.paths('{escapedRoot}') p",
                    $"SearchDiagnosticRepairAlias_{Guid.NewGuid():N}",
                    new SearchSchemaProvider(),
                    new NullSearchLoggerResolver(),
                    new CompilationOptions(
                        ParallelizationMode.Full,
                        usePrimitiveTypeValidation: true)));

            var repairedRows = Compile(
                    $"select p.Path from search.paths('{escapedRoot}') p order by p.Path")
                .Run()
                .Rows
                .Select(static row => (string)row[0])
                .ToArray();
            CollectionAssert.AreEqual(new[] { "source.txt" }, repairedRows);
        }
        finally
        {
            DeleteTemporaryRoot(root);
        }
    }

    [TestMethod]
    public void WrongMode_ShouldRepairTheRequestValueWithoutChangingLiteralIntent()
    {
        var exception = Assert.ThrowsException<SearchRequestException>(
            () => SearchTypedInputNormalizer.CreateMany(
                "fixture",
                [new SearchPatternInput("todo", "TODO", "glob")],
                options: null));

        Assert.AreEqual(SearchDiagnosticCodes.InvalidArgument, exception.Diagnostic.Code);
        Assert.AreEqual("patterns[0].mode", exception.Diagnostic.Location?.ArgumentName);
        StringAssert.Contains(exception.Diagnostic.Explanation, "unsupported type or value");

        var repaired = SearchTypedInputNormalizer.CreateMany(
            "fixture",
            [new SearchPatternInput("todo", "TODO")],
            options: null);
        Assert.AreEqual(SearchPatternMode.Literal, repaired.Patterns.Single().Mode);
    }

    [TestMethod]
    public void GlobAsLike_ShouldRepairTheWildcardLanguageWithoutChangingThePathScope()
    {
        var root = CreateTemporaryRoot();

        try
        {
            Directory.CreateDirectory(root);
            File.WriteAllText(Path.Combine(root, "source.cs"), "TODO\n");
            File.WriteAllText(Path.Combine(root, "source.txt"), "TODO\n");

            var escapedRoot = EscapeSql(root);
            var globRows = Compile(
                    $"select p.Path from search.paths('{escapedRoot}') p " +
                    "where p.Path like '*.cs' order by p.Path")
                .Run()
                .Rows
                .Select(static row => (string)row[0])
                .ToArray();
            var repairedRows = Compile(
                    $"select p.Path from search.paths('{escapedRoot}') p " +
                    "where p.Path like '%.cs' order by p.Path")
                .Run()
                .Rows
                .Select(static row => (string)row[0])
                .ToArray();

            CollectionAssert.AreEqual(Array.Empty<string>(), globRows);
            CollectionAssert.AreEqual(new[] { "source.cs" }, repairedRows);
        }
        finally
        {
            DeleteTemporaryRoot(root);
        }
    }

    [TestMethod]
    public void PathAndRegexEscapes_ShouldPreserveRawIntentAtEachTransportLayer()
    {
        const string logicalPath = @"C:\logs\build";
        const string logicalRegex = @"\bTODO\b";

        Assert.AreEqual(@"C:\\logs\\build", EscapeSql(logicalPath));
        Assert.AreNotEqual(logicalRegex, "\bTODO\b");

        var regex = SearchRegexBackend.Compile(logicalRegex);
        Assert.IsTrue(regex.IsMatch("TODO"));
        Assert.IsFalse(regex.IsMatch("TODOLOGY"));
        Assert.AreEqual(@"\\bTODO\\b", logicalRegex.Replace("\\", "\\\\", StringComparison.Ordinal));
    }

    [TestMethod]
    public void WrongCase_ShouldUseTheDeclaredCasePolicyInsteadOfSilentFolding()
    {
        var defaultRequest = SearchRequest.Create("fixture", "TODO");
        Assert.AreEqual(SearchCaseMode.Sensitive, defaultRequest.CaseMode);

        var insensitive = SearchTypedInputNormalizer.CreateMany(
            "fixture",
            [new SearchPatternInput("todo", "TODO")],
            new SearchManyOptionsInput(
                text: new SearchManyTextInput(caseMode: "insensitive")));
        Assert.AreEqual(SearchCaseMode.Insensitive, insensitive.Options.CaseMode);

        var exception = Assert.ThrowsException<SearchRequestException>(
            () => SearchCasePolicy.Parse("unicode"));
        Assert.AreEqual(SearchDiagnosticCodes.InvalidArgument, exception.Diagnostic.Code);
        Assert.AreEqual("case", exception.Diagnostic.Location?.ArgumentName);
    }

    private static CompiledQuery Compile(string query)
    {
        return InstanceCreatorHelpers.CompileForExecution(
            query,
            $"SearchDiagnosticRepair_{Guid.NewGuid():N}",
            new SearchSchemaProvider(),
            EnvironmentVariablesHelpers.CreateMockedEnvironmentVariables());
    }

    private static string EscapeSql(string value)
    {
        return value.Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("'", "''", StringComparison.Ordinal);
    }

    private static string CreateTemporaryRoot()
    {
        return Path.Combine(Path.GetTempPath(), $"musoq-search-repair-{Guid.NewGuid():N}");
    }

    private static void DeleteTemporaryRoot(string root)
    {
        if (Directory.Exists(root))
            Directory.Delete(root, recursive: true);
    }

    private sealed class NullSearchLoggerResolver : ILoggerResolver
    {
        public ILogger ResolveLogger() => NullLogger.Instance;

        public ILogger<T> ResolveLogger<T>() => NullLogger<T>.Instance;
    }
}
