#nullable enable
using System;
using Musoq.DataSources.Common;

namespace Musoq.DataSources.CANBus;

/// <summary>Reviewed source-row string getters. These never traverse child tables or perform API calls.</summary>
internal static class CANBusPatternColumns
{
    internal static readonly PatternColumnBindings<Messages.MessageEntity> MessageEntity = new(
        (nameof(Messages.MessageEntity.Name), static row => row.Name),
        (nameof(Messages.MessageEntity.Transmitter), static row => row.Transmitter),
        (nameof(Messages.MessageEntity.Comment), static row => row.Comment)
    );

    internal static readonly PatternColumnBindings<Signals.SignalEntity> SignalEntity = new(
        (nameof(Signals.SignalEntity.Name), static row => row.Name),
        (nameof(Signals.SignalEntity.Unit), static row => row.Unit),
        (nameof(Signals.SignalEntity.Comment), static row => row.Comment),
        (nameof(Signals.SignalEntity.Multiplexing), static row => row.Multiplexing),
        (nameof(Signals.SignalEntity.MessageName), static row => row.MessageName)
    );

    internal static IPatternColumnBindings? Get(string name) => name.ToLowerInvariant() switch
    {
        "messages" => MessageEntity,
        "signals" => SignalEntity,
        _ => null
    };
}
