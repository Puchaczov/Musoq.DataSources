#nullable enable

using System;

namespace Musoq.DataSources.Search.Components.Bytes;

/// <summary>
///     Holds the exact byte values and bit masks produced by the versioned
///     byte-pattern compiler.
/// </summary>
internal sealed class SearchBytePattern
{
    public SearchBytePattern(
        ReadOnlySpan<byte> bytes,
        ReadOnlySpan<byte> masks,
        SearchByteWindowOptions? window = null)
    {
        if (bytes.Length == 0)
            throw new ArgumentException("A byte pattern must contain at least one byte.", nameof(bytes));
        if (bytes.Length != masks.Length)
            throw new ArgumentException("Byte and mask sequences must have equal lengths.", nameof(masks));

        Bytes = bytes.ToArray();
        Masks = masks.ToArray();
        Window = window ?? SearchByteWindowOptions.None;
    }

    public ReadOnlyMemory<byte> Bytes { get; }

    public ReadOnlyMemory<byte> Masks { get; }

    public SearchByteWindowOptions Window { get; }

    public int Length => Bytes.Length;
}
