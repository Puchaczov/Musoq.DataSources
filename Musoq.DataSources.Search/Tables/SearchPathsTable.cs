#nullable enable

using System.Linq;
using Musoq.Schema;

using Musoq.DataSources.Search.Entities;
using Musoq.DataSources.Search.Helpers;

namespace Musoq.DataSources.Search.Tables;

internal sealed class SearchPathsTable : ISchemaTable
{
    public ISchemaColumn[] Columns => SearchPathsHelper.Columns;

    public SchemaTableMetadata Metadata { get; } = new(typeof(SearchPath));

    public ISchemaColumn? GetColumnByName(string name)
    {
        return Columns.SingleOrDefault(column => column.ColumnName == name);
    }

    public ISchemaColumn[] GetColumnsByName(string name)
    {
        return Columns.Where(column => column.ColumnName == name).ToArray();
    }
}
