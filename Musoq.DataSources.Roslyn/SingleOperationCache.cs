using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;

namespace Musoq.DataSources.Roslyn;

/// <summary>
///     Generic synchronization helper that ensures only one operation per key is executed concurrently
/// </summary>
/// <typeparam name="TKey">Type of the key used for synchronization</typeparam>
/// <typeparam name="TResult">Type of the result returned by operations</typeparam>
internal class SingleOperationCache<TKey, TResult> : IDisposable where TKey : notnull
{
    private readonly ConcurrentDictionary<TKey, TResult> _cache = new();
    private readonly object _semaphoreManagementLock = new();
    private readonly ConcurrentDictionary<TKey, SemaphoreReference> _semaphores = new();

    public void Dispose()
    {
        lock (_semaphoreManagementLock)
        {
            foreach (var semRef in _semaphores.Values)
                semRef.Dispose();

            _semaphores.Clear();
            _cache.Clear();
        }
    }

    /// <summary>
    ///     Executes an operation with guaranteed exclusive access for the given key
    /// </summary>
    /// <param name="key">The key to synchronize on</param>
    /// <param name="operation">The operation to execute if cache miss occurs</param>
    /// <param name="checkResult">Optional function to determine if result should be cached</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The result of the operation or cached value</returns>
    public async Task<TResult> GetOrAddAsync(
        TKey key,
        Func<Task<TResult>> operation,
        Func<TResult, bool>? checkResult = null,
        CancellationToken cancellationToken = default)
    {
        if (_cache.TryGetValue(key, out var cachedResult))
            return cachedResult;


        SemaphoreReference semRef;
        lock (_semaphoreManagementLock)
        {
            semRef = _semaphores.GetOrAdd(key, _ => new SemaphoreReference());
            semRef.AddReference();
        }

        try
        {
            await semRef.Semaphore.WaitAsync(cancellationToken);

            try
            {
                if (_cache.TryGetValue(key, out cachedResult))
                    return cachedResult;


                var result = await operation();


                if (checkResult == null || checkResult(result))
                    _cache.TryAdd(key, result);

                return result;
            }
            finally
            {
                semRef.Semaphore.Release();
            }
        }
        finally
        {
            lock (_semaphoreManagementLock)
            {
                var refCount = semRef.ReleaseReference();
                if (refCount == 0)
                {
                    _cache.TryRemove(key, out _);
                    if (_semaphores.TryRemove(key, out _)) semRef.Dispose();
                }
            }
        }
    }

    // Helper class to manage semaphore references
    private class SemaphoreReference : IDisposable
    {
        private int _refCount;
        public SemaphoreSlim Semaphore { get; } = new(1, 1);

        public void Dispose()
        {
            Semaphore.Dispose();
        }

        public void AddReference()
        {
            Interlocked.Increment(ref _refCount);
        }

        public int ReleaseReference()
        {
            return Interlocked.Decrement(ref _refCount);
        }
    }
}