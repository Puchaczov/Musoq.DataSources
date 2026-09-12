using System.Text;
using LibGit2Sharp;
using Musoq.DataSources.Git.Entities;

namespace Musoq.DataSources.Git.Tests;

[TestClass]
public sealed class GitReferenceProcessSeamTests
{
    [TestMethod]
    public void ReferenceReaders_ExposeExactExecutableAndProtocolArgumentsThroughTheSeam()
    {
        var options = GitReferenceBackendOptions.From(new Dictionary<string, string>
        {
            [GitReferenceBackendOptions.ExecutableSettingName] = "git-custom"
        });

        var tagFactory = new RecordingProcessFactory("\u001erefs/tags/a\0a\011111111\0");
        new GitCliTagReader(tagFactory).Read(
            "repo",
            options,
            new GitProjection(true, [nameof(TagEntity.FriendlyName), nameof(TagEntity.CanonicalName)]),
            GitTagReadQuery.Empty,
            static _ => throw new InvalidOperationException("The fake reader does not open repositories."),
            CancellationToken.None,
            _ => true);

        var tagRequest = tagFactory.Requests.Single();
        Assert.AreEqual("git-custom", tagRequest.Executable);
        CollectionAssert.AreEquivalent(
            new Dictionary<string, string>
            {
                ["GIT_OPTIONAL_LOCKS"] = "0",
                ["GIT_LFS_SKIP_SMUDGE"] = "1",
                ["GIT_PAGER"] = "cat",
                ["GIT_TERMINAL_PROMPT"] = "0"
            }.ToArray(),
            tagRequest.EnvironmentVariables!.ToArray());
        CollectionAssert.AreEqual(
            new[] { "for-each-ref", "--sort=refname", "--format=%1e%(refname)%00%(refname:short)%00%(objectname)%00", "refs/tags" },
            tagRequest.OperationArguments.ToArray());
        Assert.AreEqual(GitReferenceBackendOptions.BackendSettingName, tagRequest.BackendSettingName);

        var remoteFactory = new RecordingProcessFactory(
            "file:///offline/remote.git\n",
            "1111111\trefs/tags/a\n");
        new GitCliRemoteTagReader(remoteFactory).Read(
            "client",
            "origin",
            options,
            new GitProjection(true, [nameof(RemoteTagEntity.FriendlyName)]),
            GitRemoteTagReadQuery.Empty,
            static _ => throw new InvalidOperationException("The fake reader does not open repositories."),
            CancellationToken.None,
            _ => true);

        var remoteRequest = remoteFactory.Requests[1];
        CollectionAssert.AreEqual(
            new[] { "ls-remote", "--tags", "--refs", "file:///offline/remote.git" },
            remoteRequest.OperationArguments.ToArray());

        var stashFactory = new RecordingProcessFactory(string.Empty);
        new GitCliStashReader(stashFactory).Read(
            "repo",
            options,
            new GitProjection(true, [nameof(StashEntity.Selector)]),
            static _ => throw new InvalidOperationException("The fake reader does not open repositories."),
            CancellationToken.None,
            _ => true);

        CollectionAssert.AreEqual(
            new[] { "log", "-g", "--no-decorate", "--format=%gd%x00%H%x00", "refs/stash" },
            stashFactory.Requests.Single().OperationArguments.ToArray());
    }

    [TestMethod]
    public void RemoteReader_UsesPeelAwareProtocolOnlyWhenProjectionNeedsIt()
    {
        var factory = new RecordingProcessFactory(
            "file:///offline/remote.git\n",
            "1111111\trefs/tags/a\n2222222\trefs/tags/a^{}\n");

        new GitCliRemoteTagReader(factory).Read(
            "client",
            "origin",
            GitReferenceBackendOptions.Default,
            new GitProjection(true, [nameof(RemoteTagEntity.PeeledSha)]),
            GitRemoteTagReadQuery.Empty,
            static _ => throw new InvalidOperationException("The fake reader does not open repositories."),
            CancellationToken.None,
            _ => true);

        Assert.IsFalse(factory.Requests[1].OperationArguments.Contains("--refs"));
    }

    [TestMethod]
    public void StashSelectorUsesARealDirectLookupAndShaFilteringStopsAfterTheMatch()
    {
        var selectorFactory = new RecordingProcessFactory("stash@{0}\0aaaaaaaa\0");
        var selectorRecords = new List<GitStashRecord>();
        new GitCliStashReader(selectorFactory).Read(
            "repo",
            GitReferenceBackendOptions.Default,
            new GitProjection(true, [nameof(StashEntity.Selector), nameof(StashEntity.Sha)]),
            new GitStashReadQuery("stash@{0}", null, null, null),
            static _ => throw new InvalidOperationException("The fake reader does not open repositories."),
            CancellationToken.None,
            record =>
            {
                selectorRecords.Add(record);
                return true;
            });

        Assert.HasCount(1, selectorRecords);
        CollectionAssert.AreEqual(
            new[] { "log", "-g", "-1", "--no-decorate", "--format=%gd%x00%H%x00", "stash@{0}" },
            selectorFactory.Requests.Single().OperationArguments.ToArray());

        var shaFactory = new RecordingProcessFactory(
            "stash@{0}\0aaaaaaaa\0stash@{1}\0bbbbbbbb\0stash@{2}\0cccccccc\0");
        var shaRecords = new List<GitStashRecord>();
        new GitCliStashReader(shaFactory).Read(
            "repo",
            GitReferenceBackendOptions.Default,
            new GitProjection(true, [nameof(StashEntity.Selector), nameof(StashEntity.Sha)]),
            new GitStashReadQuery(null, "bbbbbbbb", null, null),
            static _ => throw new InvalidOperationException("The fake reader does not open repositories."),
            CancellationToken.None,
            record =>
            {
                shaRecords.Add(record);
                return true;
            });

        Assert.HasCount(1, shaRecords);
        Assert.AreEqual("stash@{1}", shaRecords[0].Selector);
        Assert.IsTrue(shaFactory.Processes.Single().Completed);
    }

    [TestMethod]
    public void RemoteReaderPushesExactPatternsAndStopsTheProcessOnEarlyConsumerTermination()
    {
        var factory = new RecordingProcessFactory(
            "file:///offline/remote.git\n",
            "1111111\trefs/tags/a\n2222222\trefs/tags/a^{}\n3333333\trefs/tags/b\n");

        new GitCliRemoteTagReader(factory).Read(
            "client",
            "origin",
            GitReferenceBackendOptions.Default,
            new GitProjection(true, [nameof(RemoteTagEntity.PeeledSha)]),
            new GitRemoteTagReadQuery(null, "a", null, null, null, false, null),
            static _ => throw new InvalidOperationException("The fake reader does not open repositories."),
            CancellationToken.None,
            _ => false);

        CollectionAssert.AreEqual(
            new[] { "ls-remote", "--tags", "file:///offline/remote.git", "refs/tags/a", "refs/tags/a^{}" },
            factory.Requests[1].OperationArguments.ToArray());
        Assert.AreEqual(1, factory.Processes[1].StopCount);
        Assert.IsFalse(factory.Processes[1].Completed);
    }

    [TestMethod]
    public void CancelledRemoteReaderDoesNotResolveConfigurationOrStartGit()
    {
        var factory = new RecordingProcessFactory(string.Empty);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        Assert.ThrowsException<OperationCanceledException>(() => new GitCliRemoteTagReader(factory).Read(
            "client",
            "origin",
            GitReferenceBackendOptions.Default,
            new GitProjection(true, [nameof(RemoteTagEntity.FriendlyName)]),
            GitRemoteTagReadQuery.Empty,
            static _ => throw new InvalidOperationException("The fake reader does not open repositories."),
            cancellation.Token,
            _ => true));
        Assert.AreEqual(0, factory.Requests.Count);
    }

    [TestMethod]
    public void ReaderStopsAndDisposesTheInjectedProcessWhenTheConsumerStops()
    {
        var factory = new RecordingProcessFactory(
            "\u001erefs/tags/a\0a\011111111\0\u001erefs/tags/b\0b\022222222\0");

        new GitCliTagReader(factory).Read(
            "repo",
            GitReferenceBackendOptions.Default,
            new GitProjection(true, [nameof(TagEntity.FriendlyName)]),
            GitTagReadQuery.Empty,
            static _ => throw new InvalidOperationException("The fake reader does not open repositories."),
            CancellationToken.None,
            _ => false);

        var process = factory.Processes.Single();
        Assert.AreEqual(1, process.StopCount);
        Assert.IsFalse(process.Completed);
        Assert.IsTrue(process.Disposed);
    }

    [TestMethod]
    public void CancelledReaderDoesNotStartAProcess()
    {
        var factory = new RecordingProcessFactory(string.Empty);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        Assert.ThrowsException<OperationCanceledException>(() => new GitCliTagReader(factory).Read(
            "repo",
            GitReferenceBackendOptions.Default,
            new GitProjection(true, [nameof(TagEntity.FriendlyName)]),
            GitTagReadQuery.Empty,
            static _ => throw new InvalidOperationException("The fake reader does not open repositories."),
            cancellation.Token,
            _ => true));
        Assert.AreEqual(0, factory.Requests.Count);
    }

    [TestMethod]
    public void ReaderPropagatesAUsefulNonzeroCompletionFailure()
    {
        var factory = new RecordingProcessFactory(string.Empty)
        {
            CompletionException = new InvalidOperationException("Git exited with code 2: bad local repository")
        };

        var exception = Assert.ThrowsException<InvalidOperationException>(() => new GitCliStashReader(factory).Read(
            "repo",
            GitReferenceBackendOptions.Default,
            new GitProjection(true, [nameof(StashEntity.Selector)]),
            static _ => throw new InvalidOperationException("The fake reader does not open repositories."),
            CancellationToken.None,
            _ => true));

        StringAssert.Contains(exception.Message, "Git exited with code 2");
    }

    private sealed class RecordingProcessFactory : IGitCliProcessFactory
    {
        private readonly Queue<byte[]> _outputs;

        public RecordingProcessFactory(params string[] outputs)
        {
            _outputs = new Queue<byte[]>(outputs.Select(output => Encoding.UTF8.GetBytes(output)));
        }

        public List<GitCliProcessRequest> Requests { get; } = [];

        public List<RecordingProcess> Processes { get; } = [];

        public Exception? CompletionException { get; init; }

        public IGitCliProcess Start(GitCliProcessRequest request, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Requests.Add(request with { OperationArguments = request.OperationArguments.ToArray() });
            var process = new RecordingProcess(_outputs.Count == 0 ? [] : _outputs.Dequeue(), CompletionException);
            Processes.Add(process);
            return process;
        }
    }

    private sealed class RecordingProcess : IGitCliProcess
    {
        private readonly MemoryStream _output;
        private readonly Exception? _completionException;

        public RecordingProcess(byte[] output, Exception? completionException)
        {
            _output = new MemoryStream(output, writable: false);
            _completionException = completionException;
        }

        public Stream StandardOutput => _output;

        public bool Completed { get; private set; }

        public bool Disposed { get; private set; }

        public int StopCount { get; private set; }

        public void Complete()
        {
            if (_completionException is not null)
                throw _completionException;

            Completed = true;
        }

        public void Stop() => StopCount++;

        public void Dispose()
        {
            Disposed = true;
            _output.Dispose();
        }
    }
}
