#nullable enable

using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Musoq.DataSources.Tests.Common;

using Musoq.DataSources.Search.Sources;

using Musoq.DataSources.Search.Components.Contracts;

using Musoq.DataSources.Search.Components.Diagnostics;

using Musoq.DataSources.Search.Components.Traversal;

namespace Musoq.DataSources.Search.Tests.Components.Traversal;

[TestClass]
public sealed class SearchLinkPolicyTests
{
    [TestMethod]
    public void DefaultPolicy_ShouldExcludeReparseEntriesAndBrokenLinks()
    {
        var root = CreateTemporaryRoot();
        var targetRoot = Path.Combine(root, "target");
        var linkedFile = Path.Combine(root, "linked.cs");
        var linkedDirectory = Path.Combine(root, "linked-target");
        var brokenFile = Path.Combine(root, "broken.cs");

        try
        {
            Directory.CreateDirectory(targetRoot);
            Write(root, "real.cs");
            Write(root, Path.Combine("target", "nested.cs"));

            RequireLinkSupport(
                TryCreateFileLink(linkedFile, Path.Combine(targetRoot, "nested.cs")) &&
                TryCreateDirectoryLink(linkedDirectory, targetRoot) &&
                TryCreateFileLink(brokenFile, Path.Combine(root, "missing.cs")));

            var actual = ReadPaths(root, new ScopePolicy());

            CollectionAssert.AreEquivalent(
                new[] { "real.cs", "target/nested.cs" },
                actual);
        }
        finally
        {
            DeleteLink(linkedFile);
            DeleteLink(linkedDirectory);
            DeleteLink(brokenFile);
            DeleteTemporaryRoot(root);
        }
    }

    [TestMethod]
    public void FollowPolicy_ShouldGuardCyclesAndDuplicateDirectoryIdentities()
    {
        var root = CreateTemporaryRoot();
        var targetRoot = Path.Combine(root, "target");
        var firstAlias = Path.Combine(root, "alias-a");
        var secondAlias = Path.Combine(root, "alias-b");
        var cycle = Path.Combine(targetRoot, "cycle");

        try
        {
            Directory.CreateDirectory(targetRoot);
            Write(root, Path.Combine("target", "nested.cs"));

            RequireLinkSupport(
                TryCreateDirectoryLink(firstAlias, targetRoot) &&
                TryCreateDirectoryLink(secondAlias, targetRoot) &&
                TryCreateDirectoryLink(cycle, root));

            var actual = ReadPaths(
                root,
                new ScopePolicy(followLinks: LinkTraversalPolicy.Follow));

            Assert.AreEqual(1, actual.Length);
            StringAssert.EndsWith(actual[0], "nested.cs");
        }
        finally
        {
            DeleteLink(firstAlias);
            DeleteLink(secondAlias);
            DeleteLink(cycle);
            DeleteTemporaryRoot(root);
        }
    }

    [TestMethod]
    public void FollowPolicy_ShouldRejectOutsideRootTargets()
    {
        var root = CreateTemporaryRoot();
        var outsideRoot = CreateTemporaryRoot();
        var outsideFile = Path.Combine(outsideRoot, "outside.cs");
        var linkedFile = Path.Combine(root, "outside.cs");

        try
        {
            Directory.CreateDirectory(root);
            Directory.CreateDirectory(outsideRoot);
            File.WriteAllText(outsideFile, "TODO");
            RequireLinkSupport(TryCreateFileLink(linkedFile, outsideFile));

            var exception = Assert.ThrowsException<SearchSourceAccessException>(
                () => ReadPaths(
                    root,
                    new ScopePolicy(followLinks: LinkTraversalPolicy.Follow)));

            Assert.AreEqual(SearchDiagnosticCodes.SourceOpenFailed, exception.Diagnostic.Code);
            Assert.AreEqual(linkedFile, exception.Diagnostic.Location?.Path);
        }
        finally
        {
            DeleteLink(linkedFile);
            DeleteTemporaryRoot(root);
            DeleteTemporaryRoot(outsideRoot);
        }
    }

    [TestMethod]
    public void HardLinks_ShouldRemainDistinctPathCandidates()
    {
        if (!OperatingSystem.IsWindows())
            Assert.Inconclusive("The hard-link fixture is only implemented for Windows in this test environment.");

        var root = CreateTemporaryRoot();
        var original = Path.Combine(root, "original.cs");
        var alias = Path.Combine(root, "alias.cs");

        try
        {
            Directory.CreateDirectory(root);
            File.WriteAllText(original, "TODO");
            RequireLinkSupport(TryCreateHardLink(alias, original));

            var actual = ReadPaths(root, new ScopePolicy());

            CollectionAssert.AreEquivalent(
                new[] { "original.cs", "alias.cs" },
                actual);
        }
        finally
        {
            DeleteTemporaryRoot(root);
        }
    }

    [TestMethod]
    public void PhysicalContainment_ShouldRespectUncShareBoundaries()
    {
        if (!OperatingSystem.IsWindows())
            Assert.Inconclusive("UNC path semantics are platform-specific.");

        Assert.IsTrue(
            SearchPathContainment.IsContained(
                @"\\server\share\root",
                @"\\server\share\root\child"));
        Assert.IsFalse(
            SearchPathContainment.IsContained(
                @"\\server\share\root",
                @"\\server\share\root2"));
        Assert.IsFalse(
            SearchPathContainment.IsContained(
                @"\\server\share\root",
                @"\\server\other\root\child"));
    }

    [TestMethod]
    public void SpecialEntries_ShouldBeExcludedWithoutASeparateSupportedMode()
    {
        var root = CreateTemporaryRoot();

        try
        {
            Directory.CreateDirectory(root);
            var matcher = new SearchScopeMatcher(
                root,
                new ScopePolicy(),
                default);
            var specialEntry = new SearchTraversalEntry(
                Path.Combine(root, "device"),
                IsDirectory: false,
                IsFile: true)
            {
                IsSpecial = true
            };

            Assert.AreEqual(SearchTraversalAction.Skip, matcher.Decide(specialEntry));

            var reparseEntry = new SearchTraversalEntry(
                Path.Combine(root, "link"),
                IsDirectory: true,
                IsFile: false)
            {
                IsReparsePoint = true
            };
            Assert.AreEqual(SearchTraversalAction.Skip, matcher.Decide(reparseEntry));
        }
        finally
        {
            DeleteTemporaryRoot(root);
        }
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

    private static bool TryCreateFileLink(string linkPath, string targetPath)
    {
        try
        {
            File.CreateSymbolicLink(linkPath, targetPath);
            return true;
        }
        catch (IOException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
        catch (PlatformNotSupportedException)
        {
            return false;
        }
        catch (NotSupportedException)
        {
            return false;
        }
    }

    private static bool TryCreateDirectoryLink(string linkPath, string targetPath)
    {
        try
        {
            Directory.CreateSymbolicLink(linkPath, targetPath);
            return true;
        }
        catch (IOException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
        catch (PlatformNotSupportedException)
        {
            return false;
        }
        catch (NotSupportedException)
        {
            return false;
        }
    }

    private static bool TryCreateHardLink(string linkPath, string existingFilePath)
    {
        return CreateHardLinkW(linkPath, existingFilePath, IntPtr.Zero);
    }

    private static void RequireLinkSupport(bool supported)
    {
        if (!supported)
        {
            Assert.Inconclusive(
                "The platform or test process does not have the privilege required for symbolic-link fixtures.");
        }
    }

    private static void DeleteLink(string path)
    {
        if (File.Exists(path))
        {
            File.Delete(path);
            return;
        }

        if (Directory.Exists(path))
        {
            Directory.Delete(path);
            return;
        }

        try
        {
            if (new FileInfo(path).LinkTarget is not null)
                File.Delete(path);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    private static string CreateTemporaryRoot()
    {
        return Path.Combine(Path.GetTempPath(), $"musoq-search-links-{Guid.NewGuid():N}");
    }

    private static void DeleteTemporaryRoot(string root)
    {
        if (Directory.Exists(root))
            Directory.Delete(root, recursive: true);
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CreateHardLinkW(
        string linkFileName,
        string existingFileName,
        IntPtr securityAttributes);
}
