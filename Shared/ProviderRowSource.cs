#nullable enable

using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using Musoq.Schema.DataSources;

namespace Musoq.DataSources.Common;

internal sealed class ProviderRowSource<T>(RowSource<T> source, ProviderDiagnostics diagnostics,
    CancellationToken cancellationToken) : RowSource<T>
{
    public override IEnumerable<IReadOnlyList<T>> Chunks
    {
        get
        {
            try { return new ChunksEnumerable(source.Chunks, diagnostics, cancellationToken); }
            catch (Exception error) when (diagnostics.TryMap(error, cancellationToken, out var failure)) { throw failure; }
        }
    }

    private sealed class ChunksEnumerable(IEnumerable<IReadOnlyList<T>> chunks, ProviderDiagnostics diagnostics,
        CancellationToken cancellationToken) : IEnumerable<IReadOnlyList<T>>
    {
        public IEnumerator<IReadOnlyList<T>> GetEnumerator()
        {
            try { return new ChunksEnumerator(chunks.GetEnumerator(), diagnostics, cancellationToken); }
            catch (Exception error) when (diagnostics.TryMap(error, cancellationToken, out var failure)) { throw failure; }
        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }

    private sealed class ChunksEnumerator(IEnumerator<IReadOnlyList<T>> chunks, ProviderDiagnostics diagnostics,
        CancellationToken cancellationToken) : IEnumerator<IReadOnlyList<T>>
    {
        public IReadOnlyList<T> Current
        {
            get
            {
                try { return chunks.Current; }
                catch (Exception error) when (diagnostics.TryMap(error, cancellationToken, out var failure)) { throw failure; }
            }
        }

        object IEnumerator.Current => Current;

        public bool MoveNext()
        {
            try { return chunks.MoveNext(); }
            catch (Exception error) when (diagnostics.TryMap(error, cancellationToken, out var failure)) { throw failure; }
        }

        public void Reset() => chunks.Reset();

        public void Dispose()
        {
            try { chunks.Dispose(); }
            catch (Exception error) when (diagnostics.TryMap(error, cancellationToken, out var failure)) { throw failure; }
        }
    }
}
