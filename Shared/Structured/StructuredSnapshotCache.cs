#nullable enable

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Musoq.DataSources.Structured;

internal enum StructuredSnapshotCacheAccess : byte
{
    Hit,
    JoinedDiscovery,
    Discovered
}

internal readonly record struct StructuredSnapshotCacheResult(
    StructuredSchemaSnapshot Snapshot,
    StructuredSnapshotCacheAccess Access);

internal sealed class StructuredSnapshotCache
{
    public const int DefaultMaximumSnapshots = 64;
    public const long DefaultMaximumBytes = 64L * 1024 * 1024;

    private readonly ConcurrentDictionary<StructuredFileIdentity, DiscoveryEntry> _discoveries =
        new(StructuredFileIdentityComparer.Instance);
    private readonly Dictionary<StructuredFileIdentity, CacheEntry> _entries =
        new(StructuredFileIdentityComparer.Instance);
    private readonly LinkedList<StructuredFileIdentity> _lru = [];
    private readonly object _sync = new();
    private readonly int _maximumSnapshots;
    private readonly long _maximumBytes;
    private long _estimatedBytes;

    public StructuredSnapshotCache(
        int maximumSnapshots = DefaultMaximumSnapshots,
        long maximumBytes = DefaultMaximumBytes)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumSnapshots);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumBytes);
        _maximumSnapshots = maximumSnapshots;
        _maximumBytes = maximumBytes;
    }

    public int Count
    {
        get
        {
            lock (_sync)
                return _entries.Count;
        }
    }

    public long EstimatedBytes
    {
        get
        {
            lock (_sync)
                return _estimatedBytes;
        }
    }

    public StructuredSnapshotCacheResult GetOrCreate(
        StructuredFileIdentity identity,
        Func<CancellationToken, StructuredSchemaSnapshot> factory,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(factory);
        return GetOrCreateAsync(
                identity,
                token => ValueTask.FromResult(factory(token)),
                cancellationToken)
            .AsTask()
            .GetAwaiter()
            .GetResult();
    }

    public async ValueTask<StructuredSnapshotCacheResult> GetOrCreateAsync(
        StructuredFileIdentity identity,
        Func<CancellationToken, ValueTask<StructuredSchemaSnapshot>> factory,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(factory);
        cancellationToken.ThrowIfCancellationRequested();

        if (TryGet(identity, out var cached))
            return new StructuredSnapshotCacheResult(cached, StructuredSnapshotCacheAccess.Hit);

        while (true)
        {
            var created = new DiscoveryEntry(this, identity, factory);
            var discovery = _discoveries.GetOrAdd(identity, created);
            if (!discovery.TryAddWaiter())
            {
                _discoveries.TryRemove(new KeyValuePair<StructuredFileIdentity, DiscoveryEntry>(identity, discovery));
                continue;
            }

            var access = ReferenceEquals(discovery, created)
                ? StructuredSnapshotCacheAccess.Discovered
                : StructuredSnapshotCacheAccess.JoinedDiscovery;

            try
            {
                var snapshot = await discovery.Task.Value.WaitAsync(cancellationToken).ConfigureAwait(false);
                return new StructuredSnapshotCacheResult(snapshot, access);
            }
            finally
            {
                discovery.RemoveWaiter();
            }
        }
    }

    public void Clear()
    {
        lock (_sync)
        {
            _entries.Clear();
            _lru.Clear();
            _estimatedBytes = 0;
        }
    }

    private async Task<StructuredSchemaSnapshot> DiscoverAndInsertAsync(
        StructuredFileIdentity identity,
        Func<CancellationToken, ValueTask<StructuredSchemaSnapshot>> factory,
        DiscoveryEntry owner,
        CancellationToken cancellationToken)
    {
        try
        {
            if (TryGet(identity, out var cached))
                return cached;

            var snapshot = await Task.Run(
                    async () => await factory(cancellationToken).ConfigureAwait(false),
                    cancellationToken)
                .ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();

            if (!StructuredFileIdentityComparer.Instance.Equals(snapshot.Identity, identity))
                throw new InvalidOperationException("Snapshot identity does not match its cache key.");

            Insert(snapshot);
            return snapshot;
        }
        finally
        {
            _discoveries.TryRemove(new KeyValuePair<StructuredFileIdentity, DiscoveryEntry>(identity, owner));
        }
    }

    private void Abandon(
        StructuredFileIdentity identity,
        DiscoveryEntry discovery)
    {
        _discoveries.TryRemove(new KeyValuePair<StructuredFileIdentity, DiscoveryEntry>(identity, discovery));
    }

    private bool TryGet(StructuredFileIdentity identity, out StructuredSchemaSnapshot snapshot)
    {
        lock (_sync)
        {
            if (!_entries.TryGetValue(identity, out var entry))
            {
                snapshot = null!;
                return false;
            }

            _lru.Remove(entry.Node);
            _lru.AddFirst(entry.Node);
            snapshot = entry.Snapshot;
            return true;
        }
    }

    private void Insert(StructuredSchemaSnapshot snapshot)
    {
        if (snapshot.EstimatedSizeBytes > _maximumBytes)
            return;

        lock (_sync)
        {
            if (_entries.TryGetValue(snapshot.Identity, out var existing))
            {
                _lru.Remove(existing.Node);
                _estimatedBytes -= existing.Snapshot.EstimatedSizeBytes;
                _entries.Remove(snapshot.Identity);
            }

            var node = _lru.AddFirst(snapshot.Identity);
            _entries.Add(snapshot.Identity, new CacheEntry(snapshot, node));
            _estimatedBytes += snapshot.EstimatedSizeBytes;

            while (_entries.Count > _maximumSnapshots || _estimatedBytes > _maximumBytes)
            {
                var last = _lru.Last!;
                _lru.RemoveLast();
                var removed = _entries[last.Value];
                _entries.Remove(last.Value);
                _estimatedBytes -= removed.Snapshot.EstimatedSizeBytes;
            }
        }
    }

    private sealed record CacheEntry(
        StructuredSchemaSnapshot Snapshot,
        LinkedListNode<StructuredFileIdentity> Node);

    private sealed class DiscoveryEntry
    {
        private readonly CancellationTokenSource _cancellation = new();
        private readonly StructuredSnapshotCache _owner;
        private readonly StructuredFileIdentity _identity;
        private readonly object _sync = new();
        private bool _abandoned;
        private int _waiters;

        public DiscoveryEntry(
            StructuredSnapshotCache owner,
            StructuredFileIdentity identity,
            Func<CancellationToken, ValueTask<StructuredSchemaSnapshot>> factory)
        {
            _owner = owner;
            _identity = identity;
            Task = new Lazy<Task<StructuredSchemaSnapshot>>(
                () => owner.DiscoverAndInsertAsync(identity, factory, this, _cancellation.Token),
                LazyThreadSafetyMode.ExecutionAndPublication);
        }

        public Lazy<Task<StructuredSchemaSnapshot>> Task { get; }

        public bool TryAddWaiter()
        {
            lock (_sync)
            {
                if (_abandoned)
                    return false;

                _waiters++;
                return true;
            }
        }

        public void RemoveWaiter()
        {
            var cancel = false;
            lock (_sync)
            {
                _waiters--;
                if (_waiters == 0 && !Task.Value.IsCompleted)
                {
                    _abandoned = true;
                    cancel = true;
                }
            }

            if (!cancel)
                return;

            _owner.Abandon(_identity, this);
            _cancellation.Cancel();
        }
    }
}
