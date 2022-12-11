using Musoq.Schema.DataSources;
using System.Dynamic;

namespace Musoq.Schema.Xml
{
    internal class XmlFileTable : ISchemaTable
    {
        public ISchemaColumn[] Columns => System.Array.Empty<ISchemaColumn>();

        public ISchemaColumn GetColumnByName(string name)
        {
            return new SchemaColumn(name, 0, typeof(IDynamicMetaObjectProvider));
        }
    }
}
