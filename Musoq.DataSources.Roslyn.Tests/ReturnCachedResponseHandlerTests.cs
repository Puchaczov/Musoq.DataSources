using System.Net;
using System.Text.Json;
using Moq;
using Moq.Protected;
using Musoq.DataSources.Roslyn.Components;
using Musoq.DataSources.Roslyn.Components.NuGet.Helpers;
using Musoq.DataSources.Roslyn.Components.NuGet.Http;

namespace Musoq.DataSources.Roslyn.Tests;

[TestClass]
public class ReturnCachedResponseHandlerTests
{
    private Mock<IFileSystem> _fileSystemMock = null!;
    private Mock<HttpMessageHandler> _httpHandlerMock = null!;
    private string _cacheDirectory = null!;
    private ReturnCachedResponseHandler _handler = null!;
    
    [TestInitialize]
    public void Setup()
    {
        _fileSystemMock = new Mock<IFileSystem>();
        _httpHandlerMock = new Mock<HttpMessageHandler>();
        _cacheDirectory = IFileSystem.Combine("test", "cache");
        _handler = new ReturnCachedResponseHandler(_fileSystemMock.Object, _cacheDirectory, _httpHandlerMock.Object);
    }
    
    [TestCleanup]
    public void TearDown()
    {
        _fileSystemMock.Reset();
        _httpHandlerMock.Reset();
        _handler.Dispose();
    }
    
    [TestMethod]
    public async Task InitializeAsync_WithEmptyDirectory_ShouldNotThrow()
    {
        var cancellationToken = CancellationToken.None;
        _fileSystemMock.Setup(fs => fs.GetFilesAsync(It.IsAny<string>(), false, cancellationToken))
            .Returns(AsyncHelpers.Empty<string>());
            
        try
        {
            await _handler.InitializeAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            Assert.Fail($"Should not throw exception but threw: {ex.Message}");
        }
    }
    
    [TestMethod]
    public async Task InitializeAsync_WithValidCacheFile_ShouldLoadFromCache()
    {
        var cancellationToken = CancellationToken.None;
        var testUrl = "https://test.com/api";
        var testContent = "Test content"u8.ToArray();
        var cacheFilePath = "test/cache/cachedfile.json";
        
        var cacheItem = new
        {
            Url = new { Value = testUrl },
            Content = testContent,
            StatusCode = HttpStatusCode.OK,
            Headers = new Dictionary<string, IEnumerable<string>>
            {
                { "Content-Type", ["application/json"] }
            }
        };
        
        var serializedCache = JsonSerializer.Serialize(cacheItem);
        
        _fileSystemMock.Setup(fs => fs.GetFilesAsync(It.IsAny<string>(), false, cancellationToken))
            .Returns(new[] { cacheFilePath }.ToAsyncEnumerable());
            
        _fileSystemMock.Setup(fs => fs.ReadAllTextAsync(cacheFilePath, cancellationToken))
            .ReturnsAsync(serializedCache);
        
        await _handler.InitializeAsync(cancellationToken);
        
        SetupHttpHandlerMock(testUrl, HttpStatusCode.BadRequest, "Error content");
        
        var client = new HttpClient(_handler);
        var response = await client.GetAsync(testUrl, cancellationToken);
        
        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        var content = await response.Content.ReadAsByteArrayAsync(cancellationToken);
        CollectionAssert.AreEqual(testContent, content);
    }
    
    [TestMethod]
    public async Task InitializeAsync_WithInvalidJson_ShouldSkipFile()
    {
        var cancellationToken = CancellationToken.None;
        var testUrl = "https://test.com/api";
        var cacheFilePath = "test/cache/invalidcache.json";
        
        _fileSystemMock.Setup(fs => fs.GetFilesAsync(It.IsAny<string>(), false, cancellationToken))
            .Returns(new[] { cacheFilePath }.ToAsyncEnumerable());
            
        _fileSystemMock.Setup(fs => fs.ReadAllTextAsync(cacheFilePath, cancellationToken))
            .ReturnsAsync("invalid json");
        
        await _handler.InitializeAsync(cancellationToken);
        
        SetupHttpHandlerMock(testUrl, HttpStatusCode.OK, "New content");
        
        var client = new HttpClient(_handler);
        var response = await client.GetAsync(testUrl, cancellationToken);
        
        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        
        var content = await response.Content.ReadAsStringAsync(cancellationToken);
        
        Assert.AreEqual("New content", content);
    }
    
    [TestMethod]
    public async Task SendAsync_CacheMiss_ShouldMakeRequestAndCacheResponse()
    {
        var testUrl = "https://test.com/api/new";
        var testContent = "New data";
        
        SetupHttpHandlerMock(testUrl, HttpStatusCode.OK, testContent);
        
        _fileSystemMock.Setup(fs => fs.WriteAllTextAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        
        var client = new HttpClient(_handler);
        
        var response1 = await client.GetAsync(testUrl);
        
        SetupHttpHandlerMock(testUrl, HttpStatusCode.NotFound, "Not found");
        
        var response2 = await client.GetAsync(testUrl);
        
        Assert.AreEqual(HttpStatusCode.OK, response1.StatusCode);
        Assert.AreEqual(HttpStatusCode.OK, response2.StatusCode);
        
        var content1 = await response1.Content.ReadAsStringAsync();
        var content2 = await response2.Content.ReadAsStringAsync();
        
        Assert.AreEqual(testContent, content1);
        Assert.AreEqual(testContent, content2);
        
        _fileSystemMock.Verify(
            fs => fs.WriteAllTextAsync(
                It.IsAny<string>(),
                It.Is<string>(s => s.Contains(testUrl)),
                It.IsAny<CancellationToken>()
            ),
            Times.Once
        );
    }
    
    [TestMethod]
    public async Task SendAsync_ErrorResponse_ShouldNotBeCached()
    {
        var testUrl = "https://test.com/api/error";
        var errorContent = "Error data";
        
        SetupHttpHandlerMock(testUrl, HttpStatusCode.InternalServerError, errorContent);
        
        var client = new HttpClient(_handler);
        
        var response = await client.GetAsync(testUrl);
        
        Assert.AreEqual(HttpStatusCode.InternalServerError, response.StatusCode);
        
        _fileSystemMock.Verify(
            fs => fs.WriteAllTextAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never
        );
    }
    
    [TestMethod]
    public async Task SendAsync_NullRequestUri_ShouldThrowException()
    {
        var client = new HttpClient(_handler);
        var request = new HttpRequestMessage();
        
        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => client.SendAsync(request));
    }
    
    [TestMethod]
    public async Task SendAsync_RequestCancellation_ShouldPropagate()
    {
        var cts = new CancellationTokenSource();
        var testUrl = "https://test.com/api/cancel";
        var tcs = new TaskCompletionSource<HttpResponseMessage>();
        
        _httpHandlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .Returns((HttpRequestMessage _, CancellationToken _) => tcs.Task);
            
        var client = new HttpClient(_handler);
        
        var requestTask = client.GetAsync(testUrl, cts.Token);
        await cts.CancelAsync();
        tcs.SetCanceled(cts.Token);
        
        await Assert.ThrowsExactlyAsync<TaskCanceledException>(() => requestTask);
    }
    
    [TestMethod]
    public async Task SendAsync_ConcurrentRequests_ShouldReturnSameResponse()
    {
        var testUrl = "https://test.com/api/concurrent";
        var testContent = "Concurrent data";
        
        SetupHttpHandlerMock(testUrl, HttpStatusCode.OK, testContent);
        
        _fileSystemMock.Setup(fs => fs.WriteAllTextAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
            
        var client = new HttpClient(_handler);
        
        var tasks = Enumerable.Range(0, 10)
            .Select(_ => client.GetAsync(testUrl))
            .ToList();
            
        await Task.WhenAll(tasks);
        
        foreach (var response in tasks.Select(task => task.Result))
        {
            Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
            var content = await response.Content.ReadAsStringAsync();
            Assert.AreEqual(testContent, content);
        }
        
        _httpHandlerMock.Protected().Verify(
            "SendAsync",
            Times.Once(),
            ItExpr.Is<HttpRequestMessage>(req => req.RequestUri != null && req.RequestUri.ToString() == testUrl),
            ItExpr.IsAny<CancellationToken>());
            
        _fileSystemMock.Verify(
            fs => fs.WriteAllTextAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Once
        );
    }
    
    [TestMethod]
    public async Task InitializeAsync_WithNullDeserializedCacheItem_ShouldSkipFile()
    {
        var cancellationToken = CancellationToken.None;
        var testUrl = "https://test.com/api";
        var cacheFilePath = "test/cache/nullcache.json";
        
        var nullCacheJson = JsonSerializer.Serialize(new {});
        
        _fileSystemMock.Setup(fs => fs.GetFilesAsync(It.IsAny<string>(), false, cancellationToken))
            .Returns(new[] { cacheFilePath }.ToAsyncEnumerable());
            
        _fileSystemMock.Setup(fs => fs.ReadAllTextAsync(cacheFilePath, cancellationToken))
            .ReturnsAsync(nullCacheJson);
        
        await _handler.InitializeAsync(cancellationToken);
        
        SetupHttpHandlerMock(testUrl, HttpStatusCode.OK, "New content");
        
        var client = new HttpClient(_handler);
        var response = await client.GetAsync(testUrl, cancellationToken);
        
        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
    }

    [TestMethod]
    public async Task SendAsync_FileWriteFailure_ShouldStillReturnResponse()
    {
        var testUrl = "https://test.com/api/writefail";
        var testContent = "Test content";
        
        SetupHttpHandlerMock(testUrl, HttpStatusCode.OK, testContent);
        
        _fileSystemMock.Setup(fs => fs.WriteAllTextAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new IOException("Write failure"));
        
        var client = new HttpClient(_handler);
        var response = await client.GetAsync(testUrl);
        
        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        var content = await response.Content.ReadAsStringAsync();
        Assert.AreEqual(testContent, content);
    }

    [TestMethod]
    public async Task InitializeAsync_FileReadFailure_ShouldSkipFile()
    {
        var cancellationToken = CancellationToken.None;
        var testUrl = "https://test.com/api";
        var cacheFilePath = "test/cache/errorcache.json";
        
        _fileSystemMock.Setup(fs => fs.GetFilesAsync(It.IsAny<string>(), false, cancellationToken))
            .Returns(new[] { cacheFilePath }.ToAsyncEnumerable());
            
        _fileSystemMock.Setup(fs => fs.ReadAllTextAsync(cacheFilePath, cancellationToken))
            .ThrowsAsync(new IOException("Read error"));
        
        try
        {
            await _handler.InitializeAsync(cancellationToken);
            
            SetupHttpHandlerMock(testUrl, HttpStatusCode.OK, "New content");
            var client = new HttpClient(_handler);
            var response = await client.GetAsync(testUrl, cancellationToken);
            Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        }
        catch (Exception ex)
        {
            Assert.Fail($"Should handle file read failures gracefully but threw: {ex.Message}");
        }
    }

    [TestMethod]
    public async Task SendAsync_NonGetHttpMethod_ShouldHandleCorrectly()
    {
        var testUrl = "https://test.com/api/post";
        var requestContent = "Request data";
        var responseContent = "Response data";
        
        var response = new HttpResponseMessage(HttpStatusCode.Created)
        {
            Content = new StringContent(responseContent)
        };
        
        _httpHandlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(req => 
                    req.RequestUri != null && 
                    req.RequestUri.ToString() == testUrl && 
                    req.Method == HttpMethod.Post),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(response);
        
        _fileSystemMock.Setup(fs => fs.WriteAllTextAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        
        var client = new HttpClient(_handler);
        var postResponse = await client.PostAsync(testUrl, new StringContent(requestContent));
        
        Assert.AreEqual(HttpStatusCode.Created, postResponse.StatusCode);
        var content = await postResponse.Content.ReadAsStringAsync();
        Assert.AreEqual(responseContent, content);
    }

    [TestMethod]
    public async Task SendAsync_HeaderPreservation_ShouldMaintainAllHeaders()
    {
        var testUrl = "https://test.com/api/headers";
        var customHeaders = new Dictionary<string, string>
        {
            { "X-Custom-Header", "CustomValue" },
            { "X-API-Key", "12345" }
        };
        
        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("{}")
        };
        
        foreach (var header in customHeaders)
        {
            response.Headers.TryAddWithoutValidation(header.Key, header.Value);
        }
        
        response.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/json");
        
        _httpHandlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(req => req.RequestUri != null && req.RequestUri.ToString() == testUrl),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(response);
        
        _fileSystemMock.Setup(fs => fs.WriteAllTextAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        
        var client = new HttpClient(_handler);
        
        await client.GetAsync(testUrl);
        
        _httpHandlerMock.Reset();
        var secondResponse = await client.GetAsync(testUrl);
        
        foreach (var header in customHeaders)
        {
            Assert.IsTrue(secondResponse.Headers.Contains(header.Key), $"Header {header.Key} was not preserved");
            Assert.AreEqual(header.Value, secondResponse.Headers.GetValues(header.Key).First());
        }
        
        Assert.AreEqual("application/json", secondResponse.Content.Headers.ContentType?.MediaType);
    }

    [TestMethod]
    public async Task SendAsync_UrlHashCollision_ShouldHandleCorrectly()
    {
        var url1 = "https://test.com/api/resource1";
        var url2 = "https://test.com/api/resource2";
        var content1 = "Content for resource 1";
        var content2 = "Content for resource 2";
        
        SetupHttpHandlerMock(url1, HttpStatusCode.OK, content1);
        
        var response2 = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(content2)
        };
        
        _httpHandlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(req => req.RequestUri != null && req.RequestUri.ToString() == url2),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(response2);
        
        var writtenFiles = new List<string>();
        _fileSystemMock.Setup(fs => fs.WriteAllTextAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback<string, string, CancellationToken>((path, content, _) => 
            {
                writtenFiles.Add(path);
            })
            .Returns(Task.CompletedTask);
        
        var client = new HttpClient(_handler);
        
        await client.GetAsync(url1);
        await client.GetAsync(url2);
        
        Assert.AreEqual(2, writtenFiles.Count, "Should create two separate cache files");
        Assert.AreNotEqual(writtenFiles[0], writtenFiles[1], "Cache filenames should be different for different URLs");
        
        var response1Cached = await client.GetAsync(url1);
        var response2Cached = await client.GetAsync(url2);
        
        Assert.AreEqual(content1, await response1Cached.Content.ReadAsStringAsync());
        Assert.AreEqual(content2, await response2Cached.Content.ReadAsStringAsync());
    }
    
    private void SetupHttpHandlerMock(string url, HttpStatusCode statusCode, string content)
    {
        var response = new HttpResponseMessage(statusCode)
        {
            Content = new StringContent(content)
        };
        
        _httpHandlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(req => req.RequestUri != null && req.RequestUri.ToString() == url),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(response);
    }
}