# GitHub plugin guide

## Purpose
- Exposes GitHub repositories, issues, pull requests, commits, branch commits, branches, and releases through API-backed sources.

## Read first
- `GitHubSchema.cs`
- `GitHubApi.cs`
- `IGitHubApi.cs`
- `Sources/`
- `Entities/`
- `GitHubLibrary.cs`

## Patterns to preserve
- Keep API calls behind the GitHub API abstraction and keep source classes focused on row production.
- The schema is mostly static, but XML docs explicitly advertise predicate-pushdown behavior for some sources; preserve that contract.
- Library helpers such as `HasLabel()` and `ShortSha()` are part of the user-facing SQL surface.
- Rate limits, pagination, and nullability are the main places where small mapping changes can break behavior.

## Integrations
- `Octokit`
- Environment variable: `GITHUB_TOKEN`

## Validate with
- `Musoq.DataSources.GitHub.Tests/GitHubRepositoriesTests.cs`
- `Musoq.DataSources.GitHub.Tests/GitHubIssuesTests.cs`
- `Musoq.DataSources.GitHub.Tests/GitHubPullRequestsTests.cs`
- `Musoq.DataSources.GitHub.Tests/GitHubLibraryTests.cs`