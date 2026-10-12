using Musoq.DataSources.GitHub.Entities;
using Musoq.Schema;
using Musoq.Schema.DataSources;

namespace Musoq.DataSources.GitHub.Sources.Releases;

internal static class ReleasesSourceHelper
{
    public static readonly IReadOnlyDictionary<string, int> ReleasesNameToIndexMap;

    public static readonly ISchemaColumn[] ReleasesColumns;

    static ReleasesSourceHelper()
    {
        ReleasesNameToIndexMap = new Dictionary<string, int>
        {
            { nameof(ReleaseEntity.Id), 0 },
            { nameof(ReleaseEntity.TagName), 1 },
            { nameof(ReleaseEntity.Name), 2 },
            { nameof(ReleaseEntity.Body), 3 },
            { nameof(ReleaseEntity.Url), 4 },
            { nameof(ReleaseEntity.TargetCommitish), 5 },
            { nameof(ReleaseEntity.Draft), 6 },
            { nameof(ReleaseEntity.Prerelease), 7 },
            { nameof(ReleaseEntity.AuthorLogin), 8 },
            { nameof(ReleaseEntity.AuthorId), 9 },
            { nameof(ReleaseEntity.CreatedAt), 10 },
            { nameof(ReleaseEntity.PublishedAt), 11 },
            { nameof(ReleaseEntity.AssetsCount), 12 },
            { nameof(ReleaseEntity.TarballUrl), 13 },
            { nameof(ReleaseEntity.ZipballUrl), 14 }
        };

        ReleasesColumns =
        [
            new SchemaColumn(nameof(ReleaseEntity.Id), 0, typeof(long)),
            new SchemaColumn(nameof(ReleaseEntity.TagName), 1, typeof(string)),
            new SchemaColumn(nameof(ReleaseEntity.Name), 2, typeof(string)),
            new SchemaColumn(nameof(ReleaseEntity.Body), 3, typeof(string)),
            new SchemaColumn(nameof(ReleaseEntity.Url), 4, typeof(string)),
            new SchemaColumn(nameof(ReleaseEntity.TargetCommitish), 5, typeof(string)),
            new SchemaColumn(nameof(ReleaseEntity.Draft), 6, typeof(bool)),
            new SchemaColumn(nameof(ReleaseEntity.Prerelease), 7, typeof(bool)),
            new SchemaColumn(nameof(ReleaseEntity.AuthorLogin), 8, typeof(string)),
            new SchemaColumn(nameof(ReleaseEntity.AuthorId), 9, typeof(long?)),
            new SchemaColumn(nameof(ReleaseEntity.CreatedAt), 10, typeof(DateTimeOffset)),
            new SchemaColumn(nameof(ReleaseEntity.PublishedAt), 11, typeof(DateTimeOffset?)),
            new SchemaColumn(nameof(ReleaseEntity.AssetsCount), 12, typeof(int)),
            new SchemaColumn(nameof(ReleaseEntity.TarballUrl), 13, typeof(string)),
            new SchemaColumn(nameof(ReleaseEntity.ZipballUrl), 14, typeof(string))
        ];
    }
}