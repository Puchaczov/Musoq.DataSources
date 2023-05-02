using System;
using System.Collections.Generic;
using System.IO;
using SharpCompress.Common;
using SharpCompress.Readers;

namespace Musoq.DataSources.Archives;

public class EntryWrapper : IEntry
{
    private readonly IEntry _entry;

    public EntryWrapper(IEntry entry, IReader reader)
    {
        _entry = entry;
        Reader = reader;
    }

    public CompressionType CompressionType => _entry.CompressionType;

    public DateTime? ArchivedTime => _entry.ArchivedTime;

    public long CompressedSize => _entry.CompressedSize;

    public long Crc => _entry.Crc;

    public DateTime? CreatedTime => _entry.CreatedTime;

    public string Key => _entry.Key;
    
    public string LinkTarget => _entry.LinkTarget;
    
    public bool IsDirectory => _entry.IsDirectory;
    
    public bool IsEncrypted => _entry.IsEncrypted;
    
    public bool IsSplitAfter => _entry.IsSplitAfter;
    
    public bool IsSolid => _entry.IsSolid;
    
    public int VolumeIndexFirst => _entry.VolumeIndexFirst;
    
    public int VolumeIndexLast => _entry.VolumeIndexLast;
    
    public DateTime? LastAccessedTime => _entry.LastAccessedTime;
    
    public DateTime? LastModifiedTime => _entry.LastModifiedTime;
    
    public long Size => _entry.Size;
    
    public int? Attrib => _entry.Attrib;

    public string TextContent
    {
        get
        {
            using var stream = Reader.OpenEntryStream();
            using var reader = new StreamReader(stream);
            return reader.ReadToEnd();
        }
    }
    
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