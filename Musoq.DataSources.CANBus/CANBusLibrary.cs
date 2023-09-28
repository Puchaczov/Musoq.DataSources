using System;
using System.Collections;
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
    internal byte[] EncodeMessage([InjectSpecificSource(typeof(MessageFrame))] MessageFrame message, string name, byte[] value)
    {
        var signal = message.Message?.Signals.FirstOrDefault(s => s.Name == name);
        
        if (signal is null)
            throw new InvalidOperationException($"Signal with name {name} does not exist.");
        
        var doubleValue = BitConverter.ToDouble(value, 0);
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
    internal byte[] EncodeMessage([InjectSpecificSource(typeof(MessageFrame))] MessageFrame message, string jsonValues)
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
    internal byte[] DecodeMessage([InjectSpecificSource(typeof(MessageFrame))] MessageFrame message, string name, byte[] value)
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
    internal byte[][] DecodeMessage([InjectSpecificSource(typeof(MessageFrame))] MessageFrame message, byte[] value)
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

    /// <summary>
    /// Converts boolean value to bytes.
    /// </summary>
    /// <param name="value">Value to convert.</param>
    /// <returns>Bytes.</returns>
    public byte[] ToBytes(bool value)
    {
        return BitConverter.GetBytes(value);
    }

    /// <summary>
    /// Converts short value to bytes.
    /// </summary>
    /// <param name="value">Value to convert.</param>
    /// <returns>Bytes.</returns>
    public byte[] ToBytes(short value)
    {
        return BitConverter.GetBytes(value);
    }

    /// <summary>
    /// Converts ushort value to bytes.
    /// </summary>
    /// <param name="value">Value to convert.</param>
    /// <returns>Bytes.</returns>
    public byte[] ToBytes(ushort value)
    {
        return BitConverter.GetBytes(value);
    }

    /// <summary>
    /// Converts int value to bytes.
    /// </summary>
    /// <param name="value">Value to convert.</param>
    /// <returns>Bytes.</returns>
    public byte[] ToBytes(int value)
    {
        return BitConverter.GetBytes(value);
    }


    /// <summary>
    /// Converts uint value to bytes.
    /// </summary>
    /// <param name="value">Value to convert.</param>
    /// <returns>Bytes.</returns>
    public byte[] ToBytes(uint value)
    {
        return BitConverter.GetBytes(value);
    }


    /// <summary>
    /// Converts long value to bytes.
    /// </summary>
    /// <param name="value">Value to convert.</param>
    /// <returns>Bytes.</returns>
    public byte[] ToBytes(long value)
    {
        return BitConverter.GetBytes(value);
    }


    /// <summary>
    /// Converts ulong value to bytes.
    /// </summary>
    /// <param name="value">Value to convert.</param>
    /// <returns>Bytes.</returns>
    public byte[] ToBytes(ulong value)
    {
        return BitConverter.GetBytes(value);
    }
    /// <summary>
    /// Converts bytes to boolean value.
    /// </summary>
    /// <param name="value">Byte array containing the value to convert.</param>
    /// <returns>Converted boolean value.</returns>
    public bool FromBytesToBool(byte[] value)
    {
        return BitConverter.ToBoolean(value, 0);
    }

    /// <summary>
    /// Converts bytes to a 16-bit signed integer.
    /// </summary>
    /// <param name="value">Byte array containing the value to convert.</param>
    /// <returns>Converted 16-bit signed integer.</returns>
    public short FromBytesToInt16(byte[] value)
    {
        return BitConverter.ToInt16(value, 0);
    }

    /// <summary>
    /// Converts bytes to a 16-bit unsigned integer.
    /// </summary>
    /// <param name="value">Byte array containing the value to convert.</param>
    /// <returns>Converted 16-bit unsigned integer.</returns>
    public ushort FromBytesToUInt16(byte[] value)
    {
        return BitConverter.ToUInt16(value, 0);
    }

    /// <summary>
    /// Converts bytes to a 32-bit signed integer.
    /// </summary>
    /// <param name="value">Byte array containing the value to convert.</param>
    /// <returns>Converted 32-bit signed integer.</returns>
    public int FromBytesToInt32(byte[] value)
    {
        return BitConverter.ToInt32(value, 0);
    }

    /// <summary>
    /// Converts bytes to a 32-bit unsigned integer.
    /// </summary>
    /// <param name="value">Byte array containing the value to convert.</param>
    /// <returns>Converted 32-bit unsigned integer.</returns>
    public uint FromBytesToUInt32(byte[] value)
    {
        return BitConverter.ToUInt32(value, 0);
    }

    /// <summary>
    /// Converts bytes to a 64-bit signed integer.
    /// </summary>
    /// <param name="value">Byte array containing the value to convert.</param>
    /// <returns>Converted 64-bit signed integer.</returns>
    public long FromBytesToInt64(byte[] value)
    {
        return BitConverter.ToInt64(value, 0);
    }
}