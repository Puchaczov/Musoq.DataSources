using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Musoq.DataSources.Structured;

namespace Musoq.DataSources.Json.Tests;

[TestClass]
public class StructuredFileIdentityAndCacheTests
{
    [DataTestMethod]
    [DataRow(0)]
    [DataRow(100)]
    [DataRow(150_000)]
    public void ComputeFingerprint_MatchesCapturedFileEdges(int length)
    {
        var path = WriteTempFile(new string('x', length));

        try
        {
            var captured = StructuredFileIdentity.Capture(path, "test");
            var buffered = StructuredFileIdentity.ComputeFingerprint(File.ReadAllBytes(path));

            Assert.AreEqual(captured.Fingerprint, buffered);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [TestMethod]
    public void Capture_WhenEdgeContentChanges_ChangesIdentity()
    {
        var path = WriteTempFile(new string('a', 150_000));

        try
        {
            var before = StructuredFileIdentity.Capture(path, "delimiter=,");
            using (var stream = new FileStream(path, FileMode.Open, FileAccess.Write, FileShare.None))
            {
                stream.Position = stream.Length - 1;
                stream.WriteByte((byte)'b');
            }

            var after = StructuredFileIdentity.Capture(path, "delimiter=,");

            Assert.AreNotEqual(before, after);
            Assert.AreNotEqual(before.Fingerprint, after.Fingerprint);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [TestMethod]
    public void Capture_WhenParserOptionsChange_ChangesCacheKey()
    {
        var path = WriteTempFile("value");

        try
        {
            var comma = StructuredFileIdentity.Capture(path, "delimiter=,");
            var tab = StructuredFileIdentity.Capture(path, "delimiter=tab");

            Assert.AreNotEqual(comma, tab);
            Assert.AreEqual(comma.Fingerprint, tab.Fingerprint);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [TestMethod]
    public void Capture_WhenCancelled_StopsBeforeReading()
    {
        var path = WriteTempFile("value");

        try
        {
            using var source = new CancellationTokenSource();
            source.Cancel();

            Assert.ThrowsExactly<OperationCanceledException>(() =>
                StructuredFileIdentity.Capture(path, "test", source.Token));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [TestMethod]
    public async Task GetOrCreateAsync_WhenConcurrent_IsSingleFlight()
    {
        var cache = new StructuredSnapshotCache();
        var identity = Identity("single-flight");
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var calls = 0;

        async ValueTask<StructuredSchemaSnapshot> Discover(CancellationToken _)
        {
            Interlocked.Increment(ref calls);
            entered.TrySetResult();
            await release.Task;
            return Snapshot(identity);
        }

        var tasks = Enumerable.Range(0, 16)
            .Select(_ => cache.GetOrCreateAsync(identity, Discover).AsTask())
            .ToArray();
        await entered.Task;
        release.SetResult();
        var results = await Task.WhenAll(tasks);

        Assert.AreEqual(1, calls);
        Assert.AreEqual(1, results.Count(result => result.Access == StructuredSnapshotCacheAccess.Discovered));
        Assert.AreEqual(15, results.Count(result => result.Access == StructuredSnapshotCacheAccess.JoinedDiscovery));
        Assert.AreEqual(1, cache.Count);
    }

    [TestMethod]
    public async Task GetOrCreateAsync_WhenWaiterCancels_DoesNotCancelSharedDiscovery()
    {
        var cache = new StructuredSnapshotCache();
        var identity = Identity("cancel");
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var calls = 0;

        async ValueTask<StructuredSchemaSnapshot> Discover(CancellationToken token)
        {
            Assert.IsTrue(token.CanBeCanceled);
            Interlocked.Increment(ref calls);
            entered.SetResult();
            await release.Task;
            return Snapshot(identity);
        }

        using var cancellation = new CancellationTokenSource();
        var cancelledWaiter = cache.GetOrCreateAsync(identity, Discover, cancellation.Token).AsTask();
        var survivingWaiter = cache.GetOrCreateAsync(identity, Discover).AsTask();
        await entered.Task;
        cancellation.Cancel();

        try
        {
            await cancelledWaiter;
            Assert.Fail("The cancelled waiter should not complete normally.");
        }
        catch (OperationCanceledException)
        {
        }

        release.SetResult();
        var completed = await survivingWaiter;

        Assert.AreEqual(1, calls);
        Assert.AreSame(completed.Snapshot, (await cache.GetOrCreateAsync(identity, Discover)).Snapshot);
    }

    [TestMethod]
    public async Task GetOrCreateAsync_WhenAllWaitersCancel_CancelsDiscoveryAndAllowsRetry()
    {
        var cache = new StructuredSnapshotCache();
        var identity = Identity("cancel-all");
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var calls = 0;

        async ValueTask<StructuredSchemaSnapshot> Discover(CancellationToken token)
        {
            Interlocked.Increment(ref calls);
            entered.SetResult();
            await Task.Delay(Timeout.InfiniteTimeSpan, token);
            return Snapshot(identity);
        }

        using var cancellation = new CancellationTokenSource();
        var cancelledWaiter = cache.GetOrCreateAsync(identity, Discover, cancellation.Token).AsTask();
        await entered.Task;
        cancellation.Cancel();

        try
        {
            await cancelledWaiter;
            Assert.Fail("The cancelled waiter should not complete normally.");
        }
        catch (OperationCanceledException)
        {
        }

        var retry = await cache.GetOrCreateAsync(
            identity,
            _ => ValueTask.FromResult(Snapshot(identity)));

        Assert.AreEqual(1, calls);
        Assert.AreEqual(StructuredSnapshotCacheAccess.Discovered, retry.Access);
        Assert.AreEqual(1, cache.Count);
    }

    [TestMethod]
    public void GetOrCreate_WhenEntryCountIsExceeded_EvictsLeastRecentlyUsed()
    {
        var cache = new StructuredSnapshotCache(2, 10_000);
        var a = Identity("a");
        var b = Identity("b");
        var c = Identity("c");
        var discoveries = new Dictionary<string, int>();

        StructuredSchemaSnapshot Discover(StructuredFileIdentity identity)
        {
            discoveries[identity.CanonicalPath] = discoveries.GetValueOrDefault(identity.CanonicalPath) + 1;
            return Snapshot(identity);
        }

        _ = cache.GetOrCreate(a, _ => Discover(a));
        _ = cache.GetOrCreate(b, _ => Discover(b));
        Assert.AreEqual(StructuredSnapshotCacheAccess.Hit, cache.GetOrCreate(a, _ => Discover(a)).Access);
        _ = cache.GetOrCreate(c, _ => Discover(c));
        _ = cache.GetOrCreate(b, _ => Discover(b));

        Assert.AreEqual(1, discoveries[a.CanonicalPath]);
        Assert.AreEqual(2, discoveries[b.CanonicalPath]);
        Assert.AreEqual(1, discoveries[c.CanonicalPath]);
        Assert.AreEqual(2, cache.Count);
    }

    [TestMethod]
    public void GetOrCreate_WhenByteLimitIsExceeded_EvictsOnInsertion()
    {
        var cache = new StructuredSnapshotCache(10, 1_000);
        var a = Identity("bytes-a");
        var b = Identity("bytes-b");

        _ = cache.GetOrCreate(a, _ => Snapshot(a, 600));
        _ = cache.GetOrCreate(b, _ => Snapshot(b, 600));

        Assert.AreEqual(1, cache.Count);
        Assert.AreEqual(600L, cache.EstimatedBytes);
        Assert.AreEqual(StructuredSnapshotCacheAccess.Discovered,
            cache.GetOrCreate(a, _ => Snapshot(a, 600)).Access);
    }

    [TestMethod]
    public void GetOrCreate_WhenSnapshotExceedsEntireBudget_DoesNotCacheIt()
    {
        var cache = new StructuredSnapshotCache(10, 100);
        var identity = Identity("oversized");
        var calls = 0;

        StructuredSchemaSnapshot Discover()
        {
            calls++;
            return Snapshot(identity, 101);
        }

        _ = cache.GetOrCreate(identity, _ => Discover());
        _ = cache.GetOrCreate(identity, _ => Discover());

        Assert.AreEqual(2, calls);
        Assert.AreEqual(0, cache.Count);
    }

    [TestMethod]
    public void GetOrCreate_WhenDiscoveryFails_AllowsRetry()
    {
        var cache = new StructuredSnapshotCache();
        var identity = Identity("retry");
        var calls = 0;

        Assert.ThrowsExactly<InvalidDataException>(() => cache.GetOrCreate(identity, _ =>
        {
            calls++;
            throw new InvalidDataException("broken");
        }));

        var result = cache.GetOrCreate(identity, _ =>
        {
            calls++;
            return Snapshot(identity);
        });

        Assert.AreEqual(2, calls);
        Assert.AreEqual(StructuredSnapshotCacheAccess.Discovered, result.Access);
    }

    private static StructuredSchemaSnapshot Snapshot(StructuredFileIdentity identity, long estimatedBytes = 600)
    {
        return new StructuredSchemaSnapshot(
            identity,
            [new StructuredColumnSnapshot("Value", 0, new StructuredTypeState(StructuredValueKind.Long, false), 1)],
            1,
            estimatedSizeBytes: estimatedBytes);
    }

    private static StructuredFileIdentity Identity(string name)
    {
        return new StructuredFileIdentity(
            name,
            1,
            1,
            "test",
            new StructuredFileFingerprint((ulong)name.Length, (ulong)name[0]));
    }

    private static string WriteTempFile(string contents)
    {
        var path = Path.Combine(Path.GetTempPath(), $"musoq-structured-{Guid.NewGuid():N}.tmp");
        File.WriteAllText(path, contents, new UTF8Encoding(false));
        return path;
    }
}
