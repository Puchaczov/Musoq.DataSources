using System;
using System.Collections.Generic;
using Musoq.Schema;
using Musoq.Schema.DataSources;

namespace Musoq.DataSources.Os.Metadata;

internal static class SchemaMetadataHelper
{
    public static readonly IReadOnlyDictionary<string, int> MetadataNameToIndexMap;
    public static readonly IReadOnlyDictionary<int, Func<MetadataEntity, object?>> MetadataIndexToMethodAccessMap;
    public static readonly ISchemaColumn[] MetadataColumns;

    static SchemaMetadataHelper()
    {
        MetadataNameToIndexMap = new Dictionary<string, int>
        {
            { nameof(MetadataEntity.FullName), 0 },
            { nameof(MetadataEntity.DirectoryName), 1 },
            { nameof(MetadataEntity.TagName), 2 },
            { nameof(MetadataEntity.Description), 3 }
        };

        MetadataIndexToMethodAccessMap = new Dictionary<int, Func<MetadataEntity, object?>>
        {
            { 0, entity => entity.FullName },
            { 1, entity => entity.DirectoryName },
            { 2, entity => entity.TagName },
            { 3, entity => entity.Description }
        };

        MetadataColumns =
        [
            new SchemaColumn(nameof(MetadataEntity.FullName), 0, typeof(string)),
            new SchemaColumn(nameof(MetadataEntity.DirectoryName), 1, typeof(string)),
            new SchemaColumn(nameof(MetadataEntity.TagName), 2, typeof(string)),
            new SchemaColumn(nameof(MetadataEntity.Description), 3, typeof(string))
        ];
    }
}