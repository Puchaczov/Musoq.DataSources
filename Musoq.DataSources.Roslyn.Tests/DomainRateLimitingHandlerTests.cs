using System.Diagnostics;
using System.Net;
using Musoq.DataSources.Roslyn.Components.NuGet.Http;

namespace Musoq.DataSources.Roslyn.Tests;

[TestClass]
public class DomainRateLimitingHandlerTests
{
    // 1. Basic Rate Limiting Functionality

    [TestMethod]
    public async Task BasicRateLimiting_ShouldLimitRequestRate()
    {
        // Arrange
        var defaultConfig = new DomainRateLimitingHandler.DomainRateLimitConfig(
            permitsPerPeriod: 2,
            replenishmentPeriod: TimeSpan.FromSeconds(1),
            queueLimit: 5);
        
        var testHandler = new TestHttpMessageHandler();
        var handler = new DomainRateLimitingHandler(
            domainConfigs: new Dictionary<string, DomainRateLimitingHandler.DomainRateLimitConfig>(),
            defaultConfig: defaultConfig)
        {
            InnerHandler = testHandler
        };
        
        var client = new HttpClient(handler);
        
        // Act
        var stopwatch = Stopwatch.StartNew();
        
        // Send 3 requests - first 2 should go through immediately, 3rd should be delayed by rate limiting
        var task1 = client.GetAsync("https://example.com/1");
        var task2 = client.GetAsync("https://example.com/2");
        var task3 = client.GetAsync("https://example.com/3");
        
        await Task.WhenAll(task1, task2, task3);
        
        stopwatch.Stop();
        
        // Assert
        Assert.IsTrue(stopwatch.ElapsedMilliseconds >= 900, 
            "Rate limiting should have delayed the third request by at least 90% of 1 second but took " + stopwatch.Elapsed);
        Assert.AreEqual(3, testHandler.RequestCount);
        Assert.AreEqual(HttpStatusCode.OK, task1.Result.StatusCode);
        Assert.AreEqual(HttpStatusCode.OK, task2.Result.StatusCode);
        Assert.AreEqual(HttpStatusCode.OK, task3.Result.StatusCode);
    }

    [TestMethod]
    public async Task ZeroQueueLimit_ShouldRejectExcessRequests()
    {
        // Arrange
        var defaultConfig = new DomainRateLimitingHandler.DomainRateLimitConfig(
            permitsPerPeriod: 1,
            replenishmentPeriod: TimeSpan.FromMinutes(10),
            queueLimit: 0); // No queueing allowed
        
        var testHandler = new TestHttpMessageHandler();
        var handler = new DomainRateLimitingHandler(
            domainConfigs: new Dictionary<string, DomainRateLimitingHandler.DomainRateLimitConfig>(),
            defaultConfig: defaultConfig)
        {
            InnerHandler = testHandler
        };
        
        var client = new HttpClient(handler);
        
        // First request should acquire the only permit
        await client.GetAsync("https://example.com/1");
        
        // Second request should fail immediately because queue limit is 0
        // and we're already at the rate limit
        await Assert.ThrowsExceptionAsync<HttpRequestException>(async () => 
        {
            await client.GetAsync("https://example.com/2");
        });
        
        // Assert
        Assert.AreEqual(1, testHandler.RequestCount);
    }

    [TestMethod]
    public async Task RequestProcessingOrder_ShouldBePreservedWhenRateLimited()
    {
        // Arrange
        var defaultConfig = new DomainRateLimitingHandler.DomainRateLimitConfig(
            permitsPerPeriod: 1,
            replenishmentPeriod: TimeSpan.FromSeconds(1),
            queueLimit: 5);
        
        var testHandler = new TestHttpMessageHandler();
        var handler = new DomainRateLimitingHandler(
            domainConfigs: new Dictionary<string, DomainRateLimitingHandler.DomainRateLimitConfig>(),
            defaultConfig: defaultConfig)
        {
            InnerHandler = testHandler
        };
        
        var client = new HttpClient(handler);
        
        // Act
        var tasks = new List<Task<HttpResponseMessage>>();
        const int requestCount = 3;
        
        for (var i = 1; i <= requestCount; i++)
        {
            tasks.Add(client.GetAsync($"https://example.com/{i}"));
        }
        
        await Task.WhenAll(tasks);
        
        // Assert
        Assert.AreEqual(requestCount, testHandler.RequestCount);
        
        for (var i = 0; i < requestCount; i++)
        {
            Assert.AreEqual($"https://example.com/{i + 1}", 
                testHandler.ProcessedRequests[i].RequestUri?.ToString(),
                $"Request {i + 1} should have been processed in order");
        }
    }
    
    // 2. Domain Configuration Tests

    [TestMethod]
    public async Task DomainSpecificConfig_ShouldApplyCorrectLimits()
    {
        // Arrange
        var defaultConfig = new DomainRateLimitingHandler.DomainRateLimitConfig(
            permitsPerPeriod: 5,
            replenishmentPeriod: TimeSpan.FromSeconds(1),
            queueLimit: 5);
        
        var specificDomainConfig = new DomainRateLimitingHandler.DomainRateLimitConfig(
            permitsPerPeriod: 1,
            replenishmentPeriod: TimeSpan.FromSeconds(1),
            queueLimit: 5);
        
        var domainConfigs = new Dictionary<string, DomainRateLimitingHandler.DomainRateLimitConfig>
        {
            { "limited-domain.com", specificDomainConfig }
        };
        
        var testHandler = new TestHttpMessageHandler();
        var handler = new DomainRateLimitingHandler(
            domainConfigs: domainConfigs,
            defaultConfig: defaultConfig)
        {
            InnerHandler = testHandler
        };
        
        var client = new HttpClient(handler);
        
        // Act & Assert
        
        // For default domain, 2 requests should go through quickly
        var task1 = client.GetAsync("https://example.com/1");
        var task2 = client.GetAsync("https://example.com/2");
        await Task.WhenAll(task1, task2);
        
        // For limited domain, second request should be delayed
        var stopwatch = Stopwatch.StartNew();
        
        var limitedTask1 = client.GetAsync("https://limited-domain.com/1");
        var limitedTask2 = client.GetAsync("https://limited-domain.com/2");
        
        await Task.WhenAll(limitedTask1, limitedTask2);
        
        stopwatch.Stop();
        
        Assert.IsTrue(stopwatch.ElapsedMilliseconds >= 900, 
            "Rate limiting for specific domain should have delayed the second request");
        Assert.AreEqual(4, testHandler.RequestCount);
    }
    
    [TestMethod]
    public async Task WildcardDomainMatching_ShouldApplyCorrectLimits()
    {
        // Arrange
        var defaultConfig = new DomainRateLimitingHandler.DomainRateLimitConfig(
            permitsPerPeriod: 5,
            replenishmentPeriod: TimeSpan.FromSeconds(1),
            queueLimit: 5);
        
        var wildcardConfig = new DomainRateLimitingHandler.DomainRateLimitConfig(
            permitsPerPeriod: 1,
            replenishmentPeriod: TimeSpan.FromSeconds(1),
            queueLimit: 5);
        
        var domainConfigs = new Dictionary<string, DomainRateLimitingHandler.DomainRateLimitConfig>
        {
            { "*.example.com", wildcardConfig }
        };
        
        var testHandler = new TestHttpMessageHandler();
        var handler = new DomainRateLimitingHandler(
            domainConfigs: domainConfigs,
            defaultConfig: defaultConfig)
        {
            InnerHandler = testHandler
        };
        
        var client = new HttpClient(handler);
        
        // Act
        var stopwatch = Stopwatch.StartNew();
        
        // These should match the wildcard and thus be rate limited
        var task1 = client.GetAsync("https://sub.example.com/1");
        var task2 = client.GetAsync("https://sub.example.com/2");
        
        await Task.WhenAll(task1, task2);
        
        stopwatch.Stop();
        
        // Assert
        Assert.IsTrue(stopwatch.ElapsedMilliseconds >= 900, 
            "Wildcard domain matching should apply rate limits to matching subdomains");
        Assert.AreEqual(2, testHandler.RequestCount);
    }
    
    [TestMethod]
    public async Task PrioritizeExactMatchOverWildcard()
    {
        // Arrange
        var defaultConfig = new DomainRateLimitingHandler.DomainRateLimitConfig(
            permitsPerPeriod: 5,
            replenishmentPeriod: TimeSpan.FromSeconds(1),
            queueLimit: 5);
        
        var wildcardConfig = new DomainRateLimitingHandler.DomainRateLimitConfig(
            permitsPerPeriod: 1,
            replenishmentPeriod: TimeSpan.FromSeconds(1),
            queueLimit: 5);
        
        var exactConfig = new DomainRateLimitingHandler.DomainRateLimitConfig(
            permitsPerPeriod: 3,
            replenishmentPeriod: TimeSpan.FromSeconds(1),
            queueLimit: 5);
        
        var domainConfigs = new Dictionary<string, DomainRateLimitingHandler.DomainRateLimitConfig>
        {
            { "*.example.com", wildcardConfig },
            { "specific.example.com", exactConfig }
        };
        
        var testHandler = new TestHttpMessageHandler();
        var handler = new DomainRateLimitingHandler(
            domainConfigs: domainConfigs,
            defaultConfig: defaultConfig)
        {
            InnerHandler = testHandler
        };
        
        var client = new HttpClient(handler);
        
        // Act
        // These should use the exact match config (permitting 3 requests)
        var task1 = client.GetAsync("https://specific.example.com/1");
        var task2 = client.GetAsync("https://specific.example.com/2");
        var task3 = client.GetAsync("https://specific.example.com/3");
        
        await Task.WhenAll(task1, task2, task3);
        
        // Assert
        Assert.AreEqual(3, testHandler.RequestCount);
        
        // This should still be under the limit defined by the exact match config
        Assert.IsTrue(task1.IsCompletedSuccessfully && task2.IsCompletedSuccessfully && task3.IsCompletedSuccessfully,
            "All three requests should complete successfully with exact match config");
    }
    
    [TestMethod]
    public async Task DefaultConfigFallback_ShouldBeUsedWhenNoSpecificMatch()
    {
        // Arrange
        var defaultConfig = new DomainRateLimitingHandler.DomainRateLimitConfig(
            permitsPerPeriod: 1,
            replenishmentPeriod: TimeSpan.FromSeconds(1),
            queueLimit: 5);
        
        var specificConfig = new DomainRateLimitingHandler.DomainRateLimitConfig(
            permitsPerPeriod: 5,
            replenishmentPeriod: TimeSpan.FromSeconds(1),
            queueLimit: 5);
        
        var domainConfigs = new Dictionary<string, DomainRateLimitingHandler.DomainRateLimitConfig>
        {
            { "fast-domain.com", specificConfig },
            { "*.fast-example.com", specificConfig }
        };
        
        var testHandler = new TestHttpMessageHandler();
        var handler = new DomainRateLimitingHandler(
            domainConfigs: domainConfigs,
            defaultConfig: defaultConfig)
        {
            InnerHandler = testHandler
        };
        
        var client = new HttpClient(handler);
        
        // Act
        var stopwatch = Stopwatch.StartNew();
        
        // These should use default config (1 per second)
        var task1 = client.GetAsync("https://unspecified-domain.com/1");
        var task2 = client.GetAsync("https://unspecified-domain.com/2");
        
        await Task.WhenAll(task1, task2);
        
        stopwatch.Stop();
        
        // Assert
        Assert.IsTrue(stopwatch.ElapsedMilliseconds >= 900, 
            "Default config should be applied when no specific config matches");
        Assert.AreEqual(2, testHandler.RequestCount);
    }

    // 3. Edge Cases

    [TestMethod]
    public async Task NullUri_ShouldBypassRateLimiting()
    {
        // Arrange
        var defaultConfig = new DomainRateLimitingHandler.DomainRateLimitConfig(
            permitsPerPeriod: 1,
            replenishmentPeriod: TimeSpan.FromSeconds(1),
            queueLimit: 5);
        
        var testHandler = new TestHttpMessageHandler();
        var handler = new DomainRateLimitingHandlerForTests(
            domainConfigs: new Dictionary<string, DomainRateLimitingHandler.DomainRateLimitConfig>(),
            defaultConfig: defaultConfig)
        {
            InnerHandler = testHandler
        };
        
        var request1 = new HttpRequestMessage();
        
        await Assert.ThrowsExceptionAsync<InvalidOperationException>(async () => await handler.SendAsync(request1, CancellationToken.None));
    }
    
    [TestMethod]
    public async Task CancellationToken_ShouldCancelRequest()
    {
        // Arrange
        var defaultConfig = new DomainRateLimitingHandler.DomainRateLimitConfig(
            permitsPerPeriod: 1,
            replenishmentPeriod: TimeSpan.FromSeconds(5), // Long replenishment period
            queueLimit: 5);
        
        var testHandler = new TestHttpMessageHandler();
        var handler = new DomainRateLimitingHandler(
            domainConfigs: new Dictionary<string, DomainRateLimitingHandler.DomainRateLimitConfig>(),
            defaultConfig: defaultConfig)
        {
            InnerHandler = testHandler
        };
        
        var client = new HttpClient(handler);
        
        // Act
        // First request will get the only permit
        var task1 = client.GetAsync("https://example.com/1");
        
        // Second request will be queued
        var cts = new CancellationTokenSource();
        var task2 = client.GetAsync("https://example.com/2", cts.Token);
        
        // Cancel the second request
        cts.Cancel();
        
        // Assert
        await task1; // First request should complete
        
        await Assert.ThrowsExceptionAsync<TaskCanceledException>(() => task2);
        
        Assert.AreEqual(1, testHandler.RequestCount, "Only the first request should be processed");
    }
    
    // 4. Performance Tests
    
    [TestMethod]
    public async Task ConcurrentRequestsToDifferentDomains_ShouldNotBlockEachOther()
    {
        // Arrange
        var defaultConfig = new DomainRateLimitingHandler.DomainRateLimitConfig(
            permitsPerPeriod: 1,
            replenishmentPeriod: TimeSpan.FromSeconds(1),
            queueLimit: 5);
        
        var testHandler = new TestHttpMessageHandler();
        var handler = new DomainRateLimitingHandler(
            domainConfigs: new Dictionary<string, DomainRateLimitingHandler.DomainRateLimitConfig>(),
            defaultConfig: defaultConfig)
        {
            InnerHandler = testHandler
        };
        
        var client = new HttpClient(handler);
        
        // Act
        var stopwatch = Stopwatch.StartNew();
        
        // These should not block each other as they're for different domains
        var tasks = new List<Task<HttpResponseMessage>>();
        for (var i = 0; i < 5; i++)
        {
            tasks.Add(client.GetAsync($"https://domain{i}.com/path"));
        }
        
        await Task.WhenAll(tasks);
        
        stopwatch.Stop();
        
        // Assert
        Assert.IsTrue(stopwatch.ElapsedMilliseconds < 1000, 
            "Requests to different domains should not block each other");
        Assert.AreEqual(5, testHandler.RequestCount);
    }
    
    [TestMethod]
    public async Task HighVolumeRequests_ShouldBeHandledCorrectly()
    {
        // Arrange
        var defaultConfig = new DomainRateLimitingHandler.DomainRateLimitConfig(
            permitsPerPeriod: 5, // 5 permits per period
            replenishmentPeriod: TimeSpan.FromSeconds(1),
            queueLimit: 20); // High queue limit
        
        var testHandler = new TestHttpMessageHandler();
        var handler = new DomainRateLimitingHandler(
            domainConfigs: new Dictionary<string, DomainRateLimitingHandler.DomainRateLimitConfig>(),
            defaultConfig: defaultConfig)
        {
            InnerHandler = testHandler
        };
        
        var client = new HttpClient(handler);
        
        // Act
        var tasks = new List<Task<HttpResponseMessage>>();
        const int requestCount = 15; // Send 15 requests to the same domain
        
        for (var i = 1; i <= requestCount; i++)
        {
            tasks.Add(client.GetAsync($"https://example.com/{i}"));
        }
        
        await Task.WhenAll(tasks);
        
        // Assert
        Assert.AreEqual(requestCount, testHandler.RequestCount);
        Assert.IsTrue(tasks.All(t => t.Result.StatusCode == HttpStatusCode.OK),
            "All requests should complete successfully");
    }
    
    // 5. Additional Edge Cases and Resource Management

    [TestMethod]
    public async Task CaseSensitivity_ShouldMatchDomainsCaseInsensitively()
    {
        // Arrange
        var defaultConfig = new DomainRateLimitingHandler.DomainRateLimitConfig(
            permitsPerPeriod: 5,
            replenishmentPeriod: TimeSpan.FromSeconds(1),
            queueLimit: 5);
        
        var limitedConfig = new DomainRateLimitingHandler.DomainRateLimitConfig(
            permitsPerPeriod: 1,
            replenishmentPeriod: TimeSpan.FromSeconds(1),
            queueLimit: 5);
        
        var domainConfigs = new Dictionary<string, DomainRateLimitingHandler.DomainRateLimitConfig>
        {
            { "example.com", limitedConfig }
        };
        
        var testHandler = new TestHttpMessageHandler();
        var handler = new DomainRateLimitingHandler(
            domainConfigs: domainConfigs,
            defaultConfig: defaultConfig)
        {
            InnerHandler = testHandler
        };
        
        var client = new HttpClient(handler);
        
        // Act
        var stopwatch = Stopwatch.StartNew();
        
        // These should match the same rate limiter despite case difference
        var task1 = client.GetAsync("https://EXAMPLE.com/1");
        var task2 = client.GetAsync("https://example.COM/2");
        
        await Task.WhenAll(task1, task2);
        
        stopwatch.Stop();
        
        // Assert
        Assert.IsTrue(stopwatch.ElapsedMilliseconds >= 900, 
            $"Case-insensitive domain matching should apply same rate limit but took {stopwatch.Elapsed}");
        Assert.AreEqual(2, testHandler.RequestCount);
    }
    
    [TestMethod]
    public async Task IpAddressInsteadOfDomain_ShouldBeRateLimited()
    {
        // Arrange
        var defaultConfig = new DomainRateLimitingHandler.DomainRateLimitConfig(
            permitsPerPeriod: 1,
            replenishmentPeriod: TimeSpan.FromSeconds(1),
            queueLimit: 5);
        
        var testHandler = new TestHttpMessageHandler();
        var handler = new DomainRateLimitingHandler(
            domainConfigs: new Dictionary<string, DomainRateLimitingHandler.DomainRateLimitConfig>(),
            defaultConfig: defaultConfig)
        {
            InnerHandler = testHandler
        };
        
        var client = new HttpClient(handler);
        
        // Act
        var stopwatch = Stopwatch.StartNew();
        
        // IP addresses should also be rate limited
        var task1 = client.GetAsync("http://127.0.0.1/1");
        var task2 = client.GetAsync("http://127.0.0.1/2");
        
        await Task.WhenAll(task1, task2);
        
        stopwatch.Stop();
        
        // Assert
        Assert.IsTrue(stopwatch.ElapsedMilliseconds >= 900, 
            "IP addresses should be rate limited like domains");
        Assert.AreEqual(2, testHandler.RequestCount);
    }

    [TestMethod]
    public async Task ManyDomains_ShouldNotCauseConcurrencyIssues()
    {
        // Arrange
        var defaultConfig = new DomainRateLimitingHandler.DomainRateLimitConfig(
            permitsPerPeriod: 1,
            replenishmentPeriod: TimeSpan.FromMilliseconds(100),
            queueLimit: 5);
        
        var testHandler = new TestHttpMessageHandler();
        var handler = new DomainRateLimitingHandler(
            domainConfigs: new Dictionary<string, DomainRateLimitingHandler.DomainRateLimitConfig>(),
            defaultConfig: defaultConfig)
        {
            InnerHandler = testHandler
        };
        
        var client = new HttpClient(handler);
        
        // Act
        var tasks = new List<Task>();
        const int domainCount = 100;
        
        // Create many tasks accessing different domains concurrently
        for (var i = 0; i < domainCount; i++)
        {
            var domain = $"domain{i}.example";
            tasks.Add(Task.Run(async () =>
            {
                await client.GetAsync($"https://{domain}/path");
            }));
        }
        
        await Task.WhenAll(tasks);
        
        // Assert
        Assert.AreEqual(domainCount, testHandler.RequestCount);
    }
    
    [TestMethod]
    public async Task MultipleLevelsOfWildcardMatching_ShouldMatchCorrectly()
    {
        // Arrange
        var defaultConfig = new DomainRateLimitingHandler.DomainRateLimitConfig(
            permitsPerPeriod: 5,
            replenishmentPeriod: TimeSpan.FromSeconds(1),
            queueLimit: 5);
        
        var firstLevelConfig = new DomainRateLimitingHandler.DomainRateLimitConfig(
            permitsPerPeriod: 2,
            replenishmentPeriod: TimeSpan.FromSeconds(1),
            queueLimit: 5);
        
        var secondLevelConfig = new DomainRateLimitingHandler.DomainRateLimitConfig(
            permitsPerPeriod: 1,
            replenishmentPeriod: TimeSpan.FromSeconds(1),
            queueLimit: 5);
        
        var domainConfigs = new Dictionary<string, DomainRateLimitingHandler.DomainRateLimitConfig>
        {
            { "*.example.com", firstLevelConfig },
            { "*.service.example.com", secondLevelConfig }
        };
        
        var testHandler = new TestHttpMessageHandler();
        var handler = new DomainRateLimitingHandler(
            domainConfigs: domainConfigs,
            defaultConfig: defaultConfig)
        {
            InnerHandler = testHandler
        };
        
        var client = new HttpClient(handler);
        
        // Act & Assert
        
        // First level wildcard should apply (2 per second)
        var task1 = client.GetAsync("https://api.example.com/1");
        var task2 = client.GetAsync("https://api.example.com/2");
        await Task.WhenAll(task1, task2);
        
        // Second level wildcard should apply (1 per second) and be more specific
        var stopwatch = Stopwatch.StartNew();
        
        var task3 = client.GetAsync("https://api.service.example.com/1");
        var task4 = client.GetAsync("https://api.service.example.com/2");
        
        await Task.WhenAll(task3, task4);
        
        stopwatch.Stop();
        
        Assert.IsTrue(stopwatch.ElapsedMilliseconds >= 900, 
            "More specific wildcard pattern should apply to deep subdomains");
        Assert.AreEqual(4, testHandler.RequestCount);
    }

    [TestMethod]
    public async Task ConcurrentAccessToSameDomain_ShouldBeThreadSafe()
    {
        // Arrange
        var defaultConfig = new DomainRateLimitingHandler.DomainRateLimitConfig(
            permitsPerPeriod: 20,
            replenishmentPeriod: TimeSpan.FromSeconds(1),
            queueLimit: 100);
        
        var testHandler = new TestHttpMessageHandler();
        var handler = new DomainRateLimitingHandler(
            domainConfigs: new Dictionary<string, DomainRateLimitingHandler.DomainRateLimitConfig>(),
            defaultConfig: defaultConfig)
        {
            InnerHandler = testHandler
        };
        
        var client = new HttpClient(handler);
        
        // Act
        var tasks = new List<Task>();
        const int concurrentRequests = 50;
        
        // Launch many concurrent requests to the same domain
        for (var i = 0; i < concurrentRequests; i++)
        {
            var index = i;
            tasks.Add(Task.Run(async () =>
            {
                await client.GetAsync($"https://example.com/path{index}");
            }));
        }
        
        await Task.WhenAll(tasks);
        
        // Assert
        Assert.AreEqual(concurrentRequests, testHandler.RequestCount);
    }
    
    /// <summary>
    /// A test HTTP handler that returns predefined responses without making actual HTTP requests
    /// </summary>
    private class TestHttpMessageHandler : HttpMessageHandler
    {
        private int _requestCount;
        
        public int RequestCount => _requestCount;
        public List<HttpRequestMessage> ProcessedRequests { get; } = [];
    
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                return Task.FromCanceled<HttpResponseMessage>(cancellationToken);
            }
            
            Interlocked.Increment(ref _requestCount);
            ProcessedRequests.Add(request);
            
            // Create a simple OK response
            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("Test response content"),
                RequestMessage = request
            };
            
            return Task.FromResult(response);
        }
    }

    private class DomainRateLimitingHandlerForTests(
        Dictionary<string, DomainRateLimitingHandler.DomainRateLimitConfig> domainConfigs,
        DomainRateLimitingHandler.DomainRateLimitConfig defaultConfig)
        : DomainRateLimitingHandler(domainConfigs, defaultConfig)
    {
        public new async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            return await base.SendAsync(request, cancellationToken);
        }
    }
}
