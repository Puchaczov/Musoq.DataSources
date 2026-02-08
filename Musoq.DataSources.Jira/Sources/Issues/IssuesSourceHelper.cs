using Musoq.DataSources.Jira.Entities;
using Musoq.Schema;
using Musoq.Schema.DataSources;

namespace Musoq.DataSources.Jira.Sources.Issues;

internal static class IssuesSourceHelper
{
    public static readonly IReadOnlyDictionary<string, int> IssuesNameToIndexMap;
    public static readonly IReadOnlyDictionary<int, Func<IJiraIssue, object?>> IssuesIndexToMethodAccessMap;
    public static readonly ISchemaColumn[] IssuesColumns;

    static IssuesSourceHelper()
    {
        IssuesNameToIndexMap = new Dictionary<string, int>
        {
            {nameof(IJiraIssue.Key), 0},
            {nameof(IJiraIssue.Id), 1},
            {nameof(IJiraIssue.Summary), 2},
            {nameof(IJiraIssue.Description), 3},
            {nameof(IJiraIssue.Type), 4},
            {nameof(IJiraIssue.Status), 5},
            {nameof(IJiraIssue.Priority), 6},
            {nameof(IJiraIssue.Resolution), 7},
            {nameof(IJiraIssue.Assignee), 8},
            {nameof(IJiraIssue.AssigneeDisplayName), 9},
            {nameof(IJiraIssue.Reporter), 10},
            {nameof(IJiraIssue.ReporterDisplayName), 11},
            {nameof(IJiraIssue.ProjectKey), 12},
            {nameof(IJiraIssue.CreatedAt), 13},
            {nameof(IJiraIssue.UpdatedAt), 14},
            {nameof(IJiraIssue.ResolvedAt), 15},
            {nameof(IJiraIssue.DueDate), 16},
            {nameof(IJiraIssue.Labels), 17},
            {nameof(IJiraIssue.Components), 18},
            {nameof(IJiraIssue.FixVersions), 19},
            {nameof(IJiraIssue.AffectsVersions), 20},
            {nameof(IJiraIssue.OriginalEstimateSeconds), 21},
            {nameof(IJiraIssue.RemainingEstimateSeconds), 22},
            {nameof(IJiraIssue.TimeSpentSeconds), 23},
            {nameof(IJiraIssue.OriginalEstimate), 24},
            {nameof(IJiraIssue.RemainingEstimate), 25},
            {nameof(IJiraIssue.TimeSpent), 26},
            {nameof(IJiraIssue.ParentKey), 27},
            {nameof(IJiraIssue.Environment), 28},
            {nameof(IJiraIssue.Votes), 29},
            {nameof(IJiraIssue.SecurityLevel), 30},
            {nameof(IJiraIssue.Url), 31}
        };

        IssuesIndexToMethodAccessMap = new Dictionary<int, Func<IJiraIssue, object?>>
        {
            {0, issue => issue.Key},
            {1, issue => issue.Id},
            {2, issue => issue.Summary},
            {3, issue => issue.Description},
            {4, issue => issue.Type},
            {5, issue => issue.Status},
            {6, issue => issue.Priority},
            {7, issue => issue.Resolution},
            {8, issue => issue.Assignee},
            {9, issue => issue.AssigneeDisplayName},
            {10, issue => issue.Reporter},
            {11, issue => issue.ReporterDisplayName},
            {12, issue => issue.ProjectKey},
            {13, issue => issue.CreatedAt},
            {14, issue => issue.UpdatedAt},
            {15, issue => issue.ResolvedAt},
            {16, issue => issue.DueDate},
            {17, issue => issue.Labels},
            {18, issue => issue.Components},
            {19, issue => issue.FixVersions},
            {20, issue => issue.AffectsVersions},
            {21, issue => issue.OriginalEstimateSeconds},
            {22, issue => issue.RemainingEstimateSeconds},
            {23, issue => issue.TimeSpentSeconds},
            {24, issue => issue.OriginalEstimate},
            {25, issue => issue.RemainingEstimate},
            {26, issue => issue.TimeSpent},
            {27, issue => issue.ParentKey},
            {28, issue => issue.Environment},
            {29, issue => issue.Votes},
            {30, issue => issue.SecurityLevel},
            {31, issue => issue.Url}
        };

        IssuesColumns =
        [
            new SchemaColumn(nameof(IJiraIssue.Key), 0, typeof(string)),
            new SchemaColumn(nameof(IJiraIssue.Id), 1, typeof(string)),
            new SchemaColumn(nameof(IJiraIssue.Summary), 2, typeof(string)),
            new SchemaColumn(nameof(IJiraIssue.Description), 3, typeof(string)),
            new SchemaColumn(nameof(IJiraIssue.Type), 4, typeof(string)),
            new SchemaColumn(nameof(IJiraIssue.Status), 5, typeof(string)),
            new SchemaColumn(nameof(IJiraIssue.Priority), 6, typeof(string)),
            new SchemaColumn(nameof(IJiraIssue.Resolution), 7, typeof(string)),
            new SchemaColumn(nameof(IJiraIssue.Assignee), 8, typeof(string)),
            new SchemaColumn(nameof(IJiraIssue.AssigneeDisplayName), 9, typeof(string)),
            new SchemaColumn(nameof(IJiraIssue.Reporter), 10, typeof(string)),
            new SchemaColumn(nameof(IJiraIssue.ReporterDisplayName), 11, typeof(string)),
            new SchemaColumn(nameof(IJiraIssue.ProjectKey), 12, typeof(string)),
            new SchemaColumn(nameof(IJiraIssue.CreatedAt), 13, typeof(DateTimeOffset?)),
            new SchemaColumn(nameof(IJiraIssue.UpdatedAt), 14, typeof(DateTimeOffset?)),
            new SchemaColumn(nameof(IJiraIssue.ResolvedAt), 15, typeof(DateTimeOffset?)),
            new SchemaColumn(nameof(IJiraIssue.DueDate), 16, typeof(DateTime?)),
            new SchemaColumn(nameof(IJiraIssue.Labels), 17, typeof(string)),
            new SchemaColumn(nameof(IJiraIssue.Components), 18, typeof(string)),
            new SchemaColumn(nameof(IJiraIssue.FixVersions), 19, typeof(string)),
            new SchemaColumn(nameof(IJiraIssue.AffectsVersions), 20, typeof(string)),
            new SchemaColumn(nameof(IJiraIssue.OriginalEstimateSeconds), 21, typeof(long?)),
            new SchemaColumn(nameof(IJiraIssue.RemainingEstimateSeconds), 22, typeof(long?)),
            new SchemaColumn(nameof(IJiraIssue.TimeSpentSeconds), 23, typeof(long?)),
            new SchemaColumn(nameof(IJiraIssue.OriginalEstimate), 24, typeof(string)),
            new SchemaColumn(nameof(IJiraIssue.RemainingEstimate), 25, typeof(string)),
            new SchemaColumn(nameof(IJiraIssue.TimeSpent), 26, typeof(string)),
            new SchemaColumn(nameof(IJiraIssue.ParentKey), 27, typeof(string)),
            new SchemaColumn(nameof(IJiraIssue.Environment), 28, typeof(string)),
            new SchemaColumn(nameof(IJiraIssue.Votes), 29, typeof(long?)),
            new SchemaColumn(nameof(IJiraIssue.SecurityLevel), 30, typeof(string)),
            new SchemaColumn(nameof(IJiraIssue.Url), 31, typeof(string))
        ];
    }
}
