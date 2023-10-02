using System.Linq;
using Musoq.Schema;

namespace Musoq.DataSources.CANBus.Signals;

internal class SignalsTable : ISchemaTable
{
    public ISchemaColumn[] Columns => SignalsSourceHelper.Columns;

    public SchemaTableMetadata Metadata => new(typeof(SignalEntity));
    
    public ISchemaColumn? GetColumnByName(string name)
    {
        return Columns.FirstOrDefault(column => column.ColumnName == name);
    }

    public ISchemaColumn[] GetColumnsByName(string name)
    {
        return Columns.Where(column => column.ColumnName == name).ToArray();
    }
}