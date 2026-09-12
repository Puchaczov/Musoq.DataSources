namespace Musoq.DataSources.Git.Tests;

[TestClass]
public sealed class OfflineGitFixtureTests
{
    [TestMethod]
    public void LocalBareRemote_IsReadableWithoutFetchedClientObjects()
    {
        using var fixture = OfflineGitFixture.Create();
        fixture.CreateLightweightTag("offline-lightweight");
        fixture.CreateAnnotatedTag("offline-annotated", "offline tag");

        var refs = OfflineGitFixture.RunGit(
            fixture.ClientPath,
            "ls-remote",
            "--tags",
            "--refs",
            fixture.RemoteUrl);

        StringAssert.Contains(refs, "refs/tags/offline-lightweight");
        StringAssert.Contains(refs, "refs/tags/offline-annotated");

        var clientRefs = OfflineGitFixture.RunGit(fixture.ClientPath, "for-each-ref", "--format=%(refname)");
        Assert.AreEqual(string.Empty, clientRefs);

        var objects = OfflineGitFixture.RunGit(fixture.ClientPath, "count-objects", "-v");
        StringAssert.Contains(objects, "count: 0");
    }

    [TestMethod]
    public void FixtureRemoteUrl_IsLocal()
    {
        using var fixture = OfflineGitFixture.Create();

        Assert.IsTrue(
            Uri.TryCreate(fixture.RemoteUrl, UriKind.Absolute, out var uri) &&
            uri.Scheme.Equals(Uri.UriSchemeFile, StringComparison.OrdinalIgnoreCase));
    }

    [TestMethod]
    public void FixtureProvidesMultipleLocalRemotesAndOfflineStashes()
    {
        using var fixture = OfflineGitFixture.Create();
        fixture.CreateStash("ordinary stash");
        fixture.CreateStash("untracked stash", includeUntrackedFiles: true);

        var remotes = OfflineGitFixture.RunGit(fixture.ClientPath, "remote").Split(
            new[] { '\r', '\n' },
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        CollectionAssert.AreEquivalent(new[] { "backup", "origin" }, remotes);

        var stashLog = OfflineGitFixture.RunGit(fixture.SeedPath, "log", "-g", "--format=%gs", "refs/stash");
        StringAssert.Contains(stashLog, "untracked stash");
        StringAssert.Contains(stashLog, "ordinary stash");
    }
}
