using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using DbcParserLib;
using DbcParserLib.Model;
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
    /// <summary>
    /// Converts bytes to encoded signal of a message for CAN bus.
    /// </summary>
    /// <param name="message">Message frame.</param>
    /// <param name="name">Name of the signal.</param>
    /// <param name="value">Value to encode.</param>
    /// <returns>Encoded signal.</returns>
    /// <exception cref="InvalidOperationException"></exception>
    [BindableMethod]
    public byte[] EncodeMessage([InjectSpecificSource(typeof(ICANDbcMessage))] ICANDbcMessage message, string name, byte[] value)
    {
        var signal = message.Message?.Signals.FirstOrDefault(s => s.Name == name);
        
        if (signal is null)
            throw new InvalidOperationException($"Signal with name {name} does not exist.");

        var copiedArray = new byte[sizeof(double)];
        Array.Copy(value, copiedArray, value.Length);
        var doubleValue = BitConverter.ToDouble(copiedArray, 0);
        var encodedMessage = Packer.TxSignalPack(doubleValue, signal);
        
        return BitConverter.GetBytes(encodedMessage);
    }
    
    /// <summary>
    /// Converts structured json to encoded signals of a message for CAN bus.
    /// </summary>
    /// <param name="message">Message frame.</param>
    /// <param name="jsonValues">Structured json. It's structure is of { signalName1: signalValue1, ..., signalNameN : signalValueN }</param>
    /// <returns>Encoded signals.</returns>
    /// <exception cref="InvalidOperationException"></exception>
    [BindableMethod]
    public byte[] EncodeMessage([InjectSpecificSource(typeof(ICANDbcMessage))] ICANDbcMessage message, string jsonValues)
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

            var valueString = value.ToString(Formatting.None, Array.Empty<JsonConverter>());
            if (!double.TryParse(valueString, NumberStyles.Any, CultureInfo.CurrentCulture, out var doubleValue))
                throw new InvalidOperationException($"Value {valueString} cannot be parsed to double.");
                
            encodedMessage |= Packer.TxSignalPack(doubleValue, signal);
        }

        return BitConverter.GetBytes(encodedMessage);
    }
    
    /// <summary>
    /// Treats bytes as encoded signal of a message for CAN bus and decodes it.
    /// </summary>
    /// <param name="message">Message frame.</param>
    /// <param name="name">Name of the signal.</param>
    /// <param name="value">Value to decode.</param>
    /// <returns>Decoded signal.</returns>
    /// <exception cref="InvalidOperationException"></exception>
    [BindableMethod]
    public byte[] DecodeMessage([InjectSpecificSource(typeof(ICANDbcMessage))] ICANDbcMessage message, string name, byte[] value)
    {
        var uint64Value = BitConverter.ToUInt64(value, 0);
        var signal = message.Message?.Signals.FirstOrDefault(s => s.Name == name);
        
        if (signal is null)
            throw new InvalidOperationException($"Signal with name {name} does not exist.");

        var unpackedValue = Packer.RxSignalUnpack(uint64Value, signal);
        var bytes = BitConverter.GetBytes(unpackedValue);
        
        return bytes;
    }

    /// <summary>
    /// Treats bytes as encoded signals of a message for CAN bus and decodes it.
    /// </summary>
    /// <param name="message">Message frame.</param>
    /// <param name="value">Value to decode.</param>
    /// <returns>Decoded signals.</returns>
    [BindableMethod]
    public byte[][] DecodeMessage([InjectSpecificSource(typeof(ICANDbcMessage))] ICANDbcMessage message, byte[] value)
    {
        var uint64Value = BitConverter.ToUInt64(value, 0);
        var signals = message.Message?.Signals ?? new List<Signal>();
        var values = new byte[signals.Count][];

        for (var index = 0; index < signals.Count; index++)
        {
            var signal = signals[index];
            var unpackedValue = Packer.RxSignalUnpack(uint64Value, signal);
            var bytes = BitConverter.GetBytes(unpackedValue);
            values[index] = bytes;
        }

        return values;
    }
}