using System.Collections.Generic;
using System.Linq;
using DbcParserLib;
using DbcParserLib.Model;
using Musoq.DataSources.CANBus.Components;
using Musoq.DataSources.CANBus.Signals;
using Musoq.Plugins.Attributes;

// ReSharper disable InconsistentNaming

namespace Musoq.DataSources.CANBus.Messages;

/// <summary>
/// Represents a single CAN message.
/// </summary>
public class MessageEntity : ICANDbcMessage
{
    private SignalEntity[]? _signals;
    
    /// <summary>
    /// Creates a new instance of <see cref="MessageEntity"/>.
    /// </summary>
    /// <param name="message">The message.</param>
    public MessageEntity(Message message)
    {
        Message = message;
    }

    /// <summary>
    /// Gets the message.
    /// </summary>
    public Message Message { get; }
    
    /// <summary>
    /// Gets the can message id.
    /// </summary>
    public uint Id => Message.ID;
    
    /// <summary>
    /// Determine whether the can message is extended.
    /// </summary>
    public bool IsExtId => Message.IsExtID;
    
    /// <summary>
    /// Gets the can message name.
    /// </summary>
    public string Name => Message.Name;
    
    /// <summary>
    /// Gets the can message data length code.
    /// </summary>
    public ushort DLC => Message.DLC;
    
    /// <summary>
    /// Gets the can message transmitter.
    /// </summary>
    public string Transmitter => Message.Transmitter;
    
    /// <summary>
    /// Gets the can message comment.
    /// </summary>
    public string Comment => Message.Comment;

    /// <summary>
    /// Gets the can message cycle time.
    /// </summary>
    public int CycleTime => Message.CycleTime(out var cycleTime) ? cycleTime : 0;

    /// <summary>
    /// Gets the can message signals.
    /// </summary>
    [BindablePropertyAsTable]
    public IEnumerable<SignalEntity> Signals => _signals ??= Message.Signals.Select((f, i) => new SignalEntity(f, Message, i)).ToArray();
}