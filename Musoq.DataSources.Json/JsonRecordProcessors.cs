using System;
using System.Runtime.CompilerServices;
using Musoq.Schema;

namespace Musoq.DataSources.Json;

internal readonly struct TypedJsonRecordProcessor<TRow, TMaterializer>(
    JsonQueryRowProcessor<TRow, TMaterializer> processor) : IJsonRecordProcessor
    where TMaterializer : struct, IQueryRowMaterializer<TRow>
{
    public long RowsRead => processor.RowsRead;

    public void Complete() => processor.Complete();

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Process(ReadOnlySpan<byte> record) => processor.Process(record);
}
