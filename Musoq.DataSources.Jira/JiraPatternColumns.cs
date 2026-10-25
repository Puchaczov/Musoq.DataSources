#nullable enable
using System;
using Musoq.DataSources.Common;

namespace Musoq.DataSources.Jira;

/// <summary>Reviewed source-row string getters. These never traverse child tables or perform API calls.</summary>
internal static class JiraPatternColumns
{
    internal static readonly PatternColumnBindings<Entities.IJiraIssue> IJiraIssue = new(
        (nameof(Entities.IJiraIssue.Key), static row => row.Key),
        (nameof(Entities.IJiraIssue.Id), static row => row.Id),
        (nameof(Entities.IJiraIssue.Summary), static row => row.Summary),
        (nameof(Entities.IJiraIssue.Description), static row => row.Description),
        (nameof(Entities.IJiraIssue.Type), static row => row.Type),
        (nameof(Entities.IJiraIssue.Status), static row => row.Status),
        (nameof(Entities.IJiraIssue.Priority), static row => row.Priority),
        (nameof(Entities.IJiraIssue.Resolution), static row => row.Resolution),
        (nameof(Entities.IJiraIssue.Assignee), static row => row.Assignee),
        (nameof(Entities.IJiraIssue.AssigneeDisplayName), static row => row.AssigneeDisplayName),
        (nameof(Entities.IJiraIssue.Reporter), static row => row.Reporter),
        (nameof(Entities.IJiraIssue.ReporterDisplayName), static row => row.ReporterDisplayName),
        (nameof(Entities.IJiraIssue.ProjectKey), static row => row.ProjectKey),
        (nameof(Entities.IJiraIssue.Labels), static row => row.Labels),
        (nameof(Entities.IJiraIssue.Components), static row => row.Components),
        (nameof(Entities.IJiraIssue.FixVersions), static row => row.FixVersions),
        (nameof(Entities.IJiraIssue.AffectsVersions), static row => row.AffectsVersions),
        (nameof(Entities.IJiraIssue.OriginalEstimate), static row => row.OriginalEstimate),
        (nameof(Entities.IJiraIssue.RemainingEstimate), static row => row.RemainingEstimate),
        (nameof(Entities.IJiraIssue.TimeSpent), static row => row.TimeSpent),
        (nameof(Entities.IJiraIssue.ParentKey), static row => row.ParentKey),
        (nameof(Entities.IJiraIssue.Environment), static row => row.Environment),
        (nameof(Entities.IJiraIssue.SecurityLevel), static row => row.SecurityLevel),
        (nameof(Entities.IJiraIssue.Url), static row => row.Url)
    );

    internal static readonly PatternColumnBindings<Entities.IJiraProject> IJiraProject = new(
        (nameof(Entities.IJiraProject.Id), static row => row.Id),
        (nameof(Entities.IJiraProject.Key), static row => row.Key),
        (nameof(Entities.IJiraProject.Name), static row => row.Name),
        (nameof(Entities.IJiraProject.Description), static row => row.Description),
        (nameof(Entities.IJiraProject.Lead), static row => row.Lead),
        (nameof(Entities.IJiraProject.Url), static row => row.Url),
        (nameof(Entities.IJiraProject.Category), static row => row.Category),
        (nameof(Entities.IJiraProject.CategoryDescription), static row => row.CategoryDescription),
        (nameof(Entities.IJiraProject.AvatarUrl), static row => row.AvatarUrl)
    );

    internal static readonly PatternColumnBindings<Entities.IJiraComment> IJiraComment = new(
        (nameof(Entities.IJiraComment.Id), static row => row.Id),
        (nameof(Entities.IJiraComment.IssueKey), static row => row.IssueKey),
        (nameof(Entities.IJiraComment.Body), static row => row.Body),
        (nameof(Entities.IJiraComment.Author), static row => row.Author),
        (nameof(Entities.IJiraComment.AuthorDisplayName), static row => row.AuthorDisplayName),
        (nameof(Entities.IJiraComment.UpdateAuthor), static row => row.UpdateAuthor),
        (nameof(Entities.IJiraComment.UpdateAuthorDisplayName), static row => row.UpdateAuthorDisplayName),
        (nameof(Entities.IJiraComment.VisibilityGroup), static row => row.VisibilityGroup),
        (nameof(Entities.IJiraComment.VisibilityRole), static row => row.VisibilityRole)
    );

    internal static IPatternColumnBindings? Get(string name) => name.ToLowerInvariant() switch
    {
        "issues" => IJiraIssue,
        "projects" => IJiraProject,
        "comments" => IJiraComment,
        _ => null
    };
}
