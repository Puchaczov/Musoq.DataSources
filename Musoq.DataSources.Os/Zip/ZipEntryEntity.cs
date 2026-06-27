using System;
using System.IO.Compression;
using Musoq.Schema.Attributes;

namespace Musoq.DataSources.Os.Zip;

internal class ZipEntryEntity(ZipArchiveEntry entry)
{
    [EntityProperty]
    public string Name { get; } = entry.Name;

    [EntityProperty]
    public string FullName { get; } = entry.FullName;

    [EntityProperty]
    public long CompressedLength { get; } = entry.CompressedLength;

    [EntityProperty]
    public DateTimeOffset LastWriteTime { get; } = entry.LastWriteTime;

    [EntityProperty]
    public long Length { get; } = entry.Length;

    [EntityProperty]
    public bool IsDirectory { get; } = entry.Name == string.Empty;

    [EntityProperty]
    public int Level { get; } = entry.FullName.Trim('/').Split('/').Length - 1;
}
