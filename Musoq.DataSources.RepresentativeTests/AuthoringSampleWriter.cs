using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Musoq.Converter;
using Musoq.Evaluator;
using Musoq.Schema;

namespace Musoq.DataSources.RepresentativeTests;

internal static class AuthoringSampleWriter
{
    internal static void Capture(string sql, ISchemaProvider provider, string name)
    {
        var directory = Environment.GetEnvironmentVariable("MUSOQ_AUTHORING_SAMPLES");
        if (string.IsNullOrWhiteSpace(directory))
            return;
        foreach (var mode in new[] { QueryInstrumentationMode.Disabled, QueryInstrumentationMode.Full })
        {
            var result = InstanceCreator.CompileForInspection(sql, "Authoring" + name, provider, new LoggerResolver(),
                new CompilationOptions(instrumentationMode: mode));
            var target = Path.Combine(directory, mode == QueryInstrumentationMode.Disabled ? "current" : "profiled");
            Directory.CreateDirectory(target);
            File.WriteAllText(Path.Combine(target, name + ".cs"), result.GeneratedCSharpCode);
        }
        File.WriteAllText(Path.Combine(directory, name + ".sql"), sql);
    }

    private sealed class LoggerResolver : ILoggerResolver
    {
        public ILogger ResolveLogger() => NullLogger.Instance;
        public ILogger<T> ResolveLogger<T>() => NullLogger<T>.Instance;
    }
}
