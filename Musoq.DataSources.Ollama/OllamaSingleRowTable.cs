using Musoq.Schema;

namespace Musoq.DataSources.Ollama;

internal class OllamaSingleRowTable : ISchemaTable
{
    public ISchemaColumn[] Columns => OllamaSchemaHelper.Columns;
    
    public SchemaTableMetadata Metadata { get; } = new(typeof(OllamaEntity));

    public ISchemaColumn GetColumnByName(string name)
    {
        return Columns.Single(column => column.ColumnName == name);
    }
    
    public ISchemaColumn[] GetColumnsByName(string name)
    {
        return Columns.Where(column => column.ColumnName == name).ToArray();
    }
}