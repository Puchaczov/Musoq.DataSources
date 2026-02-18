using System.Linq;
using Musoq.Schema;

namespace Musoq.DataSources.CANBus.Messages;

internal class MessagesTable : ISchemaTable
{
    public ISchemaColumn[] Columns => MessagesSourceHelper.Columns;

    public SchemaTableMetadata Metadata => new(typeof(MessageEntity));

    public ISchemaColumn? GetColumnByName(string name)
    {
        return Columns.FirstOrDefault(column => column.ColumnName == name);
    }

    public ISchemaColumn[] GetColumnsByName(string name)
    {
        return Columns.Where(column => column.ColumnName == name).ToArray();
    }
}