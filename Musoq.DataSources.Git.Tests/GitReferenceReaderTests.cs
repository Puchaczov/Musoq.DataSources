using LibGit2Sharp;
using Musoq.DataSources.Git.Entities;

namespace Musoq.DataSources.Git.Tests;

[TestClass]
public sealed class GitReferenceReaderTests
{
    [TestMethod]
    public void CliTags_UsesIdentityProtocolWhenOnlyNamesAreProjected()
    {
        using var fixture = OfflineGitFixture.Create();
        fixture.CreateLightweightTag("lightweight");
        fixture.CreateAnnotatedTag("annotated", "annotated message");

        var records = Read(fixture.SeedPath, nameof(TagEntity.FriendlyName), nameof(TagEntity.CanonicalName));

        Assert.HasCount(2, records);
        Assert.IsTrue(records.All(record => record.TargetSha is null));
        Assert.IsTrue(records.All(record => record.Annotation is null));
        Assert.IsTrue(records.All(record => record.Message is null));
        Assert.IsTrue(records.All(record => !record.IsAnnotated));
    }

    [TestMethod]
    public void CliTags_UsesTypeProtocolForAnnotatedPredicateColumnsWithoutPeeling()
    {
        using var fixture = OfflineGitFixture.Create();
        fixture.CreateLightweightTag("lightweight");
        fixture.CreateAnnotatedTag("annotated", "annotated message");

        var records = Read(fixture.SeedPath, nameof(TagEntity.IsAnnotated));

        Assert.IsFalse(records.Single(record => record.FriendlyName == "lightweight").IsAnnotated);
        var annotated = records.Single(record => record.FriendlyName == "annotated");
        Assert.IsTrue(annotated.IsAnnotated);
        Assert.IsNull(annotated.TargetSha);
        Assert.IsNotNull(annotated.Annotation);
    }

    [TestMethod]
    public void CliTags_UsesPeeledTargetOnlyWhenTargetIsProjected()
    {
        using var fixture = OfflineGitFixture.Create();
        fixture.CreateLightweightTag("lightweight");
        fixture.CreateAnnotatedTag("annotated", "annotated message");
        var headSha = OfflineGitFixture.RunGit(fixture.SeedPath, "rev-parse", "HEAD").Trim();

        var records = Read(fixture.SeedPath, nameof(TagEntity.TargetSha));

        Assert.IsTrue(
            records.All(record => record.TargetSha == headSha),
            $"Expected {headSha}; got {string.Join(", ", records.Select(record => $"{record.FriendlyName}={record.TargetSha}"))}");
        Assert.IsNotNull(records.Single(record => record.FriendlyName == "annotated").Annotation);
        Assert.IsNull(records.Single(record => record.FriendlyName == "lightweight").Annotation);
    }

    [TestMethod]
    public void CliTags_UsesMetadataProtocolWhenAnnotationIsProjected()
    {
        using var fixture = OfflineGitFixture.Create();
        fixture.CreateLightweightTag("lightweight");
        fixture.CreateAnnotatedTag("annotated", "annotated message");

        var records = Read(fixture.SeedPath, nameof(TagEntity.Annotation));

        Assert.IsNull(records.Single(record => record.FriendlyName == "lightweight").Annotation);
        var annotated = records.Single(record => record.FriendlyName == "annotated");
        Assert.IsNotNull(annotated.Annotation);
        Assert.AreEqual("annotated message\n", annotated.Message);
    }

    [TestMethod]
    public void RemoteTagParser_StreamsCapturedAdvertisementFixtures()
    {
        var lines = new[]
        {
            "1111111\trefs/tags/a-lightweight",
            "2222222\trefs/tags/z-annotated",
            "3333333\trefs/tags/z-annotated^{}",
            "4444444\trefs/heads/main"
        };
        var skippedByCursor = 0;
        var query = new GitRemoteTagReadQuery(
            null,
            null,
            null,
            null,
            "refs/tags/a-lightweight",
            false,
            () => skippedByCursor++);

        var records = GitRemoteTagProtocolParser.Parse(
                lines,
                "client",
                "origin",
                "file:///remote.git",
                needsPeel: true,
                query,
                CancellationToken.None)
            .ToArray();

        Assert.HasCount(1, records);
        Assert.AreEqual("z-annotated", records[0].FriendlyName);
        Assert.AreEqual("2222222", records[0].ObjectSha);
        Assert.AreEqual("3333333", records[0].PeeledSha);
        Assert.IsTrue(records[0].IsAnnotated);
        Assert.AreEqual(1, skippedByCursor);
    }

    [TestMethod]
    public void RemoteTagParser_DoesNotRequireACompleteInputSequence()
    {
        var produced = 0;

        IEnumerable<string> Lines()
        {
            for (var index = 0; index < 1_000_000; index++)
            {
                produced++;
                yield return $"{index:x8}\trefs/tags/tag-{index:D7}";
            }
        }

        var records = GitRemoteTagProtocolParser.Parse(
                Lines(),
                "client",
                "origin",
                "file:///remote.git",
                needsPeel: false,
                GitRemoteTagReadQuery.Empty,
                CancellationToken.None)
            .Take(3)
            .ToArray();

        Assert.HasCount(3, records);
        Assert.IsTrue(produced < 10, $"Parser consumed {produced} rows for a three-row window.");
    }

    [TestMethod]
    public void RemoteTagParser_IgnoresMalformedBranchesAndUnmatchedPeelRecordsWithoutRetainingInput()
    {
        var lines = new[]
        {
            "bad line",
            "1111111\trefs/heads/not-a-tag",
            "2222222\trefs/tags/a",
            "2222222\trefs/tags/a",
            "3333333\trefs/tags/orphan^{}",
            "4444444\trefs/tags/b^{}",
            "5555555\trefs/tags/c",
            "6666666\trefs/tags/d",
            "7777777\trefs/tags/c^{}",
            "8888888\trefs/tags/c^{}",
            "9999999\trefs/tags/e",
            "aaaaaaaa\trefs/tags/e^{}"
        };

        var records = GitRemoteTagProtocolParser.Parse(
                lines,
                "client",
                "origin",
                "file:///offline/remote.git",
                needsPeel: true,
                GitRemoteTagReadQuery.Empty,
                CancellationToken.None)
            .ToArray();

        CollectionAssert.AreEqual(new[] { "a", "c", "d", "e" }, records.Select(record => record.FriendlyName).ToArray());
        Assert.IsFalse(records.Any(record => record.CanonicalName.Contains("heads", StringComparison.Ordinal)));
        Assert.IsTrue(records.Single(record => record.FriendlyName == "e").IsAnnotated);
    }

    private static List<GitTagRecord> Read(string path, params string[] columns)
    {
        var records = new List<GitTagRecord>();
        GitOperationReaders.CliTags.Read(
            path,
            GitReferenceBackendOptions.Default,
            new GitProjection(true, columns),
            GitTagReadQuery.Empty,
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
