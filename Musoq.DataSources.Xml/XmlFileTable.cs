using System.Linq;
using Musoq.Schema.DataSources;

namespace Musoq.Schema.Xml
{
    internal class XmlFileTable : ISchemaTable
    {
        public ISchemaColumn[] Columns
        {
            get
            {
                return new ISchemaColumn[]
                {
                    new SchemaColumn("element", 0, typeof(string)),
                    new SchemaColumn("parent", 1, typeof(DynamicElement)),
                    new SchemaColumn("value", 2, typeof(string)),
                };
            }
        }

        public SchemaTableMetadata Metadata { get; } = new(typeof(DynamicElement));

        public ISchemaColumn GetColumnByName(string name)
        {
            var column = Columns.SingleOrDefault(column => column.ColumnName == name);
            
            if (column == null)
                return new SchemaColumn(name, 3, typeof(string));
            
            return column;
        }
        
        public ISchemaColumn[] GetColumnsByName(string name)
        {
            var columns = Columns.Where(column => column.ColumnName == name).ToArray();
            
            if (columns.Length == 0)
                return new ISchemaColumn[] { new SchemaColumn(name, 3, typeof(string)) };
            
            return columns;
        }
    }
}
