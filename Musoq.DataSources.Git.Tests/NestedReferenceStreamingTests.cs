using LibGit2Sharp;
using Musoq.DataSources.Git.Entities;
using Musoq.DataSources.Tests.Common;

namespace Musoq.DataSources.Git.Tests;

[TestClass]
public sealed class NestedReferenceStreamingTests
{
    [TestMethod]
    public void NestedTagsStreamRepeatedlyAndLoadRichPropertiesOnDemand()
    {
        using var fixture = OfflineGitFixture.Create();
        fixture.CreateLightweightTag("nested-lightweight");
        fixture.CreateAnnotatedTag("nested-annotated", "nested annotation");

        var repository = ReadRepository(fixture.SeedPath);
        var tags = repository.Tags;

        Assert.AreEqual(2, tags.Count());
        Assert.AreEqual(2, tags.Count());

        var annotated = tags.Single(tag => tag.FriendlyName == "nested-annotated");
        Assert.IsTrue(annotated.IsAnnotated);
        Assert.AreEqual("nested annotation\n", annotated.Message);
        Assert.IsNotNull(annotated.Annotation);
        Assert.IsNotNull(annotated.TargetSha);
        Assert.IsNotNull(annotated.Commit);
    }

    [TestMethod]
    public void NestedStashesUseTheConfiguredLibGit2FallbackAndRemainRepeatable()
    {
        using var fixture = OfflineGitFixture.Create();
        fixture.CreateStash("nested stash");
        fixture.CreateStash("nested untracked", includeUntrackedFiles: true);

        var repository = ReadRepository(
            fixture.SeedPath,
            new Dictionary<string, string>
            {
                [GitReferenceBackendOptions.BackendSettingName] = "libgit2",
                [GitReferenceBackendOptions.ExecutableSettingName] = "missing-git-for-nested-test"
            });

        var stashes = repository.Stashes;
        Assert.AreEqual(2, stashes.Count());
        Assert.AreEqual(2, stashes.Count());
        Assert.IsTrue(stashes.All(stash => !string.IsNullOrWhiteSpace(stash.Selector)));
        Assert.IsTrue(stashes.All(stash => !string.IsNullOrWhiteSpace(stash.Sha)));
        Assert.IsTrue(stashes.Any(stash => stash.UntrackedFiles is not null));
    }

    [TestMethod]
    public void StreamingTagReaderReleasesItsProcessWhenEnumerationIsAbandoned()
    {
        var factory = new NestedReferenceProcessFactory(
            "\u001erefs/tags/a\0a\011111111\0\u001erefs/tags/b\0b\022222222\0");
        using (var enumerator = new GitCliTagReader(factory)
                   .ReadStreaming(
                       "repo",
                       GitReferenceBackendOptions.Default,
                       new GitProjection(true, [nameof(TagEntity.FriendlyName)]),
                       GitTagReadQuery.Empty,
                       static _ => throw new InvalidOperationException(),
                       CancellationToken.None)
                   .GetEnumerator())
        {
            Assert.IsTrue(enumerator.MoveNext());
        }

        Assert.IsNotNull(factory.Process);
        Assert.AreEqual(1, factory.Process.StopCount);
        Assert.IsTrue(factory.Process.Disposed);
    }

    private static RepositoryEntity ReadRepository(string path, IReadOnlyDictionary<string, string>? settings = null)
    {
        var context = RuntimeV2TestContexts.CreateExecutionContext(
            sourceRuntimeSettings: settings ?? new Dictionary<string, string>());
        return new RepositoryRowsSource(path, static repositoryPath => new Repository(repositoryPath), context)
            .Chunks.SelectMany(static chunk => chunk).Single();
    }

    private sealed class NestedReferenceProcessFactory : IGitCliProcessFactory
    {
        private readonly byte[] _output;

        public NestedReferenceProcessFactory(string output)
        {
            _output = System.Text.Encoding.UTF8.GetBytes(output);
        }

        public NestedReferenceProcess? Process { get; private set; }

        public IGitCliProcess Start(GitCliProcessRequest request, CancellationToken cancellationToken)
        {
            var process = new NestedReferenceProcess(_output);
            Process = process;
            return process;
        }
    }

    private sealed class NestedReferenceProcess : IGitCliProcess
    {
        private readonly MemoryStream _output;

        public NestedReferenceProcess(byte[] output)
        {
            _output = new MemoryStream(output, writable: false);
        }

        public Stream StandardOutput => _output;

        public int StopCount { get; private set; }

        public bool Disposed { get; private set; }

        public void Complete()
        {
        }

        public void Stop() => StopCount++;

        public void Dispose()
        {
            Disposed = true;
            _output.Dispose();
        }
    }
}
