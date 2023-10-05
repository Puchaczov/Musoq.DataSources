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
public class CANBusSchema : SchemaBase
{
    private const string SchemaName = "can";

    private readonly Func<string, ICANBusApi> _createCanBusApi;
    
    /// <virtual-constructors>
    /// <virtual-constructor>
    /// <examples>
    /// <example>
    /// <from>
    /// <environmentVariables>
    /// </environmentVariables>
    /// #can.separatedvalues(string csvData, string dbcData)
    /// </from>
    /// <description>Treats csv, tsv or others separated values files as CAN bus records. The file must be of format **Timestamp**, **ID**, **DLC**, **Data** where **Data** values must be in format of unsigned integer number (123) or in hexadecimal (0x7b). Based on the loaded dbc file, you will have access access to additional column named {DBC_MESSAGE_NAME}. From here, you can access value {DBC_SIGNAL_NAME} of a message (ie. {DBC_MESSAGE_NAME}.{DBC_SIGNAL_NAME}). Returned value will be of type double</description>
    /// <columns isDynamic="true">
    /// <column name="ID" type="uint">ID of the message entity</column>
    /// <column name="Timestamp" type="ulong">Timestamp of the message entity</column>
    /// <column name="Message" type="Message">Message entity</column>
    /// <column name="IsWellKnown" type="uint">Whether the message is well known or not (is within dbc file)</column>
    /// </columns>
    /// </example>
    /// </examples>
    /// </virtual-constructor>
    /// <virtual-constructor>
    /// <examples>
    /// <example>
    /// <from>
    /// <environmentVariables>
    /// </environmentVariables>
    /// #can.messages(string dbc)
    /// </from>
    /// <description>Parses dbc file and returns all messages defined within it.</description>
    /// <columns>
    /// <column name="ID" type="uint">ID of the message entity</column>
    /// <column name="IsExtID" type="bool">Is external ID</column>
    /// <column name="Name" type="string">Name of the message entity</column>
    /// <column name="DLC" type="ushort">DLC of the message entity</column>
    /// <column name="Transmitter" type="string">Transmitter of the message entity</column>
    /// <column name="Comment" type="string">Comment for the message entity</column>
    /// <column name="CycleTime" type="int">Cycle time for the message entity</column>
    /// </columns>
    /// </example>
    /// </examples>
    /// </virtual-constructor>
    /// <virtual-constructor>
    /// <examples>
    /// <example>
    /// <from>
    /// <environmentVariables>
    /// </environmentVariables>
    /// #can.signals(string dbc)
    /// </from>
    /// <description>Parses dbc file and returns all signals defined within it.</description>
    /// <columns>
    /// <column name="ID" type="uint">ID of the signal entity</column>
    /// <column name="Name" type="string">Name of the signal entity</column>
    /// <column name="StartBit" type="ushort">Start bit of the signal entity</column>
    /// <column name="Length" type="ushort">Length of the signal entity</column>
    /// <column name="ByteOrder" type="byte">Byte order of the signal entity</column>
    /// <column name="InitialValue" type="double">Initial value of the signal entity</column>
    /// <column name="Factor" type="double">Factor for the signal entity</column>
    /// <column name="IsInteger" type="bool">Whether the signal entity is integer or not</column>
    /// <column name="Offset" type="double">Offset for the signal entity</column>
    /// <column name="Minimum" type="double">Minimum value for the signal entity</column>
    /// <column name="Maximum" type="double">Maximum value for the signal entity</column>
    /// <column name="Unit" type="string">Unit for the signal entity</column>
    /// <column name="Receiver" type="string[]">Receiver for the signal entity</column>
    /// <column name="Comment" type="string">Comment for the signal entity</column>
    /// <column name="Multiplexing" type="string">Multiplexing details for the signal entity</column>
    /// <column name="MessageName" type="string">Message name for the signal entity</column>
    /// </columns>
    /// </example>
    /// </examples>
    /// </virtual-constructor>
    /// </virtual-constructors>
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

    /// <summary>
    /// Gets the table name based on the given data source and parameters.
    /// </summary>
    /// <param name="name">Data Source name</param>
    /// <param name="runtimeContext">Runtime context</param>
    /// <param name="parameters">Parameters to pass to data source</param>
    /// <returns>Requested table metadata</returns>
    public override ISchemaTable GetTableByName(string name, RuntimeContext runtimeContext, params object[] parameters)
    {
        return name.ToLowerInvariant() switch
        {
            "separatedvalues" => new SeparatedValuesFromFileCanFramesTable(_createCanBusApi((string)parameters[1]), runtimeContext.EndWorkToken),
            "messages" => new MessagesTable(),
            "signals" => new SignalsTable(),
            _ => base.GetTableByName(name, runtimeContext, parameters)
        };
    }

    /// <summary>
    /// Gets the data source based on the given data source and parameters.
    /// </summary>
    /// <param name="name">Data source name</param>
    /// <param name="runtimeContext">Runtime context</param>
    /// <param name="parameters">Parameters to pass data to data source</param>
    /// <returns>Data source</returns>
    /// <exception cref="NotSupportedException">Thrown when data source is not supported.</exception>
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