using Musoq.Schema;

namespace Musoq.DataSources.OpenAI;

internal class OpenAiSingleRowTable : ISchemaTable
{
    public ISchemaColumn[] Columns => SchemaOpenAiHelper.Columns;

    public ISchemaColumn GetColumnByName(string name)
    {
        return Columns.Single(column => column.ColumnName == name);
    }
}