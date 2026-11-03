#nullable enable
using System;
using Musoq.DataSources.Common;
using Musoq.Schema.Diagnostics;

namespace Musoq.DataSources.Jira;

internal static class JiraDiagnostics
{
    internal static DataSourceException? Map(Exception error)
        => error switch
        {
            System.Security.Authentication.AuthenticationException => JiraSchema.Reports.Failure(DataSourceFailureKind.AccessDenied, error),
            Atlassian.Jira.Remote.ResourceNotFoundException => JiraSchema.Reports.Failure(DataSourceFailureKind.ResourceNotFound, error),
            _ => null
        };
}
