#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Security;
using System.Text;
using System.Text.RegularExpressions;
using Musoq.Schema.Diagnostics;
using Musoq.DataSources.Common;

namespace Musoq.DataSources.Search.Components.Diagnostics;

internal static class SearchDiagnosticReport
{
    internal static DataSourceException? Error(Exception error)
    {
        if (error is not ISearchDiagnosticException known) return null;
        return new DataSourceException(Create(known.Diagnostic, error.InnerException), error);
    }

    internal static DataSourceDiagnostic Warning(SearchDiagnostic diagnostic, Exception? privateException)
        => ProviderDiagnostics.AsWarning(Create(diagnostic, privateException), "The explicit audit surface retained the failed scan as a summary row.");

    private static DataSourceDiagnostic Create(SearchDiagnostic diagnostic, Exception? privateException)
    {
        var kind = diagnostic.Code switch
        {
            SearchDiagnosticCodes.MissingRoot => DataSourceFailureKind.ResourceNotFound,
            SearchDiagnosticCodes.SourceOpenFailed when privateException is UnauthorizedAccessException or SecurityException => DataSourceFailureKind.AccessDenied,
            SearchDiagnosticCodes.SourceOpenFailed when privateException is FileNotFoundException or DirectoryNotFoundException => DataSourceFailureKind.ResourceNotFound,
            SearchDiagnosticCodes.SourceReadFailed when privateException is DecoderFallbackException or InvalidDataException => DataSourceFailureKind.InvalidInput,
            SearchDiagnosticCodes.SourceOpenFailed or SearchDiagnosticCodes.SourceReadFailed or SearchDiagnosticCodes.SourceChanged => DataSourceFailureKind.Unavailable,
            SearchDiagnosticCodes.OutputFailed => DataSourceFailureKind.ProviderFailure,
            SearchDiagnosticCodes.ResourceLimit when privateException is RegexMatchTimeoutException => DataSourceFailureKind.Timeout,
            _ => DataSourceFailureKind.InvalidInput
        };
        return new DataSourceDiagnostic(diagnostic.Code, diagnostic.Phase.ToString(), diagnostic.Message,
            new DataSourceDiagnosticOptions
            {
                FailureKind = kind,
                SuggestedFixes = [diagnostic.SuggestedFix],
                ArgumentName = diagnostic.Location?.ArgumentName,
                InputLocation = diagnostic.Location?.Offset is { } offset ? new DataSourceInputLocation(characterOffset: offset) : null,
                Details = new Dictionary<string, string> { ["searchPhase"] = diagnostic.Phase.ToString() },
                CanRetry = kind is DataSourceFailureKind.Unavailable or DataSourceFailureKind.Timeout
            });
    }
}
