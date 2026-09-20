using Musoq.Schema.DataSources;

namespace Musoq.DataSources.AsyncRowsSource.Tests;

[TestClass]
public class AsyncRowsSourceBaseTests
{
    [TestMethod]
    public void WhenCollectingRows_ThenEmitsTypedChunks()
    {
        var source = new TestRowsSource(CancellationToken.None, async (writer, _) =>
        {
            writer.Write([1, 2]);
            await Task.Yield();
            writer.Write([3]);
        });

        var chunks = source.Chunks.ToArray();

        Assert.AreEqual(2, chunks.Length);
        CollectionAssert.AreEqual(new[] { 1, 2 }, chunks[0].ToArray());
        CollectionAssert.AreEqual(new[] { 3 }, chunks[1].ToArray());
    }

    [TestMethod]
    public void WhenQueryTokenIsCancelled_ThenLinkedTokenIsCancelled()
    {
        using var queryCancellation = new CancellationTokenSource();
        var observedCancellation = false;
        var source = new TestRowsSource(queryCancellation.Token, (writer, token) =>
        {
            queryCancellation.Cancel();
            observedCancellation = token.IsCancellationRequested;
            writer.Write([1]);
            return Task.CompletedTask;
        });

        _ = source.Chunks.ToArray();

        Assert.IsTrue(observedCancellation);
    }

    [TestMethod]
    public void WhenCollectionObservesCancellation_ThenCompletesWithoutRows()
    {
        using var queryCancellation = new CancellationTokenSource();
        var source = new TestRowsSource(queryCancellation.Token, (_, token) =>
        {
            queryCancellation.Cancel();
            token.ThrowIfCancellationRequested();
            return Task.CompletedTask;
        });

        var chunks = source.Chunks.ToArray();

        Assert.AreEqual(0, chunks.Length);
    }

    [TestMethod]
    public void WhenCollectingThrows_ThenExceptionIsPropagated()
    {
        var expectedException = new InvalidOperationException("test exception");
        var source = new TestRowsSource(CancellationToken.None, (_, _) => throw expectedException);

        var actualException = Assert.ThrowsException<InvalidOperationException>(() => source.Chunks.ToArray());

        Assert.AreSame(expectedException, actualException);
    }

    private sealed class TestRowsSource(
        CancellationToken cancellationToken,
        Func<IChunkWriter<int>, CancellationToken, Task> collect)
        : AsyncRowsSourceBase<int>(cancellationToken)
    {
        protected override Task CollectChunksAsync(IChunkWriter<int> writer, CancellationToken cancellationToken)
        {
            return collect(writer, cancellationToken);
        }
    }
}
