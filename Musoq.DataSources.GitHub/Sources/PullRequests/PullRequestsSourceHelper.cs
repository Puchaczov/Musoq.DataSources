using Musoq.DataSources.GitHub.Entities;
using Musoq.Schema;
using Musoq.Schema.DataSources;

namespace Musoq.DataSources.GitHub.Sources.PullRequests;

internal static class PullRequestsSourceHelper
{
    public static readonly IReadOnlyDictionary<string, int> PullRequestsNameToIndexMap;

    public static readonly IReadOnlyDictionary<int, Func<PullRequestEntity, object?>>
        PullRequestsIndexToMethodAccessMap;

    public static readonly ISchemaColumn[] PullRequestsColumns;

    static PullRequestsSourceHelper()
    {
        PullRequestsNameToIndexMap = new Dictionary<string, int>
        {
            { nameof(PullRequestEntity.Id), 0 },
            { nameof(PullRequestEntity.Number), 1 },
            { nameof(PullRequestEntity.Title), 2 },
            { nameof(PullRequestEntity.Body), 3 },
            { nameof(PullRequestEntity.State), 4 },
            { nameof(PullRequestEntity.Url), 5 },
            { nameof(PullRequestEntity.AuthorLogin), 6 },
            { nameof(PullRequestEntity.AuthorId), 7 },
            { nameof(PullRequestEntity.AssigneeLogin), 8 },
            { nameof(PullRequestEntity.Assignees), 9 },
            { nameof(PullRequestEntity.Labels), 10 },
            { nameof(PullRequestEntity.LabelNames), 11 },
            { nameof(PullRequestEntity.MilestoneTitle), 12 },
            { nameof(PullRequestEntity.MilestoneNumber), 13 },
            { nameof(PullRequestEntity.HeadRef), 14 },
            { nameof(PullRequestEntity.HeadSha), 15 },
            { nameof(PullRequestEntity.HeadRepository), 16 },
            { nameof(PullRequestEntity.BaseRef), 17 },
            { nameof(PullRequestEntity.BaseSha), 18 },
            { nameof(PullRequestEntity.BaseRepository), 19 },
            { nameof(PullRequestEntity.Merged), 20 },
            { nameof(PullRequestEntity.Mergeable), 21 },
            { nameof(PullRequestEntity.MergeableState), 22 },
            { nameof(PullRequestEntity.MergedByLogin), 23 },
            { nameof(PullRequestEntity.MergeCommitSha), 24 },
            { nameof(PullRequestEntity.Comments), 25 },
            { nameof(PullRequestEntity.Commits), 26 },
            { nameof(PullRequestEntity.Additions), 27 },
            { nameof(PullRequestEntity.Deletions), 28 },
            { nameof(PullRequestEntity.ChangedFiles), 29 },
            { nameof(PullRequestEntity.Draft), 30 },
            { nameof(PullRequestEntity.CreatedAt), 31 },
            { nameof(PullRequestEntity.UpdatedAt), 32 },
            { nameof(PullRequestEntity.ClosedAt), 33 },
            { nameof(PullRequestEntity.MergedAt), 34 },
            { nameof(PullRequestEntity.Locked), 35 },
            { nameof(PullRequestEntity.ActiveLockReason), 36 }
        };

        PullRequestsIndexToMethodAccessMap = new Dictionary<int, Func<PullRequestEntity, object?>>
        {
            { 0, pr => pr.Id },
            { 1, pr => pr.Number },
            { 2, pr => pr.Title },
            { 3, pr => pr.Body },
            { 4, pr => pr.State },
            { 5, pr => pr.Url },
            { 6, pr => pr.AuthorLogin },
            { 7, pr => pr.AuthorId },
            { 8, pr => pr.AssigneeLogin },
            { 9, pr => pr.Assignees },
            { 10, pr => pr.Labels },
            { 11, pr => pr.LabelNames },
            { 12, pr => pr.MilestoneTitle },
            { 13, pr => pr.MilestoneNumber },
            { 14, pr => pr.HeadRef },
            { 15, pr => pr.HeadSha },
            { 16, pr => pr.HeadRepository },
            { 17, pr => pr.BaseRef },
            { 18, pr => pr.BaseSha },
            { 19, pr => pr.BaseRepository },
            { 20, pr => pr.Merged },
            { 21, pr => pr.Mergeable },
            { 22, pr => pr.MergeableState },
            { 23, pr => pr.MergedByLogin },
            { 24, pr => pr.MergeCommitSha },
            { 25, pr => pr.Comments },
            { 26, pr => pr.Commits },
            { 27, pr => pr.Additions },
            { 28, pr => pr.Deletions },
            { 29, pr => pr.ChangedFiles },
            { 30, pr => pr.Draft },
            { 31, pr => pr.CreatedAt },
            { 32, pr => pr.UpdatedAt },
            { 33, pr => pr.ClosedAt },
            { 34, pr => pr.MergedAt },
            { 35, pr => pr.Locked },
            { 36, pr => pr.ActiveLockReason }
        };

        PullRequestsColumns =
        [
            new SchemaColumn(nameof(PullRequestEntity.Id), 0, typeof(long)),
            new SchemaColumn(nameof(PullRequestEntity.Number), 1, typeof(int)),
            new SchemaColumn(nameof(PullRequestEntity.Title), 2, typeof(string)),
            new SchemaColumn(nameof(PullRequestEntity.Body), 3, typeof(string)),
            new SchemaColumn(nameof(PullRequestEntity.State), 4, typeof(string)),
            new SchemaColumn(nameof(PullRequestEntity.Url), 5, typeof(string)),
            new SchemaColumn(nameof(PullRequestEntity.AuthorLogin), 6, typeof(string)),
            new SchemaColumn(nameof(PullRequestEntity.AuthorId), 7, typeof(long?)),
            new SchemaColumn(nameof(PullRequestEntity.AssigneeLogin), 8, typeof(string)),
            new SchemaColumn(nameof(PullRequestEntity.Assignees), 9, typeof(string)),
            new SchemaColumn(nameof(PullRequestEntity.Labels), 10, typeof(string)),
            new SchemaColumn(nameof(PullRequestEntity.LabelNames), 11, typeof(IReadOnlyList<string>)),
            new SchemaColumn(nameof(PullRequestEntity.MilestoneTitle), 12, typeof(string)),
            new SchemaColumn(nameof(PullRequestEntity.MilestoneNumber), 13, typeof(int?)),
            new SchemaColumn(nameof(PullRequestEntity.HeadRef), 14, typeof(string)),
            new SchemaColumn(nameof(PullRequestEntity.HeadSha), 15, typeof(string)),
            new SchemaColumn(nameof(PullRequestEntity.HeadRepository), 16, typeof(string)),
            new SchemaColumn(nameof(PullRequestEntity.BaseRef), 17, typeof(string)),
            new SchemaColumn(nameof(PullRequestEntity.BaseSha), 18, typeof(string)),
            new SchemaColumn(nameof(PullRequestEntity.BaseRepository), 19, typeof(string)),
            new SchemaColumn(nameof(PullRequestEntity.Merged), 20, typeof(bool)),
            new SchemaColumn(nameof(PullRequestEntity.Mergeable), 21, typeof(bool?)),
            new SchemaColumn(nameof(PullRequestEntity.MergeableState), 22, typeof(string)),
            new SchemaColumn(nameof(PullRequestEntity.MergedByLogin), 23, typeof(string)),
            new SchemaColumn(nameof(PullRequestEntity.MergeCommitSha), 24, typeof(string)),
            new SchemaColumn(nameof(PullRequestEntity.Comments), 25, typeof(int)),
            new SchemaColumn(nameof(PullRequestEntity.Commits), 26, typeof(int)),
            new SchemaColumn(nameof(PullRequestEntity.Additions), 27, typeof(int)),
            new SchemaColumn(nameof(PullRequestEntity.Deletions), 28, typeof(int)),
            new SchemaColumn(nameof(PullRequestEntity.ChangedFiles), 29, typeof(int)),
            new SchemaColumn(nameof(PullRequestEntity.Draft), 30, typeof(bool)),
            new SchemaColumn(nameof(PullRequestEntity.CreatedAt), 31, typeof(DateTimeOffset)),
            new SchemaColumn(nameof(PullRequestEntity.UpdatedAt), 32, typeof(DateTimeOffset)),
            new SchemaColumn(nameof(PullRequestEntity.ClosedAt), 33, typeof(DateTimeOffset?)),
            new SchemaColumn(nameof(PullRequestEntity.MergedAt), 34, typeof(DateTimeOffset?)),
            new SchemaColumn(nameof(PullRequestEntity.Locked), 35, typeof(bool)),
            new SchemaColumn(nameof(PullRequestEntity.ActiveLockReason), 36, typeof(string))
        ];
    }
}