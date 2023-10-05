using Musoq.Parser.Nodes;
using Musoq.Parser.Nodes.From;
using Musoq.Schema;
using Musoq.Schema.DataSources;

namespace Musoq.DataSources.InferrableDataSourceHelpers.Components;

public interface IRowsSourceDetector<TInput>
{
    Task<IRowsReader<TInput>> InferAsync((SchemaFromNode FromNode, IReadOnlyCollection<ISchemaColumn> Columns, WhereNode WhereNode) queryInformation, Type inputType, CancellationToken cancellationToken);
    
    IObjectResolver Resolve(TInput item, CancellationToken cancellationToken);
}