using System.Dynamic;
using System.Linq;
using DbcParserLib;
using DbcParserLib.Model;
using Musoq.Plugins.Attributes;

namespace Musoq.DataSources.CANBus.Components;

/// <summary>
/// Represents a single CAN message.
/// </summary>
[DynamicObjectPropertyTypeHint("RawData", typeof(ulong))]
[DynamicObjectPropertyDefaultTypeHint(typeof(double))]
public class SignalFrameEntity : DynamicObject
{
    private readonly ulong _rawData;
    private readonly Message? _message;

    /// <summary>
    /// Creates a new instance of <see cref="SignalFrameEntity"/>.
    /// </summary>
    /// <param name="rawData">Gets the raw data.</param>
    /// <param name="message">Gets the message.</param>
    public SignalFrameEntity(ulong rawData, Message? message)
    {
        _rawData = rawData;
        _message = message;
    }

    /// <inheritdoc />
    public override bool TryGetMember(GetMemberBinder binder, out object? result)
    {
        if (binder.Name == "RawData")
        {
            result = _rawData;
            return true;
        }
        
        if (_message is null)
        {
            result = null;
            return false;
        }
        
        var signal = _message.Signals.FirstOrDefault(f => f.Name == binder.Name);

        if (signal is null)
        {
            result = null;
            return false;
        }
        
        result = Packer.RxSignalUnpack(_rawData, signal);
        return true;
    }
}