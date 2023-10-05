using System.Collections.Concurrent;
using Musoq.Schema.DataSources;

namespace Musoq.DataSources.InferrableDataSourceHelpers;

public abstract class AsyncRowsSourceBase<T> : RowSource
{
    private readonly CancellationToken _endWorkToken;
    
    private Exception? _exception;

    protected AsyncRowsSourceBase(CancellationToken endWorkToken)
    {
        _endWorkToken = endWorkToken;
        _exception = null;
    }

    public override IEnumerable<IObjectResolver> Rows
    {
        get
        {
            var chunkedSource = new BlockingCollection<IReadOnlyList<IObjectResolver>>();
            var workFinishedSignalizer = new CancellationTokenSource();
            var workFinishedToken = workFinishedSignalizer.Token;
            var errorSignalizer = new CancellationTokenSource();
            var linkedTokenSource = CancellationTokenSource.CreateLinkedTokenSource(workFinishedToken, errorSignalizer.Token, _endWorkToken);
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
                    workFinishedSignalizer.Cancel();
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