using System;
using Musoq.DataSources.CANBus.Components;
using Musoq.DataSources.CANBus.Messages;
using Musoq.DataSources.CANBus.SeparatedValuesFromFile;
using Musoq.DataSources.CANBus.Signals;
using Musoq.Schema;
using Musoq.Schema.DataSources;
using Musoq.Schema.Managers;

namespace Musoq.DataSources.CANBus;

/// <description>
/// Provides schema to work with CAN bus data.
/// </description>
/// <short-description>
/// Provides schema to work with CAN bus data.
/// </short-description>
/// <project-url>https://github.com/Puchaczov/Musoq.DataSources</project-url>
internal class CANBusSchema : SchemaBase
{
    private const string SchemaName = "can";

    private readonly Func<string, ICANBusApi> _createCanBusApi;
    
    public CANBusSchema() 
        : base(SchemaName, CreateLibrary())
    {
        _createCanBusApi = path => new CANBusApi(path);
    }

    internal CANBusSchema(ICANBusApi canBusApi)
        : base(SchemaName, CreateLibrary())
    {
        _createCanBusApi = _ => canBusApi;
    }

    public override ISchemaTable GetTableByName(string name, RuntimeContext runtimeContext, params object[] parameters)
    {
        return name.ToLowerInvariant() switch
        {
            "separatedvalues" => new SeparatedValuesFromFileCanFramesTable(_createCanBusApi((string)parameters[1])),
            "messages" => new MessagesTable(),
            "signals" => new SignalsTable(),
            _ => base.GetTableByName(name, runtimeContext, parameters)
        };
    }

    public override RowSource GetRowSource(string name, RuntimeContext runtimeContext, params object[] parameters)
    {
        return name.ToLowerInvariant() switch
        {
            "separatedvalues" => new SeparatedValuesFromFileCanFramesSource(
                (string)parameters[0], 
                _createCanBusApi((string)parameters[1]), 
                runtimeContext
            ),
            "messages" => new MessagesSource(_createCanBusApi((string)parameters[0]), runtimeContext),
            "signals" => new SignalsSource(_createCanBusApi((string)parameters[0]), runtimeContext),
            _ => base.GetRowSource(name, runtimeContext, parameters)
        };
    }
    
    private static MethodsAggregator CreateLibrary()
    {
        var methodsManager = new MethodsManager();
        var library = new CANBusLibrary();
        
        methodsManager.RegisterLibraries(library);
        
        return new MethodsAggregator(methodsManager);
    }
}