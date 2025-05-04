using System.Collections.Concurrent;
using Moq;
using Musoq.Schema.DataSources;
using AsyncRowsSourceBaseChunkEnumerator = Musoq.DataSources.AsyncRowsSource.ChunkEnumerator;

namespace Musoq.DataSources.AsyncRowsSource.Tests;

[TestClass]
public class ChunkEnumeratorTests
{
    [Timeout(15000)]
    [TestMethod]
    public void WhenIteratingThroughSingleChunk_ThenReturnsAllItems()
    {
        // Arrange
        var collection = new BlockingCollection<IReadOnlyList<IObjectResolver>>();
        var mockResolver1 = new Mock<IObjectResolver>();
        var mockResolver2 = new Mock<IObjectResolver>();
        var chunk = new List<IObjectResolver> { mockResolver1.Object, mockResolver2.Object };
        collection.Add(chunk);
        collection.CompleteAdding();
        
        var enumerator = new AsyncRowsSourceBaseChunkEnumerator(
            collection,
            () => null,
            CancellationToken.None);

        // Act & Assert
        Assert.IsTrue(enumerator.MoveNext());
        Assert.AreSame(mockResolver1.Object, enumerator.Current);
        
        Assert.IsTrue(enumerator.MoveNext());
        Assert.AreSame(mockResolver2.Object, enumerator.Current);
        
        Assert.IsFalse(enumerator.MoveNext());
    }

    [Timeout(15000)]
    [TestMethod]
    public void WhenIteratingThroughMultipleChunks_ThenReturnsAllItems()
    {
        // Arrange
        var collection = new BlockingCollection<IReadOnlyList<IObjectResolver>>();
        var mockResolver1 = new Mock<IObjectResolver>();
        var mockResolver2 = new Mock<IObjectResolver>();
        var mockResolver3 = new Mock<IObjectResolver>();
        
        var chunk1 = new List<IObjectResolver> { mockResolver1.Object };
        var chunk2 = new List<IObjectResolver> { mockResolver2.Object, mockResolver3.Object };
        
        collection.Add(chunk1);
        collection.Add(chunk2);
        collection.CompleteAdding();
        
        var enumerator = new AsyncRowsSourceBaseChunkEnumerator(
            collection,
            () => null,
            CancellationToken.None);

        // Act & Assert
        Assert.IsTrue(enumerator.MoveNext());
        Assert.AreSame(mockResolver1.Object, enumerator.Current);
        
        Assert.IsTrue(enumerator.MoveNext());
        Assert.AreSame(mockResolver2.Object, enumerator.Current);
        
        Assert.IsTrue(enumerator.MoveNext());
        Assert.AreSame(mockResolver3.Object, enumerator.Current);
        
        Assert.IsFalse(enumerator.MoveNext());
    }

    [Timeout(15000)]
    [TestMethod]
    public void WhenEncounterEmptyChunks_ThenSkipThemAndContinue()
    {
        // Arrange
        var collection = new BlockingCollection<IReadOnlyList<IObjectResolver>>();
        var mockResolver1 = new Mock<IObjectResolver>();
        var mockResolver2 = new Mock<IObjectResolver>();
        
        collection.Add(new List<IObjectResolver>());
        collection.Add(new List<IObjectResolver> { mockResolver1.Object });
        collection.Add(new List<IObjectResolver>());
        collection.Add(new List<IObjectResolver> { mockResolver2.Object });
        collection.CompleteAdding();

        var enumerator = new AsyncRowsSourceBaseChunkEnumerator(
            collection,
            () => null,
            CancellationToken.None);

        // Act & Assert
        Assert.IsTrue(enumerator.MoveNext());
        Assert.AreSame(mockResolver1.Object, enumerator.Current);
        
        Assert.IsTrue(enumerator.MoveNext());
        Assert.AreSame(mockResolver2.Object, enumerator.Current);
        
        Assert.IsFalse(enumerator.MoveNext());
    }

    [Timeout(15000)]
    [TestMethod]
    public void WhenParentThrowsException_ThenExceptionIsPropagated()
    {
        // Arrange
        var collection = new BlockingCollection<IReadOnlyList<IObjectResolver>>();
        var mockResolver = new Mock<IObjectResolver>();
        collection.Add(new List<IObjectResolver> { mockResolver.Object });
        collection.CompleteAdding();
        
        var expectedException = new InvalidOperationException("Test exception");
        var enumerator = new AsyncRowsSourceBaseChunkEnumerator(
            collection,
            () => expectedException,
            CancellationToken.None);

        // Act & Assert
        var actualException = Assert.ThrowsException<InvalidOperationException>(() => enumerator.MoveNext());
        Assert.AreSame(expectedException, actualException);
    }

    [Timeout(15000)]
    [TestMethod]
    public void WhenExceptionOccursDuringEnumeration_ThenExceptionIsPropagated()
    {
        // Arrange
        var collection = new BlockingCollection<IReadOnlyList<IObjectResolver>>();
        var mockResolver = new Mock<IObjectResolver>();
        collection.Add(new List<IObjectResolver> { mockResolver.Object });
        collection.CompleteAdding();
        
        var expectedException = new InvalidOperationException("Test exception");
        var exceptionTriggered = false;
        
        var enumerator = new AsyncRowsSourceBaseChunkEnumerator(
            collection,
            // ReSharper disable once AccessToModifiedClosure
            () => exceptionTriggered ? expectedException : null,
            CancellationToken.None);

        // Act & Assert
        Assert.IsTrue(enumerator.MoveNext());
        
        exceptionTriggered = true;
        var actualException = Assert.ThrowsException<InvalidOperationException>(() => enumerator.MoveNext());
        Assert.AreSame(expectedException, actualException);
    }

    [Timeout(15000)]
    [TestMethod]
    public void WhenCancellationTokenTriggeredBeforeEnumeration_ThenNoItemsReturned()
    {
        // Arrange
        var collection = new BlockingCollection<IReadOnlyList<IObjectResolver>>();
        collection.CompleteAdding();
        
        var enumerator = new AsyncRowsSourceBaseChunkEnumerator(
            collection,
            () => null,
            CancellationToken.None);

        // Act & Assert
        Assert.IsFalse(enumerator.MoveNext());
    }

    [Timeout(15000)]
    [TestMethod]
    public void WhenCancellationTokenTriggeredDuringEnumeration_ThenRemainingItemsProcessed()
    {
        // Arrange
        var collection = new BlockingCollection<IReadOnlyList<IObjectResolver>>();
        var mockResolver1 = new Mock<IObjectResolver>();
        var mockResolver2 = new Mock<IObjectResolver>();
        
        collection.Add(new List<IObjectResolver> { mockResolver1.Object });
        
        var tokenSource = new CancellationTokenSource();
        var enumerator = new AsyncRowsSourceBaseChunkEnumerator(
            collection,
            () => null,
            tokenSource.Token);

        // Act & Assert
        Assert.IsTrue(enumerator.MoveNext());
        Assert.AreSame(mockResolver1.Object, enumerator.Current);
        
        collection.Add(new List<IObjectResolver> { mockResolver2.Object }, tokenSource.Token);
        tokenSource.Cancel();
        collection.CompleteAdding();
        
        Assert.IsTrue(enumerator.MoveNext());
        Assert.AreSame(mockResolver2.Object, enumerator.Current);
        
        Assert.IsFalse(enumerator.MoveNext());
    }

    [Timeout(15000)]
    [TestMethod]
    public void WhenCollectionIsEmpty_ThenMoveNextReturnsFalse()
    {
        // Arrange
        var collection = new BlockingCollection<IReadOnlyList<IObjectResolver>>();
        collection.CompleteAdding();
        
        var enumerator = new AsyncRowsSourceBaseChunkEnumerator(
            collection,
            () => null,
            CancellationToken.None);

        // Act & Assert
        Assert.IsFalse(enumerator.MoveNext());
    }

    [Timeout(15000)]
    [TestMethod]
    public void WhenResetIsCalled_ThenThrowsNotSupportedException()
    {
        // Arrange
        var collection = new BlockingCollection<IReadOnlyList<IObjectResolver>>();
        var enumerator = new AsyncRowsSourceBaseChunkEnumerator(
            collection,
            () => null,
            CancellationToken.None);

        // Act & Assert
        Assert.ThrowsException<NotSupportedException>(() => enumerator.Reset());
    }

    [Timeout(15000)]
    [TestMethod]
    public void WhenAccessingCurrentBeforeMoveNext_ThenThrowsException()
    {
        // Arrange
        var collection = new BlockingCollection<IReadOnlyList<IObjectResolver>>();
        collection.CompleteAdding();
        var enumerator = new AsyncRowsSourceBaseChunkEnumerator(
            collection,
            () => null,
            CancellationToken.None);

        // Act & Assert
        Assert.ThrowsException<InvalidOperationException>(() => _ = enumerator.Current);
    }
    
    [Timeout(15000)]
    [TestMethod]
    public void WhenCollectionIsEmpty_AndExceptionThrown_ThenExceptionIsPropagated()
    {
        // Arrange
        var collection = new BlockingCollection<IReadOnlyList<IObjectResolver>>();
        collection.CompleteAdding();
        
        var expectedException = new InvalidOperationException("Test exception");
        var enumerator = new AsyncRowsSourceBaseChunkEnumerator(
            collection,
            () => expectedException,
            CancellationToken.None);

        // Act & Assert
        var actualException = Assert.ThrowsException<InvalidOperationException>(() => enumerator.MoveNext());
        Assert.AreSame(expectedException, actualException);
    }
    
    [Timeout(15000)]
    [TestMethod]
    public void WhenCollectionIsEmpty_AndExceptionThrown_WithoutCompleteAdding_ThenExceptionIsPropagated()
    {
        // Arrange
        var collection = new BlockingCollection<IReadOnlyList<IObjectResolver>>();
        
        var expectedException = new InvalidOperationException("Test exception");
        var enumerator = new AsyncRowsSourceBaseChunkEnumerator(
            collection,
            () => expectedException,
            CancellationToken.None);

        // Act & Assert
        var actualException = Assert.ThrowsException<InvalidOperationException>(() => enumerator.MoveNext());
        Assert.AreSame(expectedException, actualException);
    }
}
