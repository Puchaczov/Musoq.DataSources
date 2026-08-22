#nullable enable

using System;
using System.Threading;
using System.Threading.Tasks;

namespace Musoq.DataSources.SeparatedValues;

/// <summary>
/// Process-wide byte permits for read-ahead buffers.  A query can choose its
/// worker count independently, but it cannot multiply the machine-wide memory
/// footprint by starting another scan.
/// </summary>
internal static class SeparatedValuesStructuralMemoryBudget
{
    private const int PermitSize = 1024 * 1024;
    private const int DefaultCapacityBytes = 256 * 1024 * 1024;
    private const int OverflowHeadroomBytes = 64 * 1024 * 1024;
    private static readonly SemaphoreSlim AcquisitionGate = new(1, 1);
    private static readonly SemaphoreSlim Permits = new(DefaultCapacityBytes / PermitSize, DefaultCapacityBytes / PermitSize);
    private static readonly SemaphoreSlim ReadAheadPermits = new(
        (DefaultCapacityBytes - OverflowHeadroomBytes) / PermitSize,
        (DefaultCapacityBytes - OverflowHeadroomBytes) / PermitSize);

    public static int CapacityBytes => DefaultCapacityBytes;

    internal static long CurrentReservedBytes =>
        checked((long)(DefaultCapacityBytes / PermitSize - Permits.CurrentCount) * PermitSize);

    internal static long CurrentReadAheadReservedBytes =>
        checked((long)((DefaultCapacityBytes - OverflowHeadroomBytes) / PermitSize - ReadAheadPermits.CurrentCount) * PermitSize);

    public static int EstimatePooledInt32ArrayBytes(int minimumLength)
    {
        return EstimatePooledArrayBytes(minimumLength, sizeof(int));
    }

    public static int EstimatePooledByteArrayBytes(int minimumLength)
    {
        return EstimatePooledArrayBytes(minimumLength, sizeof(byte));
    }

    public static async ValueTask<LeasePair> AcquirePairAsync(
        int firstBytes,
        int secondBytes,
        CancellationToken cancellationToken)
    {
        var firstCount = GetPermitCount(firstBytes);
        var secondCount = GetPermitCount(secondBytes);
        var totalCount = checked(firstCount + secondCount);
        if (totalCount > (DefaultCapacityBytes - OverflowHeadroomBytes) / PermitSize)
        {
            throw new InvalidOperationException(
                $"Separated-values combined buffering request of " +
                $"{checked((long)firstBytes + secondBytes):N0} bytes exceeds the read-ahead share of the " +
                $"process-wide {CapacityBytes:N0}-byte budget.");
        }

        await AcquisitionGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await AcquirePermitsAsync(ReadAheadPermits, totalCount, cancellationToken).ConfigureAwait(false);
            try
            {
                await AcquirePermitsAsync(Permits, totalCount, cancellationToken).ConfigureAwait(false);
            }
            catch
            {
                ReadAheadPermits.Release(totalCount);
                throw;
            }
        }
        finally
        {
            AcquisitionGate.Release();
        }

        return new LeasePair(
            new Lease(firstCount, releaseReadAheadPermits: true),
            new Lease(secondCount, releaseReadAheadPermits: true));
    }

    private static int EstimatePooledArrayBytes(int minimumLength, int elementSize)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(minimumLength);
        long capacity = 16;
        while (capacity < minimumLength)
            capacity = checked(capacity * 2);
        return checked((int)(capacity * elementSize));
    }

    public static async ValueTask<Lease> AcquireAsync(int bytes, CancellationToken cancellationToken)
    {
        var count = GetPermitCount(bytes);
        if (count > DefaultCapacityBytes / PermitSize)
            throw new InvalidOperationException(
                $"Separated-values buffering request of {bytes:N0} bytes exceeds the process-wide " +
                $"{CapacityBytes:N0}-byte budget.");
        await AcquisitionGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await AcquirePermitsAsync(Permits, count, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            AcquisitionGate.Release();
        }

        return new Lease(count);
    }

    public static Lease? TryAcquire(int bytes)
    {
        var count = GetPermitCount(bytes);
        if (count > DefaultCapacityBytes / PermitSize)
            return null;

        var acquired = 0;
        while (acquired < count && Permits.Wait(0))
            acquired++;
        if (acquired != count)
        {
            if (acquired > 0)
                Permits.Release(acquired);
            return null;
        }

        return new Lease(count);
    }

    private static async ValueTask AcquirePermitsAsync(
        SemaphoreSlim permits,
        int count,
        CancellationToken cancellationToken)
    {
        for (var index = 0; index < count; index++)
        {
            try
            {
                await permits.WaitAsync(cancellationToken).ConfigureAwait(false);
            }
            catch
            {
                if (index > 0)
                    permits.Release(index);
                throw;
            }
        }
    }

    private static int GetPermitCount(int bytes)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(bytes);
        return checked((bytes + PermitSize - 1) / PermitSize);
    }

    public readonly record struct LeasePair(Lease First, Lease Second);

    public sealed class Lease : IDisposable
    {
        private readonly bool _releaseReadAheadPermits;
        private int _count;

        internal Lease(int count, bool releaseReadAheadPermits = false)
        {
            _count = count;
            _releaseReadAheadPermits = releaseReadAheadPermits;
        }

        public void Dispose()
        {
            var count = Interlocked.Exchange(ref _count, 0);
            if (count > 0)
            {
                Permits.Release(count);
                if (_releaseReadAheadPermits)
                    ReadAheadPermits.Release(count);
            }
        }
    }
}
