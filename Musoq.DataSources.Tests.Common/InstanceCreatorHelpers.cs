using System.Collections.Generic;
using Musoq.Converter;
using Musoq.Converter.Build;
using Musoq.Evaluator;
using Musoq.Schema;

namespace Musoq.DataSources.Tests.Common;

public static class InstanceCreatorHelpers
{
    public static CompiledQuery CompileForExecution(
        string script,
        string assemblyName,
        ISchemaProvider schemaProvider,
        IReadOnlyDictionary<uint, IReadOnlyDictionary<string, string>> environmentVariables)
    {
        return InstanceCreator.CompileForExecution(
            script,
            assemblyName,
            schemaProvider,
            () => new CreateTree(
                new TransformTree(
                    new TurnQueryIntoRunnableCode(null))),
                items =>
                {
                    items.CreateBuildMetadataAndInferTypesVisitor = (provider, columns) =>
                        new BuildMetadataAndInferTypesForTestsVisitor(provider, columns, environmentVariables);
                });
    }
}