using System;
using Microsoft.CodeAnalysis.MSBuild;
using Microsoft.Extensions.Logging;

namespace Musoq.DataSources.Roslyn.Components;

internal class ProjectLoadProgressLogger(ILogger logger) : IProgress<ProjectLoadProgress>
{
    public void Report(ProjectLoadProgress value)
    {
        logger.LogTrace("Project load progress: {filePath}, {operation}, {targetFramework}, {elapsedTime}",
            value.FilePath,
            value.Operation,
            value.TargetFramework,
            value.ElapsedTime);
    }
}