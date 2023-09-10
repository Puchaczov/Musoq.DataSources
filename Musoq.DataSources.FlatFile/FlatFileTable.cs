using System.Linq;
using Musoq.Schema;

namespace Musoq.DataSources.FlatFile
{
    internal class FlatFileTable : ISchemaTable
    {
        public ISchemaColumn[] Columns { get; } = FlatFileHelper.FlatColumns;
    
        public SchemaTableMetadata Metadata { get; } = new(typeof(FlatFileEntity));
        
        public ISchemaColumn GetColumnByName(string name)
        {
            return Columns.SingleOrDefault(column => column.ColumnName == name);
        }

        public ISchemaColumn[] GetColumnsByName(string name)
        {
            return Columns.Where(column => column.ColumnName == name).ToArray();
        }
    }
}