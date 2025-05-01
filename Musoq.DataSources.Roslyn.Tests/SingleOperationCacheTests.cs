namespace Musoq.DataSources.Roslyn.Tests;

[TestClass]
public class SingleOperationCacheTests
{
    [TestMethod]
    public async Task GetOrAddAsync_WhenCalledWithSameKey_OperationsAreSynchronous_ShouldRemoveFromCacheAfterFirstCall()
    {
        // Since the cache is only to make sure a single operation is executed, when no other threads are awaiting for value,
        // the value will be removed since the lifespan is only that big as the queue of the threads awaiting for the value.
        
        // Arrange
        var cache = new SingleOperationCache<string, int>();
        var operationCallCount = 0;
        
        // Act
        var result1 = await cache.GetOrAddAsync("key1", async () => 
        {
            operationCallCount++;
            await Task.Delay(10);
            return 42;
        });
        
        var result2 = await cache.GetOrAddAsync("key1", async () => 
        {
            operationCallCount++;
            await Task.Delay(10);
            return 100;
        });
        
        // Assert
        Assert.AreEqual(42, result1);
        Assert.AreEqual(100, result2); // Should return second value
        Assert.AreEqual(2, operationCallCount); // Operation should be called only once
    }
    
    [TestMethod]
    public async Task GetOrAddAsync_WhenCalledWithDifferentKeys_ExecutesOperationForEachKey()
    {
        // Arrange
        var cache = new SingleOperationCache<string, int>();
        var callCounts = new Dictionary<string, int>();
        
        // Act
        var tasks = new List<Task<int>>();
        for (var i = 0; i < 5; i++)
        {
            var key = $"key{i}";
            callCounts[key] = 0;

            var captured = i;
            var task = cache.GetOrAddAsync(key, async () =>
            {
                callCounts[key]++;
                await Task.Delay(10);
                return captured * 10;
            });
            
            tasks.Add(task);
        }
        
        await Task.WhenAll(tasks);
        
        // Assert
        for (var i = 0; i < 5; i++)
        {
            Assert.AreEqual(i * 10, tasks[i].Result);
            Assert.AreEqual(1, callCounts[$"key{i}"]); // Each operation should be called exactly once
        }
    }
    
    [TestMethod]
    public async Task GetOrAddAsync_WhenCalledConcurrentlyWithSameKey_ExecutesOperationOnce()
    {
        // Arrange
        var cache = new SingleOperationCache<string, int>();
        var operationCallCount = 0;
        const int concurrentCalls = 10;
        
        // Act
        var tasks = Enumerable.Range(0, concurrentCalls).Select(_ =>
            cache.GetOrAddAsync("sameKey", async () =>
            {
                Interlocked.Increment(ref operationCallCount);
                await Task.Delay(50); // Delay to increase chance of concurrency
                return 42;
            })).ToArray();
        
        await Task.WhenAll(tasks);
        
        // Assert
        Assert.AreEqual(1, operationCallCount);
        foreach (var result in tasks)
        {
            Assert.AreEqual(42, result.Result);
        }
    }
    
    [TestMethod]
    public async Task GetOrAddAsync_WithCheckResult_OnlyCachesWhenCheckPasses()
    {
        // Arrange
        var cache = new SingleOperationCache<string, int>();
        var operationCallCount = 0;
        var semaphore = new SemaphoreSlim(0, 1); // Semafor do kontrolowania, kiedy druga operacja się zakończy
        
        // Act - pierwszy wywołanie z warunkiem, który nie zostanie spełniony
        var result1 = await cache.GetOrAddAsync("key1", 
            async () =>
            {
                operationCallCount++;
                await Task.Delay(10);
                return 15;
            },
            result => result > 20 // Ten warunek nie będzie spełniony, więc wynik nie trafi do pamięci podręcznej
        );
        
        // Rozpoczynamy drugą operację, ale nie pozwalamy jej się zakończyć od razu
        var result2Task = cache.GetOrAddAsync("key1", 
            async () =>
            {
                operationCallCount++;
                await semaphore.WaitAsync(); // Czekamy na sygnał
                return 30;
            },
            result => result > 20 // Ten warunek będzie spełniony, więc wynik trafi do pamięci podręcznej
        );
        
        // Dajemy drugiej operacji chwilę na pobranie semafora wewnątrz SingleOperationCache
        await Task.Delay(50);
        
        // Rozpoczynamy trzecią operację, która powinna być w kolejce za drugą
        var result3Task = cache.GetOrAddAsync("key1", 
            async () =>
            {
                operationCallCount++;
                await Task.Delay(10);
                return 50;
            }
        );
        
        // Teraz pozwalamy drugiej operacji się zakończyć
        semaphore.Release();
        
        // Czekamy na zakończenie obu zadań
        var result2 = await result2Task;
        var result3 = await result3Task;
        
        // Assert
        Assert.AreEqual(15, result1);
        Assert.AreEqual(30, result2);
        Assert.AreEqual(30, result3); // Powinien użyć wartości z pamięci podręcznej z drugiego wywołania
        Assert.AreEqual(2, operationCallCount); // Operacja powinna być wywołana tylko dwa razy
    }
    
    [TestMethod]
    public async Task GetOrAddAsync_WithCancellation_CancelsOperation()
    {
        // Arrange
        var cache = new SingleOperationCache<string, int>();
        using var cts = new CancellationTokenSource();
        
        // Act & Assert
        await cts.CancelAsync();
        await Assert.ThrowsExceptionAsync<TaskCanceledException>(async () =>
        {
            await cache.GetOrAddAsync("key1", 
                async () =>
                {
                    await Task.Delay(1000, cts.Token);
                    return 42;
                }, 
                cancellationToken: cts.Token);
        });
    }
}

