#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Musoq.DataSources.Tests.Common;
using Musoq.Evaluator;
using Musoq.Evaluator.Exceptions;
using Musoq.Schema.Exceptions;
using Musoq.Schema.Optimization;

using Musoq.DataSources.Search.Entities;

using Musoq.DataSources.Search.Components.Diagnostics;

using Musoq.DataSources.Search.Tests.Infrastructure;

namespace Musoq.DataSources.Search.Tests.Sources.Many;

[TestClass]
public sealed class SearchManyTableInputTests
{
    private const string TodoRequest =
        "{\"version\":1,\"patterns\":[{\"id\":\"todo\",\"pattern\":\"TODO\",\"mode\":\"literal\"}]}";

    private const string FixmeRequest =
        "{\"version\":1,\"patterns\":[{\"id\":\"fixme\",\"pattern\":\"FIXME\",\"mode\":\"literal\"}]}";

    [TestMethod]
    public void Metadata_ShouldDescribeManyWithoutRuntimeValues()
    {
        var schema = new SearchSchema();
        var descriptor = schema.DescribeSource(
            "many",
            new SourceDescribeContext(
                new SourceIdentity("search", "many", "search-tests", "metadata-only"),
                CreateMetadataContext()),
            "\0",
            string.Empty);

        Assert.AreEqual(typeof(SearchMatch), descriptor.RowType);
        CollectionAssert.AreEqual(
            new[]
            {
                "Path", "PatternId", "MatchIndex", "ByteOffset", "ByteLength",
                "LineNumber", "Utf16Column", "Utf16Length", "MatchText", "Captures", "Context"
            },
            descriptor.Columns.Select(column => column.ColumnName).ToArray());

        var constructor = schema.GetRawConstructors(CreateMetadataContext())
            .Single(method => method.MethodName == "many");
        CollectionAssert.AreEqual(
            new[] { typeof(string), typeof(string) },
            constructor.ConstructorInfo.Arguments.Select(argument => argument.Type).ToArray());
    }

    [TestMethod]
    public void CompiledManyQuery_ShouldConsumeScalarArgumentsProducedByCte()
    {
        var root = CreateFixture(
            ("alpha.txt", "TODO TODO\n"),
            ("beta.txt", "TODO\n"));

        try
        {
            var escapedRoot = EscapeSql(root);
            var result = Compile(
                    $"with inputs as (" +
                    $"select '{escapedRoot}' as Root, '{TodoRequest}' as Request " +
                    $"from search.paths('{escapedRoot}') p take 1) " +
                    "select i.Root, m.Path, m.PatternId, m.MatchIndex " +
                    "from inputs i cross apply search.many(i.Root, i.Request) m " +
                    "order by m.Path, m.MatchIndex")
                .Run();

            Assert.AreEqual(3, result.Count);
            CollectionAssert.AreEqual(
                new[]
                {
                    ("alpha.txt", "todo", 0L),
                    ("alpha.txt", "todo", 1L),
                    ("beta.txt", "todo", 0L)
                },
                result.Rows.Select(row => ((string)row[1], (string)row[2], (long)row[3])).ToArray());
        }
        finally
        {
            DeleteFixture(root);
        }
    }

    [TestMethod]
    public void CteTake_ShouldBoundCorrelatedManyInvocation()
    {
        var root = CreateFixture(
            ("alpha.txt", "TODO TODO\n"),
            ("beta.txt", "TODO\n"));

        try
        {
            var escapedRoot = EscapeSql(root);
            var result = Compile(
                    $"with inputs as (" +
                    $"select '{escapedRoot}' as Root, '{TodoRequest}' as Request " +
                    $"from search.paths('{escapedRoot}') p take 1) " +
                    "select m.Path, m.MatchIndex " +
                    "from inputs i cross apply search.many(i.Root, i.Request) m " +
                    "order by m.Path, m.MatchIndex")
                .Run();

            Assert.AreEqual(3, result.Count);
            CollectionAssert.AreEqual(
                new[]
                {
                    ("alpha.txt", 0L),
                    ("alpha.txt", 1L),
                    ("beta.txt", 0L)
                },
                result.Rows.Select(row => ((string)row[0], (long)row[1])).ToArray());
        }
        finally
        {
            DeleteFixture(root);
        }
    }

    [TestMethod]
    public void CteUnionAll_ShouldPreserveDuplicatePathIdentity()
    {
        var root = CreateFixture(
            ("alpha.txt", "TODO TODO\n"),
            ("beta.txt", "TODO\n"));

        try
        {
            var escapedRoot = EscapeSql(root);
            var result = Compile(
                    $"with inputs as (" +
                    $"select p.Path as InputPath, '{escapedRoot}' as Root " +
                    $"from search.paths('{escapedRoot}') p " +
                    "union all (InputPath, Root) " +
                    $"select p.Path, '{escapedRoot}' from search.paths('{escapedRoot}') p) " +
                    $"select i.InputPath, m.Path, m.PatternId, m.MatchIndex " +
                    $"from inputs i cross apply search.many(i.Root, '{TodoRequest}') m " +
                    "where m.Path = i.InputPath " +
                    "order by i.InputPath, m.MatchIndex")
                .Run();

            CollectionAssert.AreEqual(
                new[]
                {
                    ("alpha.txt", "alpha.txt", "todo", 0L),
                    ("alpha.txt", "alpha.txt", "todo", 0L),
                    ("alpha.txt", "alpha.txt", "todo", 1L),
                    ("alpha.txt", "alpha.txt", "todo", 1L),
                    ("beta.txt", "beta.txt", "todo", 0L),
                    ("beta.txt", "beta.txt", "todo", 0L)
                },
                result.Rows.Select(row =>
                    ((string)row[0], (string)row[1], (string)row[2], (long)row[3])).ToArray());
        }
        finally
        {
            DeleteFixture(root);
        }
    }

    [TestMethod]
    public void CorrelatedCteRows_ShouldKeepPatternSettingsIsolated()
    {
        var root = CreateFixture(
            ("alpha.txt", "TODO\n"),
            ("beta.txt", "FIXME\n"));

        try
        {
            var escapedRoot = EscapeSql(root);
            var result = Compile(
                    $"with requests as (" +
                    $"select 'todo-run' as RequestId, '{escapedRoot}' as Root, '{TodoRequest}' as Request " +
                    $"from search.paths('{escapedRoot}') p where p.Path = 'alpha.txt' " +
                    "union all (RequestId, Root, Request) " +
                    $"select 'fixme-run', '{escapedRoot}', '{FixmeRequest}' " +
                    $"from search.paths('{escapedRoot}') p where p.Path = 'alpha.txt') " +
                    "select r.RequestId, m.Path, m.PatternId, m.MatchIndex " +
                    "from requests r cross apply search.many(r.Root, r.Request) m " +
                    "order by r.RequestId, m.Path")
                .Run();

            CollectionAssert.AreEqual(
                new[]
                {
                    ("fixme-run", "beta.txt", "fixme", 0L),
                    ("todo-run", "alpha.txt", "todo", 0L)
                },
                result.Rows.Select(row =>
                    ((string)row[0], (string)row[1], (string)row[2], (long)row[3])).ToArray());
        }
        finally
        {
            DeleteFixture(root);
        }
    }

    [TestMethod]
    public void TableValuedArguments_ShouldRemainUnsupportedAndPreserveScalarFallback()
    {
        var root = CreateFixture(("alpha.txt", "TODO\n"));

        try
        {
            var escapedRoot = EscapeSql(root);
            var exception = Assert.ThrowsException<QueryExecutionException>(() =>
                Compile(
                        $"with patterns as (select 'TODO' as Pattern from search.paths('{escapedRoot}') take 1) " +
                        $"select m.Path from search.many('{escapedRoot}', patterns) m")
                    .Run()
                    .Count);

            Assert.AreEqual("MQ7010_DataSourceOpenFailed", exception.Envelope!.Code.ToString());
            var lifecycle = exception.InnerException as DataSourceLifecycleException;
            Assert.IsNotNull(lifecycle);
            var requestException = lifecycle!.InnerException as SearchRequestException;
            Assert.IsNotNull(requestException);
            Assert.AreEqual("request", requestException!.Diagnostic.Location?.ArgumentName);

            var pathException = Assert.ThrowsException<QueryExecutionException>(() =>
                Compile(
                        $"with paths as (select p.Path as Path from search.paths('{escapedRoot}') p take 1) " +
                        $"select m.Path from search.many(paths, '{TodoRequest}') m")
                    .Run()
                    .Count);

            Assert.AreEqual("MQ7010_DataSourceOpenFailed", pathException.Envelope!.Code.ToString());
            var pathLifecycle = pathException.InnerException as DataSourceLifecycleException;
            Assert.IsNotNull(pathLifecycle);
            var rootException = pathLifecycle!.InnerException as SearchRequestException;
            Assert.IsNotNull(rootException);
            Assert.AreEqual("root", rootException!.Diagnostic.Location?.ArgumentName);
        }
        finally
        {
            DeleteFixture(root);
        }
    }

    private static CompiledQuery Compile(string query)
    {
        return InstanceCreatorHelpers.CompileForExecution(
            query,
            Guid.NewGuid().ToString(),
            new SearchSchemaProvider(),
            EnvironmentVariablesHelpers.CreateMockedEnvironmentVariables());
    }

    private static SourceMetadataContext CreateMetadataContext()
    {
        return new SourceMetadataContext(
            "search-tests",
            CancellationToken.None,
            [],
            new Dictionary<string, string>(),
            NullLogger.Instance);
    }

    private static string CreateFixture(params (string RelativePath, string Content)[] files)
    {
        var root = Path.Combine(Path.GetTempPath(), $"musoq-search-many-table-input-{Guid.NewGuid():N}");
        foreach (var file in files)
        {
            var path = Path.Combine(root, file.RelativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, file.Content);
        }

        return root;
    }

    private static string EscapeSql(string value)
    {
        return value.Replace("\\", "\\\\", StringComparison.Ordinal);
    }

    private static void DeleteFixture(string root)
    {
        if (Directory.Exists(root))
            Directory.Delete(root, recursive: true);
    }
}
