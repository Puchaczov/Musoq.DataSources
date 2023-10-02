using System.Collections.Concurrent;
using Musoq.Schema.DataSources;

namespace Musoq.DataSources.InferrableDataSourceHelpers;

public abstract class AsyncRowsSourceBase<T> : RowSource
{
    public override IEnumerable<IObjectResolver> Rows
    {
        get
        {
            var chunkedSource = new BlockingCollection<IReadOnlyList<IObjectResolver>>();
            var workFinishedSignalizer = new CancellationTokenSource();
            var workFinishedToken = workFinishedSignalizer.Token;

            Task.Run(async () =>
            {
                try
                {
                    await CollectChunksAsync(chunkedSource);
                }
                catch (OperationCanceledException)
                {
                }
                finally
                {
                    chunkedSource.Add(new List<EntityResolver<T>>());
                    workFinishedSignalizer.Cancel();
                }
            });

            return new ChunkedSource<T>(chunkedSource, workFinishedToken);
        }
    }

    protected abstract Task CollectChunksAsync(BlockingCollection<IReadOnlyList<IObjectResolver>> chunkedSource);
}