﻿using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Dynamic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
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
    private readonly RuntimeContext _runtimeContext;

    public SeparatedValuesFromFileCanFramesSource(string framesCsvPath, ICANBusApi canBusApi, RuntimeContext runtimeContext)
    {
        _messages = new MessagesLookup();
        _file = new FileInfo(framesCsvPath);
        _canBusApi = canBusApi;
        _runtimeContext = runtimeContext;
    }
    
    protected override async Task InitializeAsync()
    {
        var messages = await _canBusApi.GetMessagesAsync(_runtimeContext.EndWorkToken);
        
        foreach (var message in messages)
        {
            _messages.Add(message.Name, message);
        }
    }

    protected override async IAsyncEnumerable<SourceCanFrame> GetFramesAsync()
    {
        using var reader = new StreamReader(_file.FullName, Encoding.UTF8);
        var configuration = new CsvConfiguration(CultureInfo.CurrentCulture)
        {
            DetectDelimiter = true
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
                Id = record.ID
            };
            
            var message = _messages.SingleOrDefault(f => f.Value.ID == record.ID).Value;

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
            var number = ulong.Parse(recordData.Substring(2), NumberStyles.HexNumber);
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
        
        throw new InvalidOperationException($"Cannot convert {recordData} to byte array.");
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
        
        public uint ID { get; set; }
        
        public byte DLC { get; set; }
        
        public string? Data { get; set; }
    }
}