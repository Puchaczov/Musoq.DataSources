namespace Musoq.DataSources.CANBus.Components;

/// <summary>
/// Represents a single CAN frame.
/// </summary>
/// <param name="Id">The CAN frame identifier.</param>
/// <param name="Data">The CAN frame data.</param>
public record struct CANFrame(uint Id, byte[] Data);