using DbcParserLib.Model;

namespace Musoq.DataSources.CANBus.Components;

internal record SourceCanFrame(ulong Timestamp, CANFrame Frame, byte? Dlc, Message? Message);