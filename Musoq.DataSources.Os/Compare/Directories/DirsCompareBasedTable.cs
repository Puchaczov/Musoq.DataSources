using System.Linq;
using Musoq.Schema;

namespace Musoq.DataSources.Os.Compare.Directories;

internal class DirsCompareBasedTable : ISchemaTable
{
    public ISchemaColumn[] Columns => CompareDirectoriesHelper.CompareDirectoriesColumns;

    public ISchemaColumn GetColumnByName(string name)
    {
        return CompareDirectoriesHelper.CompareDirectoriesColumns.SingleOrDefault(column => column.ColumnName == name);
    }
}