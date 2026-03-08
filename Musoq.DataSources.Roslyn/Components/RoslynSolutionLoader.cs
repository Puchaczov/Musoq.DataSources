using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.MSBuild;
using Microsoft.Extensions.Logging;

namespace Musoq.DataSources.Roslyn.Components;

internal static class RoslynSolutionLoader
{
    public static async Task<Solution> OpenSolutionAsync(string solutionFilePath, ILogger logger,
        CancellationToken cancellationToken)
    {
        var workspace = MSBuildWorkspace.Create();
        var diagnostics = new ConcurrentQueue<WorkspaceDiagnostic>();

        workspace.WorkspaceFailed += (_, args) =>
        {
            diagnostics.Enqueue(args.Diagnostic);

            if (args.Diagnostic.Kind == WorkspaceDiagnosticKind.Failure)
                logger.LogError("Roslyn workspace failure while loading solution {solutionFilePath}: {message}",
                    solutionFilePath,
                    args.Diagnostic.Message);
            else
                logger.LogWarning("Roslyn workspace warning while loading solution {solutionFilePath}: {message}",
                    solutionFilePath,
                    args.Diagnostic.Message);
        };

        var solutionLoadLogger = new SolutionLoadLogger(logger);
        var projectLoadProgressLogger = new ProjectLoadProgressLogger(logger);
        var solution = await workspace.OpenSolutionAsync(solutionFilePath, solutionLoadLogger,
            projectLoadProgressLogger, cancellationToken);

        EnsureSolutionLoaded(solutionFilePath, solution, diagnostics);

        return solution;
    }

    private static void EnsureSolutionLoaded(string solutionFilePath, Solution solution,
        ConcurrentQueue<WorkspaceDiagnostic> diagnostics)
    {
        if (solution.Projects.Any())
            return;

        var message = new StringBuilder()
            .Append("Roslyn loaded the solution but no projects were available.")
            .AppendLine()
            .Append("Solution path: ")
            .Append(solutionFilePath)
            .AppendLine();

        if (diagnostics.Count > 0)
        {
            message.AppendLine("Workspace diagnostics:");

            foreach (var diagnostic in diagnostics)
                message.Append(" - [")
                    .Append(diagnostic.Kind)
                    .Append("] ")
                    .Append(diagnostic.Message)
                    .AppendLine();
        }
        else
        {
            message.AppendLine("Workspace diagnostics: none reported.");
        }

        if (OperatingSystem.IsWindows())
        {
            message.AppendLine(
                "On Windows, this can happen when Roslyn/MSBuild cannot resolve the solution from a long or problematic path.");
            message.Append("Current solution path length: ")
                .Append(solutionFilePath.Length)
                .AppendLine();
        }

        throw new InvalidOperationException(message.ToString());
    }
}
