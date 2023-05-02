using System;
using System.Collections.Generic;
using System.IO;
using SharpCompress.Common;
using SharpCompress.Readers;

namespace Musoq.DataSources.Archives;

/// <summary>
/// A wrapper for the IEntry interface, providing additional functionality for reading and accessing entry data.
/// </summary>
public class EntryWrapper : IEntry
{
    private readonly IEntry _entry;

    /// <summary>
    /// Initializes a new instance of the EntryWrapper class.
    /// </summary>
    /// <param name="entry">The IEntry object that this wrapper is encapsulating.</param>
    /// <param name="reader">The IReader object responsible for reading entry data.</param>
    public EntryWrapper(IEntry entry, IReader reader)
    {
        _entry = entry;
        Reader = reader;
    }

    /// <summary>
    /// Gets the compression type of the entry.
    /// </summary>
    public CompressionType CompressionType => _entry.CompressionType;

    /// <summary>
    /// Gets the archived time of the entry, if available.
    /// </summary>
    public DateTime? ArchivedTime => _entry.ArchivedTime;

    /// <summary>
    /// Gets the compressed size of the entry.
    /// </summary>
    public long CompressedSize => _entry.CompressedSize;

    /// <summary>
    /// Gets the CRC value of the entry.
    /// </summary>
    public long Crc => _entry.Crc;

    /// <summary>
    /// Gets the created time of the entry, if available.
    /// </summary>
    public DateTime? CreatedTime => _entry.CreatedTime;

    /// <summary>
    /// Gets the unique key of the entry.
    /// </summary>
    public string Key => _entry.Key;

    /// <summary>
    /// Gets the link target of the entry, if applicable.
    /// </summary>
    public string LinkTarget => _entry.LinkTarget;

    /// <summary>
    /// Gets a value indicating whether the entry is a directory.
    /// </summary>
    public bool IsDirectory => _entry.IsDirectory;

    /// <summary>
    /// Gets a value indicating whether the entry is encrypted.
    /// </summary>
    public bool IsEncrypted => _entry.IsEncrypted;

    /// <summary>
    /// Gets a value indicating whether the entry is split after a specified volume.
    /// </summary>
    public bool IsSplitAfter => _entry.IsSplitAfter;

    /// <summary>
    /// Gets a value indicating whether the entry is part of a solid archive.
    /// </summary>
    public bool IsSolid => _entry.IsSolid;

    /// <summary>
    /// Gets the first volume index for the entry.
    /// </summary>
    public int VolumeIndexFirst => _entry.VolumeIndexFirst;

    /// <summary>
    /// Gets the last volume index for the entry.
    /// </summary>
    public int VolumeIndexLast => _entry.VolumeIndexLast;

    /// <summary>
    /// Gets the last accessed time of the entry, if available.
    /// </summary>
    public DateTime? LastAccessedTime => _entry.LastAccessedTime;

    /// <summary>
    /// Gets the last modified time of the entry, if available.
    /// </summary>
    public DateTime? LastModifiedTime => _entry.LastModifiedTime;

    /// <summary>
    /// Gets the uncompressed size of the entry.
    /// </summary>
    public long Size => _entry.Size;

    /// <summary>
    /// Gets the file attributes of the entry, if available.
    /// </summary>
    public int? Attrib => _entry.Attrib;

    /// <summary>
    /// Gets the text content of the entry, if
    /// available. This property reads the entry data using the provided IReader.
    /// </summary>
    public string TextContent
    {
        get
        {
            // Read entry data using the provided IReader
            using var stream = Reader.OpenEntryStream();
            using var reader = new StreamReader(stream);
            return reader.ReadToEnd();
        }
    }

    /// <summary>
    /// Gets the IReader object responsible for reading entry data.
    /// </summary>
    internal IReader Reader { get; }

    internal static IDictionary<string, int> NameToIndexMap { get; } = new Dictionary<string, int>()
    {
        {nameof(CompressionType), 0},
        {nameof(ArchivedTime), 1},
        {nameof(CompressedSize), 2},
        {nameof(Crc), 3},
        {nameof(CreatedTime), 4},
        {nameof(Key), 5},
        {nameof(LinkTarget), 6},
        {nameof(IsDirectory), 7},
        {nameof(IsEncrypted), 8},
        {nameof(IsSplitAfter), 9},
        {nameof(IsSolid), 10},
        {nameof(VolumeIndexFirst), 11},
        {nameof(VolumeIndexLast), 12},
        {nameof(LastAccessedTime), 13},
        {nameof(LastModifiedTime), 14},
        {nameof(Size), 15},
        {nameof(Attrib), 16},
        {nameof(TextContent), 17}
    };

    internal static IDictionary<int, Func<EntryWrapper, object>> IndexToMethodAccessMap { get; } =
        new Dictionary<int, Func<EntryWrapper, object>>()
        {
            {0, wrapper => wrapper.CompressionType},
            {1, wrapper => wrapper.ArchivedTime},
            {2, wrapper => wrapper.CompressedSize},
            {3, wrapper => wrapper.Crc},
            {4, wrapper => wrapper.CreatedTime},
            {5, wrapper => wrapper.Key},
            {6, wrapper => wrapper.LinkTarget},
            {7, wrapper => wrapper.IsDirectory},
            {8, wrapper => wrapper.IsEncrypted},
            {9, wrapper => wrapper.IsSplitAfter},
            {10, wrapper => wrapper.IsSolid},
            {11, wrapper => wrapper.VolumeIndexFirst},
            {12, wrapper => wrapper.VolumeIndexLast},
            {13, wrapper => wrapper.LastAccessedTime},
            {14, wrapper => wrapper.LastModifiedTime},
            {15, wrapper => wrapper.Size},
            {16, wrapper => wrapper.Attrib},
            {17, wrapper => wrapper.TextContent}
        };
}