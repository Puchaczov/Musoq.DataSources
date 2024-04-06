using DbcParserLib.Model;
using Musoq.DataSources.CANBus.Components;

// ReSharper disable InconsistentNaming

namespace Musoq.DataSources.CANBus.Messages;

/// <summary>
/// Represents a single CAN message.
/// </summary>
public class MessageEntity : ICANDbcMessage
{
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
    public int CycleTime => Message.CycleTime;
}