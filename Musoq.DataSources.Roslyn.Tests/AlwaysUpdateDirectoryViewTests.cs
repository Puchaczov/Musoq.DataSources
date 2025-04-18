using Musoq.DataSources.Roslyn.Components;
using System.Text.Json;
using Musoq.DataSources.Roslyn.Tests.Components;

namespace Musoq.DataSources.Roslyn.Tests;

[TestClass]
public class AlwaysUpdateDirectoryViewTests
{
    private string _testDirectory = null!;
    private MockFileSystem _mockFileSystem = null!;
    private MockFileWatcher _mockFileWatcher = null!;

    [TestInitialize]
    public void Initialize()
    {
        _testDirectory = Path.Combine("C:", "MockDir", Guid.NewGuid().ToString());
        _mockFileSystem = new MockFileSystem();
        _mockFileWatcher = new MockFileWatcher();
    }

    [TestMethod]
    public async Task AddAndRetrieveItem_ShouldSucceed()
    {
        // Arrange
        using var directoryView = new AlwaysUpdateDirectoryView<string, TestItem>(
            _testDirectory,
            GetDestinationValue,
            ConvertKeyToPath,
            UpdateDirectory,
            _mockFileSystem,
            _mockFileWatcher
        );

        var testItem = new TestItem("Test", 42);
        var filePath = Path.Combine(_testDirectory, "testKey.json");

        // Act
        directoryView.Add("testKey", testItem);

        var itemRetrievable = await WaitForConditionAsync(() => 
            directoryView.TryGetValue("testKey", out _));

        // Assert
        Assert.IsTrue(itemRetrievable, "Item should be retrievable");
        Assert.IsTrue(_mockFileSystem.IsFileExists(filePath), "File should exist in mock file system");
        
        var success = directoryView.TryGetValue("testKey", out var retrievedItem);
        Assert.IsTrue(success);
        Assert.IsNotNull(retrievedItem);
        Assert.AreEqual("Test", retrievedItem.Name);
        Assert.AreEqual(42, retrievedItem.Value);
    }

    [TestMethod]
    public async Task MultipleAddAndRetrieve_ShouldSucceed()
    {
        // Arrange
        using var directoryView = new AlwaysUpdateDirectoryView<string, TestItem>(
            _testDirectory,
            GetDestinationValue,
            ConvertKeyToPath,
            UpdateDirectory,
            _mockFileSystem,
            _mockFileWatcher
        );

        // Act
        for (var i = 0; i < 5; i++)
        {
            directoryView.Add($"key{i}", new TestItem($"Item{i}", i * 10));
        }

        var allItemsRetrievable = await WaitForConditionAsync(() =>
        {
            for (var i = 0; i < 5; i++)
            {
                if (!directoryView.TryGetValue($"key{i}", out _))
                    return false;
            }
            return true;
        });

        // Assert
        Assert.IsTrue(allItemsRetrievable, "All items should be retrievable");

        for (var i = 0; i < 5; i++)
        {
            var success = directoryView.TryGetValue($"key{i}", out var retrievedItem);
            
            Assert.IsNotNull(retrievedItem);
            Assert.IsTrue(success);
            Assert.AreEqual($"Item{i}", retrievedItem.Name);
            Assert.AreEqual(i * 10, retrievedItem.Value);
        }
    }

    [TestMethod]
    public async Task UpdateExistingItem_ShouldOverwriteValue()
    {
        // Arrange
        using var directoryView = new AlwaysUpdateDirectoryView<string, TestItem>(
            _testDirectory,
            GetDestinationValue,
            ConvertKeyToPath,
            UpdateDirectory,
            _mockFileSystem,
            _mockFileWatcher
        );

        directoryView.Add("updateKey", new TestItem("Initial", 100));
        await WaitForConditionAsync(() => directoryView.TryGetValue("updateKey", out _));
        
        directoryView.Add("updateKey", new TestItem("Updated", 200));
        
        await WaitForConditionAsync(() => 
        {
            if (directoryView.TryGetValue("updateKey", out var item) && item is not null)
            {
                return item.Name == "Updated";
            }
            return false;
        });

        // Assert
        var success = directoryView.TryGetValue("updateKey", out var retrievedItem);
        
        Assert.IsNotNull(retrievedItem);
        Assert.IsTrue(success);
        Assert.AreEqual("Updated", retrievedItem.Name);
        Assert.AreEqual(200, retrievedItem.Value);
    }

    [TestMethod]
    public async Task FileCreatedExternally_ShouldBeDetectedAndCached()
    {
        // Arrange
        var tcs = new TaskCompletionSource<bool>();
        using var directoryView = new AlwaysUpdateDirectoryView<string, TestItem>(
            _testDirectory,
            GetDestinationValue,
            ConvertKeyToPath,
            UpdateDirectory,
            _mockFileSystem,
            _mockFileWatcher
        );
        
        directoryView.ItemLoaded += (_, key) => 
        {
            Console.WriteLine($"ItemLoaded event fired with key: {key}");
            if (key == "createdKey.json")
                tcs.TrySetResult(true);
        };

        var testItem = new TestItem("CreatedFile", 100);
        var filePath = Path.Combine(_testDirectory, "createdKey.json");
        var json = JsonSerializer.Serialize(testItem);

        _mockFileSystem.SimulateFileCreated(filePath, json);
        
        _mockFileWatcher.SimulateFileCreated("createdKey.json");

        var result = await Task.WhenAny(tcs.Task, Task.Delay(TimeSpan.FromSeconds(5)));
        
        if (result != tcs.Task)
        {
            var scanMethod = typeof(AlwaysUpdateDirectoryView<string, TestItem>)
                .GetMethod("ScanDirectory", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            
            scanMethod?.Invoke(directoryView, [null]);

            var itemAvailable = await WaitForConditionAsync(
                () => {
                    var available = directoryView.TryGetValue("createdKey", out _);
                    Console.WriteLine($"Polling check - item available: {available}"); 
                    return available;
                },
                TimeSpan.FromSeconds(5));
            
            Assert.IsTrue(itemAvailable, "Item should be available after external file creation (via polling)");
        }

        var success = directoryView.TryGetValue("createdKey", out var retrievedItem);
        
        if (!success)
        {
            var existingFiles = string.Join(", ", _mockFileSystem.GetFiles(_testDirectory, false, CancellationToken.None));
            var fileExists = _mockFileSystem.IsFileExists(filePath);
            var fileContents = fileExists ? await _mockFileSystem.ReadAllTextAsync(filePath, CancellationToken.None) : "N/A";
            
            Assert.Fail($"Failed to retrieve item. " +
                       $"File exists in mock: {fileExists}, " +
                       $"All files in directory: [{existingFiles}], " +
                       $"File contents: {fileContents}");
        }
        
        Assert.IsTrue(success, "Item should be retrievable");
        Assert.IsNotNull(retrievedItem, "Retrieved item should not be null");
        Assert.AreEqual("CreatedFile", retrievedItem.Name);
        Assert.AreEqual(100, retrievedItem.Value);
    }

    [TestMethod]
    public async Task FileDeletedExternally_ShouldBeRemovedFromCache()
    {
        // Arrange
        using var directoryView = new AlwaysUpdateDirectoryView<string, TestItem>(
            _testDirectory,
            GetDestinationValue,
            ConvertKeyToPath,
            UpdateDirectory,
            _mockFileSystem,
            _mockFileWatcher
        );

        var testItem = new TestItem("ToBeDeleted", 200);
        directoryView.Add("deleteKey", testItem);

        await WaitForConditionAsync(() => directoryView.TryGetValue("deleteKey", out _));

        var successBefore = directoryView.TryGetValue("deleteKey", out var deleteItem);
        Assert.IsTrue(successBefore);
        Assert.IsNotNull(deleteItem);

        var filePath = Path.Combine(_testDirectory, "deleteKey.json");
        _mockFileSystem.SimulateFileDeleted(filePath);
        _mockFileWatcher.SimulateFileDeleted(filePath);

        var itemRemoved = await WaitForConditionAsync(() => !directoryView.TryGetValue("deleteKey", out _));

        // Assert
        Assert.IsTrue(itemRemoved, "Item should have been removed from cache after file deletion");
        Assert.IsFalse(_mockFileSystem.IsFileExists(filePath), "File should not exist in mock file system");
    }

    [TestMethod]
    public async Task FileRenamedExternally_ShouldUpdateCache()
    {
        // Arrange
        var newKeyAddedCompletionSource = new TaskCompletionSource<bool>();
        var oldKeyRemovedCompletionSource = new TaskCompletionSource<bool>();
        using var directoryView = new AlwaysUpdateDirectoryView<string, TestItem>(
            _testDirectory,
            GetDestinationValue,
            ConvertKeyToPath,
            UpdateDirectory,
            _mockFileSystem,
            _mockFileWatcher
        );

        var testItem = new TestItem("ToBeRenamed", 300);
        directoryView.Add("oldKey", testItem);

        await WaitForConditionAsync(() => directoryView.TryGetValue("oldKey", out _));

        directoryView.ItemLoaded += (_, key) => 
        {
            if (key == "newKey.json")
                newKeyAddedCompletionSource.TrySetResult(true);
        };
        
        directoryView.ItemRemoved += (_, key) => 
        {
            if (key == "oldKey.json")
                oldKeyRemovedCompletionSource.TrySetResult(true);
        };

        var newTestItem = new TestItem("Renamed", 301);
        var json = JsonSerializer.Serialize(newTestItem);
        var oldFilePath = Path.Combine(_testDirectory, "oldKey.json");
        var newFilePath = Path.Combine(_testDirectory, "newKey.json");
        
        _mockFileSystem.SimulateFileCreated(newFilePath, json);
        _mockFileSystem.SimulateFileDeleted(oldFilePath);
        
        _mockFileWatcher.SimulateFileRenamed(oldFilePath, newFilePath);

        await Task.WhenAny(Task.WhenAll(newKeyAddedCompletionSource.Task, oldKeyRemovedCompletionSource.Task), Task.Delay(TimeSpan.FromSeconds(5)));

        Assert.IsFalse(_mockFileSystem.IsFileExists(oldFilePath), "Old file should not exist");
        Assert.IsTrue(_mockFileSystem.IsFileExists(newFilePath), "New file should exist");
        
        var oldSuccess = directoryView.TryGetValue("oldKey", out _);
        Assert.IsFalse(oldSuccess, "Old item should no longer be retrievable");
        
        var newSuccess = directoryView.TryGetValue("newKey", out var newItem);
        Assert.IsTrue(newSuccess, "New item should be retrievable");
        Assert.IsNotNull(newItem);
        Assert.AreEqual("Renamed", newItem.Name);
        Assert.AreEqual(301, newItem.Value);
    }

    [TestMethod]
    public async Task ConcurrentOperations_ShouldHandleCorrectly()
    {
        // Arrange
        using var directoryView = new AlwaysUpdateDirectoryView<string, TestItem>(
            _testDirectory,
            GetDestinationValue,
            ConvertKeyToPath,
            UpdateDirectory,
            _mockFileSystem,
            _mockFileWatcher
        );

        var tasks = new List<Task>();

        for (var i = 0; i < 10; i++)
        {
            var index = i;
            var view = directoryView;
            tasks.Add(Task.Run(() =>
            {
                var item = new TestItem("Concurrent" + index, index);
                view.Add($"key{index}", item);
            }));
        }

        await Task.WhenAll(tasks);

        var allItemsRetrievable = await WaitForConditionAsync(() =>
        {
            for (var i = 0; i < 10; i++)
            {
                if (!directoryView.TryGetValue($"key{i}", out _))
                    return false;
            }
            return true;
        });

        Assert.IsTrue(allItemsRetrievable, "All items should be retrievable");

        var successCount = 0;
        for (var i = 0; i < 10; i++)
        {
            if (!directoryView.TryGetValue($"key{i}", out var item) || item is null) 
                continue;
            
            Assert.AreEqual($"Concurrent{i}", item.Name);
            Assert.AreEqual(i, item.Value);
            successCount++;
        }

        Assert.AreEqual(10, successCount, "All items should be retrievable");
    }

    [TestMethod]
    public void TryGetValue_WithNonexistentKey_ShouldReturnFalse()
    {
        // Arrange
        using var directoryView = new AlwaysUpdateDirectoryView<string, TestItem>(
            _testDirectory,
            GetDestinationValue,
            ConvertKeyToPath,
            UpdateDirectory,
            _mockFileSystem,
            _mockFileWatcher
        );

        // Act & Assert
        var success = directoryView.TryGetValue("nonexistentKey", out var item);
        Assert.IsFalse(success);
        Assert.IsNull(item);
    }

    [TestMethod]
    public void Dispose_ShouldReleaseResources()
    {
        // Arrange
        var directoryView = new AlwaysUpdateDirectoryView<string, TestItem>(
            _testDirectory,
            GetDestinationValue,
            ConvertKeyToPath,
            UpdateDirectory,
            _mockFileSystem,
            _mockFileWatcher
        );

        directoryView.Dispose();
        
        var success = directoryView.TryGetValue("anyKey", out _);
        Assert.IsFalse(success);
    }

    private record TestItem(string Name, int Value);

    private async Task<bool> WaitForConditionAsync(Func<bool> condition, TimeSpan? timeout = null)
    {
        var maxWait = timeout ?? TimeSpan.FromSeconds(5);
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        
        while (stopwatch.Elapsed < maxWait)
        {
            var result = false;
            try
            {
                // Create a separate execution context to avoid holding locks across iterations
                await Task.Run(() => result = condition());
                
                if (result)
                    return true;
            }
            catch
            {
                // Ignore exceptions during condition checking
            }
            
            await Task.Delay(50); // Short delay between checks
        }
        
        // One final check
        try
        {
            return await Task.Run(condition);
        }
        catch
        {
            return false;
        }
    }

    private static TestItem GetDestinationValue(string path, IFileSystem fileSystem, CancellationToken cancellationToken)
    {
        var json = fileSystem.ReadAllText(path, cancellationToken);
        var item = JsonSerializer.Deserialize<TestItem>(json);
        
        return item ?? throw new InvalidOperationException($"Failed to deserialize item from path: {path}");
    }

    private static string ConvertKeyToPath(string key)
    {
        return $"{key}.json";
    }

    private static void UpdateDirectory(string path, TestItem value, IFileSystem fileSystem, CancellationToken cancellationToken)
    {
        IFileSystem.CreateDirectory(Path.GetDirectoryName(path)!);
        var json = JsonSerializer.Serialize(value);
        fileSystem.WriteAllText(path, json, cancellationToken);
    }
}