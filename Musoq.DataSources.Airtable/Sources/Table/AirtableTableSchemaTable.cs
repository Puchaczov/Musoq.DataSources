using Musoq.DataSources.Airtable.Components;
using Musoq.DataSources.Airtable.Helpers;
using Musoq.Schema;
using Musoq.Schema.DataSources;

namespace Musoq.DataSources.Airtable.Sources.Table;

internal class AirtableTableSchemaTable : ISchemaTable
{
    private readonly IAirtableApi _api;
    private readonly RuntimeContext _runtimeContext;

    private ISchemaColumn[]? _columns;

    public AirtableTableSchemaTable(IAirtableApi api, RuntimeContext runtimeContext)
    {
        _api = api;
        _runtimeContext = runtimeContext;
        _columns = null;
    }

    public ISchemaColumn[] Columns
    {
        get
        {
            if (_columns != null) return _columns;

            var index = 0;

            _columns = _api.GetColumns(
                    _runtimeContext.QuerySourceInfo.Columns.Select(f => f.ColumnName)
                ).Select(field => new SchemaColumn(field.Name, index++, ConvertToCsharpType(field)))
                .Cast<ISchemaColumn>()
                .ToArray();

            return _columns;
        }
    }

    public SchemaTableMetadata Metadata { get; } = new(typeof(object));

    public ISchemaColumn GetColumnByName(string name)
    {
        return Columns.First(column => column.ColumnName == name);
    }

    public ISchemaColumn[] GetColumnsByName(string name)
    {
        return Columns.Where(column => column.ColumnName == name).ToArray();
    }

    private static Type ConvertToCsharpType(AirtableField field)
    {
        return TypeMappingHelpers.Mapping[Enum.Parse<AirtableType>(field.Type, true)];
    }
}