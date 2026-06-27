using Musoq.Schema.DataSources;

namespace Musoq.DataSources.AsyncRowsSource;

/// <summary>
///     Read rows asynchronously and emit runtime-v2 typed chunks.
/// </summary>
/// <param name="queryCancelledToken">Token that signals the end of the work.</param>
/// <typeparam name="T">Type of the entity.</typeparam>
public abstract class AsyncRowsSourceBase<T>(CancellationToken queryCancelledToken) : RowSourceBase<T>
{
    /// <summary>
    ///     Collect chunks of typed rows.
    /// </summary>
    /// <param name="writer">Writer used to emit chunks.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Task.</returns>
    protected abstract Task CollectChunksAsync(IChunkWriter<T> writer, CancellationToken cancellationToken);

    /// <inheritdoc />
    protected sealed override void CollectChunks(IChunkWriter<T> writer)
    {
        using var linkedTokenSource =
            CancellationTokenSource.CreateLinkedTokenSource(queryCancelledToken, writer.CancellationToken);

        CollectChunksAsync(writer, linkedTokenSource.Token).GetAwaiter().GetResult();
    }
}
