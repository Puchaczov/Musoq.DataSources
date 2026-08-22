#nullable enable

using System;
using System.Threading;
using System.Threading.Tasks;

namespace Musoq.DataSources.SeparatedValues;

internal interface ISeparatedValuesOutputMemoryBudget
{
    int CapacityBytes { get; }

    long CurrentReservedBytes { get; }

    long HighWaterBytes { get; }

    long OversizedReservationCount { get; }

    long LargestOversizedRequestBytes { get; }

    ValueTask<ISeparatedValuesOutputMemoryLease> AcquireAsync(
        long estimatedBytes,
        CancellationToken cancellationToken);
}

internal interface ISeparatedValuesOutputMemoryLease : IDisposable
{
    long RequestedBytes { get; }

    long ReservedBytes { get; }

    bool IsOversized { get; }
}

/// <summary>
/// Process-wide retained-output permits. Acquisitions are serialized so a
/// large reservation cannot deadlock with another request after both acquire a
/// partial set of permits. A result larger than the configured budget reserves
/// every permit and therefore runs exclusively.
/// </summary>
internal sealed class SeparatedValuesOutputMemoryBudget : ISeparatedValuesOutputMemoryBudget
{
    public const int DefaultCapacityBytes = 256 * 1024 * 1024;
    private const int DefaultPermitBytes = 64 * 1024;
    private static readonly SeparatedValuesOutputMemoryBudget ProcessWide = new();

    private readonly SemaphoreSlim _acquisitionGate = new(1, 1);
    private readonly int _permitBytes;
    private readonly int _permitCount;
    private readonly SemaphoreSlim _permits;
    private long _currentReservedBytes;
    private long _highWaterBytes;
    private long _largestOversizedRequestBytes;
    private long _oversizedReservationCount;

    public SeparatedValuesOutputMemoryBudget(
        int capacityBytes = DefaultCapacityBytes,
        int permitBytes = DefaultPermitBytes)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(capacityBytes);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(permitBytes);
        if (permitBytes > capacityBytes)
            throw new ArgumentOutOfRangeException(nameof(permitBytes));

        _permitBytes = permitBytes;
        _permitCount = capacityBytes / permitBytes;
        CapacityBytes = checked(_permitCount * permitBytes);
        _permits = new SemaphoreSlim(_permitCount, _permitCount);
    }

    public static ISeparatedValuesOutputMemoryBudget Shared => ProcessWide;

    public int CapacityBytes { get; }

    public long CurrentReservedBytes => Volatile.Read(ref _currentReservedBytes);

    public long HighWaterBytes => Volatile.Read(ref _highWaterBytes);

    public long OversizedReservationCount => Volatile.Read(ref _oversizedReservationCount);

    public long LargestOversizedRequestBytes => Volatile.Read(ref _largestOversizedRequestBytes);

    public async ValueTask<ISeparatedValuesOutputMemoryLease> AcquireAsync(
        long estimatedBytes,
        CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(estimatedBytes);
        cancellationToken.ThrowIfCancellationRequested();
        var requestedBytes = Math.Max(1, estimatedBytes);
        var isOversized = requestedBytes > CapacityBytes;
        var permits = isOversized
            ? _permitCount
            : checked((int)((requestedBytes - 1) / _permitBytes + 1));
        var acquired = 0;

        await _acquisitionGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            while (acquired < permits)
            {
                await _permits.WaitAsync(cancellationToken).ConfigureAwait(false);
                acquired++;
                var current = Interlocked.Add(ref _currentReservedBytes, _permitBytes);
                UpdateHighWater(current);
            }
        }
        catch
        {
            if (acquired > 0)
            {
                Interlocked.Add(ref _currentReservedBytes, -(long)acquired * _permitBytes);
                _permits.Release(acquired);
            }
            throw;
        }
        finally
        {
            _acquisitionGate.Release();
        }

        var reservedBytes = checked((long)permits * _permitBytes);
        if (isOversized)
        {
            Interlocked.Increment(ref _oversizedReservationCount);
            UpdateMaximum(ref _largestOversizedRequestBytes, requestedBytes);
        }
        return new Lease(this, permits, requestedBytes, reservedBytes, isOversized);
    }

    private void Release(int permits, long reservedBytes)
    {
        Interlocked.Add(ref _currentReservedBytes, -reservedBytes);
        _permits.Release(permits);
    }

    private void UpdateHighWater(long current)
    {
        UpdateMaximum(ref _highWaterBytes, current);
    }

    private static void UpdateMaximum(ref long target, long value)
    {
        while (true)
        {
            var observed = Volatile.Read(ref target);
            if (value <= observed ||
                Interlocked.CompareExchange(ref target, value, observed) == observed)
                return;
        }
    }

    private sealed class Lease : ISeparatedValuesOutputMemoryLease
    {
        private readonly int _permits;
        private readonly long _reservedBytes;
        private SeparatedValuesOutputMemoryBudget? _owner;

        public Lease(
            SeparatedValuesOutputMemoryBudget owner,
            int permits,
            long requestedBytes,
            long reservedBytes,
            bool isOversized)
        {
            _owner = owner;
            _permits = permits;
            _reservedBytes = reservedBytes;
            RequestedBytes = requestedBytes;
            ReservedBytes = reservedBytes;
            IsOversized = isOversized;
        }

        public long RequestedBytes { get; }

        public long ReservedBytes { get; }

        public bool IsOversized { get; }

        public void Dispose()
        {
            Interlocked.Exchange(ref _owner, null)?.Release(_permits, _reservedBytes);
        }
    }
}
