using Microsoft.Build.Framework;
using Microsoft.Extensions.Logging;
using ILogger = Microsoft.Extensions.Logging.ILogger;

namespace Musoq.DataSources.Roslyn.Components;

internal class SolutionLoadLogger(ILogger logger) : Microsoft.Build.Framework.ILogger
{
    public void Initialize(IEventSource eventSource)
    {
        eventSource.ProjectStarted += (sender, args) =>
        {
            logger.LogTrace("Project started: {project}", args.ProjectFile);
        };

        eventSource.ProjectFinished += (sender, args) =>
        {
            logger.LogTrace("Project finished: {project}", args.ProjectFile);
        };

        eventSource.MessageRaised += (sender, args) => { logger.LogTrace(args.Message); };
    }

    public void Shutdown()
    {
    }

    public LoggerVerbosity Verbosity { get; set; } = LoggerVerbosity.Normal;
    public string Parameters { get; set; } = string.Empty;
}