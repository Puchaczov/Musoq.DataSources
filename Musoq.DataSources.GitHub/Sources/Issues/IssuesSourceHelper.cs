using Musoq.DataSources.GitHub.Entities;
using Musoq.Schema;
using Musoq.Schema.DataSources;

namespace Musoq.DataSources.GitHub.Sources.Issues;

internal static class IssuesSourceHelper
{
    public static readonly IReadOnlyDictionary<string, int> IssuesNameToIndexMap;

    public static readonly ISchemaColumn[] IssuesColumns;

    static IssuesSourceHelper()
    {
        IssuesNameToIndexMap = new Dictionary<string, int>
        {
            { nameof(IssueEntity.Id), 0 },
            { nameof(IssueEntity.Number), 1 },
            { nameof(IssueEntity.Title), 2 },
            { nameof(IssueEntity.Body), 3 },
            { nameof(IssueEntity.State), 4 },
            { nameof(IssueEntity.Url), 5 },
            { nameof(IssueEntity.AuthorLogin), 6 },
            { nameof(IssueEntity.AuthorId), 7 },
            { nameof(IssueEntity.AssigneeLogin), 8 },
            { nameof(IssueEntity.Assignees), 9 },
            { nameof(IssueEntity.Labels), 10 },
            { nameof(IssueEntity.LabelNames), 11 },
            { nameof(IssueEntity.MilestoneTitle), 12 },
            { nameof(IssueEntity.MilestoneNumber), 13 },
            { nameof(IssueEntity.Comments), 14 },
            { nameof(IssueEntity.IsPullRequest), 15 },
            { nameof(IssueEntity.CreatedAt), 16 },
            { nameof(IssueEntity.UpdatedAt), 17 },
            { nameof(IssueEntity.ClosedAt), 18 },
            { nameof(IssueEntity.ClosedByLogin), 19 },
            { nameof(IssueEntity.Locked), 20 },
            { nameof(IssueEntity.ActiveLockReason), 21 },
            { nameof(IssueEntity.RepositoryUrl), 22 },
            { nameof(IssueEntity.StateReason), 23 }
        };

        IssuesColumns =
        [
            new SchemaColumn(nameof(IssueEntity.Id), 0, typeof(long)),
            new SchemaColumn(nameof(IssueEntity.Number), 1, typeof(int)),
            new SchemaColumn(nameof(IssueEntity.Title), 2, typeof(string)),
            new SchemaColumn(nameof(IssueEntity.Body), 3, typeof(string)),
            new SchemaColumn(nameof(IssueEntity.State), 4, typeof(string)),
            new SchemaColumn(nameof(IssueEntity.Url), 5, typeof(string)),
            new SchemaColumn(nameof(IssueEntity.AuthorLogin), 6, typeof(string)),
            new SchemaColumn(nameof(IssueEntity.AuthorId), 7, typeof(long?)),
            new SchemaColumn(nameof(IssueEntity.AssigneeLogin), 8, typeof(string)),
            new SchemaColumn(nameof(IssueEntity.Assignees), 9, typeof(string)),
            new SchemaColumn(nameof(IssueEntity.Labels), 10, typeof(string)),
            new SchemaColumn(nameof(IssueEntity.LabelNames), 11, typeof(IReadOnlyList<string>)),
            new SchemaColumn(nameof(IssueEntity.MilestoneTitle), 12, typeof(string)),
            new SchemaColumn(nameof(IssueEntity.MilestoneNumber), 13, typeof(int?)),
            new SchemaColumn(nameof(IssueEntity.Comments), 14, typeof(int)),
            new SchemaColumn(nameof(IssueEntity.IsPullRequest), 15, typeof(bool)),
            new SchemaColumn(nameof(IssueEntity.CreatedAt), 16, typeof(DateTimeOffset)),
            new SchemaColumn(nameof(IssueEntity.UpdatedAt), 17, typeof(DateTimeOffset?)),
            new SchemaColumn(nameof(IssueEntity.ClosedAt), 18, typeof(DateTimeOffset?)),
            new SchemaColumn(nameof(IssueEntity.ClosedByLogin), 19, typeof(string)),
            new SchemaColumn(nameof(IssueEntity.Locked), 20, typeof(bool)),
            new SchemaColumn(nameof(IssueEntity.ActiveLockReason), 21, typeof(string)),
            new SchemaColumn(nameof(IssueEntity.RepositoryUrl), 22, typeof(string)),
            new SchemaColumn(nameof(IssueEntity.StateReason), 23, typeof(string))
        ];
    }
}