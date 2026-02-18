using Musoq.DataSources.Jira.Entities;

namespace Musoq.DataSources.Jira.Tests.TestHelpers;

/// <summary>
///     Factory for creating mock Jira entities for testing.
/// </summary>
internal static class MockEntityFactory
{
    /// <summary>
    ///     Creates a mock IssueEntity for testing.
    /// </summary>
    public static IJiraIssue CreateIssue(
        string key = "TEST-123",
        string id = "12345",
        string summary = "Test Issue Summary",
        string? description = null,
        string type = "Bug",
        string status = "Open",
        string? priority = "Medium",
        string? resolution = null,
        string? assignee = "testuser",
        string? reporter = "reporter",
        string projectKey = "TEST",
        DateTimeOffset? createdAt = null,
        DateTimeOffset? updatedAt = null,
        DateTimeOffset? resolvedAt = null,
        DateTime? dueDate = null,
        string? labels = null,
        string? components = null,
        string? parentKey = null)
    {
        return new MockIssueEntity(
            key,
            id,
            summary,
            description,
            type,
            status,
            priority,
            resolution,
            assignee,
            reporter,
            projectKey,
            createdAt ?? DateTimeOffset.UtcNow,
            updatedAt ?? DateTimeOffset.UtcNow,
            resolvedAt,
            dueDate,
            labels ?? string.Empty,
            components ?? string.Empty,
            parentKey);
    }

    /// <summary>
    ///     Creates a mock ProjectEntity for testing.
    /// </summary>
    public static IJiraProject CreateProject(
        string id = "10000",
        string key = "TEST",
        string name = "Test Project",
        string? description = null,
        string? lead = "projectlead",
        string? category = null)
    {
        return new MockProjectEntity(id, key, name, description, lead, category);
    }

    /// <summary>
    ///     Creates a mock CommentEntity for testing.
    /// </summary>
    public static IJiraComment CreateComment(
        string id = "10001",
        string issueKey = "TEST-123",
        string body = "Test comment body",
        string author = "commenter",
        DateTimeOffset? createdAt = null,
        DateTimeOffset? updatedAt = null)
    {
        return new MockCommentEntity(id, issueKey, body, author, createdAt ?? DateTimeOffset.UtcNow, updatedAt);
    }
}

/// <summary>
///     Mock implementation of IssueEntity for testing without actual Jira SDK dependency.
/// </summary>
public class MockIssueEntity : IJiraIssue
{
    public MockIssueEntity(
        string key,
        string id,
        string summary,
        string? description,
        string type,
        string status,
        string? priority,
        string? resolution,
        string? assignee,
        string? reporter,
        string projectKey,
        DateTimeOffset? createdAt,
        DateTimeOffset? updatedAt,
        DateTimeOffset? resolvedAt,
        DateTime? dueDate,
        string labels,
        string components,
        string? parentKey)
    {
        Key = key;
        Id = id;
        Summary = summary;
        Description = description;
        Type = type;
        Status = status;
        Priority = priority;
        Resolution = resolution;
        Assignee = assignee;
        AssigneeDisplayName = assignee;
        Reporter = reporter;
        ReporterDisplayName = reporter;
        ProjectKey = projectKey;
        CreatedAt = createdAt;
        UpdatedAt = updatedAt;
        ResolvedAt = resolvedAt;
        DueDate = dueDate;
        Labels = labels;
        Components = components;
        ParentKey = parentKey;
    }

    public string Key { get; }
    public string Id { get; }
    public string Summary { get; }
    public string? Description { get; }
    public string Type { get; }
    public string Status { get; }
    public string? Priority { get; }
    public string? Resolution { get; }
    public string? Assignee { get; }
    public string? AssigneeDisplayName { get; }
    public string? Reporter { get; }
    public string? ReporterDisplayName { get; }
    public string ProjectKey { get; }
    public DateTimeOffset? CreatedAt { get; }
    public DateTimeOffset? UpdatedAt { get; }
    public DateTimeOffset? ResolvedAt { get; }
    public DateTime? DueDate { get; }
    public string Labels { get; }
    public string Components { get; }
    public string FixVersions { get; } = string.Empty;
    public string AffectsVersions { get; } = string.Empty;
    public long? OriginalEstimateSeconds { get; } = null;
    public long? RemainingEstimateSeconds { get; } = null;
    public long? TimeSpentSeconds { get; } = null;
    public string? OriginalEstimate { get; } = null;
    public string? RemainingEstimate { get; } = null;
    public string? TimeSpent { get; } = null;
    public string? ParentKey { get; }
    public string? Environment { get; } = null;
    public long? Votes { get; } = null;
    public string? SecurityLevel { get; } = null;
    public string Url => $"https://test.atlassian.net/browse/{Key}";
}

/// <summary>
///     Mock implementation of ProjectEntity for testing.
/// </summary>
public class MockProjectEntity : IJiraProject
{
    public MockProjectEntity(
        string id,
        string key,
        string name,
        string? description,
        string? lead,
        string? category)
    {
        Id = id;
        Key = key;
        Name = name;
        Description = description;
        Lead = lead;
        Category = category;
    }

    public string Id { get; }
    public string Key { get; }
    public string Name { get; }
    public string? Description { get; }
    public string? Lead { get; }
    public string? Url { get; } = null;
    public string? Category { get; }
    public string? CategoryDescription { get; } = null;
    public string? AvatarUrl { get; } = null;
}

/// <summary>
///     Mock implementation of CommentEntity for testing.
/// </summary>
public class MockCommentEntity : IJiraComment
{
    public MockCommentEntity(
        string id,
        string issueKey,
        string body,
        string author,
        DateTimeOffset? createdAt,
        DateTimeOffset? updatedAt)
    {
        Id = id;
        IssueKey = issueKey;
        Body = body;
        Author = author;
        AuthorDisplayName = author;
        CreatedAt = createdAt;
        UpdatedAt = updatedAt;
    }

    public string Id { get; }
    public string IssueKey { get; }
    public string Body { get; }
    public string Author { get; }
    public string? AuthorDisplayName { get; }
    public string? UpdateAuthor { get; } = null;
    public string? UpdateAuthorDisplayName { get; } = null;
    public DateTimeOffset? CreatedAt { get; }
    public DateTimeOffset? UpdatedAt { get; }
    public string? VisibilityGroup { get; } = null;
    public string? VisibilityRole { get; } = null;
}