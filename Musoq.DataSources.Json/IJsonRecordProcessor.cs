using System;

namespace Musoq.DataSources.Json;

internal interface IJsonRecordProcessor
{
    long RowsRead { get; }

    void Complete();

    bool Process(ReadOnlySpan<byte> record);
}
