using Spectre.Console.Cli;

namespace Musoq.DataSources.Roslyn.CommandLineArguments.Commands;

public abstract class CliCommandBase<T> : AsyncCommand<T>
    where T : CommandSettings
{
    protected Task<int> InvokeAsync(CommandContext context, HttpRequestMessage request)
    {
        var data = context.Data;

        if (data == null)
            return Task.FromResult(1);

        var func = (Func<HttpRequestMessage, Task<int>>)data;

        return func(request);
    }
}