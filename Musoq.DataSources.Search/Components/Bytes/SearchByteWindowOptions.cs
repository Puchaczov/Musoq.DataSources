#nullable enable

using System;

using Musoq.DataSources.Search.Components.Diagnostics;

namespace Musoq.DataSources.Search.Components.Bytes;

/// <summary>
///     Describes the bounded byte window requested around each raw-byte match.
/// </summary>
internal sealed class SearchByteWindowOptions
{
    public const int MaxWindowBytes = 1_048_576;

    public static SearchByteWindowOptions None { get; } = new(0, 0, enabled: false);

    private SearchByteWindowOptions(int beforeBytes, int afterBytes, bool enabled)
    {
        BeforeBytes = beforeBytes;
        AfterBytes = afterBytes;
        IsEnabled = enabled;
    }

    public int BeforeBytes { get; }

    public int AfterBytes { get; }

    public bool IsEnabled { get; }

    public static SearchByteWindowOptions Create(
        long beforeBytes,
        long afterBytes,
        int patternLength)
    {
        if (beforeBytes < 0)
            throw new ArgumentOutOfRangeException(nameof(beforeBytes));
        if (afterBytes < 0)
            throw new ArgumentOutOfRangeException(nameof(afterBytes));
        if (patternLength <= 0)
            throw new ArgumentOutOfRangeException(nameof(patternLength));

        var remaining = (long)MaxWindowBytes - patternLength;
        if (beforeBytes > MaxWindowBytes ||
            afterBytes > MaxWindowBytes ||
            remaining < 0 ||
            beforeBytes > remaining ||
            afterBytes > remaining - beforeBytes)
        {
            throw new SearchResourceLimitException(
                SearchDiagnosticCatalog.ResourceLimit(
                    "byte-window",
                    MaxWindowBytes),
                budgetCode: "byte-window");
        }

        return beforeBytes == 0 && afterBytes == 0
            ? None
            : new SearchByteWindowOptions(
                (int)beforeBytes,
                (int)afterBytes,
                enabled: true);
    }
}

/// <summary>
///     Immutable metadata and optional bytes for one bounded raw-byte window.
/// </summary>
internal sealed class SearchByteWindow
{
    public SearchByteWindow(
        long startByteOffset,
        long byteLength,
        bool complete,
        byte[]? bytes)
    {
        if (startByteOffset < 0)
            throw new ArgumentOutOfRangeException(nameof(startByteOffset));
        if (byteLength < 0)
            throw new ArgumentOutOfRangeException(nameof(byteLength));
        if (bytes is not null && bytes.LongLength != byteLength)
            throw new ArgumentException(
                "Window bytes must have the declared byte length.",
                nameof(bytes));

        StartByteOffset = startByteOffset;
        ByteLength = byteLength;
        Complete = complete;
        Bytes = bytes;
    }

    public long StartByteOffset { get; }

    public long ByteLength { get; }

    public bool Complete { get; }

    public byte[]? Bytes { get; }
}
