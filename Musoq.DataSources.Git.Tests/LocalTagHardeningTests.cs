using LibGit2Sharp;
using Musoq.DataSources.Git.Entities;

namespace Musoq.DataSources.Git.Tests;

[TestClass]
public sealed class LocalTagHardeningTests
{
    [TestMethod]
    public void LibGit2ReaderUsesExactFriendlyAndFiniteCanonicalLookups()
    {
        using var fixture = OfflineGitFixture.Create();
        fixture.CreateLightweightTag("a");
        fixture.CreateLightweightTag("b");
        fixture.CreateLightweightTag("c");

        var projection = new GitProjection(true, [nameof(TagEntity.FriendlyName), nameof(TagEntity.CanonicalName)]);
        var exact = Read(
            new GitTagReadQuery("refs/tags/b", null, null, null, null, false, null),
            projection,
            fixture.SeedPath,
            GitOperationReaders.Tags);
        var finite = Read(
            new GitTagReadQuery(null, null, ["refs/tags/a", "refs/tags/c"], null, null, false, null),
            projection,
            fixture.SeedPath,
            GitOperationReaders.Tags);

        Assert.HasCount(1, exact);
        Assert.AreEqual("b", exact[0].FriendlyName);
        CollectionAssert.AreEquivalent(new[] { "a", "c" }, finite.Select(tag => tag.FriendlyName).ToArray());
    }

    [TestMethod]
    public void CliReaderKeepsCanonicalOrderAcrossLooseAndPackedRefsAndUnusualNames()
    {
        using var fixture = OfflineGitFixture.Create();
        fixture.CreateLightweightTag("zeta");
        fixture.CreateLightweightTag("alpha");
        fixture.CreateAnnotatedTag("unicode-Ω", "unicode message");
        fixture.CreateLightweightTag("space-name");
        OfflineGitFixture.RunGit(fixture.SeedPath, "pack-refs", "--all", "--prune");

        var records = Read(
            GitTagReadQuery.Empty,
            new GitProjection(true, [nameof(TagEntity.FriendlyName), nameof(TagEntity.CanonicalName)]),
            fixture.SeedPath,
            GitOperationReaders.CliTags);

        CollectionAssert.AreEqual(
            records.Select(record => record.CanonicalName).OrderBy(name => name, StringComparer.Ordinal).ToArray(),
            records.Select(record => record.CanonicalName).ToArray());
        Assert.IsTrue(records.Any(record => record.FriendlyName == "unicode-Ω"));
    }

    [TestMethod]
    public void LibGit2AndCliAgreeOnDereferencedTargetShaForLightweightAndAnnotatedTags()
    {
        using var fixture = OfflineGitFixture.Create();
        fixture.CreateLightweightTag("lightweight");
        fixture.CreateAnnotatedTag("annotated", "annotated message");
        var headSha = OfflineGitFixture.RunGit(fixture.SeedPath, "rev-parse", "HEAD").Trim();
        var projection = new GitProjection(true, [nameof(TagEntity.TargetSha)]);

        var cli = Read(GitTagReadQuery.Empty, projection, fixture.SeedPath, GitOperationReaders.CliTags);
        var libGit2 = Read(GitTagReadQuery.Empty, projection, fixture.SeedPath, GitOperationReaders.Tags);

        Assert.IsTrue(cli.All(tag => tag.TargetSha == headSha));
        Assert.IsTrue(libGit2.All(tag => tag.TargetSha == headSha));
    }

    private static List<GitTagRecord> Read(
        GitTagReadQuery query,
        GitProjection projection,
        string path,
        IGitTagReader reader)
    {
        var records = new List<GitTagRecord>();
        reader.Read(
            path,
            GitReferenceBackendOptions.Default,
            projection,
            query,
            static repositoryPath => new Repository(repositoryPath),
            CancellationToken.None,
            record =>
            {
                records.Add(record);
                return true;
            });
        return records;
    }
}
