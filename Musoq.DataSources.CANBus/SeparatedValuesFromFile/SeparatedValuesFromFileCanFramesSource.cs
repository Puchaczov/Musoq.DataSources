using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Dynamic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using CsvHelper;
using CsvHelper.Configuration;
using Musoq.DataSources.CANBus.Components;
using Musoq.Schema;

namespace Musoq.DataSources.CANBus.SeparatedValuesFromFile;

internal class SeparatedValuesFromFileCanFramesSource : MessageFrameSourceBase
{
    private readonly MessagesLookup _messages;
    private readonly FileInfo _file;
    private readonly ICANBusApi _canBusApi;
    private readonly string _idOfType;

    public SeparatedValuesFromFileCanFramesSource(string framesCsvPath, ICANBusApi canBusApi, RuntimeContext runtimeContext, string idOfType)
        : base(runtimeContext.EndWorkToken)
    {
        _messages = new MessagesLookup();
        _file = new FileInfo(framesCsvPath);
        _canBusApi = canBusApi;
        _idOfType = idOfType;
    }
    
    protected override async Task InitializeAsync(CancellationToken cancellationToken)
    {
        var messages = await _canBusApi.GetMessagesAsync(cancellationToken);
        
        foreach (var message in messages)
        {
            _messages.Add(message.Name, message);
        }
    }

    protected override async IAsyncEnumerable<SourceCanFrame> GetFramesAsync([EnumeratorCancellation] CancellationToken cancellationToken)
    {
        using var reader = new StreamReader(_file.FullName, Encoding.UTF8);
        var configuration = new CsvConfiguration(CultureInfo.CurrentCulture)
        {
            DetectDelimiter = true,
            HeaderValidated = null,
            MissingFieldFound = null
        };

        var convertFrom = _idOfType switch
        {
            "hex" => ConvertFrom.Hex,
            "dec" => ConvertFrom.Decimal,
            "bin" => ConvertFrom.Binary,
            _ => throw new ArgumentOutOfRangeException(nameof(_idOfType), _idOfType, null)
        };

        using var csvReader = new CsvReader(reader, configuration);
        while (await csvReader.ReadAsync())
        {
            var record = csvReader.GetRecord<SeparatedValuesFromFileCanFrameEntity>();
            
            if (record is null)
                throw new InvalidOperationException("Record cannot be null.");

            var canFrame = new CANFrame
            {
                Data = ConvertStringToByteArray(record.Data),
                Id = ConvertStringToUInt32(record.ID, convertFrom)
            };
            
            var message = _messages.SingleOrDefault(f => f.Value.ID == canFrame.Id).Value;

            if (message is null)
            {
                yield return new SourceCanFrame(record.Timestamp, canFrame, record.DLC, null);
                continue;
            }
            
            yield return new SourceCanFrame(record.Timestamp, canFrame, record.DLC, message);
        }
    }

    private static byte[] ConvertStringToByteArray(string? recordData)
    {
        if (recordData is null)
            return Array.Empty<byte>();

        //tread data as hex string and convert to byte array (ie. 0x123)
        if (recordData.StartsWith("0x"))
        {
            var number = ulong.Parse(recordData[2..], NumberStyles.HexNumber);
            return BitConverter.GetBytes(number);
        }
        
        //tread data as decimal string and convert to byte array (ie. 123)
        if (recordData.All(char.IsDigit))
        {
            var value = uint.Parse(recordData);
            return BitConverter.GetBytes(value);
        }
        
        //tread data as binary string and convert to byte array (ie. 0b1010)
        if (recordData.StartsWith("0b"))
        {
            var binaryString = recordData.Substring(2);
            var value = Convert.ToUInt32(binaryString, 2);
            var bytes = BitConverter.GetBytes(value);
            return bytes;
        }
        
        return BitConverter.GetBytes(ulong.Parse(recordData, NumberStyles.HexNumber));
    }
    
    private enum ConvertFrom
    {
        Hex,
        Decimal,
        Binary
    }
    
    private static uint ConvertStringToUInt32(string? recordData, ConvertFrom convertFrom)
    {
        if (recordData is null)
            return 0;

        switch (convertFrom)
        {
            case ConvertFrom.Hex:
                return uint.Parse(recordData.StartsWith("0x") ? recordData[2..] : recordData, NumberStyles.HexNumber);
            case ConvertFrom.Decimal:
                return uint.Parse(recordData);
            case ConvertFrom.Binary:
                var binaryString = recordData.StartsWith("0b") ? recordData[2..] : recordData;
                return Convert.ToUInt32(binaryString, 2);
            default:
                return uint.Parse(recordData, NumberStyles.HexNumber);
        }
    } 

    protected override HashSet<string> AllMessagesSet => _messages.Select(f => f.Key).ToHashSet();

    private Dictionary<string, int>? _messagesNameToIndexMap;
    private Dictionary<int, string>? _indexToMessagesNameMap;

    protected override IReadOnlyDictionary<string, int> MessagesNameToIndexMap
    {
        get
        {
            if (_messagesNameToIndexMap is not null) return _messagesNameToIndexMap;
            
            var messagesNameToIndexMap = new Dictionary<string, int>();
            var indexToMessagesNameMap = new Dictionary<int, string>();
            var index = 0;
            foreach (var message in _messages)
            {
                messagesNameToIndexMap.Add(message.Key, index);
                indexToMessagesNameMap.Add(index, message.Key);
                index += 1;
            }

            _messagesNameToIndexMap = messagesNameToIndexMap;
            _indexToMessagesNameMap = indexToMessagesNameMap;

            return _messagesNameToIndexMap;
        }
    }

    protected override IReadOnlyDictionary<int, Func<MessageFrameEntity, object?>> MessagesIndexToMethodAccessMap
    {
        get
        {
            if (_indexToMessagesNameMap is null)
                throw new InvalidOperationException("Index to messages name map cannot be null.");

            var indexToMessagesNameMap = _indexToMessagesNameMap;
            var indexToMethodAccessMap = new Dictionary<int, Func<MessageFrameEntity, object?>>();
            
            for (var i = 0; i < _indexToMessagesNameMap.Count; i++)
            {
                var member = new MessageFrameMemberBinder(indexToMessagesNameMap[i], false);
                indexToMethodAccessMap.Add(i, f => f.TryGetMember(member, out var result) ? result : null);
            }

            return indexToMethodAccessMap;
        }
    }
    
    private class MessageFrameMemberBinder : GetMemberBinder
    {
        public MessageFrameMemberBinder(string name, bool ignoreCase) 
            : base(name, ignoreCase)
        {
        }

        public override DynamicMetaObject FallbackGetMember(DynamicMetaObject target, DynamicMetaObject? errorSuggestion)
        {
            throw new NotImplementedException();
        }
    }
    
    // ReSharper disable once ClassNeverInstantiated.Local
    [SuppressMessage("ReSharper", "InconsistentNaming")]
    private class SeparatedValuesFromFileCanFrameEntity
    {
        public ulong Timestamp { get; set; }
        
        public string? ID { get; set; }
        
        public byte? DLC { get; set; }
        
        public string? Data { get; set; }
    }
}