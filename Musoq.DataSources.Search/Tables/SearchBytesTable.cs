#nullable enable

using System.Linq;
using Musoq.Schema;

using Musoq.DataSources.Search.Entities;
using Musoq.DataSources.Search.Helpers;

namespace Musoq.DataSources.Search.Tables;

internal sealed class SearchBytesTable : ISchemaTable
{
    public ISchemaColumn[] Columns => SearchBytesHelper.Columns;

    public SchemaTableMetadata Metadata { get; } = new(typeof(SearchByteMatch));

    public ISchemaColumn? GetColumnByName(string name)
    {
        return Columns.SingleOrDefault(column => column.ColumnName == name);
    }

    public ISchemaColumn[] GetColumnsByName(string name)
    {
        return Columns.Where(column => column.ColumnName == name).ToArray();
    }
}
