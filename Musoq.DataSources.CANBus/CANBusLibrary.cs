using System;
using System.Globalization;
using System.Linq;
using DbcParserLib;
using Musoq.DataSources.CANBus.Components;
using Musoq.Plugins;
using Musoq.Plugins.Attributes;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Musoq.DataSources.CANBus;

/// <summary>
/// Set of functions to work with CAN bus data.
/// </summary>
public class CANBusLibrary : LibraryBase
{
    private static readonly DateTime UnixEpoch = new(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
    
    /// <summary>
    /// Converts bytes to encoded signal of a message for CAN bus.
    /// </summary>
    /// <param name="message">Message frame.</param>
    /// <param name="name">Name of the signal.</param>
    /// <param name="value">Value to encode.</param>
    /// <returns>Encoded signal.</returns>
    /// <exception cref="InvalidOperationException"></exception>
    [BindableMethod]
    public ulong EncodeMessage([InjectSpecificSource(typeof(ICANDbcMessage))] ICANDbcMessage message, string name, byte value) => EncodeMessage(message, name, value, v => v);
    
    /// <summary>
    /// Converts short to encoded signal of a message for CAN bus.
    /// </summary>
    /// <param name="message">Message frame.</param>
    /// <param name="name">Name of the signal.</param>
    /// <param name="value">Value to encode.</param>
    /// <returns>Encoded signal.</returns>
    /// <exception cref="InvalidOperationException"></exception>
    [BindableMethod]
    public ulong EncodeMessage([InjectSpecificSource(typeof(ICANDbcMessage))] ICANDbcMessage message, string name, short value) => EncodeMessage(message, name, value, v => v);
    
    /// <summary>
    /// Converts ushort to encoded signal of a message for CAN bus.
    /// </summary>
    /// <param name="message">Message frame.</param>
    /// <param name="name">Name of the signal.</param>
    /// <param name="value">Value to encode.</param>
    /// <returns>Encoded signal.</returns>
    /// <exception cref="InvalidOperationException"></exception>
    [BindableMethod]
    public ulong EncodeMessage([InjectSpecificSource(typeof(ICANDbcMessage))] ICANDbcMessage message, string name, ushort value) => EncodeMessage(message, name, value, v => v);
    
    /// <summary>
    /// Converts int to encoded signal of a message for CAN bus.
    /// </summary>
    /// <param name="message">Message frame.</param>
    /// <param name="name">Name of the signal.</param>
    /// <param name="value">Value to encode.</param>
    /// <returns>Encoded signal.</returns>
    /// <exception cref="InvalidOperationException"></exception>
    [BindableMethod]
    public ulong EncodeMessage([InjectSpecificSource(typeof(ICANDbcMessage))] ICANDbcMessage message, string name, int value) => EncodeMessage(message, name, value, v => v);
    
    /// <summary>
    /// Converts uint to encoded signal of a message for CAN bus.
    /// </summary>
    /// <param name="message">Message frame.</param>
    /// <param name="name">Name of the signal.</param>
    /// <param name="value">Value to encode.</param>
    /// <returns>Encoded signal.</returns>
    /// <exception cref="InvalidOperationException"></exception>
    [BindableMethod]
    public ulong EncodeMessage([InjectSpecificSource(typeof(ICANDbcMessage))] ICANDbcMessage message, string name, uint value) => EncodeMessage(message, name, value, v => v);
    
    /// <summary>
    /// Converts long to encoded signal of a message for CAN bus.
    /// </summary>
    /// <param name="message">Message frame.</param>
    /// <param name="name">Name of the signal.</param>
    /// <param name="value">Value to encode.</param>
    /// <returns>Encoded signal.</returns>
    /// <exception cref="InvalidOperationException"></exception>
    [BindableMethod]
    public ulong EncodeMessage([InjectSpecificSource(typeof(ICANDbcMessage))] ICANDbcMessage message, string name, long value) => EncodeMessage(message, name, value, v => v);
    
    /// <summary>
    /// Converts ulong to encoded signal of a message for CAN bus.
    /// </summary>
    /// <param name="message">Message frame.</param>
    /// <param name="name">Name of the signal.</param>
    /// <param name="value">Value to encode.</param>
    /// <returns>Encoded signal.</returns>
    /// <exception cref="InvalidOperationException"></exception>
    [BindableMethod]
    public ulong EncodeMessage([InjectSpecificSource(typeof(ICANDbcMessage))] ICANDbcMessage message, string name, ulong value) => EncodeMessage(message, name, value, v => v);
    
    /// <summary>
    /// Converts float to encoded signal of a message for CAN bus.
    /// </summary>
    /// <param name="message">Message frame.</param>
    /// <param name="name">Name of the signal.</param>
    /// <param name="value">Value to encode.</param>
    /// <returns>Encoded signal.</returns>
    /// <exception cref="InvalidOperationException"></exception>
    [BindableMethod]
    public ulong EncodeMessage([InjectSpecificSource(typeof(ICANDbcMessage))] ICANDbcMessage message, string name, float value) => EncodeMessage(message, name, value, v => v);
    
    /// <summary>
    /// Converts double to encoded signal of a message for CAN bus.
    /// </summary>
    /// <param name="message">Message frame.</param>
    /// <param name="name">Name of the signal.</param>
    /// <param name="value">Value to encode.</param>
    /// <returns>Encoded signal.</returns>
    /// <exception cref="InvalidOperationException"></exception>
    [BindableMethod]
    public ulong EncodeMessage([InjectSpecificSource(typeof(ICANDbcMessage))] ICANDbcMessage message, string name, double value) => EncodeMessage(message, name, value, v => v);
    
    /// <summary>
    /// Converts structured json to encoded signals of a message for CAN bus. It's structure is of `{ signalName1: signalValue1, ..., signalNameN : signalValueN }`
    /// </summary>
    /// <param name="message">Message frame.</param>
    /// <param name="jsonValues">Structured json. It's structure is of { signalName1: signalValue1, ..., signalNameN : signalValueN }</param>
    /// <returns>Encoded signals.</returns>
    /// <exception cref="InvalidOperationException"></exception>
    [BindableMethod]
    public ulong EncodeMessage([InjectSpecificSource(typeof(ICANDbcMessage))] ICANDbcMessage message, string jsonValues)
    {
        using var reader = new JTokenReader(JObject.Parse(jsonValues));
        var obj = JObject.Load(reader);

        ulong encodedMessage = 0;
        foreach (var key in obj.Properties())
        {
            if (!obj.TryGetValue(key.Name, out var value)) continue;
            
            var signal = message.Message?.Signals.FirstOrDefault(s => s.Name == key.Name);
                
            if (signal is null)
                throw new InvalidOperationException($"Signal with name {key.Name} does not exist.");

            var valueString = value.ToString(Formatting.None, []);
            if (!double.TryParse(valueString, NumberStyles.Any, CultureInfo.CurrentCulture, out var doubleValue))
                throw new InvalidOperationException($"Value {valueString} cannot be parsed to double.");
                
            encodedMessage |= Packer.TxSignalPack(doubleValue, signal);
        }

        return encodedMessage;
    }
    
    /// <summary>
    /// Treats byte as encoded signal of a message for CAN bus and decodes it.
    /// </summary>
    /// <param name="message">Message frame.</param>
    /// <param name="name">Name of the signal.</param>
    /// <param name="value">Value to decode.</param>
    /// <returns>Decoded signal.</returns>
    /// <exception cref="InvalidOperationException"></exception>
    [BindableMethod]
    public double DecodeMessage([InjectSpecificSource(typeof(ICANDbcMessage))] ICANDbcMessage message, string name, byte value) => DecodeMessage(message, name, value, v => v);
    
    /// <summary>
    /// Treats short as encoded signal of a message for CAN bus and decodes it.
    /// </summary>
    /// <param name="message">Message frame.</param>
    /// <param name="name">Name of the signal.</param>
    /// <param name="value">Value to decode.</param>
    /// <returns>Decoded signal.</returns>
    /// <exception cref="InvalidOperationException"></exception>
    [BindableMethod]
    public double DecodeMessage([InjectSpecificSource(typeof(ICANDbcMessage))] ICANDbcMessage message, string name, short value) => DecodeMessage(message, name, value,
        v =>
        {
            ulong result = 0;
            
            result |= (ushort)v;
            
            return result;
        });
    
    /// <summary>
    /// Treats ushort as encoded signal of a message for CAN bus and decodes it.
    /// </summary>
    /// <param name="message">Message frame.</param>
    /// <param name="name">Name of the signal.</param>
    /// <param name="value">Value to decode.</param>
    /// <returns>Decoded signal.</returns>
    /// <exception cref="InvalidOperationException"></exception>
    [BindableMethod]
    public double DecodeMessage([InjectSpecificSource(typeof(ICANDbcMessage))] ICANDbcMessage message, string name, ushort value) => DecodeMessage(message, name, value, v => v);
    
    /// <summary>
    /// Treats int as encoded signal of a message for CAN bus and decodes it.
    /// </summary>
    /// <param name="message">Message frame.</param>
    /// <param name="name">Name of the signal.</param>
    /// <param name="value">Value to decode.</param>
    /// <returns>Decoded signal.</returns>
    /// <exception cref="InvalidOperationException"></exception>
    [BindableMethod]
    public double DecodeMessage([InjectSpecificSource(typeof(ICANDbcMessage))] ICANDbcMessage message, string name, int value) => DecodeMessage(message, name, value,
        v =>
        {
            ulong result = 0;
            
            result |= (uint)v;
            
            return result;
        });
    
    /// <summary>
    /// Treats uint as encoded signal of a message for CAN bus and decodes it.
    /// </summary>
    /// <param name="message">Message frame.</param>
    /// <param name="name">Name of the signal.</param>
    /// <param name="value">Value to decode.</param>
    /// <returns>Decoded signal.</returns>
    /// <exception cref="InvalidOperationException"></exception>
    [BindableMethod]
    public double DecodeMessage([InjectSpecificSource(typeof(ICANDbcMessage))] ICANDbcMessage message, string name, uint value) => DecodeMessage(message, name, value, v => v);
    
    /// <summary>
    /// Treats long as encoded signal of a message for CAN bus and decodes it.
    /// </summary>
    /// <param name="message">Message frame.</param>
    /// <param name="name">Name of the signal.</param>
    /// <param name="value">Value to decode.</param>
    /// <returns>Decoded signal.</returns>
    /// <exception cref="InvalidOperationException"></exception>
    [BindableMethod]
    public double DecodeMessage([InjectSpecificSource(typeof(ICANDbcMessage))] ICANDbcMessage message, string name, long value) => DecodeMessage(message, name, value,
        v =>
        {
            ulong result = 0;
            
            result |= (ulong)v;
            
            return result;
        });
    
    /// <summary>
    /// Treats ulong as encoded signal of a message for CAN bus and decodes it.
    /// </summary>
    /// <param name="message">Message frame.</param>
    /// <param name="name">Name of the signal.</param>
    /// <param name="value">Value to decode.</param>
    /// <returns>Decoded signal.</returns>
    /// <exception cref="InvalidOperationException"></exception>
    [BindableMethod]
    public double DecodeMessage([InjectSpecificSource(typeof(ICANDbcMessage))] ICANDbcMessage message, string name, ulong value) => DecodeMessage(message, name, value, v => v);

    /// <summary>
    /// Converts timestamp to date time offset.
    /// </summary>
    /// <param name="timestamp">Timestamp.</param>
    /// <param name="resolution">Resolution of the timestamp. Possible values are: s, ms, us, ns.</param>
    /// <returns>Date time offset.</returns>
    [BindableMethod]
    public DateTimeOffset? FromTimestamp(ulong timestamp, string resolution)
    {
        return resolution switch
        {
            "s" => UnixEpoch.AddSeconds(timestamp),
            "ms" => UnixEpoch.AddMilliseconds(timestamp),
            "us" => UnixEpoch.AddTicks((long) timestamp),
            "ns" => UnixEpoch.AddTicks((long) timestamp),
            _ => throw new InvalidOperationException($"Resolution {resolution} is not supported.")
        };
    }
    
    /// <summary>
    /// Creates the date time offset from given date and time in a given format.
    /// </summary>
    /// <param name="date">Date.</param>
    /// <param name="format">Format of the date.</param>
    /// <returns>Date time offset.</returns>
    /// <exception cref="InvalidOperationException"></exception>
    [BindableMethod]
    public DateTimeOffset? ToDateTimeOffset(string date, string format)
    {
        if (!DateTimeOffset.TryParseExact(date, format, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var dateTimeOffset))
            throw new InvalidOperationException($"Date {date} cannot be parsed to DateTimeOffset with format {format}.");
        
        return dateTimeOffset;
    }
    
    private static ulong EncodeMessage<TNumeric>([InjectSpecificSource(typeof(ICANDbcMessage))] ICANDbcMessage message, string name, TNumeric value, Func<TNumeric, double> convertToDouble)
        where TNumeric : struct, IComparable, IComparable<TNumeric>, IConvertible, IEquatable<TNumeric>, IFormattable
    {
        var signal = message.Message?.Signals.FirstOrDefault(s => s.Name == name);
        
        if (signal is null)
            throw new InvalidOperationException($"Signal with name {name} does not exist.");
        
        var doubleValue = convertToDouble(value);
        return Packer.TxSignalPack(doubleValue, signal);
    }
    
    private static double DecodeMessage<TNumeric>([InjectSpecificSource(typeof(ICANDbcMessage))] ICANDbcMessage message, string name, TNumeric value, Func<TNumeric, ulong> convertToUInt64)
        where TNumeric : struct, IComparable, IComparable<TNumeric>, IConvertible, IEquatable<TNumeric>, IFormattable
    {
        var signal = message.Message?.Signals.FirstOrDefault(s => s.Name == name);
        
        if (signal is null)
            throw new InvalidOperationException($"Signal with name {name} does not exist.");

        var uint64Value = convertToUInt64(value);
        return Packer.RxSignalUnpack(uint64Value, signal);
    }
}