#nullable enable

using System.Linq;
using Musoq.Schema;

using Musoq.DataSources.Search.Entities;
using Musoq.DataSources.Search.Helpers;

namespace Musoq.DataSources.Search.Tables;

internal sealed class SearchFilesTable : ISchemaTable
{
    public ISchemaColumn[] Columns => SearchFilesHelper.Columns;

    public SchemaTableMetadata Metadata { get; } = new(typeof(SearchFile));

    public ISchemaColumn? GetColumnByName(string name)
    {
        return Columns.SingleOrDefault(column => column.ColumnName == name);
    }

    public ISchemaColumn[] GetColumnsByName(string name)
    {
        return Columns.Where(column => column.ColumnName == name).ToArray();
    }
}
