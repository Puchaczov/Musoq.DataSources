using System.Collections.Concurrent;
using Musoq.Schema.DataSources;

namespace Musoq.DataSources.AsyncRowsSource;

public abstract class AsyncRowsSourceBase<T>(CancellationToken endWorkToken) : RowSource
{
    private Exception? _exception;

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

    protected abstract Task CollectChunksAsync(BlockingCollection<IReadOnlyList<IObjectResolver>> chunkedSource, CancellationToken cancellationToken);
    
    private Exception? GetParentException()
    {
        return _exception;
    }
}