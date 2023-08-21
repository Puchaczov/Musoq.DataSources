using System.Collections.Concurrent;
using System.Dynamic;
using DbcParserLib;
using DbcParserLib.Model;
using Musoq.Schema.DataSources;
using Iot.Device.SocketCan;

namespace Musoq.DataSources.CANBus;

internal interface ICANBusApi
{
    Task<Message[]> GetMessagesAsync(CancellationToken cancellationToken);
    
    Task<Signal[]> GetMessagesSignalsAsync(CancellationToken cancellationToken);
}

internal class MessagesSource : RowSourceBase<Message>
{
    private readonly ICANBusApi _canBusApi;

    public MessagesSource(ICANBusApi canBusApi)
    {
        _canBusApi = canBusApi;
    }

    protected override void CollectChunks(BlockingCollection<IReadOnlyList<IObjectResolver>> chunkedSource)
    {
        var messages = _canBusApi.GetMessagesAsync(CancellationToken.None).Result;
        chunkedSource.Add(
            messages.Select(f => new EntityResolver<Message>(f, MessagesSourceHelper.MessagesNameToIndexMap, MessagesSourceHelper.MessagesIndexToMethodAccessMap)).ToList());
    }
}

internal class SignalsSource : RowSourceBase<Signal>
{
    private readonly ICANBusApi _canBusApi;

    public SignalsSource(ICANBusApi canBusApi)
    {
        _canBusApi = canBusApi;
    }

    protected override void CollectChunks(BlockingCollection<IReadOnlyList<IObjectResolver>> chunkedSource)
    {
        var signals = _canBusApi.GetMessagesSignalsAsync(CancellationToken.None).Result;
        chunkedSource.Add(
            signals.Select(f => new EntityResolver<Signal>(f, SignalsSourceHelper.SignalsNameToIndexMap, SignalsSourceHelper.SignalsIndexToMethodAccessMap)).ToList());
    }
}

internal static class MessagesSourceHelper
{
    internal static readonly IDictionary<string, int> MessagesNameToIndexMap = new Dictionary<string, int>
    {
        { nameof(Message.ID), 0 },
        { nameof(Message.IsExtID), 1 },
        { nameof(Message.Name), 2 },
        { nameof(Message.DLC), 3 },
        { nameof(Message.Transmitter), 4 },
        { nameof(Message.Comment), 5 },
        { nameof(Message.CycleTime), 6 },
    };

    internal static readonly IDictionary<int, Func<Message, object>> MessagesIndexToMethodAccessMap = new Dictionary<int, Func<Message, object>>
    {
        { 0, f => f.ID },
        { 1, f => f.IsExtID },
        { 2, f => f.Name },
        { 3, f => f.DLC },
        { 4, f => f.Transmitter },
        { 5, f => f.Comment },
        { 6, f => f.CycleTime },
    };
}

internal static class SignalsSourceHelper
{
    internal static readonly IDictionary<string, int> SignalsNameToIndexMap = new Dictionary<string, int>
    {
        { nameof(Signal.ID), 0 },
        { nameof(Signal.Name), 1 },
        { nameof(Signal.StartBit), 2 },
        { nameof(Signal.Length), 3 },
        { nameof(Signal.ByteOrder), 4 },
        { nameof(Signal.InitialValue), 5 },
        { nameof(Signal.Factor), 6 },
        { nameof(Signal.IsInteger), 7 },
        { nameof(Signal.Offset), 8 },
        { nameof(Signal.Minimum), 9 },
        { nameof(Signal.Maximum), 10 },
        { nameof(Signal.Unit), 11 },
        { nameof(Signal.Receiver), 12 },
        { nameof(Signal.Comment), 13 },
        { nameof(Signal.Multiplexing), 14 },
        { nameof(Signal.ValueType), 15 },
        { nameof(Signal.ValueTableMap), 16 },
    };

    internal static readonly IDictionary<int, Func<Signal, object>> SignalsIndexToMethodAccessMap = new Dictionary<int, Func<Signal, object>>
    {
        { 0, f => f.ID },
        { 1, f => f.Name },
        { 2, f => f.StartBit },
        { 3, f => f.Length },
        { 4, f => f.ByteOrder },
        { 5, f => f.InitialValue },
        { 6, f => f.Factor },
        { 7, f => f.IsInteger },
        { 8, f => f.Offset },
        { 9, f => f.Minimum },
        { 10, f => f.Maximum },
        { 11, f => f.Unit },
        { 12, f => f.Receiver },
        { 13, f => f.Comment },
        { 14, f => f.Multiplexing },
        { 15, f => f.ValueType },
        { 16, f => f.ValueTableMap },
    };
}

internal record SourceCanFrame(DateTime Timestamp, CanFrame Frame, Message? Message, Signal? Signal);

internal class SignalFrame : DynamicObject
{
    private readonly HashSet<string> _allSignalsSet;
    private double? _value;

    public SignalFrame(HashSet<string> allSignalsSet)
    {
        _allSignalsSet = allSignalsSet;
        _value = null;
    }
    
    public void Add(string name, double? value)
    {
        _value = value;
    }
}

internal class MessageFrame : DynamicObject
{
    private const string Timestamp = nameof(Timestamp);

    private readonly Dictionary<string, Func<object?>> _memberToValueMap;
    private readonly HashSet<string> _allMessagesSet;

    public MessageFrame(DateTime timestamp, CanFrame frame, Message? message, Signal? signal, HashSet<string> allMessagesSet, HashSet<string> messageSignalsSet)
    {
        _allMessagesSet = allMessagesSet;
        _memberToValueMap = new Dictionary<string, Func<object?>>
        {
            { nameof(Timestamp), () => timestamp },
            { nameof(Message), () => message },
            { nameof(Signal), () => signal },
            { "IsWellKnown", () => message is not null && signal is not null }
        };

        if (message is not null)
        {
            unsafe
            {
                var expandoObject = new SignalFrame(messageSignalsSet);

                expandoObject.Add("RawData", Unpack(frame.Data));
                
                if (signal is not null)
                {
                    var value = Packer.RxSignalUnpack(Unpack(frame.Data), signal);
                    expandoObject.Add(signal.Name, value);
                    expandoObject.Add("UnpackedData", value);
                }
                else
                {
                    expandoObject.Add("UnpackedData", null);
                }
                
                _memberToValueMap.Add(message.Name, () => expandoObject);
            }
        }
    }

    public override bool TryGetMember(GetMemberBinder binder, out object? result)
    {
        if (_memberToValueMap.TryGetValue(binder.Name, out var value))
        {
            result = value();
            return true;
        }
        
        if (_allMessagesSet.Contains(binder.Name))
        {
            result = null;
            return true;
        }
        
        result = null;
        return false;
    }

    private static unsafe ulong Unpack(byte* data)
    {
        var value = *(ulong*)data;
        return value;
    } 
}

internal abstract class CanFramesSource : RowSourceBase<MessageFrame>
{
    protected abstract IEnumerable<SourceCanFrame> GetFrames();
    
    protected abstract HashSet<string> AllMessagesSet { get; }
    
    protected abstract HashSet<string> GetMessageSignalsSet(Message? message);
    
    protected abstract IDictionary<string, int> MessagesNameToIndexMap { get; }
    
    protected abstract IDictionary<int, Func<MessageFrame, object?>> MessagesIndexToMethodAccessMap { get; }

    protected override void CollectChunks(BlockingCollection<IReadOnlyList<IObjectResolver>> chunkedSource)
    {
        foreach (var frame in GetFrames())
        {
            var itemsAdded = 0;
            const int maxItems = 1000;
            var chunk = new List<IObjectResolver>();

            while (itemsAdded < maxItems)
            {
                var messageFrame = new MessageFrame(
                    frame.Timestamp, 
                    frame.Frame, 
                    frame.Message, 
                    frame.Signal,
                    AllMessagesSet,
                    GetMessageSignalsSet(frame.Message));
                chunk.Add(
                    new EntityResolver<MessageFrame>(
                        messageFrame,
                        MessagesNameToIndexMap, 
                        MessagesIndexToMethodAccessMap));
                
                itemsAdded += 1;
            }
            
            chunkedSource.Add(chunk);
        }
    }
}

internal class MessagesLookup : Dictionary<string, Message>{}

internal class CsvFileCanFramesSource : CanFramesSource
{
    private readonly string _framesCsvPath;
    private readonly MessagesLookup _messages;

    public CsvFileCanFramesSource(string framesCsvPath, string dbcPath)
    {
        _framesCsvPath = framesCsvPath;
        _messages = new MessagesLookup();
        var dbc = DbcParserLib.Parser.ParseFromPath(dbcPath);

        foreach (var message in dbc.Messages)
        {
            _messages.Add(message.Name, message);
        }
    }

    protected override IEnumerable<SourceCanFrame> GetFrames()
    {
    }

    protected override HashSet<string> AllMessagesSet => _messages.Select(f => f.Key).ToHashSet();
    protected override HashSet<string> GetMessageSignalsSet(Message? message)
    {
        return message is null ? new HashSet<string>() : _messages[message.Name].Signals.Select(f => f.Name).ToHashSet();
    }

    protected override IDictionary<string, int> MessagesNameToIndexMap { get; }
    protected override IDictionary<int, Func<MessageFrame, object?>> MessagesIndexToMethodAccessMap { get; }
}