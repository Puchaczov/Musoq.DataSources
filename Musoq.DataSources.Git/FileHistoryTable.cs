using System.Linq;
using Musoq.DataSources.Git.Entities;
using Musoq.Schema;

namespace Musoq.DataSources.Git;

internal class FileHistoryTable : ISchemaTable
{
    public ISchemaColumn[] Columns => FileHistoryEntity.Columns;

    public SchemaTableMetadata Metadata => new(typeof(FileHistoryEntity));

    public ISchemaColumn? GetColumnByName(string name)
    {
        return Columns.SingleOrDefault(column => column.ColumnName == name);
    }

    public ISchemaColumn[] GetColumnsByName(string name)
    {
        return Columns.Where(column => column.ColumnName == name).ToArray();
    }
}