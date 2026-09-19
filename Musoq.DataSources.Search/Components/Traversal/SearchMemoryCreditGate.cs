#nullable enable

using System;
using System.Threading;

namespace Musoq.DataSources.Search.Components.Traversal;

/// <summary>
///     Process-wide memory credits shared by queued output chunks and the
///     adaptive small-file input path. Keeping one gate prevents concurrent
///     queries from independently reaching the advertised memory ceiling.
/// </summary>
internal static class SearchMemoryCreditGate
{
    internal const long CreditBytes = 64 * 1024;
    internal const long ProcessWideBytes = 256L * 1024 * 1024;
    internal const int ProcessWideWorkerLimit = 32;
    private static readonly SemaphoreSlim Credits = new(
        ToCredits(ProcessWideBytes),
        ToCredits(ProcessWideBytes));
    private static readonly SemaphoreSlim Workers = new(
        ProcessWideWorkerLimit,
        ProcessWideWorkerLimit);

    public static int ToCredits(long bytes)
    {
        if (bytes < 0)
            throw new ArgumentOutOfRangeException(nameof(bytes));

        return checked((int)Math.Max(1, (bytes + CreditBytes - 1) / CreditBytes));
    }

    public static void Acquire(long bytes, CancellationToken cancellationToken)
    {
        var credits = ToCredits(bytes);
        for (var index = 0; index < credits; index++)
        {
            try
            {
                Credits.Wait(cancellationToken);
            }
            catch
            {
                if (index > 0)
                    Credits.Release(index);
                throw;
            }
        }
    }

    public static void Release(long bytes)
    {
        Credits.Release(ToCredits(bytes));
    }

    public static void AcquireWorker(CancellationToken cancellationToken)
    {
        Workers.Wait(cancellationToken);
    }

    public static void ReleaseWorker()
    {
        Workers.Release();
    }
}
