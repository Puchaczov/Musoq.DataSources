# Jira plugin guide

## Purpose
- Exposes Jira `issues`, `projects`, and `comments`, with explicit JQL-oriented predicate pushdown.

## Read first
- `JiraSchema.cs`
- `JiraApi.cs`
- `IJiraApi.cs`
- `Sources/`
- `Entities/`
- `Helpers/`
- `JiraLibrary.cs`

## Patterns to preserve
- Keep filtering work upstream when possible; `issues(string projectKeyOrJql)` is designed to benefit from JQL pushdown.
- Keep Jira API concerns in the API layer and row/table shape in source/entity classes.
- `JqlBuilder` behavior is effectively part of the optimization contract.
- Schema XML docs define the public surface and should stay aligned with query behavior.

## Integrations
- `Atlassian.SDK`
- Environment variables: `JIRA_URL`, `JIRA_USERNAME`, `JIRA_API_TOKEN`

## Validate with
- `Musoq.DataSources.Jira.Tests/JiraIssuesTests.cs`
- `Musoq.DataSources.Jira.Tests/JqlBuilderTests.cs`
- `Musoq.DataSources.Jira.Tests/JiraProjectsTests.cs`
- `Musoq.DataSources.Jira.Tests/JiraCommentsTests.cs`