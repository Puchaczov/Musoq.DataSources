using System.Linq;
using Musoq.Schema;

namespace Musoq.DataSources.Os.Process
{
    internal class ProcessBasedTable : ISchemaTable
    {
        public ProcessBasedTable()
        {
            Columns = ProcessHelper.ProcessColumns;
        }

        public ISchemaColumn[] Columns { get; }

        public ISchemaColumn GetColumnByName(string name)
        {
            return Columns.SingleOrDefault(column => column.ColumnName == name);
        }
    }
}