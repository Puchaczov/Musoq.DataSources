using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
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
using Musoq.Schema.Optimization;

namespace Musoq.DataSources.CANBus.SeparatedValuesFromFile;

internal class SeparatedValuesFromFileCanFramesSource : MessageFrameSourceBase
{
    private readonly string _bigOrLittle;
    private readonly ICANBusApi _canBusApi;
    private readonly FileInfo _file;
    private readonly string _idOfType;
    private readonly MessagesLookup _messages;

    public SeparatedValuesFromFileCanFramesSource(string framesCsvPath, ICANBusApi canBusApi,
        SourceExecutionContext executionContext, string idOfType, string bigOrLittle)
        : base(executionContext)
    {
        _messages = new MessagesLookup();
        _file = new FileInfo(framesCsvPath);
        _canBusApi = canBusApi;
        _idOfType = idOfType;
        _bigOrLittle = bigOrLittle;
    }

    protected override HashSet<string> AllMessagesSet => _messages.Select(f => f.Key).ToHashSet();

    protected override async Task InitializeAsync(CancellationToken cancellationToken)
    {
        var messages = await _canBusApi.GetMessagesAsync(cancellationToken);

        foreach (var message in messages) _messages.Add(message.Name, message);
    }

    protected override async IAsyncEnumerable<SourceCanFrame> GetFramesAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken)
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
                Data = ConvertStringToByteArray(record.Data, _bigOrLittle == "little"),
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

    private static byte[] ConvertStringToByteArray(string? recordData, bool isLittleEndian)
    {
        if (recordData is null)
            return [];


        if (recordData.StartsWith("0x"))
        {
            var convertedNumber = Convert.ToUInt64(recordData[2..], 16);
            var bytes = BitConverter.GetBytes(convertedNumber);

            if (BitConverter.IsLittleEndian != isLittleEndian)
                Array.Reverse(bytes);

            return bytes;
        }


        if (recordData.All(char.IsDigit))
        {
            var value = ulong.Parse(recordData);
            var bytes = BitConverter.GetBytes(value);

            if (BitConverter.IsLittleEndian != isLittleEndian)
                Array.Reverse(bytes);

            return bytes;
        }


        if (recordData.StartsWith("0b"))
        {
            var binaryString = recordData.Substring(2);
            var value = Convert.ToUInt32(binaryString, 2);
            var bytes = BitConverter.GetBytes(value);

            if (BitConverter.IsLittleEndian != isLittleEndian)
                Array.Reverse(bytes);

            return bytes;
        }

        return Convert.FromHexString(recordData.PadLeft(16, '0'));
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

    private enum ConvertFrom
    {
        Hex,
        Decimal,
        Binary
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
