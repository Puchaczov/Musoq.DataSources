#nullable enable

using System;
using System.Threading;

using Musoq.DataSources.Search.Components.Diagnostics;

namespace Musoq.DataSources.Search.Components.Traversal;

/// <summary>
///     Bounded output credits shared by one Search execution and by all
///     concurrent Search executions in the process. One credit represents one
///     bounded row chunk and is intentionally conservative for row types whose
///     exact managed size is not known to the generic coordinator.
/// </summary>
internal sealed class SearchOutputCreditPool : IDisposable
{
    private readonly SemaphoreSlim _queryCredits;
    private readonly long _bufferedOutputBytes;
    private bool _disposed;

    public SearchOutputCreditPool(long bufferedOutputBytes)
    {
        if (bufferedOutputBytes < SearchMemoryCreditGate.CreditBytes)
            throw new ArgumentOutOfRangeException(nameof(bufferedOutputBytes));

        // A host may advertise a larger per-query setting, but one query can
        // never reserve more than the process-wide Search allowance. Without
        // this clamp a single oversized chunk could wait forever for credits
        // that the global gate intentionally does not contain.
        _bufferedOutputBytes = Math.Min(
            bufferedOutputBytes,
            SearchMemoryCreditGate.ProcessWideBytes);
        _queryCredits = new SemaphoreSlim(
            SearchMemoryCreditGate.ToCredits(_bufferedOutputBytes),
            SearchMemoryCreditGate.ToCredits(_bufferedOutputBytes));
    }

    public long Acquire(long requestedBytes, CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (requestedBytes <= 0)
            requestedBytes = SearchMemoryCreditGate.CreditBytes;

        if (requestedBytes > _bufferedOutputBytes)
        {
            throw new SearchResourceLimitException(
                SearchDiagnosticCatalog.ResourceLimit(
                    "buffered-output-bytes",
                    _bufferedOutputBytes),
                budgetCode: "buffered-output-bytes");
        }

        var creditCount = SearchMemoryCreditGate.ToCredits(requestedBytes);
        var acquiredQueryCredits = 0;
        try
        {
            while (acquiredQueryCredits < creditCount)
            {
                _queryCredits.Wait(cancellationToken);
                acquiredQueryCredits++;
            }

            var creditedBytes = checked((long)creditCount * SearchMemoryCreditGate.CreditBytes);
            SearchMemoryCreditGate.Acquire(creditedBytes, cancellationToken);
            return creditedBytes;
        }
        catch
        {
            if (acquiredQueryCredits > 0)
                _queryCredits.Release(acquiredQueryCredits);

            throw;
        }
    }

    public void Release(long creditedBytes)
    {
        if (_disposed)
            return;

        if (creditedBytes <= 0)
            throw new ArgumentOutOfRangeException(nameof(creditedBytes));

        SearchMemoryCreditGate.Release(creditedBytes);
        _queryCredits.Release(SearchMemoryCreditGate.ToCredits(creditedBytes));
    }

    public long MaximumChunkBytes => _bufferedOutputBytes;

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        _queryCredits.Dispose();
    }
}
