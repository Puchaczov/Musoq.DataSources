using System.Dynamic;
using System.Linq;
using DbcParserLib;
using DbcParserLib.Model;
using Musoq.Plugins.Attributes;

namespace Musoq.DataSources.CANBus.Components;

[DynamicObjectPropertyTypeHint("RawData", typeof(ulong))]
[DynamicObjectPropertyDefaultTypeHint("", typeof(double))]
internal class SignalFrame : DynamicObject
{
    private readonly ulong _rawData;
    private readonly Message? _message;

    public SignalFrame(ulong rawData, Message? message)
    {
        _rawData = rawData;
        _message = message;
    }
    
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