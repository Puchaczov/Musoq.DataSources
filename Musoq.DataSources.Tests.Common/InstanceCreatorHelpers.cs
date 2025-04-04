using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Microsoft.Extensions.Logging;
using Moq;
using Musoq.Converter;
using Musoq.Converter.Build;
using Musoq.Evaluator;
using Musoq.Schema;

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

        var compiledQuery = InstanceCreator.CompileForExecution(
            script,
            assemblyName,
            schemaProvider,
            loggerResolver,
            () => new CreateTree(
                new TransformTree(
                    new TurnQueryIntoRunnableCode(null), loggerResolver)),
                items =>
                {
                    items.PositionalEnvironmentVariables = environmentVariables;
                    items.CreateBuildMetadataAndInferTypesVisitor = (provider, columns) =>
                        new BuildMetadataAndInferTypesForTestsVisitor(provider, columns, environmentVariables, loggerResolver.ResolveLogger<BuildMetadataAndInferTypesForTestsVisitor>());
                });
        
        var runnableField = compiledQuery.GetType().GetRuntimeFields().FirstOrDefault(f => f.Name.Contains("runnable"));

        var runnable = (IRunnable)runnableField?.GetValue(compiledQuery);
        runnable.Logger = loggerResolver.ResolveLogger<BuildMetadataAndInferTypesForTestsVisitor>();

        return compiledQuery;
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