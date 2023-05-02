using System;
using Musoq.Schema;
using Musoq.Schema.DataSources;

namespace Musoq.DataSources.Archives;

public class ArchivesSchema : SchemaBase
{
    public ArchivesSchema()
        : base("archives", null)
    {
    }
    
    public override ISchemaTable GetTableByName(string name, RuntimeContext runtimeContext, params object[] parameters)
    {
        return new ArchivesTable();
    }
    
    public override RowSource GetRowSource(string name, RuntimeContext runtimeContext, params object[] parameters)
    {
        return name.ToLowerInvariant() switch
        {
            "file" => new ArchivesRowSource((string) parameters[0]),
            _ => throw new NotSupportedException($"Source {parameters[0]} is not supported.")
        };
    }
}