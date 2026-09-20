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
using Musoq.Schema.Optimization;

using Musoq.DataSources.Search;
using Musoq.DataSources.Search.Entities;

using Musoq.DataSources.Search.Tests.Infrastructure;

namespace Musoq.DataSources.Search.Tests.Sources.Many;

[TestClass]
public sealed class SearchManyTableInputTests
{
    [TestMethod]
    public void Metadata_ShouldDescribeTypedManyWithoutRuntimeValues()
    {
        var schema = new SearchSchema();
        var descriptor = schema.DescribeSource(
            "many",
            new SourceDescribeContext(
                new SourceIdentity("search", "many", "search-tests", "metadata-only"),
                CreateMetadataContext()),
            "fixture",
            Array.Empty<SearchPatternInput>());

        Assert.AreEqual(typeof(SearchMatch), descriptor.RowType);
        CollectionAssert.AreEqual(
            new[]
            {
                "Path", "PatternId", "MatchIndex", "ByteOffset", "ByteLength",
                "LineNumber", "Utf16Column", "Utf16Length", "MatchText", "Captures", "Context"
            },
            descriptor.Columns.Select(column => column.ColumnName).ToArray());

        var constructors = schema.GetRawConstructors(CreateMetadataContext())
            .Where(method => method.MethodName == "many")
            .ToArray();
        Assert.AreEqual(2, constructors.Length);
        CollectionAssert.AreEquivalent(
            new[]
            {
                typeof(string),
                typeof(IReadOnlyList<SearchPatternInput>)
            },
            constructors[0].ConstructorInfo.Arguments.Select(argument => argument.Type).ToArray());
        CollectionAssert.AreEquivalent(
            new[]
            {
                typeof(string),
                typeof(IReadOnlyList<SearchPatternInput>),
                typeof(SearchManyOptionsInput)
            },
            constructors[1].ConstructorInfo.Arguments.Select(argument => argument.Type).ToArray());
    }

    [TestMethod]
    public void CompiledManyQuery_ShouldConsumeACollectionCte()
    {
        var root = CreateFixture(
            ("alpha.txt", "TODO TODO\n"),
            ("beta.txt", "TODO\n"));

        try
        {
            var escapedRoot = EscapeSql(root);
            var result = Compile(
                    $"with patterns as (" +
                    $"select 'todo' as Id, 'TODO' as Pattern, 'literal' as Mode " +
                    $"from search.paths('{escapedRoot}') p take 1) " +
                    $"select m.Path, m.PatternId, m.MatchIndex " +
                    $"from search.many('{escapedRoot}', patterns) m " +
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
                result.Rows.Select(row => ((string)row[0], (string)row[1], (long)row[2])).ToArray());
        }
        finally
        {
            DeleteFixture(root);
        }
    }

    [TestMethod]
    public void CollectionCte_ShouldRemainReusableAcrossCorrelatedRows()
    {
        var root = CreateFixture(
            ("alpha.txt", "TODO TODO\n"),
            ("beta.txt", "TODO\n"));

        try
        {
            var escapedRoot = EscapeSql(root);
            var result = Compile(
                    $"with patterns as (" +
                    $"select 'todo' as Id, 'TODO' as Pattern, 'literal' as Mode " +
                    $"from search.paths('{escapedRoot}') p take 1), " +
                    "inputs as (" +
                    $"select p.Path as InputPath, '{escapedRoot}' as Root " +
                    $"from search.paths('{escapedRoot}') p " +
                    "union all (InputPath, Root) " +
                    $"select p.Path, '{escapedRoot}' from search.paths('{escapedRoot}') p) " +
                    "select i.InputPath, m.Path, m.PatternId, m.MatchIndex " +
                    "from inputs i cross apply search.many(i.Root, patterns) m " +
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
    public void TableValuedPatternCte_ShouldBeAcceptedByTypedCoreApi()
    {
        var root = CreateFixture(("alpha.txt", "TODO\n"));

        try
        {
            var escapedRoot = EscapeSql(root);
            var result = Compile(
                    $"with patterns as (" +
                    $"select 'todo' as Id, 'TODO' as Pattern, 'literal' as Mode " +
                    $"from search.paths('{escapedRoot}') p take 1) " +
                    $"select m.Path, m.PatternId from search.many('{escapedRoot}', patterns) m")
                .Run();

            Assert.AreEqual(1, result.Count);
            Assert.AreEqual("alpha.txt", result.Rows[0][0]);
            Assert.AreEqual("todo", result.Rows[0][1]);
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
