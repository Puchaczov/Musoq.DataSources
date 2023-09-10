using Musoq.DataSources.OpenAIHelpers;
using Musoq.Schema;

namespace Musoq.DataSources.OpenAI;

internal class OpenAiSingleRowTable : ISchemaTable
{
    public ISchemaColumn[] Columns => OpenAiSchemaHelper.Columns;
    
    public SchemaTableMetadata Metadata { get; } = new(typeof(OpenAiEntity));

    public ISchemaColumn GetColumnByName(string name)
    {
        return Columns.Single(column => column.ColumnName == name);
    }
    
    public ISchemaColumn[] GetColumnsByName(string name)
    {
        return Columns.Where(column => column.ColumnName == name).ToArray();
    }
}