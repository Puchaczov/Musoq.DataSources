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
    private static readonly SemaphoreSlim Permits = new(DefaultCapacityBytes / PermitSize, DefaultCapacityBytes / PermitSize);

    public static int CapacityBytes => DefaultCapacityBytes;

    public static async ValueTask<Lease> AcquireAsync(int bytes, CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(bytes);
        var count = checked((bytes + PermitSize - 1) / PermitSize);
        if (count > DefaultCapacityBytes / PermitSize)
            throw new InvalidOperationException(
                $"Separated-values buffering request of {bytes:N0} bytes exceeds the process-wide " +
                $"{CapacityBytes:N0}-byte budget.");
        for (var index = 0; index < count; index++)
        {
            try
            {
                await Permits.WaitAsync(cancellationToken).ConfigureAwait(false);
            }
            catch
            {
                Permits.Release(index);
                throw;
            }
        }

        return new Lease(count);
    }

    public static Lease? TryAcquire(int bytes)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(bytes);
        var count = checked((bytes + PermitSize - 1) / PermitSize);
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

    public sealed class Lease : IDisposable
    {
        private int _count;

        internal Lease(int count)
        {
            _count = count;
        }

        public void Dispose()
        {
            var count = Interlocked.Exchange(ref _count, 0);
            if (count > 0)
                Permits.Release(count);
        }
    }
}
