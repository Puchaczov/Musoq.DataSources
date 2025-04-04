using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.RateLimiting;
using System.Threading.Tasks;

namespace Musoq.DataSources.Roslyn.Components.NuGet.Http;

internal class DomainRateLimitingHandler : DelegatingHandler
{
    private readonly ConcurrentDictionary<string, RateLimiter> _limiters = new();
    private readonly IReadOnlyDictionary<string, DomainRateLimitConfig> _domainConfigs;
    private readonly DomainRateLimitConfig _defaultConfig;

    public DomainRateLimitingHandler(IReadOnlyDictionary<string, DomainRateLimitConfig> domainConfigs,
        DomainRateLimitConfig defaultConfig)
    {
        _domainConfigs = domainConfigs;
        _defaultConfig = defaultConfig;
        
        InnerHandler = new HttpClientHandler();
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
        
        using var lease = await limiter.AcquireAsync(1, cancellationToken);
        
        if (!lease.IsAcquired)
        {
            throw new HttpRequestException("Request rejected due to rate limit.");
        }
        
        return await base.SendAsync(request, cancellationToken);
    }

    private RateLimiter CreateRateLimiter(string domain)
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
            
        if (wildcardMatches.Count > 0)
        {
            return _domainConfigs[wildcardMatches[0]];
        }
        
        return _defaultConfig;
    }

    internal record DomainRateLimitingConfigKey(string Domain, bool HasApiKey)
    {
    }

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