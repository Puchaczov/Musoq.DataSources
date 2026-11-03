#nullable enable
using System;
using Musoq.DataSources.Common;
using Musoq.Schema.Diagnostics;

namespace Musoq.DataSources.GitHub;

internal static class GitHubDiagnostics
{
    internal static DataSourceException? Map(Exception error)
        => error switch
        {
            Octokit.RateLimitExceededException rate => GitHubSchema.Reports.Failure(DataSourceFailureKind.RateLimited, error,
                retryAfter: rate.GetRetryAfterTimeSpan()),
            Octokit.AbuseException abuse => GitHubSchema.Reports.Failure(DataSourceFailureKind.RateLimited, error,
                retryAfter: abuse.RetryAfterSeconds is { } seconds ? TimeSpan.FromSeconds(seconds) : null),
            Octokit.ApiException api when ProviderDiagnostics.HttpFailureKind(api.StatusCode) is { } kind
                => GitHubSchema.Reports.Failure(kind, error),
            _ => null
        };
}
