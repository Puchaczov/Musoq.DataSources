using System.Collections.Generic;
using System.Linq;
using DbcParserLib.Model;
using Musoq.Plugins.Attributes;

namespace Musoq.DataSources.CANBus.Signals;

/// <summary>
/// Represents a single CAN signal.
/// </summary>
public class SignalEntity
{
    private readonly Signal _signal;
    private readonly Message _message;

    private ValueMapEntity[]? _valueMapEntities;

    /// <summary>
    /// Creates a new instance of <see cref="SignalEntity"/>.
    /// </summary>
    /// <param name="signal">The signal.</param>
    /// <param name="message">The message.</param>
    /// <param name="order">The order of the signal in the message.</param>
    public SignalEntity(Signal signal, Message message, int order)
    {
        _signal = signal;
        _message = message;
        MessageOrder = order;
    }
    
    /// <summary>
    /// Gets the can signal id.
    /// </summary>
    public uint Id => _signal.Parent.ID;
    
    /// <summary>
    /// Gets the can signal message id.
    /// </summary>
    public uint MessageId => _message.ID;
    
    /// <summary>
    /// Gets the can signal name.
    /// </summary>
    public string Name => _signal.Name;
    
    /// <summary>
    /// Gets the can signal start bit.
    /// </summary>
    public ushort StartBit => _signal.StartBit;
    
    /// <summary>
    /// Gets the can signal length.
    /// </summary>
    public ushort Length => _signal.Length;
    
    /// <summary>
    /// Gets the can signal byte order.
    /// </summary>
    public byte ByteOrder => _signal.ByteOrder;
    
    /// <summary>
    /// Gets the can signal initial value.
    /// </summary>
    public double InitialValue => _signal.InitialValue;
    
    /// <summary>
    /// Gets the can signal factor.
    /// </summary>
    public double Factor => _signal.Factor;
    
    /// <summary>
    /// Gets the can signal is integer.
    /// </summary>
    public bool IsInteger => _signal.IsInteger;
    
    /// <summary>
    /// Gets the can signal offset.
    /// </summary>
    public double Offset => _signal.Offset;
    
    /// <summary>
    /// Gets the can signal minimum.
    /// </summary>
    public double Minimum => _signal.Minimum;
    
    /// <summary>
    /// Gets the can signal maximum.
    /// </summary>
    public double Maximum => _signal.Maximum;
    
    /// <summary>
    /// Gets the can signal unit.
    /// </summary>
    public string Unit => _signal.Unit;
    
    /// <summary>
    /// Gets the can signal receiver.
    /// </summary>
    public string[] Receiver => _signal.Receiver;
    
    /// <summary>
    /// Gets the can signal comment.
    /// </summary>
    public string Comment => _signal.Comment;
    
    /// <summary>
    /// Gets the can signal multiplexing.
    /// </summary>
    public string Multiplexing => _signal.Multiplexing;
    
    /// <summary>
    /// Gets the message name that the signal belongs to.
    /// </summary>
    public string MessageName => _message.Name;
    
    /// <summary>
    /// Order of signal within the message definition.
    /// </summary>
    public int MessageOrder { get; }

    /// <summary>
    /// Gets the map of values and names can be observed in the signal.
    /// </summary>
    [BindablePropertyAsTable]
    public ValueMapEntity[] ValueMap
    {
        get
        {
            return _valueMapEntities ??= _signal.ValueTableMap.Select(x => new ValueMapEntity(x)).ToArray();
        }
    }
}