using DbcParserLib.Model;

namespace Musoq.DataSources.CANBus.Components;

/// <summary>
///     Represents a single CAN message.
/// </summary>
public interface ICANDbcMessage
{
    /// <summary>
    ///     Gets the message.
    /// </summary>
    Message? Message { get; }
}