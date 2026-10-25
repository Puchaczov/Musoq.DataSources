using System;

namespace Musoq.DataSources.Json;

internal interface IJsonRecordProcessor : IDisposable
{
    void IDisposable.Dispose() { }

    long RowsRead { get; }

    void Complete();

    bool Process(ReadOnlySpan<byte> record);
}
