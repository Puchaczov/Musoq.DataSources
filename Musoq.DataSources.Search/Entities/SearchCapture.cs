#nullable enable

using Musoq.Schema.Attributes;

namespace Musoq.DataSources.Search.Entities;

/// <summary>
///     One capture emitted by a regular-expression match.
/// </summary>
public sealed class SearchCapture
{
    /// <summary>
    ///     Creates a regular-expression capture row.
    /// </summary>
    /// <param name="groupName">The named group, or null for an unnamed capturing group.</param>
    /// <param name="groupIndex">The one-based .NET group number.</param>
    /// <param name="captureIndex">The zero-based capture ordinal within the group.</param>
    /// <param name="success">Whether the group produced a capture.</param>
    /// <param name="text">The captured text, or null when the group did not participate.</param>
    /// <param name="byteOffset">The original-byte start when losslessly mapped.</param>
    /// <param name="byteLength">The original-byte length when losslessly mapped.</param>
    /// <param name="utf16Column">The zero-based UTF-16 column within the containing record.</param>
    /// <param name="utf16Length">The captured length in UTF-16 code units.</param>
    public SearchCapture(
        string? groupName,
        int groupIndex,
        int captureIndex,
        bool success,
        string? text,
        long? byteOffset,
        long? byteLength,
        long? utf16Column,
        long? utf16Length)
    {
        GroupName = groupName;
        GroupIndex = groupIndex;
        CaptureIndex = captureIndex;
        Success = success;
        Text = text;
        ByteOffset = byteOffset;
        ByteLength = byteLength;
        Utf16Column = utf16Column;
        Utf16Length = utf16Length;
    }

    /// <summary>
    ///     Gets the named group, or null for an unnamed capturing group.
    /// </summary>
    [EntityProperty]
    public string? GroupName { get; }

    /// <summary>
    ///     Gets the one-based .NET group number. Group zero is the complete match and is not emitted.
    /// </summary>
    [EntityProperty]
    public int GroupIndex { get; }

    /// <summary>
    ///     Gets the zero-based capture ordinal within the group.
    /// </summary>
    [EntityProperty]
    public int CaptureIndex { get; }

    /// <summary>
    ///     Gets a value indicating whether the group produced a capture.
    /// </summary>
    [EntityProperty]
    public bool Success { get; }

    /// <summary>
    ///     Gets the captured text, or null when the group did not participate.
    /// </summary>
    [EntityProperty]
    public string? Text { get; }

    /// <summary>
    ///     Gets the original-byte start when the capture is losslessly mapped.
    /// </summary>
    [EntityProperty]
    public long? ByteOffset { get; }

    /// <summary>
    ///     Gets the original-byte length when the capture is losslessly mapped.
    /// </summary>
    [EntityProperty]
    public long? ByteLength { get; }

    /// <summary>
    ///     Gets the zero-based UTF-16 column within the containing record when available.
    /// </summary>
    [EntityProperty]
    public long? Utf16Column { get; }

    /// <summary>
    ///     Gets the capture length in UTF-16 code units when available.
    /// </summary>
    [EntityProperty]
    public long? Utf16Length { get; }
}
