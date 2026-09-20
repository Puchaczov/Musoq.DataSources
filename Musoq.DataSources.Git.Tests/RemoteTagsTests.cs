using LibGit2Sharp;
using Musoq.DataSources.Git.Entities;
using Musoq.DataSources.Git.Tests.Components;
using Musoq.DataSources.Tests.Common;
using Musoq.Evaluator;
using Musoq.Schema.Optimization;

namespace Musoq.DataSources.Git.Tests;

[TestClass]
public sealed class RemoteTagsTests
{
    [TestMethod]
    public void RemoteTags_ReadsLocalBareRemoteWithoutFetching()
    {
        using var fixture = OfflineGitFixture.Create();
        fixture.CreateLightweightTag("offline-lightweight");
        fixture.CreateAnnotatedTag("offline-annotated", "offline tag");
        var beforeRefs = OfflineGitFixture.RunGit(fixture.ClientPath, "for-each-ref", "--format=%(refname)");
        var beforeObjects = OfflineGitFixture.RunGit(fixture.ClientPath, "count-objects", "-v");

        var query = $"select t.RemoteName, t.RemoteUrl, t.FriendlyName, t.CanonicalName, t.ObjectSha, t.PeeledSha, t.IsAnnotated " +
                    $"from git.remotetags('{fixture.ClientPath.Escape()}', 'origin') t";
        var result = Compile(query).Run();

        Assert.AreEqual(2, result.Count);
        var lightweight = result.Single(row => (string)row[2] == "offline-lightweight");
        var annotated = result.Single(row => (string)row[2] == "offline-annotated");
        Assert.AreEqual("origin", (string)lightweight[0]);
        Assert.AreEqual(fixture.RemotePath, (string)lightweight[1]);
        Assert.AreEqual("refs/tags/offline-lightweight", (string)lightweight[3]);
        Assert.IsFalse((bool)lightweight[6]);
        Assert.IsNull(lightweight[5]);
        Assert.IsTrue((bool)annotated[6]);
        Assert.IsFalse(string.IsNullOrWhiteSpace((string)annotated[4]));
        Assert.IsFalse(string.IsNullOrWhiteSpace((string)annotated[5]));
        Assert.AreNotEqual((string)annotated[4], (string)annotated[5]);

        var annotatedPeelQuery = $"select t.PeeledSha from git.remotetags('{fixture.ClientPath.Escape()}', 'origin') t " +
                                 "where t.FriendlyName = 'offline-annotated' and t.IsAnnotated = true";
        var annotatedPeel = Compile(annotatedPeelQuery).Run();
        Assert.AreEqual(1, annotatedPeel.Count);
        Assert.IsFalse(string.IsNullOrWhiteSpace((string)annotatedPeel[0][0]));

        var afterRefs = OfflineGitFixture.RunGit(fixture.ClientPath, "for-each-ref", "--format=%(refname)");
        var afterObjects = OfflineGitFixture.RunGit(fixture.ClientPath, "count-objects", "-v");
        Assert.AreEqual(beforeRefs, afterRefs);
        Assert.AreEqual(beforeObjects, afterObjects);
    }

    [TestMethod]
    public void RemoteTags_CursorAndExactNamePredicatesAreApplied()
    {
        using var fixture = OfflineGitFixture.Create();
        fixture.CreateLightweightTag("a");
        fixture.CreateLightweightTag("b");
        fixture.CreateLightweightTag("c");

        var query = $"select t.FriendlyName from git.remotetags('{fixture.ClientPath.Escape()}', 'origin') t " +
                    "where t.CanonicalName > 'refs/tags/a'";
        var result = Compile(query).Run();

        CollectionAssert.AreEquivalent(new[] { "b", "c" }, result.Select(row => (string)row[0]).ToArray());

        var exactQuery = $"select t.FriendlyName from git.remotetags('{fixture.ClientPath.Escape()}', 'origin') t " +
                         "where t.FriendlyName = 'b'";
        var exact = Compile(exactQuery).Run();
        Assert.AreEqual(1, exact.Count);
        Assert.AreEqual("b", (string)exact[0][0]);

        var peelQuery = $"select t.PeeledSha from git.remotetags('{fixture.ClientPath.Escape()}', 'origin') t " +
                        "where t.FriendlyName = 'b' and t.IsAnnotated = false";
        var peel = Compile(peelQuery).Run();
        Assert.AreEqual(1, peel.Count);
        Assert.IsNull(peel[0][0]);
    }

    [TestMethod]
    public void RemoteTags_FiniteInPredicateRemainsStreamingAndExact()
    {
        using var fixture = OfflineGitFixture.Create();
        fixture.CreateLightweightTag("a");
        fixture.CreateLightweightTag("b");
        fixture.CreateLightweightTag("c");

        var query = $"select t.FriendlyName from git.remotetags('{fixture.ClientPath.Escape()}', 'origin') t " +
                    "where t.FriendlyName in ('a', 'c')";
        var result = Compile(query).Run();

        CollectionAssert.AreEquivalent(new[] { "a", "c" }, result.Select(row => (string)row[0]).ToArray());
    }

    [TestMethod]
    public void RemoteTags_MissingRemoteReportsTheRemoteName()
    {
        using var fixture = OfflineGitFixture.Create();

        var exception = Assert.ThrowsException<InvalidOperationException>(() =>
            GitOperationReaders.RemoteTags.Read(
                fixture.ClientPath,
                "missing",
                GitReferenceBackendOptions.Default,
                GitProjection.NotAccepted,
                new GitRemoteTagReadQuery(null, null, null, null, null, false, null),
                static path => new Repository(path),
                CancellationToken.None,
                static _ => true));

        StringAssert.Contains(exception.Message, "missing");
    }

    [TestMethod]
    public void RemoteTags_SelectsTheRequestedLocalRemote()
    {
        using var fixture = OfflineGitFixture.Create();
        fixture.CreateLightweightTag("backup-tag");

        var query = $"select t.RemoteName, t.RemoteUrl, t.FriendlyName from git.remotetags('{fixture.ClientPath.Escape()}', 'backup') t";
        var result = Compile(query).Run();

        var tag = result.Single(row => (string)row[2] == "backup-tag");
        Assert.AreEqual("backup", (string)tag[0]);
        Assert.AreEqual(fixture.BackupRemotePath, (string)tag[1]);
    }

    [TestMethod]
    public void RemoteTags_AcceptsAFileUriAndDoesNotMutateTheClient()
    {
        using var fixture = OfflineGitFixture.Create();
        fixture.CreateLightweightTag("file-uri-tag");
        OfflineGitFixture.RunGit(fixture.ClientPath, "remote", "set-url", "origin", fixture.RemoteUrl);
        var beforeRefs = OfflineGitFixture.RunGit(fixture.ClientPath, "for-each-ref", "--format=%(refname)");
        var beforeObjects = OfflineGitFixture.RunGit(fixture.ClientPath, "count-objects", "-v");

        var query = $"select t.FriendlyName from git.remotetags('{fixture.ClientPath.Escape()}', 'origin') t";
        var result = Compile(query).Run();

        Assert.AreEqual("file-uri-tag", (string)result.Single()[0]);
        Assert.AreEqual(beforeRefs, OfflineGitFixture.RunGit(fixture.ClientPath, "for-each-ref", "--format=%(refname)"));
        Assert.AreEqual(beforeObjects, OfflineGitFixture.RunGit(fixture.ClientPath, "count-objects", "-v"));
    }

    [TestMethod]
    public void RemoteTags_InvalidLocalPathFailsWithoutFallingBackToRepositoryObjects()
    {
        using var fixture = OfflineGitFixture.Create();
        var missingPath = Path.Combine(fixture.Root, "missing-remote.git");
        OfflineGitFixture.RunGit(fixture.ClientPath, "remote", "set-url", "origin", missingPath);

        var exception = Assert.ThrowsException<InvalidOperationException>(() =>
            GitOperationReaders.RemoteTags.Read(
                fixture.ClientPath,
                "origin",
                GitReferenceBackendOptions.Default,
                new GitProjection(true, [nameof(RemoteTagEntity.FriendlyName)]),
                GitRemoteTagReadQuery.Empty,
                static path => new Repository(path),
                CancellationToken.None,
                static _ => true));

        StringAssert.Contains(exception.Message, "Git exited with code");
    }

    [TestMethod]
    public void RemoteTagsReportsCliUnavailableClearly()
    {
        using var fixture = OfflineGitFixture.Create();
        var options = GitReferenceBackendOptions.From(new Dictionary<string, string>
        {
            [GitReferenceBackendOptions.ExecutableSettingName] = "git-executable-that-does-not-exist"
        });

        Assert.ThrowsException<GitCliUnavailableException>(() =>
            GitOperationReaders.RemoteTags.Read(
                fixture.ClientPath,
                "origin",
                options,
                new GitProjection(true, [nameof(RemoteTagEntity.FriendlyName)]),
                GitRemoteTagReadQuery.Empty,
                static path => new Repository(path),
                CancellationToken.None,
                static _ => true));
    }

    [TestMethod]
    public void RemoteTagsTakeZeroDoesNotStartTheConfiguredGitExecutable()
    {
        using var fixture = OfflineGitFixture.Create();
        var context = RuntimeV2TestContexts.CreateExecutionContext(
            executionPlan: new SourceExecutionPlan
            {
                Identity = new SourceIdentity("git", "git", "git", "remotetags"),
                AcceptedTake = 0
            },
            sourceRuntimeSettings: new Dictionary<string, string>
            {
                [GitReferenceBackendOptions.ExecutableSettingName] = "git-executable-that-does-not-exist"
            });

        var rows = new RemoteTagsRowsSource(
                fixture.ClientPath,
                "origin",
                static path => new Repository(path),
                context)
            .Chunks.SelectMany(static chunk => chunk);

        Assert.IsEmpty(rows);
    }

    [TestMethod]
    public void RemoteTags_RejectsLibGit2ReferenceBackend()
    {
        using var fixture = OfflineGitFixture.Create();
        var context = RuntimeV2TestContexts.CreateExecutionContext(
            sourceRuntimeSettings: new Dictionary<string, string>
            {
                [GitReferenceBackendOptions.BackendSettingName] = "libgit2"
            });

        var exception = Assert.ThrowsException<InvalidOperationException>(() =>
            new RemoteTagsRowsSource(fixture.ClientPath, "origin", static path => new Repository(path), context));

        StringAssert.Contains(exception.Message, "git-cli");
    }

    private static CompiledQuery Compile(string query)
    {
        return InstanceCreatorHelpers.CompileForExecution(
            query,
            Guid.NewGuid().ToString(),
            new GitSchemaProvider(),
            EnvironmentVariablesHelpers.CreateMockedEnvironmentVariables());
    }
}
