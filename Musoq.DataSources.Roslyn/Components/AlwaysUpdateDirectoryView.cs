using System;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Musoq.DataSources.Roslyn.Components;

internal class AlwaysUpdateDirectoryView<TKey, TDestinationValue> : IDisposable
    where TKey : notnull
    where TDestinationValue : class
{
    private readonly Timer _backupPollingTimer;
    private readonly ConcurrentDictionary<string, TDestinationValue> _cachedItems;
    private readonly CancellationTokenSource _cancellationTokenSource = new();
    private readonly Func<TKey, string> _convertKeyToPath;
    private readonly string _directoryPath;
    private readonly IFileSystem _fileSystem;
    private readonly IFileWatcher _fileWatcher;
    private readonly Func<string, IFileSystem, CancellationToken, TDestinationValue> _getDestinationValue;
    private readonly Channel<(TKey key, TDestinationValue Value)> _itemsToStore =
        Channel.CreateUnbounded<(TKey key, TDestinationValue Value)>();
    private readonly ILogger? _logger;
    private readonly string _mutexNamePrefix;
    private readonly ManualResetEventSlim _storeStarted = new(false);
    private readonly Channel<FileInfo> _synchronizationQueue = Channel.CreateUnbounded<FileInfo>();
    private readonly ManualResetEventSlim _synchronizeStarted = new(false);
    private readonly Action<string, TDestinationValue, IFileSystem, CancellationToken> _updateDirectory;

    private bool _isDisposed;

    public AlwaysUpdateDirectoryView(
        string directoryPath,
        Func<string, IFileSystem, CancellationToken, TDestinationValue> getDestinationValue,
        Func<TKey, string> convertKeyToPath,
        Action<string, TDestinationValue, IFileSystem, CancellationToken> updateDirectory,
        IFileSystem? fileSystem = null,
        IFileWatcher? fileWatcher = null,
        ILogger? logger = null)
    {
        _directoryPath = directoryPath;
        _fileWatcher = fileWatcher ?? new DefaultFileWatcher(directoryPath, "*.json", true);
        _fileWatcher.EnableRaisingEvents = false;
        _fileWatcher.Created += OnCreated;
        _fileWatcher.Deleted += OnDeleted;
        _fileWatcher.Renamed += OnRenamed;
        _cachedItems = new ConcurrentDictionary<string, TDestinationValue>();
        _fileSystem = fileSystem ?? new DefaultFileSystem();
        _getDestinationValue = getDestinationValue;
        _convertKeyToPath = convertKeyToPath;
        _updateDirectory = updateDirectory;
        _logger = logger;

        _mutexNamePrefix = "Musoq_AUDV_";

        Task.Run(async () => await SynchronizeAsync(_cancellationTokenSource.Token));
        Task.Run(async () => await StoreWithinDirectoryAsync(_cancellationTokenSource.Token));

        foreach (var file in _fileSystem.GetFiles(_directoryPath, false, _cancellationTokenSource.Token))
            _synchronizationQueue.Writer.TryWrite(new FileInfo(file));

        _synchronizeStarted.Wait();
        _storeStarted.Wait();

        _fileWatcher.EnableRaisingEvents = true;

        _backupPollingTimer = new Timer(ScanDirectory, null, TimeSpan.FromSeconds(0), TimeSpan.FromSeconds(10));
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
        _synchronizationQueue.Writer.TryComplete();
        _itemsToStore.Writer.TryComplete();

        ItemStored = null;
        ItemLoaded = null;
        ItemRemoved = null;

        _cachedItems.Clear();
        _backupPollingTimer.Dispose();
    }

    public event EventHandler<string>? ItemStored;
    public event EventHandler<string>? ItemLoaded;
    public event EventHandler<string>? ItemRemoved;

    public bool TryGetValue(TKey key, out TDestinationValue? value)
    {
        if (_isDisposed)
        {
            value = null;
            return false;
        }

        var keyPath = _convertKeyToPath(key);

        if (_cachedItems.TryGetValue(keyPath, out value)) return true;

        value = null;
        return false;
    }

    public void Add(TKey key, TDestinationValue destinationValue)
    {
        if (_isDisposed)
            return;

        _itemsToStore.Writer.TryWrite((key, destinationValue));
    }

    private void OnCreated(object sender, FileSystemEventArgs e)
    {
        if (_isDisposed)
            return;

        if (e.ChangeType != WatcherChangeTypes.Created)
            return;

        var filePath = Path.Combine(_directoryPath, e.Name!);
        var fileInfo = new FileInfo(filePath);

        _synchronizationQueue.Writer.TryWrite(fileInfo);
    }

    private void OnDeleted(object sender, FileSystemEventArgs e)
    {
        if (_isDisposed)
            return;

        if (e.ChangeType != WatcherChangeTypes.Deleted)
            return;

        var filePath = Path.Combine(_directoryPath, e.Name!);
        var fileInfo = new FileInfo(filePath);

        _synchronizationQueue.Writer.TryWrite(fileInfo);
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

        _synchronizationQueue.Writer.TryWrite(newFileInfo);
        _synchronizationQueue.Writer.TryWrite(oldFileInfo);
    }

    private async Task SynchronizeAsync(CancellationToken cancellationToken)
    {
        _synchronizeStarted.Set();

        try
        {
            await Parallel.ForEachAsync(_synchronizationQueue.Reader.ReadAllAsync(cancellationToken),
                cancellationToken, (fileInfo, token) =>
                {
                    if (!_fileSystem.IsFileExists(fileInfo.FullName))
                    {
                        _cachedItems.TryRemove(fileInfo.Name, out _);
                        ItemRemoved?.Invoke(this, fileInfo.Name);
                        return ValueTask.CompletedTask;
                    }

                    if (_cachedItems.TryGetValue(fileInfo.Name, out _)) return ValueTask.CompletedTask;

                    var filePath = fileInfo.FullName;
                    var mutexName = _mutexNamePrefix + TurnPathIntoMutexName(filePath);

                    ExecuteWithMutex(mutexName, () =>
                    {
                        var item = _getDestinationValue(filePath, _fileSystem, token);

                        _cachedItems.AddOrUpdate(fileInfo.Name,
                            _ => item,
                            (_, destinationValue) => destinationValue);

                        ItemLoaded?.Invoke(this, fileInfo.Name);
                    }, () => _cachedItems.TryRemove(fileInfo.FullName, out _), _logger, token);
                    return ValueTask.CompletedTask;
                });
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
    }

    private async Task StoreWithinDirectoryAsync(CancellationToken cancellationToken)
    {
        _storeStarted.Set();

        try
        {
            await Parallel.ForEachAsync(_itemsToStore.Reader.ReadAllAsync(cancellationToken), cancellationToken,
                (item, token) =>
                {
                    var keyPath = _convertKeyToPath(item.key);
                    var filePath = IFileSystem.Combine(_directoryPath, keyPath);
                    var mutexName = _mutexNamePrefix + TurnPathIntoMutexName(filePath);

                    ExecuteWithMutex(mutexName, () =>
                    {
                        var value = item.Value;
                        _updateDirectory(filePath, value, _fileSystem, token);

                        _cachedItems.AddOrUpdate(keyPath,
                            _ => value,
                            (_, _) => value);

                        ItemStored?.Invoke(this, _convertKeyToPath(item.key));
                    }, null, _logger, token);
                    return ValueTask.CompletedTask;
                });
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
    }

    private void ScanDirectory(object? state)
    {
        if (_isDisposed)
            return;

        try
        {
            var existingFiles = _fileSystem.GetFiles(_directoryPath, false, _cancellationTokenSource.Token);
            var cachedKeys = _cachedItems;

            foreach (var file in existingFiles)
            {
                var fileInfo = new FileInfo(file);
                if (!cachedKeys.ContainsKey(fileInfo.Name) && _fileSystem.IsFileExists(fileInfo.FullName))
                    _synchronizationQueue.Writer.TryWrite(fileInfo);
            }

            foreach (var path in cachedKeys.Keys.Select(cachedKey => IFileSystem.Combine(_directoryPath, cachedKey))
                         .Where(path => !_fileSystem.IsFileExists(path)))
                _synchronizationQueue.Writer.TryWrite(new FileInfo(path));
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error while scanning directory: {DirectoryPath}", _directoryPath);
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

    private static void ExecuteWithMutex(string mutexName, Action operation, Action? onIoException, ILogger? logger,
        CancellationToken token)
    {
        using var mutex = new Mutex(false, mutexName);
        var mutexAcquired = false;

        try
        {
            var totalWaitMs = 0;
            const int maxWaitMs = 120000;
            const int waitIntervalMs = 1000;

            while (true)
            {
                token.ThrowIfCancellationRequested();

                try
                {
                    mutexAcquired = mutex.WaitOne(waitIntervalMs);
                    if (mutexAcquired)
                        break;

                    totalWaitMs += waitIntervalMs;
                    if (totalWaitMs >= maxWaitMs)
                        throw new TimeoutException(
                            $"Could not acquire mutex '{mutexName}' within {maxWaitMs / 1000} seconds.");
                }
                catch (AbandonedMutexException)
                {
                    mutexAcquired = true;
                    break;
                }
            }

            using var operationCts = CancellationTokenSource.CreateLinkedTokenSource(token);
            operationCts.CancelAfter(TimeSpan.FromMinutes(5));

            try
            {
                operation();
            }
            catch (IOException)
            {
                onIoException?.Invoke();
            }
        }
        finally
        {
            if (mutexAcquired)
                try
                {
                    mutex.ReleaseMutex();
                }
                catch (ApplicationException)
                {
                    logger?.LogWarning("Failed to release mutex '{MutexName}'. It may be held by another process.",
                        mutexName);
                }
        }
    }
}
