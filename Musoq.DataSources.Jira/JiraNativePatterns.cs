#nullable enable
using Atlassian.Jira;
using Musoq.DataSources.Jira.Entities;
using Musoq.DataSources.Common;

namespace Musoq.DataSources.Jira;

internal static class JiraNativePatterns
{
    internal readonly record struct CommentInput(Comment Value, string IssueKey);
    internal static readonly PatternColumnBindings<Issue> Issue = new(
        ("Key", static row => IssueEntity.ReadKey(row)),
        ("Id", static row => IssueEntity.ReadId(row)),
        ("Summary", static row => IssueEntity.ReadSummary(row)),
        ("Description", static row => IssueEntity.ReadDescription(row)),
        ("Type", static row => IssueEntity.ReadType(row)),
        ("Status", static row => IssueEntity.ReadStatus(row)),
        ("Priority", static row => IssueEntity.ReadPriority(row)),
        ("Resolution", static row => IssueEntity.ReadResolution(row)),
        ("Assignee", static row => IssueEntity.ReadAssignee(row)),
        ("AssigneeDisplayName", static row => IssueEntity.ReadAssigneeDisplayName(row)),
        ("Reporter", static row => IssueEntity.ReadReporter(row)),
        ("ReporterDisplayName", static row => IssueEntity.ReadReporterDisplayName(row)),
        ("ProjectKey", static row => IssueEntity.ReadProjectKey(row)),
        ("Labels", static row => IssueEntity.ReadLabels(row)),
        ("Components", static row => IssueEntity.ReadComponents(row)),
        ("FixVersions", static row => IssueEntity.ReadFixVersions(row)),
        ("AffectsVersions", static row => IssueEntity.ReadAffectsVersions(row)),
        ("OriginalEstimate", static row => IssueEntity.ReadOriginalEstimate(row)),
        ("RemainingEstimate", static row => IssueEntity.ReadRemainingEstimate(row)),
        ("TimeSpent", static row => IssueEntity.ReadTimeSpent(row)),
        ("ParentKey", static row => IssueEntity.ReadParentKey(row)),
        ("Environment", static row => IssueEntity.ReadEnvironment(row)),
        ("SecurityLevel", static row => IssueEntity.ReadSecurityLevel(row)),
        ("Url", static row => IssueEntity.ReadUrl(row))
    );

    internal static readonly PatternColumnBindings<Project> Project = new(
        ("Id", static row => ProjectEntity.ReadId(row)),
        ("Key", static row => ProjectEntity.ReadKey(row)),
        ("Name", static row => ProjectEntity.ReadName(row)),
        ("Description", static row => ProjectEntity.ReadDescription(row)),
        ("Lead", static row => ProjectEntity.ReadLead(row)),
        ("Url", static row => ProjectEntity.ReadUrl(row)),
        ("Category", static row => ProjectEntity.ReadCategory(row)),
        ("CategoryDescription", static row => ProjectEntity.ReadCategoryDescription(row)),
        ("AvatarUrl", static row => ProjectEntity.ReadAvatarUrl(row))
    );

    internal static readonly PatternColumnBindings<CommentInput> Comment = new(
        ("Id", static row => CommentEntity.ReadId(row.Value)),
        ("Body", static row => CommentEntity.ReadBody(row.Value)),
        ("Author", static row => CommentEntity.ReadAuthor(row.Value)),
        ("AuthorDisplayName", static row => CommentEntity.ReadAuthorDisplayName(row.Value)),
        ("UpdateAuthor", static row => CommentEntity.ReadUpdateAuthor(row.Value)),
        ("UpdateAuthorDisplayName", static row => CommentEntity.ReadUpdateAuthorDisplayName(row.Value)),
        ("VisibilityGroup", static row => CommentEntity.ReadVisibilityGroup(row.Value)),
        ("VisibilityRole", static row => CommentEntity.ReadVisibilityRole(row.Value)),
        ("IssueKey", static row => row.IssueKey)
    );
}
