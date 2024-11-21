using System.Collections.Generic;
using Musoq.Evaluator.Visitors;
using Musoq.Parser.Nodes.From;
using Musoq.Schema;

namespace Musoq.DataSources.Tests.Common;

public class BuildMetadataAndInferTypesForTestsVisitor(
    ISchemaProvider provider,
    IReadOnlyDictionary<string, string[]> columns,
    IReadOnlyDictionary<uint, IReadOnlyDictionary<string, string>> defaultEnvironmentVariables)
    : BuildMetadataAndInferTypesVisitor(provider, columns)
{
    protected override IReadOnlyDictionary<string, string> RetrieveEnvironmentVariables(uint position, SchemaFromNode node)
    {   
        var emptyEnvironmentVariables = new Dictionary<string, string>();
        
        if (defaultEnvironmentVariables.TryGetValue(position, out var variables))
        {
            foreach (var variable in variables)
            {
                emptyEnvironmentVariables.TryAdd(variable.Key, variable.Value);
            }
        }
        
        InternalPositionalEnvironmentVariables.TryAdd(position, emptyEnvironmentVariables);

        return emptyEnvironmentVariables;
    }
}