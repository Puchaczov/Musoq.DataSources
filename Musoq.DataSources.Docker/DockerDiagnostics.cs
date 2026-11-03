#nullable enable
using System;
using Musoq.DataSources.Common;
using Musoq.Schema.Diagnostics;

namespace Musoq.DataSources.Docker;

internal static class DockerDiagnostics
{
    internal static DataSourceException? Map(Exception error)
        => error is global::Docker.DotNet.DockerApiException api && ProviderDiagnostics.HttpFailureKind(api.StatusCode) is { } kind
            ? DockerSchema.Reports.Failure(kind, error) : null;
}
