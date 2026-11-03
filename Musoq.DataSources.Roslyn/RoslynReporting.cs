using System;
using System.Threading;
using Musoq.Schema.Diagnostics;

namespace Musoq.DataSources.Roslyn;

internal sealed class RoslynReporting(Action<DataSourceDiagnostic>? warningReporter, CancellationToken cancellationToken)
{
    internal static bool MetadataFallback(RoslynReporting? reporting, Exception error, CancellationToken token)
        => CSharpSchema.Reports.Warn(reporting?.Reporter, error,
            "The existing package-metadata fallback continued with remaining sources or an unavailable value.", token);

    private Action<DataSourceDiagnostic>? Reporter => warningReporter;

    internal CancellationToken CancellationToken { get; } = cancellationToken;

    internal void AnalysisTimeout()
        => warningReporter?.Invoke(new DataSourceDiagnostic("ROSLYN.AnalysisTimeout", "analysis",
            "A bounded Roslyn analysis timed out; its existing default-value fallback was used.",
            new DataSourceDiagnosticOptions
            {
                Severity = DataSourceDiagnosticSeverity.Warning,
                FailureKind = DataSourceFailureKind.Timeout,
                SuggestedFixes = ["Reduce the analysis scope or retry the query with fewer projects or documents."],
                CanRetry = true
            }));

    internal void WorkspaceProblem()
        => warningReporter?.Invoke(new DataSourceDiagnostic("ROSLYN.WorkspaceLoadWarning", "workspace",
            "Roslyn reported a workspace loading problem; available content is used when loading can continue.",
            new DataSourceDiagnosticOptions
            {
                Severity = DataSourceDiagnosticSeverity.Warning,
                SuggestedFixes = ["Inspect the solution and its required SDKs, workloads, imports, and project references."]
            }));
}
