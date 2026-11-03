#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Security;
using System.Threading;
using System.Threading.Tasks;
using Musoq.Schema.DataSources;
using Musoq.Schema.Diagnostics;
using Musoq.Schema.Optimization;

namespace Musoq.DataSources.Common;

internal sealed class ProviderDiagnostics(
    string provider,
    bool fileSystem = false,
    Func<Exception, DataSourceException?>? classify = null)
{
    internal TResult Run<TResult>(Func<TResult> operation, CancellationToken cancellationToken = default)
    {
        try { return operation(); }
        catch (Exception error) when (TryMap(error, cancellationToken, out var failure)) { throw failure; }
    }

    internal RowSource<T> Wrap<T>(RowSource<T> source, CancellationToken cancellationToken)
        => new ProviderRowSource<T>(source, this, cancellationToken);

    internal bool TryMap(Exception error, CancellationToken cancellationToken, out DataSourceException failure)
    {
        failure = null!;
        if (error is AggregateException || HasExplicitReport(error) ||
            error is OperationCanceledException && (cancellationToken.IsCancellationRequested || error is not TaskCanceledException))
            return false;

        failure = classify?.Invoke(error)!;
        if (failure is not null) return true;

        if (fileSystem && error is PathTooLongException)
        {
            failure = new DataSourceException(new DataSourceDiagnostic(provider + ".PathTooLong", "filesystem-path",
                "A filesystem path exceeds the platform path-length limit.", new DataSourceDiagnosticOptions
                {
                    FailureKind = DataSourceFailureKind.InvalidInput,
                    SuggestedFixes = ["Use a shorter path or configure supported long-path handling on the executing platform."]
                }), error);
            return true;
        }

        var kind = error switch
        {
            HttpRequestException http when http.StatusCode.HasValue => HttpFailureKind(http.StatusCode.Value),
            HttpRequestException => DataSourceFailureKind.Unavailable,
            TimeoutException => DataSourceFailureKind.Timeout,
            TaskCanceledException when !fileSystem => DataSourceFailureKind.Timeout,
            FileNotFoundException or DirectoryNotFoundException when fileSystem => DataSourceFailureKind.ResourceNotFound,
            UnauthorizedAccessException or SecurityException when fileSystem => DataSourceFailureKind.AccessDenied,
            IOException when fileSystem && error is not InvalidDataException => DataSourceFailureKind.Unavailable,
            _ => (DataSourceFailureKind?)null
        };
        if (!kind.HasValue) return false;
        failure = Failure(kind.Value, error);
        return true;
    }

    internal DataSourceException Failure(DataSourceFailureKind kind, Exception? privateException = null,
        IReadOnlyDictionary<string, string>? details = null, TimeSpan? retryAfter = null)
    {
        var (category, message, fix) = Content(kind);
        return new DataSourceException(new DataSourceDiagnostic(provider + "." + kind, category, message,
            new DataSourceDiagnosticOptions
            {
                FailureKind = kind,
                SuggestedFixes = [fix],
                Details = details ?? new Dictionary<string, string>(),
                RetryAfter = retryAfter,
                CanRetry = kind is DataSourceFailureKind.RateLimited or DataSourceFailureKind.Timeout ||
                           kind == DataSourceFailureKind.Unavailable && !fileSystem
            }), privateException);
    }

    internal DataSourceException Configuration(string settingName)
        => Failure(DataSourceFailureKind.InvalidConfiguration,
            details: new Dictionary<string, string> { ["settingName"] = settingName });

    internal string RequiredSetting(IReadOnlyDictionary<string, string> settings, string name)
        => settings.TryGetValue(name, out var value) && !string.IsNullOrWhiteSpace(value)
            ? value : throw Configuration(name);

    internal bool Warn(SourceExecutionContext context, Exception error, string continuation,
        CancellationToken cancellationToken = default)
        => Warn(context.WarningReporter, error, continuation, cancellationToken);

    internal bool Warn(Action<DataSourceDiagnostic>? reporter, Exception error, string continuation,
        CancellationToken cancellationToken = default)
    {
        if (cancellationToken.IsCancellationRequested || error is OperationCanceledException && error is not TaskCanceledException) return false;
        var failure = error as DataSourceException;
        if (failure is null && !TryMap(error, cancellationToken, out failure)) return false;
        reporter?.Invoke(AsWarning(failure.Report, continuation));
        return true;
    }

    internal static DataSourceDiagnostic AsWarning(DataSourceDiagnostic report, string continuation)
        => new(report.Code + ".Fallback", report.Category, report.Message + " " + continuation,
            new DataSourceDiagnosticOptions
            {
                Severity = DataSourceDiagnosticSeverity.Warning,
                FailureKind = report.FailureKind,
                SuggestedFixes = report.SuggestedFixes,
                Details = report.Details,
                InputLocation = report.InputLocation,
                ArgumentName = report.ArgumentName,
                ArgumentIndex = report.ArgumentIndex,
                CanRetry = report.CanRetry,
                RetryAfter = report.RetryAfter
            });

    internal static DataSourceFailureKind? HttpFailureKind(HttpStatusCode status)
        => (int)status switch
        {
            400 or 422 => DataSourceFailureKind.InvalidInput,
            401 or 403 => DataSourceFailureKind.AccessDenied,
            404 => DataSourceFailureKind.ResourceNotFound,
            408 or 504 => DataSourceFailureKind.Timeout,
            429 => DataSourceFailureKind.RateLimited,
            500 or 502 or 503 => DataSourceFailureKind.Unavailable,
            _ => null
        };

    private static bool HasExplicitReport(Exception error)
    {
        for (Exception? current = error; current is not null; current = current.InnerException)
        {
            if (current is AggregateException) return true;
            if (current is DataSourceException or DataSourceInputException) return true;
        }
        return false;
    }

    private static (string Category, string Message, string Fix) Content(DataSourceFailureKind kind)
        => kind switch
        {
            DataSourceFailureKind.InvalidInput => ("input", "The datasource rejected the supplied input.", "Check the method arguments and input format."),
            DataSourceFailureKind.InvalidConfiguration => ("configuration", "The datasource requires valid runtime configuration.", "Configure the required runtime settings before retrying."),
            DataSourceFailureKind.AccessDenied => ("access", "Access to the requested datasource resource was denied.", "Check credentials and permissions for the requested resource."),
            DataSourceFailureKind.ResourceNotFound => ("resource", "The requested datasource resource was not found.", "Check that the requested resource exists and is accessible."),
            DataSourceFailureKind.RateLimited => ("rate-limit", "The datasource service rate limit was reached.", "Wait for the service rate limit to reset before retrying."),
            DataSourceFailureKind.Timeout => ("timeout", "The datasource operation timed out.", "Retry later or reduce the requested work."),
            DataSourceFailureKind.Unavailable => ("availability", "The datasource resource is currently unavailable.", "Check resource availability and connectivity before retrying."),
            _ => ("provider", "The datasource could not complete the operation.", "Report the issue with the provider code and query operation.")
        };
}
