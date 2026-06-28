using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Musoq.DataSources.CANBus.Components;
using Musoq.DataSources.CANBus.Messages;
using Musoq.DataSources.CANBus.SeparatedValuesFromFile;
using Musoq.DataSources.CANBus.Signals;
using Musoq.Schema;
using Musoq.Schema.DataSources;
using Musoq.Schema.Managers;
using Musoq.Schema.Optimization;
using Musoq.Schema.Reflection;

namespace Musoq.DataSources.CANBus;

/// <description>
///     Provides schema to work with CAN bus data.
/// </description>
/// <short-description>
///     Provides schema to work with CAN bus data.
/// </short-description>
/// <project-url>https://github.com/Puchaczov/Musoq.DataSources</project-url>
public class CANBusSchema : SchemaBase
{
    private const string SchemaName = "can";
    private const string SeparatedValuesTable = "separatedvalues";
    private const string MessagesTable = "messages";
    private const string SignalsTable = "signals";

    private readonly Func<string, ICANBusApi> _createCanBusApi;

    /// <virtual-constructors>
    ///     <virtual-constructor>
    ///         <examples>
    ///             <example>
    ///                 <from>
    ///                     <environmentVariables>
    ///                     </environmentVariables>
    ///                     #can.separatedvalues(string csvData, string dbcData, string idOfType = "dec" | "hex" | "bin")
    ///                 </from>
    ///                 <description>
    ///                     Treats csv, tsv or others separated values files as CAN bus records. The file must be of
    ///                     format **Timestamp**, **ID**, **DLC**, **Data** where **Data** values must be in format of unsigned
    ///                     integer number (123) or in hexadecimal (0x7b). Based on the loaded dbc file, you will have access
    ///                     access to additional column named {DBC_MESSAGE_NAME}. From here, you can access value
    ///                     {DBC_SIGNAL_NAME} of a message (ie. {DBC_MESSAGE_NAME}.{DBC_SIGNAL_NAME}). Returned value will be
    ///                     of type double
    ///                 </description>
    ///                 <columns isDynamic="true">
    ///                     <column name="ID" type="uint">ID of the message entity</column>
    ///                     <column name="Timestamp" type="ulong">Timestamp of the message entity</column>
    ///                     <column name="Message" type="MessageEntity">The Message</column>
    ///                     <column name="IsWellKnown" type="uint">
    ///                         Whether the message is well known or not (is within dbc
    ///                         file)
    ///                     </column>
    ///                     <column name="DataAsBytes" type="byte[]">Data as bytes</column>
    ///                     <column name="Data" type="ulong">Data as ulong</column>
    ///                 </columns>
    ///             </example>
    ///         </examples>
    ///     </virtual-constructor>
    ///     <virtual-constructor>
    ///         <examples>
    ///             <example>
    ///                 <from>
    ///                     <environmentVariables>
    ///                     </environmentVariables>
    ///                     #can.messages(string dbc)
    ///                 </from>
    ///                 <description>Parses dbc file and returns all messages defined within it.</description>
    ///                 <columns>
    ///                     <column name="Id" type="uint">ID of the message entity</column>
    ///                     <column name="IsExtId" type="bool">Is external Id</column>
    ///                     <column name="Name" type="string">Name of the message entity</column>
    ///                     <column name="DLC" type="ushort">DLC of the message entity</column>
    ///                     <column name="Transmitter" type="string">Transmitter of the message entity</column>
    ///                     <column name="Comment" type="string">Comment for the message entity</column>
    ///                     <column name="CycleTime" type="int">Cycle time for the message entity</column>
    ///                     <column name="Signals" type="SignalEntity[]">Signals of the message</column>
    ///                 </columns>
    ///             </example>
    ///         </examples>
    ///     </virtual-constructor>
    ///     <virtual-constructor>
    ///         <examples>
    ///             <example>
    ///                 <from>
    ///                     <environmentVariables>
    ///                     </environmentVariables>
    ///                     #can.signals(string dbc)
    ///                 </from>
    ///                 <description>Parses dbc file and returns all signals defined within it.</description>
    ///                 <columns>
    ///                     <column name="Id" type="uint">Id of the signal entity</column>
    ///                     <column name="Name" type="string">Name of the signal entity</column>
    ///                     <column name="StartBit" type="ushort">Start bit of the signal entity</column>
    ///                     <column name="Length" type="ushort">Length of the signal entity</column>
    ///                     <column name="ByteOrder" type="byte">Byte order of the signal entity</column>
    ///                     <column name="InitialValue" type="double">Initial value of the signal entity</column>
    ///                     <column name="Factor" type="double">Factor for the signal entity</column>
    ///                     <column name="IsInteger" type="bool">Whether the signal entity is integer or not</column>
    ///                     <column name="Offset" type="double">Offset for the signal entity</column>
    ///                     <column name="Minimum" type="double">Minimum value for the signal entity</column>
    ///                     <column name="Maximum" type="double">Maximum value for the signal entity</column>
    ///                     <column name="Unit" type="string">Unit for the signal entity</column>
    ///                     <column name="Receiver" type="string[]">Receiver for the signal entity</column>
    ///                     <column name="Comment" type="string">Comment for the signal entity</column>
    ///                     <column name="Multiplexing" type="string">Multiplexing details for the signal entity</column>
    ///                     <column name="MessageName" type="string">Message name for the signal entity</column>
    ///                     <column name="ValueMap" type="string">Value map for the signal entity</column>
    ///                     <column name="MessageOrder" type="int">Order of signal within the message definition</column>
    ///                 </columns>
    ///             </example>
    ///         </examples>
    ///     </virtual-constructor>
    /// </virtual-constructors>
    /// <additional-tables>
    ///     <additional-table>
    ///         <description>Represent possible values of a signal</description>
    ///         <columns type="ValueMapEntity[]">
    ///             <column name="Value" type="int">Value of signal</column>
    ///             <column name="Name" type="string">Name of the value</column>
    ///         </columns>
    ///     </additional-table>
    /// </additional-tables>
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
    ///     Gets the table name based on the given data source and parameters.
    /// </summary>
    /// <param name="name">Data Source name</param>
    /// <param name="metadataContext">Metadata context</param>
    /// <param name="parameters">Parameters to pass to data source</param>
    /// <returns>Requested table metadata</returns>
    public override ISchemaTable GetTableByName(
        string name,
        SourceMetadataContext metadataContext,
        params object[] parameters)
    {
        return name.ToLowerInvariant() switch
        {
            SeparatedValuesTable => new SeparatedValuesFromFileCanFramesTable(
                _createCanBusApi((string)parameters[1]),
                CancellationToken.None),
            MessagesTable => new MessagesTable(),
            SignalsTable => new SignalsTable(),
            _ => base.GetTableByName(name, metadataContext, parameters)
        };
    }

    public override SourceDescriptor DescribeSource(
        string name,
        SourceDescribeContext context,
        params object[] parameters)
    {
        var table = GetTableByName(name, context.MetadataContext, parameters);

        return new SourceDescriptor
        {
            Identity = context.Identity,
            Columns = table.Columns,
            RowType = table.Metadata.TableEntityType,
            Diagnostics = [],
            ContractDiagnostics = []
        };
    }

    public override IReadOnlyList<SourceRuntimeSettingRequirement> DescribeSourceRuntimeSettings(
        string name,
        SourceRuntimeSettingsDescribeContext context,
        params object[] parameters)
    {
        return [];
    }

    public override SourcePlanResult TryPlanSource(string name, SourcePlanRequest request, params object[] parameters)
    {
        return name.ToLowerInvariant() switch
        {
            SeparatedValuesTable => PlanSeparatedValuesProjection(request),
            MessagesTable => CANBusSourcePlanner.PlanMessages(request),
            SignalsTable => CANBusSourcePlanner.PlanSignals(request),
            _ => SourcePlanResult.RejectAll(request)
        };
    }

    public override SchemaMethodInfo[] GetRawConstructors(
        string methodName,
        SourceMetadataContext metadataContext)
    {
        return methodName.ToLowerInvariant() switch
        {
            SeparatedValuesTable => CreateSeparatedValuesMethodInfos(),
            MessagesTable => [CreateMessagesMethodInfo()],
            SignalsTable => [CreateSignalsMethodInfo()],
            _ => throw new NotSupportedException(
                $"Data source '{methodName}' is not supported by {SchemaName} schema. " +
                $"Available data sources: {string.Join(", ", SeparatedValuesTable, MessagesTable, SignalsTable)}")
        };
    }

    public override SchemaMethodInfo[] GetRawConstructors(SourceMetadataContext metadataContext)
    {
        var constructors = new List<SchemaMethodInfo>
        {
            CreateMessagesMethodInfo(),
            CreateSignalsMethodInfo()
        };

        constructors.AddRange(CreateSeparatedValuesMethodInfos());

        return constructors.ToArray();
    }

    private static SchemaMethodInfo[] CreateSeparatedValuesMethodInfos()
    {
        var overload1 = new ConstructorInfo(
            null!,
            false,
            [
                ("csvData", typeof(string)),
                ("dbcData", typeof(string))
            ]
        );

        var overload2 = new ConstructorInfo(
            null!,
            false,
            [
                ("csvData", typeof(string)),
                ("dbcData", typeof(string)),
                ("idOfType", typeof(string))
            ]
        );

        var overload3 = new ConstructorInfo(
            null!,
            false,
            [
                ("csvData", typeof(string)),
                ("dbcData", typeof(string)),
                ("idOfType", typeof(string)),
                ("endianness", typeof(string))
            ]
        );

        return
        [
            new SchemaMethodInfo(SeparatedValuesTable, overload1),
            new SchemaMethodInfo(SeparatedValuesTable, overload2),
            new SchemaMethodInfo(SeparatedValuesTable, overload3)
        ];
    }

    private static SchemaMethodInfo CreateMessagesMethodInfo()
    {
        var constructorInfo = new ConstructorInfo(
            null!,
            false,
            [
                ("dbc", typeof(string))
            ]
        );

        return new SchemaMethodInfo(MessagesTable, constructorInfo);
    }

    private static SchemaMethodInfo CreateSignalsMethodInfo()
    {
        var constructorInfo = new ConstructorInfo(
            null!,
            false,
            [
                ("dbc", typeof(string))
            ]
        );

        return new SchemaMethodInfo(SignalsTable, constructorInfo);
    }

    /// <summary>
    ///     Gets the data source based on the given data source and parameters.
    /// </summary>
    /// <param name="name">Data source name</param>
    /// <param name="executionContext">Execution context</param>
    /// <param name="parameters">Parameters to pass data to data source</param>
    /// <returns>Data source</returns>
    /// <exception cref="NotSupportedException">Thrown when data source is not supported.</exception>
    public override RowSource<T> GetRowSource<T>(
        string name,
        SourceExecutionContext executionContext,
        params object[] parameters)
    {
        return name.ToLowerInvariant() switch
        {
            SeparatedValuesTable => CreateSeparatedValuesRowSource<T>(name, executionContext, parameters),
            MessagesTable => EnsureSourceType<T, MessageEntity>(
                name,
                new MessagesSource(_createCanBusApi((string)parameters[0]), executionContext)),
            SignalsTable => EnsureSourceType<T, SignalEntity>(
                name,
                new SignalsSource(_createCanBusApi((string)parameters[0]), executionContext)),
            _ => base.GetRowSource<T>(name, executionContext, parameters)
        };
    }

    private RowSource<T> CreateSeparatedValuesRowSource<T>(
        string name,
        SourceExecutionContext executionContext,
        object[] parameters)
    {
        var source = new SeparatedValuesFromFileCanFramesSource(
            (string)parameters[0],
            _createCanBusApi((string)parameters[1]),
            executionContext,
            parameters.Length > 2 ? (string)parameters[2] : "dec",
            parameters.Length > 3 ? (string)parameters[3] : "little");

        return typeof(T) == typeof(object)
            ? EnsureSourceType<T, object>(name, new MessageFrameObjectRowSource(source, executionContext))
            : EnsureSourceType<T, MessageFrameEntity>(name, source);
    }

    private sealed class MessageFrameObjectRowSource(
        RowSource<MessageFrameEntity> source,
        SourceExecutionContext executionContext) : RowSourceBase<object>
    {
        protected override void CollectChunks(IChunkWriter<object> writer)
        {
            foreach (var chunk in source.Chunks)
            {
                writer.CancellationToken.ThrowIfCancellationRequested();

                var objects = new List<object>(chunk.Count);
                foreach (var item in chunk)
                    objects.Add(ToDictionary(item));

                writer.Write(objects);
            }
        }

        private Dictionary<string, object?> ToDictionary(MessageFrameEntity entity)
        {
            var nameToIndexMap = entity.CreateMessageNameToIndexMap();
            var accessMap = entity.CreateMessageIndexToMethodAccessMap();
            var values = new Dictionary<string, object?>();
            var columns = GetProjectedColumns(executionContext, out var projectionAccepted);

            if (columns.Length == 0 && !projectionAccepted)
            {
                foreach (var (name, index) in nameToIndexMap)
                    values[name] = accessMap[index](entity);

                return values;
            }

            foreach (var column in columns)
            {
                values[column.ColumnName] = nameToIndexMap.TryGetValue(column.ColumnName, out var index)
                    ? accessMap[index](entity)
                    : null;
            }

            return values;
        }

        private static ISchemaColumn[] GetProjectedColumns(
            SourceExecutionContext context,
            out bool projectionAccepted)
        {
            var acceptedColumns = context.Plan.AcceptedColumns;
            projectionAccepted = acceptedColumns.Count > 0;

            if (!projectionAccepted)
                return [.. context.AllColumns];

            var acceptedNames = CreateAcceptedColumnNameSet(acceptedColumns, context.AllColumns);

            return context.AllColumns
                .Where(column => acceptedNames.Contains(column.ColumnName))
                .ToArray();
        }

        private static HashSet<string> CreateAcceptedColumnNameSet(
            IReadOnlyCollection<SourceColumnRef> acceptedColumns,
            IReadOnlyCollection<ISchemaColumn> allColumns)
        {
            var allNames = allColumns
                .Select(column => column.ColumnName)
                .ToHashSet(StringComparer.Ordinal);
            var acceptedNames = new HashSet<string>(StringComparer.Ordinal);

            foreach (var acceptedColumn in acceptedColumns)
            {
                AddIfKnown(acceptedColumn.Name);

                foreach (var part in acceptedColumn.Name.Split('.'))
                    AddIfKnown(part);
            }

            return acceptedNames;

            void AddIfKnown(string name)
            {
                if (allNames.Count == 0 || allNames.Contains(name))
                    acceptedNames.Add(name);
            }
        }
    }

    private static SourcePlanResult PlanSeparatedValuesProjection(SourcePlanRequest request)
    {
        var (acceptedPredicate, residualPredicate) = CANBusSourcePlanner.SplitFramePredicate(request.Predicate);
        var acceptedColumns = CanSafelyAcceptProjection(request.RequiredColumns)
            ? request.RequiredColumns ?? []
            : [];

        return new SourcePlanResult
        {
            ExecutionPlan = new SourceExecutionPlan
            {
                Identity = request.Identity,
                AcceptedColumns = acceptedColumns,
                AcceptedPredicate = acceptedPredicate,
                AcceptedOrderBy = [],
                Properties = new Dictionary<string, object?>()
            },
            AcceptedColumns = acceptedColumns,
            AcceptedPredicate = acceptedPredicate,
            ResidualPredicate = residualPredicate,
            AcceptedOrderBy = [],
            ResidualOrderBy = request.OrderBy ?? [],
            ResidualSkip = request.Skip,
            ResidualTake = request.Take,
            Cardinality = CardinalityEstimate.Unknown("CAN bus frame cardinality depends on the frame file contents."),
            Diagnostics = [],
            ContractDiagnostics = []
        };
    }

    private static bool CanSafelyAcceptProjection(IReadOnlyList<SourceColumnRef> requiredColumns)
    {
        if (requiredColumns.Count == 0)
            return false;

        // Dynamic member accesses are not always surfaced as required top-level columns.
        // Base-only requests are therefore not enough to safely prune frame members.
        var baseColumns = new HashSet<string>(StringComparer.Ordinal)
        {
            "ID",
            "Timestamp",
            "Message",
            "IsWellKnown",
            "DataAsBytes",
            "Data"
        };

        foreach (var requiredColumn in requiredColumns)
        {
            var parts = requiredColumn.Name.Split('.', StringSplitOptions.RemoveEmptyEntries);
            var sourceParts = parts.Length > 1 ? parts[1..] : parts;

            if (sourceParts.Any(part => !baseColumns.Contains(part)))
                return true;
        }

        return false;
    }

    private static MethodsAggregator CreateLibrary()
    {
        var methodsManager = new MethodsManager();
        var library = new CANBusLibrary();

        methodsManager.RegisterLibraries(library);

        return new MethodsAggregator(methodsManager);
    }
}
