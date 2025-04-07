using System;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Musoq.DataSources.Roslyn.Components;

internal class AlwaysUpdateDirectoryView<TKey, TDestinationValue> : IDisposable
    where TKey : notnull
    where TDestinationValue : class
{
    private readonly string _directoryPath;
    private readonly IFileWatcher _fileWatcher;
    private readonly ConcurrentDictionary<string, TDestinationValue> _cachedItems;
    private readonly IFileSystem _fileSystem;
    private readonly BlockingCollection<FileInfo> _synchronizationQueue = new();
    private readonly BlockingCollection<(TKey key, TDestinationValue Value)> _itemsToStore = new();
    private readonly CancellationTokenSource _cancellationTokenSource = new();
    private readonly Func<string, IFileSystem, CancellationToken, Task<TDestinationValue>> _getDestinationValueAsync;
    private readonly Func<TKey, string> _convertKeyToPath;
    private readonly Func<string, TDestinationValue, IFileSystem, CancellationToken, Task> _updateDirectoryAsync;
    private readonly ManualResetEventSlim _synchronizeStarted = new(false);
    private readonly ManualResetEventSlim _storeStarted = new(false);
    private readonly Timer _backupPollingTimer;
    private readonly string _mutexNamePrefix;

    private bool _isDisposed;

    public event EventHandler<string>? ItemStored;
    public event EventHandler<string>? ItemLoaded;
    public event EventHandler<string>? ItemRemoved;
    
    public AlwaysUpdateDirectoryView(
        string directoryPath, 
        Func<string, IFileSystem, CancellationToken, Task<TDestinationValue>> getDestinationValueAsync, 
        Func<TKey, string> convertKeyToPath, 
        Func<string, TDestinationValue, IFileSystem, CancellationToken, Task> updateDirectoryAsync,
        IFileSystem? fileSystem = null,
        IFileWatcher? fileWatcher = null)
    {
        _directoryPath = directoryPath;
        _fileWatcher = fileWatcher ?? new DefaultFileWatcher(directoryPath, "*.json", true);
        _fileWatcher.EnableRaisingEvents = false;
        _fileWatcher.Created += OnCreated;
        _fileWatcher.Deleted += OnDeleted;
        _fileWatcher.Renamed += OnRenamed;
        _cachedItems = new ConcurrentDictionary<string, TDestinationValue>();
        _fileSystem = fileSystem ?? new DefaultFileSystem();
        _getDestinationValueAsync = getDestinationValueAsync;
        _convertKeyToPath = convertKeyToPath;
        _updateDirectoryAsync = updateDirectoryAsync;
        
        _mutexNamePrefix = "Musoq_AUDV_";
        
        Task.Run(async () => await SynchronizeAsync(_cancellationTokenSource.Token));
        Task.Run(async () => await StoreWithinDirectoryAsync(_cancellationTokenSource.Token));

        foreach (var file in _fileSystem.GetFiles(_directoryPath, false, _cancellationTokenSource.Token))
        {
            _synchronizationQueue.Add(new FileInfo(file));
        }
        
        _synchronizeStarted.Wait();
        _storeStarted.Wait();
        
        _fileWatcher.EnableRaisingEvents = true;

        _backupPollingTimer = new Timer(ScanDirectory, null, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(3));
    }

    public bool TryGetValue(TKey key, out TDestinationValue? value)
    {
        if (_isDisposed)
        {
            value = null;
            return false;
        }

        var keyPath = _convertKeyToPath(key);
        
        if (_cachedItems.TryGetValue(keyPath, out value))
        {
            return true;
        }

        value = null;
        return false;
    }

    public void Add(TKey key, TDestinationValue destinationValue)
    {
        if (_isDisposed)
            return;
        
        _itemsToStore.Add((key, destinationValue));
    }

    public void Dispose()
    {
        if (_isDisposed)
            return;
        
        _isDisposed = true;
            
        _fileWatcher.Created -= OnCreated;
        _fileWatcher.Deleted -= OnDeleted;
        _fileWatcher.Renamed -= OnRenamed;
        _fileWatcher.Dispose();
            
        _cancellationTokenSource.Cancel();
        
        ItemStored = null;
        ItemLoaded = null;
        ItemRemoved = null;
            
        _cachedItems.Clear();
        _synchronizationQueue.Dispose();
        _itemsToStore.Dispose();
        _backupPollingTimer.Dispose();
    }

    private void OnCreated(object sender, FileSystemEventArgs e)
    {
        if (_isDisposed) 
            return;

        if (e.ChangeType != WatcherChangeTypes.Created) 
            return;
        
        var filePath = Path.Combine(_directoryPath, e.Name!);
        var fileInfo = new FileInfo(filePath);
        
        _synchronizationQueue.Add(fileInfo);
    }

    private void OnDeleted(object sender, FileSystemEventArgs e)
    {
        if (_isDisposed) 
            return;

        if (e.ChangeType != WatcherChangeTypes.Deleted) 
            return;
        
        var filePath = Path.Combine(_directoryPath, e.Name!);
        var fileInfo = new FileInfo(filePath);
        
        _synchronizationQueue.Add(fileInfo);
    }

    private void OnRenamed(object sender, RenamedEventArgs e)
    {
        if (_isDisposed) 
            return;

        if (e.ChangeType != WatcherChangeTypes.Renamed) 
            return;
        
        var filePath = Path.Combine(_directoryPath, e.Name!);
        var oldFilePath = Path.Combine(_directoryPath, e.OldName!);
        var newFileInfo = new FileInfo(filePath);
        var oldFileInfo = new FileInfo(oldFilePath);
        
        _synchronizationQueue.Add(newFileInfo);
        _synchronizationQueue.Add(oldFileInfo);
    }

    private async Task SynchronizeAsync(CancellationToken cancellationToken)
    {
        _synchronizeStarted.Set();
        
        while (!cancellationToken.IsCancellationRequested)
        {
            await Parallel.ForEachAsync(_synchronizationQueue.GetConsumingEnumerable(cancellationToken), cancellationToken, async (fileInfo, token) =>
            {
                if (!_fileSystem.IsFileExists(fileInfo.FullName))
                {
                    _cachedItems.TryRemove(fileInfo.Name, out _);
                    ItemRemoved?.Invoke(this, fileInfo.Name);
                    return;
                }
                
                if (_cachedItems.TryGetValue(fileInfo.Name, out _))
                {
                    return;
                }

                var filePath = fileInfo.FullName;
                var mutexName = _mutexNamePrefix + TurnPathIntoMutexName(filePath);

                await ExecuteWithMutexAsync(mutexName, token, async () =>
                {
                    var item = await _getDestinationValueAsync(filePath, _fileSystem, token);
                    _cachedItems.AddOrUpdate(fileInfo.Name,
                        _ => item,
                        (_, destinationValue) => destinationValue);
                    
                    ItemLoaded?.Invoke(this, fileInfo.Name);
                }, () => _cachedItems.TryRemove(fileInfo.FullName, out _));
            });
        }
    }

    private async Task StoreWithinDirectoryAsync(CancellationToken cancellationToken)
    {
        _storeStarted.Set();
        
        while (!cancellationToken.IsCancellationRequested)
        {
            await Parallel.ForEachAsync(_itemsToStore.GetConsumingEnumerable(cancellationToken), cancellationToken, async (item, token) =>
            {
                var keyPath = _convertKeyToPath(item.key);
                var filePath = IFileSystem.Combine(_directoryPath, keyPath);
                var mutexName = _mutexNamePrefix + TurnPathIntoMutexName(filePath);

                await ExecuteWithMutexAsync(mutexName, token, async () =>
                {
                    await _updateDirectoryAsync(filePath, item.Value, _fileSystem, token);
                    
                    _cachedItems.AddOrUpdate(keyPath,
                        _ => item.Value,
                        (_, _) => item.Value);
                    
                    ItemStored?.Invoke(this, _convertKeyToPath(item.key));
                }, null);
            });
        }
    }

    private void ScanDirectory(object? state)
    {
        if (_isDisposed)
            return;
        
        try
        {
            var existingFiles = _fileSystem.GetFiles(_directoryPath, false, _cancellationTokenSource.Token);
            var cachedKeys = _cachedItems.Keys.ToList();
        
            foreach (var file in existingFiles)
            {
                var fileInfo = new FileInfo(file);
                if (!cachedKeys.Contains(fileInfo.Name) && _fileSystem.IsFileExists(fileInfo.FullName))
                {
                    _synchronizationQueue.Add(fileInfo);
                }
            }
        
            foreach (var cachedKey in cachedKeys)
            {
                var path = Path.Combine(_directoryPath, cachedKey);
                if (!_fileSystem.IsFileExists(path))
                {
                    _synchronizationQueue.Add(new FileInfo(path));
                }
            }
        }
        catch (Exception)
        {
            // Log the specific exception during backup polling
            // Consider implementing a retry mechanism or adaptive polling interval
        }
    }

    private static string TurnPathIntoMutexName(string directoryPath)
    {
        var path = directoryPath.Replace(Path.DirectorySeparatorChar, '_');
        path = path.Replace(Path.AltDirectorySeparatorChar, '_');
        path = path.Replace(Path.VolumeSeparatorChar, '_');
        path = path.Replace(":", "_");
        return path;
    }

    private static async Task ExecuteWithMutexAsync(string mutexName, CancellationToken token, Func<Task> operation, Action? onIoException)
    {
        Mutex? mutex = null;
        var mutexAcquired = false;
        
        try
        {
            mutex = new Mutex(false, mutexName);
            
            try
            {
                while (!(mutexAcquired = mutex.WaitOne(1000)))
                {
                    token.ThrowIfCancellationRequested();
                }
                
                await operation();
            }
            catch (AbandonedMutexException)
            {
                mutexAcquired = true;
                
                try
                {
                    await operation();
                }
                catch (IOException)
                {
                    onIoException?.Invoke();
                }
                catch (Exception)
                {
                    // Log the specific exception
                    // Consider retrying the operation or adding the item back to the queue
                }
            }
            catch (IOException)
            {
                onIoException?.Invoke();
            }
            catch (Exception)
            {
                // Log the specific exception
                // Consider retrying the operation or adding the item back to the queue
            }
            finally
            {
                if (mutexAcquired)
                {
                    try
                    {
                        mutex.ReleaseMutex();
                    }
                    catch (ApplicationException)
                    {
                        // Log that we couldn't release the mutex
                        // This can happen if the mutex was released elsewhere or the thread ownership changed
                    }
                }
            }
        }
        finally
        {
            mutex?.Dispose();
        }
    }
}