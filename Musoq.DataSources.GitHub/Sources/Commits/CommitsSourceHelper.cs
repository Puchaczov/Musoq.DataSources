using Musoq.DataSources.GitHub.Entities;
using Musoq.Schema;
using Musoq.Schema.DataSources;

namespace Musoq.DataSources.GitHub.Sources.Commits;

internal static class CommitsSourceHelper
{
    public static readonly IReadOnlyDictionary<string, int> CommitsNameToIndexMap;

    public static readonly ISchemaColumn[] CommitsColumns;

    static CommitsSourceHelper()
    {
        CommitsNameToIndexMap = new Dictionary<string, int>
        {
            { nameof(CommitEntity.Sha), 0 },
            { nameof(CommitEntity.ShortSha), 1 },
            { nameof(CommitEntity.Message), 2 },
            { nameof(CommitEntity.Url), 3 },
            { nameof(CommitEntity.AuthorName), 4 },
            { nameof(CommitEntity.AuthorEmail), 5 },
            { nameof(CommitEntity.AuthorLogin), 6 },
            { nameof(CommitEntity.AuthorId), 7 },
            { nameof(CommitEntity.AuthorDate), 8 },
            { nameof(CommitEntity.CommitterName), 9 },
            { nameof(CommitEntity.CommitterEmail), 10 },
            { nameof(CommitEntity.CommitterLogin), 11 },
            { nameof(CommitEntity.CommitterId), 12 },
            { nameof(CommitEntity.CommitterDate), 13 },
            { nameof(CommitEntity.Additions), 14 },
            { nameof(CommitEntity.Deletions), 15 },
            { nameof(CommitEntity.Total), 16 },
            { nameof(CommitEntity.ParentShas), 17 },
            { nameof(CommitEntity.ParentCount), 18 },
            { nameof(CommitEntity.CommentCount), 19 },
            { nameof(CommitEntity.Verified), 20 },
            { nameof(CommitEntity.VerificationReason), 21 },
            { nameof(CommitEntity.FilesChanged), 22 }
        };

        CommitsColumns =
        [
            new SchemaColumn(nameof(CommitEntity.Sha), 0, typeof(string)),
            new SchemaColumn(nameof(CommitEntity.ShortSha), 1, typeof(string)),
            new SchemaColumn(nameof(CommitEntity.Message), 2, typeof(string)),
            new SchemaColumn(nameof(CommitEntity.Url), 3, typeof(string)),
            new SchemaColumn(nameof(CommitEntity.AuthorName), 4, typeof(string)),
            new SchemaColumn(nameof(CommitEntity.AuthorEmail), 5, typeof(string)),
            new SchemaColumn(nameof(CommitEntity.AuthorLogin), 6, typeof(string)),
            new SchemaColumn(nameof(CommitEntity.AuthorId), 7, typeof(long?)),
            new SchemaColumn(nameof(CommitEntity.AuthorDate), 8, typeof(DateTimeOffset?)),
            new SchemaColumn(nameof(CommitEntity.CommitterName), 9, typeof(string)),
            new SchemaColumn(nameof(CommitEntity.CommitterEmail), 10, typeof(string)),
            new SchemaColumn(nameof(CommitEntity.CommitterLogin), 11, typeof(string)),
            new SchemaColumn(nameof(CommitEntity.CommitterId), 12, typeof(long?)),
            new SchemaColumn(nameof(CommitEntity.CommitterDate), 13, typeof(DateTimeOffset?)),
            new SchemaColumn(nameof(CommitEntity.Additions), 14, typeof(int)),
            new SchemaColumn(nameof(CommitEntity.Deletions), 15, typeof(int)),
            new SchemaColumn(nameof(CommitEntity.Total), 16, typeof(int)),
            new SchemaColumn(nameof(CommitEntity.ParentShas), 17, typeof(string)),
            new SchemaColumn(nameof(CommitEntity.ParentCount), 18, typeof(int)),
            new SchemaColumn(nameof(CommitEntity.CommentCount), 19, typeof(int)),
            new SchemaColumn(nameof(CommitEntity.Verified), 20, typeof(bool?)),
            new SchemaColumn(nameof(CommitEntity.VerificationReason), 21, typeof(string)),
            new SchemaColumn(nameof(CommitEntity.FilesChanged), 22, typeof(int))
        ];
    }
}