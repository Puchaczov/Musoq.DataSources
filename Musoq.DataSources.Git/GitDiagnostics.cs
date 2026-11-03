#nullable enable

using System;
using System.Collections.Generic;
using LibGit2Sharp;
using Musoq.Schema.Diagnostics;
using Musoq.Schema.Optimization;

namespace Musoq.DataSources.Git;

internal static class GitDiagnostics
{
    internal static DataSourceException? Map(Exception error)
        => error switch
        {
            RepositoryNotFoundException or NotFoundException => GitSchema.Reports.Failure(DataSourceFailureKind.ResourceNotFound, error),
            GitCliUnavailableException => GitSchema.Reports.Failure(DataSourceFailureKind.InvalidConfiguration, error,
                new Dictionary<string, string> { ["settingName"] = "GIT_EXECUTABLE" }),
            _ => null
        };

    internal static ArgumentException InvalidRevision(string privateMessage, string argumentName)
        => new(privateMessage, argumentName, new DataSourceException(new DataSourceDiagnostic(
            "GIT.InvalidRevision", "input", "The requested Git revision does not identify an available commit.",
            new DataSourceDiagnosticOptions
            {
                FailureKind = DataSourceFailureKind.InvalidInput,
                ArgumentName = "revision",
                ArgumentIndex = 2,
                SuggestedFixes = ["Choose an existing commit revision from the selected repository."]
            })));

    internal static void BackendFallback(SourceExecutionContext context, bool usingCli)
        => context.ReportWarning(new DataSourceDiagnostic(
            usingCli ? "GIT.NativeBackendFallback" : "GIT.CliBackendFallback", "backend",
            usingCli ? "Git used the CLI backend because the native backend could not read this repository." :
                "Git used the native backend because the CLI backend was unavailable.",
            new DataSourceDiagnosticOptions
            {
                Severity = DataSourceDiagnosticSeverity.Warning,
                FailureKind = DataSourceFailureKind.Unavailable,
                SuggestedFixes = ["Check the configured Git backend and GIT_EXECUTABLE runtime setting."]
            }));
}
