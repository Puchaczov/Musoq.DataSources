using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.RateLimiting;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Musoq.DataSources.Roslyn.Components.NuGet.Http;

internal class DomainRateLimitingHandler : DelegatingHandler, IAsyncDisposable
{
    private readonly ConcurrentDictionary<string, TokenBucketRateLimiter> _limiters = new();
    private readonly IReadOnlyDictionary<string, DomainRateLimitConfig> _domainConfigs;
    private readonly DomainRateLimitConfig _defaultConfig;
    private readonly TimeSpan _millisecondsTimeout;
    private readonly ILogger _logger;
    private readonly bool _rejectWhenNotAcquired;
    private int _concurrentRequests;

    public DomainRateLimitingHandler(
        IReadOnlyDictionary<string, DomainRateLimitConfig> domainConfigs,
        DomainRateLimitConfig defaultConfig,
        bool rejectWhenNotAcquired = true,
        ILogger? logger = null
    )
    {
        _domainConfigs = domainConfigs;
        _defaultConfig = defaultConfig;
        _millisecondsTimeout = TimeSpan.FromMilliseconds(50);
        _rejectWhenNotAcquired = rejectWhenNotAcquired;
        _logger = logger ?? NullLogger.Instance;
        
        InnerHandler = new HttpClientHandler();
    }

    public async ValueTask DisposeAsync()
    {
        await DisposeAsyncCore();
        GC.SuppressFinalize(this);
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        if (request.RequestUri == null)
        {
            throw new InvalidOperationException("Request URI is null.");
        }

        var domain = request.RequestUri.Host;

        var limiter = _limiters.GetOrAdd(domain, _ => CreateRateLimiter(domain));
        
        await AcquireAsync(limiter, domain, cancellationToken);
        
        Interlocked.Increment(ref _concurrentRequests);

        var sendResult = await RetryWhenRateLimitingOccured(request, domain, cancellationToken);
        
        Interlocked.Decrement(ref _concurrentRequests);

        if (sendResult is null)
        {
            throw new HttpRequestException("Response is null.");
        }
        
        return sendResult;
    }

    private TokenBucketRateLimiter CreateRateLimiter(string domain)
    {
        var config = GetConfigForDomain(domain);
        
        return new TokenBucketRateLimiter(new TokenBucketRateLimiterOptions
        {
            TokenLimit = config.PermitsPerPeriod,
            QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
            QueueLimit = config.QueueLimit,
            ReplenishmentPeriod = config.ReplenishmentPeriod,
            TokensPerPeriod = config.PermitsPerPeriod,
            AutoReplenishment = true
        });
    }
    
    private DomainRateLimitConfig GetConfigForDomain(string domain)
    {
        var normalizedDomain = domain.ToLowerInvariant();
        
        foreach (var entry in _domainConfigs)
        {
            if (string.Equals(entry.Key, normalizedDomain, StringComparison.OrdinalIgnoreCase))
            {
                return entry.Value;
            }
        }
        
        var wildcardMatches = _domainConfigs.Keys
            .Where(key => key.StartsWith("*.") && normalizedDomain.EndsWith(key[1..], StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(key => key.Length)
            .ToList();
            
        return wildcardMatches.Count > 0 ? _domainConfigs[wildcardMatches[0]] : _defaultConfig;
    }

    private async Task AcquireAsync(TokenBucketRateLimiter limiter, string domain, CancellationToken cancellationToken)
    {
        if (_rejectWhenNotAcquired)
        {
            using var lease = await limiter.AcquireAsync(1, cancellationToken);
            
            if (!lease.IsAcquired)
            {
                _logger.LogWarning("Rate limit exceeded for domain {Domain}.", domain);
                throw new HttpRequestException($"Rate limit exceeded for domain {domain}.");
            }
        }
        else
        {
            var hasSuccessfullyLease = false;
            while (!hasSuccessfullyLease)
            {
                using var lease = await limiter.AcquireAsync(1, cancellationToken);
                hasSuccessfullyLease = lease.IsAcquired;
            
                if (!hasSuccessfullyLease)
                    await Task.Delay(_millisecondsTimeout, cancellationToken);
            }
        }
    }

    private async Task<HttpResponseMessage?> RetryWhenRateLimitingOccured(HttpRequestMessage request, string domain, CancellationToken cancellationToken)
    {
        HttpResponseMessage? sendResult = null;
        var isSuccess = false;
        
        while (!isSuccess)
        {
            sendResult = await base.SendAsync(request, cancellationToken);
            
            if (sendResult.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
            {
                _logger.LogWarning("Rate limit exceeded for domain {Domain}.", domain);
                
                var waitTime = sendResult.Headers.RetryAfter?.Delta ?? TimeSpan.FromSeconds(1);
                
                await Task.Delay(waitTime, cancellationToken);
                isSuccess = false;
                continue;
            }

            isSuccess = true;
        }
        
        return sendResult;
    }

    private async ValueTask DisposeAsyncCore()
    {
        foreach (var limiter in _limiters.Values)
        {
            await limiter.DisposeAsync();
        }

        _limiters.Clear();
    }

    internal record DomainRateLimitingConfigKey(string Domain, bool HasApiKey);

    internal class DomainRateLimitConfig(
        int permitsPerPeriod,
        TimeSpan replenishmentPeriod,
        int queueLimit)
    {
        public int PermitsPerPeriod { get; } = permitsPerPeriod;
        public TimeSpan ReplenishmentPeriod { get; } = replenishmentPeriod;
        public int QueueLimit { get; } = queueLimit;
    }
}