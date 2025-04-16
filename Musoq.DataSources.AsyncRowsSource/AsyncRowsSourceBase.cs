using System.Collections.Concurrent;
using System.Diagnostics;
using Musoq.Schema.DataSources;

namespace Musoq.DataSources.AsyncRowsSource;

/// <summary>
/// Read rows asynchronously in chunks.
/// </summary>
/// <param name="queryCancelledToken">Token that signals the end of the work.</param>
/// <typeparam name="T">Type of the entity.</typeparam>
public abstract class AsyncRowsSourceBase<T>(CancellationToken queryCancelledToken) : RowSource
{
    private readonly TaskCompletionSource<Exception?> _exception = new();

    /// <summary>
    /// Enumerate rows.
    /// </summary>
    public override IEnumerable<IObjectResolver> Rows
    {
        get
        {
            var chunkedSourceBlockingCollection = new BlockingCollection<IReadOnlyList<IObjectResolver>>();
            var workFinishedCancellationTokenSource = new CancellationTokenSource();
            var workFinishedToken = workFinishedCancellationTokenSource.Token;
            var linkedTokenSource = CancellationTokenSource.CreateLinkedTokenSource(workFinishedToken, queryCancelledToken);
            var linkedToken = linkedTokenSource.Token;

            Task.Run(async () =>
            {
                try
                {
                    await CollectChunksAsync(chunkedSourceBlockingCollection, linkedToken);
                    _exception.SetResult(null);
                }
                catch (OperationCanceledException)
                {
                }
                catch (Exception exc)
                {
                    _exception.SetResult(exc);
                }
                finally
                {
                    chunkedSourceBlockingCollection.Add(new List<EntityResolver<T>>());
                    workFinishedCancellationTokenSource.Cancel();
                }
            });
            Task.Run(async () =>
            {
                while (!queryCancelledToken.IsCancellationRequested)
                {
                    ThreadPool.GetAvailableThreads(out int workerThreads, out int completionPortThreads);
                    ThreadPool.GetMaxThreads(out int maxWorkerThreads, out int maxCompletionPortThreads);
            
                    int usedWorkerThreads = maxWorkerThreads - workerThreads;
                    int percentUsed = (int)(((double)usedWorkerThreads / maxWorkerThreads) * 100);
            
                    if (percentUsed > 80)
                    {
                        Debug.WriteLine("High thread pool usage: {0}% ({1}/{2})",
                            percentUsed, usedWorkerThreads, maxWorkerThreads);
                    }
            
                    await Task.Delay(TimeSpan.FromMilliseconds(50), queryCancelledToken);
                }
            });

            return new ChunkedSource(chunkedSourceBlockingCollection, workFinishedToken, GetParentException);
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
        return _exception.Task.IsCompleted ? _exception.Task.Result : null;
    }
}