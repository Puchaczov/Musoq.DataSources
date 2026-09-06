#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Musoq.DataSources.Tests.Common;

using Musoq.DataSources.Search.Sources;

using Musoq.DataSources.Search.Components.Contracts;

namespace Musoq.DataSources.Search.Tests.Components.Traversal;

[TestClass]
public sealed class SearchScopeTests
{
    [TestMethod]
    public void RepositoryIgnores_ShouldApplyNestedNegationAndPruneIgnoredParents()
    {
        var root = CreateTemporaryRoot();

        try
        {
            Directory.CreateDirectory(Path.Combine(root, "src"));
            Directory.CreateDirectory(Path.Combine(root, "build"));
            Write(root, ".gitignore", "*.tmp\nbuild/\n");
            Write(root, Path.Combine("src", ".ignore"), "*.generated.cs\n!keep.generated.cs\n");
            Write(root, Path.Combine("src", "a.cs"));
            Write(root, Path.Combine("src", "drop.generated.cs"));
            Write(root, Path.Combine("src", "keep.generated.cs"));
            Write(root, Path.Combine("src", "cache.tmp"));
            Write(root, Path.Combine("build", ".ignore"), "!keep.cs\n");
            Write(root, Path.Combine("build", "drop.cs"));
            Write(root, Path.Combine("build", "keep.cs"));

            var actual = ReadPaths(root, new ScopePolicy());

            CollectionAssert.AreEquivalent(
                new[] { "src/a.cs", "src/keep.generated.cs" },
                actual);
        }
        finally
        {
            DeleteTemporaryRoot(root);
        }
    }

    [TestMethod]
    public void IgnoreRules_ShouldHonorAnchorsDirectoryOnlyEscapesAndRuleOrder()
    {
        var root = CreateTemporaryRoot();

        try
        {
            Directory.CreateDirectory(Path.Combine(root, "nested"));
            Directory.CreateDirectory(Path.Combine(root, "cache"));
            Write(
                root,
                ".gitignore",
                "nested/reincluded.tmp\n!nested/reincluded.tmp\n/root-only.tmp\ncache/\n\\#literal.txt\n\\!literal.txt\n");
            Write(root, "root-only.tmp");
            Write(root, Path.Combine("nested", "root-only.tmp"));
            Write(root, Path.Combine("nested", "reincluded.tmp"));
            Write(root, Path.Combine("cache", "deep.txt"));
            Write(root, "#literal.txt");
            Write(root, "!literal.txt");

            var actual = ReadPaths(root, new ScopePolicy());

            CollectionAssert.AreEquivalent(
                new[] { "nested/root-only.tmp", "nested/reincluded.tmp" },
                actual,
                $"Actual paths: {string.Join(",", actual)}");
        }
        finally
        {
            DeleteTemporaryRoot(root);
        }
    }

    [TestMethod]
    public void IncludeAndExcludeGlobs_ShouldNotPruneAnUnmatchedDirectory()
    {
        var root = CreateTemporaryRoot();

        try
        {
            Directory.CreateDirectory(Path.Combine(root, "docs"));
            Directory.CreateDirectory(Path.Combine(root, "docs.cs"));
            Directory.CreateDirectory(Path.Combine(root, "generated"));
            Write(root, "keep.cs");
            Write(root, "data.txt");
            Write(root, Path.Combine("docs", "example.cs"));
            Write(root, Path.Combine("docs.cs", "example.txt"));
            Write(root, Path.Combine("generated", "example.cs"));

            var actual = ReadPaths(
                root,
                new ScopePolicy(
                    include: ["*.cs"],
                    exclude: ["generated/"]));

            CollectionAssert.AreEquivalent(
                new[] { "keep.cs", "docs/example.cs" },
                actual);
        }
        finally
        {
            DeleteTemporaryRoot(root);
        }
    }

    [TestMethod]
    public void IgnoreSources_ShouldHonorPrecedenceGlobalOptInAndDisablement()
    {
        var root = CreateTemporaryRoot();

        try
        {
            Write(root, ".gitignore", "*.txt\n!low.txt\n");
            Write(root, ".ignore", "!override.txt\n");
            Write(root, ".rgignore", "highest.txt\n");
            Write(root, "low.txt");
            Write(root, "override.txt");
            Write(root, "highest.txt");
            Write(root, "global.bin");

            var defaultActual = ReadPaths(
                root,
                new ScopePolicy(
                    globalIgnores: GlobalIgnorePolicy.Configured,
                    globalIgnoreRules: ["global.bin"]));
            CollectionAssert.AreEquivalent(
                new[] { "low.txt", "override.txt" },
                defaultActual);

            var disabledActual = ReadPaths(
                root,
                new ScopePolicy(
                    repositoryIgnores: RepositoryIgnorePolicy.Disabled,
                    globalIgnores: GlobalIgnorePolicy.Configured,
                    globalIgnoreRules: ["global.bin"]));
            CollectionAssert.AreEquivalent(
                new[] { "highest.txt", "low.txt", "override.txt" },
                disabledActual,
                $"Actual paths: {string.Join(",", disabledActual)}");
        }
        finally
        {
            DeleteTemporaryRoot(root);
        }
    }

    [TestMethod]
    public void ExplicitFileRoot_ShouldOverrideRepositoryIgnoreRules()
    {
        var root = CreateTemporaryRoot();
        var file = Path.Combine(root, "ignored.cs");

        try
        {
            Directory.CreateDirectory(root);
            Write(root, ".gitignore", "*.cs\n");
            Write(root, "ignored.cs");

            var actual = ReadPaths(file, new ScopePolicy());

            CollectionAssert.AreEquivalent(new[] { "ignored.cs" }, actual);
        }
        finally
        {
            DeleteTemporaryRoot(root);
        }
    }

    [TestMethod]
    public void GlobalIgnoreRules_ShouldBeCopiedAndFrozen()
    {
        var rules = new[] { "*.global" };
        var policy = new ScopePolicy(
            globalIgnores: GlobalIgnorePolicy.Configured,
            globalIgnoreRules: rules);
        rules[0] = "*.changed";

        Assert.AreEqual("*.global", policy.GlobalIgnoreRules[0]);
        var readOnly = (IList<string>)policy.GlobalIgnoreRules;
        Assert.ThrowsException<NotSupportedException>(() => readOnly[0] = "*.changed");
    }

    private static string[] ReadPaths(string root, ScopePolicy policy)
    {
        return new SearchMatchesSource(
                SearchRequest.Create(root, "TODO", policy),
                RuntimeV2TestContexts.CreateExecutionContext())
            .Chunks
            .SelectMany(static chunk => chunk)
            .Select(static row => row.Path)
            .ToArray();
    }

    private static void Write(string root, string relativePath, string content = "TODO")
    {
        var path = Path.Combine(root, relativePath);
        var parent = Path.GetDirectoryName(path);
        if (parent is not null)
            Directory.CreateDirectory(parent);

        File.WriteAllText(path, content);
    }

    private static string CreateTemporaryRoot()
    {
        return Path.Combine(Path.GetTempPath(), $"musoq-search-scope-{Guid.NewGuid():N}");
    }

    private static void DeleteTemporaryRoot(string root)
    {
        if (Directory.Exists(root))
            Directory.Delete(root, recursive: true);
    }
}
