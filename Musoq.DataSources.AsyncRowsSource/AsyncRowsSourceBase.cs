using System.Collections.Concurrent;
using Musoq.Schema.DataSources;

namespace Musoq.DataSources.AsyncRowsSource;

/// <summary>
/// Read rows asynchronously in chunks.
/// </summary>
/// <param name="endWorkToken">Token that signals the end of the work.</param>
/// <typeparam name="T">Type of the entity.</typeparam>
public abstract class AsyncRowsSourceBase<T>(CancellationToken endWorkToken) : RowSource
{
    private Exception? _exception;

    /// <summary>
    /// Enumerate rows.
    /// </summary>
    public override IEnumerable<IObjectResolver> Rows
    {
        get
        {
            var chunkedSource = new BlockingCollection<IReadOnlyList<IObjectResolver>>();
            var workFinishedCancellationTokenSource = new CancellationTokenSource();
            var workFinishedToken = workFinishedCancellationTokenSource.Token;
            var errorCancellationTokenSource = new CancellationTokenSource();
            var linkedTokenSource = CancellationTokenSource.CreateLinkedTokenSource(workFinishedToken, errorCancellationTokenSource.Token, endWorkToken);
            var linkedToken = linkedTokenSource.Token;

            Task.Run(async () =>
            {
                try
                {
                    await CollectChunksAsync(chunkedSource, linkedToken);
                }
                catch (OperationCanceledException)
                {
                }
                catch (Exception exc)
                {
                    _exception = exc;
                }
                finally
                {
                    chunkedSource.Add(new List<EntityResolver<T>>());
                    workFinishedCancellationTokenSource.Cancel();
                }
            });

            return new ChunkedSource(chunkedSource, workFinishedToken, GetParentException);
        }
    }

    /// <summary>
    /// Collect chunks of rows.
    /// </summary>
    /// <param name="chunkedSource">Collection of chunks.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Task.</returns>
    protected abstract Task CollectChunksAsync(BlockingCollection<IReadOnlyList<IObjectResolver>> chunkedSource, CancellationToken cancellationToken);
    
    private Exception? GetParentException()
    {
        return _exception;
    }
}