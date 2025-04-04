using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;

namespace Musoq.DataSources.Roslyn.Components.NuGet;

internal class PackageVersionConcurrencyManager : IPackageVersionConcurrencyManager
{
    private readonly ConcurrentDictionary<(string PackageId, string Version), SemaphoreSlim> _locks = new();
    
    public async Task<IDisposable> AcquireLockAsync(string packageId, string version, CancellationToken cancellationToken)
    {
        var key = (packageId, version);
        SemaphoreSlim semaphore;

        lock (_locks)
        {
            semaphore = _locks.GetOrAdd(key, _ => new SemaphoreSlim(1, 1));
        }
        
        await semaphore.WaitAsync(cancellationToken);
        
        return new Releaser(this, semaphore, key);
    }

    private void ReleaseLock(SemaphoreSlim semaphore, (string PackageId, string Version) key)
    {
        lock (_locks)
        {
            semaphore.Release();
        
            if (semaphore.CurrentCount == 1)
            {
                _locks.TryRemove(key, out _);
            }
        }
    }
    
    private class Releaser(
        PackageVersionConcurrencyManager manager,
        SemaphoreSlim semaphore,
        (string PackageId, string Version) key)
        : IDisposable
    {
        private bool _disposed;
        
        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }
            
            manager.ReleaseLock(semaphore, key);
            _disposed = true;
        }
    }
}