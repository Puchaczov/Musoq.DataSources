#nullable enable

using System;
using System.Threading;
using System.Threading.Tasks;

namespace Musoq.DataSources.SeparatedValues;

internal static class SeparatedValuesCpuBudget
{
    public static int Capacity { get; } = Math.Max(1, Environment.ProcessorCount - 1);
    private static readonly SemaphoreSlim Permits = new(Capacity, Capacity);

    internal static int CurrentLeases => Capacity - Permits.CurrentCount;

    public static async ValueTask<Lease> AcquireAsync(CancellationToken cancellationToken)
    {
        await Permits.WaitAsync(cancellationToken).ConfigureAwait(false);
        return new Lease(Permits);
    }

    internal readonly struct Lease : IDisposable
    {
        private readonly SemaphoreSlim? _permits;

        public Lease(SemaphoreSlim permits)
        {
            _permits = permits;
        }

        public void Dispose()
        {
            _permits?.Release();
        }
    }
}
