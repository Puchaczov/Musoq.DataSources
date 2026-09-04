using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Logging;
using Moq;
using Musoq.Converter;
using Musoq.Evaluator;
using Musoq.Schema;
using Musoq.Schema.Optimization;

namespace Musoq.DataSources.Tests.Common;

public static class InstanceCreatorHelpers
{
    private static ILoggerResolver DefaultLoggerResolver => new VoidLoggerResolver();

    public static CompiledQuery CompileForExecution(
        string script,
        string assemblyName,
        ISchemaProvider schemaProvider,
        IReadOnlyDictionary<uint, IReadOnlyDictionary<string, string>> environmentVariables,
        ILoggerResolver loggerResolver = null)
    {
        loggerResolver ??= DefaultLoggerResolver;

        var compilationOptions = new CompilationOptions(
            ParallelizationMode.Full,
            usePrimitiveTypeValidation: true,
            sourceRuntimeSettingsResolver: new EnvironmentVariablesRuntimeSettingsResolver(environmentVariables));

        return InstanceCreator.CompileForExecution(
            script,
            assemblyName,
            schemaProvider,
            loggerResolver,
            compilationOptions);
    }

    private sealed class EnvironmentVariablesRuntimeSettingsResolver(
        IReadOnlyDictionary<uint, IReadOnlyDictionary<string, string>> environmentVariables)
        : ISourceRuntimeSettingsResolver
    {
        public IReadOnlyDictionary<string, string> Resolve(SourceRuntimeSettingsResolutionRequest request)
        {
            return environmentVariables.Values.FirstOrDefault() ?? new Dictionary<string, string>();
        }
    }

    private class VoidLoggerResolver : ILoggerResolver
    {
        public ILogger ResolveLogger()
        {
            var logger = new Mock<ILogger>();

            return logger.Object;
        }

        public ILogger<T> ResolveLogger<T>()
        {
            var logger = new Mock<ILogger<T>>();

            return logger.Object;
        }
    }
}
