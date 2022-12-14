using System.Linq;
using Musoq.Schema;

namespace Musoq.DataSources.System
{
    internal class RangeTable : ISchemaTable
    {
        public ISchemaColumn[] Columns => RangeHelper.RangeColumns;

        public ISchemaColumn GetColumnByName(string name)
        {
            return Columns.SingleOrDefault(column => column.ColumnName == name);
        }
    }
}