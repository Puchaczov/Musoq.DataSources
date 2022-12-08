using System.Linq;
using Musoq.Schema;

namespace Musoq.DataSources.Time
{
    public class TimeTable : ISchemaTable
    {
        public ISchemaColumn[] Columns { get; } = TimeHelper.TimeColumns;

        public ISchemaColumn GetColumnByName(string name)
        {
            return Columns.SingleOrDefault(column => column.ColumnName == name);
        }
    }
}