using Musoq.DataSources.GitHub.Sources.Branches;
using Musoq.DataSources.GitHub.Sources.Commits;
using Musoq.DataSources.GitHub.Sources.Issues;
using Musoq.DataSources.GitHub.Sources.PullRequests;
using Musoq.DataSources.GitHub.Sources.Releases;
using Musoq.DataSources.GitHub.Sources.Repositories;
using Musoq.Schema;
using Musoq.Schema.DataSources;
using Musoq.Schema.Managers;
using Musoq.Schema.Reflection;

namespace Musoq.DataSources.GitHub;

/// <description>
/// Provides schema to work with GitHub repositories, issues, pull requests, commits, branches and releases.
/// </description>
/// <short-description>
/// Provides schema to work with GitHub data through the GitHub API.
/// </short-description>
/// <project-url>https://github.com/Puchaczov/Musoq.DataSources</project-url>
public class GitHubSchema : SchemaBase
{
    private const string SchemaName = "github";
    
    private const string RepositoriesTableName = "repositories";
    private const string IssuesTableName = "issues";
    private const string PullRequestsTableName = "pullrequests";
    private const string CommitsTableName = "commits";
    private const string BranchesTableName = "branches";
    private const string ReleasesTableName = "releases";

    private readonly IGitHubApi? _api;

    /// <virtual-constructors>
    /// <virtual-constructor>
    /// <examples>
    /// <example>
    /// <from>
    /// <environmentVariables>
    /// <environmentVariable name="GITHUB_TOKEN" isRequired="true">GitHub personal access token</environmentVariable>
    /// </environmentVariables>
    /// #github.repositories()
    /// </from>
    /// <description>Gets repositories for the authenticated user</description>
    /// <columns>
    /// <column name="Id" type="long">Repository ID</column>
    /// <column name="Name" type="string">Repository name</column>
    /// <column name="FullName" type="string">Full repository name (owner/repo)</column>
    /// <column name="Description" type="string">Repository description</column>
    /// <column name="Url" type="string">Repository URL</column>
    /// <column name="CloneUrl" type="string">Clone URL</column>
    /// <column name="SshUrl" type="string">SSH URL</column>
    /// <column name="DefaultBranch" type="string">Default branch name</column>
    /// <column name="IsPrivate" type="bool">Whether the repository is private</column>
    /// <column name="IsFork" type="bool">Whether the repository is a fork</column>
    /// <column name="IsArchived" type="bool">Whether the repository is archived</column>
    /// <column name="Language" type="string">Primary programming language</column>
    /// <column name="ForksCount" type="int">Number of forks</column>
    /// <column name="StargazersCount" type="int">Number of stars</column>
    /// <column name="WatchersCount" type="int">Number of watchers</column>
    /// <column name="OpenIssuesCount" type="int">Number of open issues</column>
    /// <column name="Size" type="long">Repository size in KB</column>
    /// <column name="CreatedAt" type="DateTimeOffset">Creation date</column>
    /// <column name="UpdatedAt" type="DateTimeOffset">Last update date</column>
    /// <column name="PushedAt" type="DateTimeOffset?">Last push date</column>
    /// <column name="OwnerLogin" type="string">Owner's login name</column>
    /// <column name="License" type="string">License name</column>
    /// <column name="Topics" type="IReadOnlyList&lt;string&gt;">Repository topics</column>
    /// <column name="HasIssues" type="bool">Whether issues are enabled</column>
    /// <column name="HasWiki" type="bool">Whether wiki is enabled</column>
    /// <column name="HasDownloads" type="bool">Whether downloads are enabled</column>
    /// <column name="Visibility" type="string">Repository visibility</column>
    /// </columns>
    /// </example>
    /// </examples>
    /// </virtual-constructor>
    /// <virtual-constructor>
    /// <virtual-param>Owner name (user or organization)</virtual-param>
    /// <examples>
    /// <example>
    /// <from>
    /// <environmentVariables>
    /// <environmentVariable name="GITHUB_TOKEN" isRequired="true">GitHub personal access token</environmentVariable>
    /// </environmentVariables>
    /// #github.repositories(string owner)
    /// </from>
    /// <description>Gets repositories for a specific owner (user or organization)</description>
    /// <columns>
    /// <column name="Id" type="long">Repository ID</column>
    /// <column name="Name" type="string">Repository name</column>
    /// <column name="FullName" type="string">Full repository name (owner/repo)</column>
    /// <column name="Description" type="string">Repository description</column>
    /// <column name="Url" type="string">Repository URL</column>
    /// <column name="Language" type="string">Primary programming language</column>
    /// <column name="StargazersCount" type="int">Number of stars</column>
    /// <column name="ForksCount" type="int">Number of forks</column>
    /// <column name="CreatedAt" type="DateTimeOffset">Creation date</column>
    /// <column name="UpdatedAt" type="DateTimeOffset">Last update date</column>
    /// </columns>
    /// </example>
    /// </examples>
    /// </virtual-constructor>
    /// <virtual-constructor>
    /// <virtual-param>Repository owner</virtual-param>
    /// <virtual-param>Repository name</virtual-param>
    /// <examples>
    /// <example>
    /// <from>
    /// <environmentVariables>
    /// <environmentVariable name="GITHUB_TOKEN" isRequired="true">GitHub personal access token</environmentVariable>
    /// </environmentVariables>
    /// #github.issues(string owner, string repo)
    /// </from>
    /// <description>Gets issues for a repository. Supports predicate pushdown for State, Author, Assignee, and Milestone filters.</description>
    /// <columns>
    /// <column name="Id" type="long">Issue ID</column>
    /// <column name="Number" type="int">Issue number</column>
    /// <column name="Title" type="string">Issue title</column>
    /// <column name="Body" type="string">Issue body</column>
    /// <column name="State" type="string">Issue state (open/closed)</column>
    /// <column name="Url" type="string">Issue URL</column>
    /// <column name="AuthorLogin" type="string">Author's login</column>
    /// <column name="AssigneeLogin" type="string">Assignee's login</column>
    /// <column name="Labels" type="string">Comma-separated labels</column>
    /// <column name="MilestoneTitle" type="string">Milestone title</column>
    /// <column name="Comments" type="int">Number of comments</column>
    /// <column name="IsPullRequest" type="bool">Whether this is a pull request</column>
    /// <column name="CreatedAt" type="DateTimeOffset">Creation date</column>
    /// <column name="UpdatedAt" type="DateTimeOffset?">Last update date</column>
    /// <column name="ClosedAt" type="DateTimeOffset?">Closed date</column>
    /// </columns>
    /// </example>
    /// </examples>
    /// </virtual-constructor>
    /// <virtual-constructor>
    /// <virtual-param>Repository owner</virtual-param>
    /// <virtual-param>Repository name</virtual-param>
    /// <examples>
    /// <example>
    /// <from>
    /// <environmentVariables>
    /// <environmentVariable name="GITHUB_TOKEN" isRequired="true">GitHub personal access token</environmentVariable>
    /// </environmentVariables>
    /// #github.pullrequests(string owner, string repo)
    /// </from>
    /// <description>Gets pull requests for a repository. Supports predicate pushdown for State, Head, and Base filters.</description>
    /// <columns>
    /// <column name="Id" type="long">Pull request ID</column>
    /// <column name="Number" type="int">Pull request number</column>
    /// <column name="Title" type="string">Pull request title</column>
    /// <column name="Body" type="string">Pull request body</column>
    /// <column name="State" type="string">Pull request state (open/closed)</column>
    /// <column name="Url" type="string">Pull request URL</column>
    /// <column name="AuthorLogin" type="string">Author's login</column>
    /// <column name="HeadRef" type="string">Source branch</column>
    /// <column name="BaseRef" type="string">Target branch</column>
    /// <column name="Merged" type="bool">Whether merged</column>
    /// <column name="Additions" type="int">Lines added</column>
    /// <column name="Deletions" type="int">Lines deleted</column>
    /// <column name="ChangedFiles" type="int">Files changed</column>
    /// <column name="Draft" type="bool">Whether draft</column>
    /// <column name="CreatedAt" type="DateTimeOffset">Creation date</column>
    /// <column name="MergedAt" type="DateTimeOffset?">Merge date</column>
    /// </columns>
    /// </example>
    /// </examples>
    /// </virtual-constructor>
    /// <virtual-constructor>
    /// <virtual-param>Repository owner</virtual-param>
    /// <virtual-param>Repository name</virtual-param>
    /// <examples>
    /// <example>
    /// <from>
    /// <environmentVariables>
    /// <environmentVariable name="GITHUB_TOKEN" isRequired="true">GitHub personal access token</environmentVariable>
    /// </environmentVariables>
    /// #github.commits(string owner, string repo)
    /// </from>
    /// <description>Gets commits for a repository. Supports predicate pushdown for SHA, Author, and Since filters.</description>
    /// <columns>
    /// <column name="Sha" type="string">Commit SHA</column>
    /// <column name="ShortSha" type="string">Short SHA (7 chars)</column>
    /// <column name="Message" type="string">Commit message</column>
    /// <column name="Url" type="string">Commit URL</column>
    /// <column name="AuthorName" type="string">Author name</column>
    /// <column name="AuthorEmail" type="string">Author email</column>
    /// <column name="AuthorLogin" type="string">Author's GitHub login</column>
    /// <column name="AuthorDate" type="DateTimeOffset?">Author date</column>
    /// <column name="CommitterName" type="string">Committer name</column>
    /// <column name="CommitterDate" type="DateTimeOffset?">Committer date</column>
    /// <column name="Additions" type="int">Lines added</column>
    /// <column name="Deletions" type="int">Lines deleted</column>
    /// <column name="Verified" type="bool?">Whether commit is verified</column>
    /// </columns>
    /// </example>
    /// </examples>
    /// </virtual-constructor>
    /// <virtual-constructor>
    /// <virtual-param>Repository owner</virtual-param>
    /// <virtual-param>Repository name</virtual-param>
    /// <virtual-param>SHA or branch name (optional)</virtual-param>
    /// <examples>
    /// <example>
    /// <from>
    /// <environmentVariables>
    /// <environmentVariable name="GITHUB_TOKEN" isRequired="true">GitHub personal access token</environmentVariable>
    /// </environmentVariables>
    /// #github.commits(string owner, string repo, string sha)
    /// </from>
    /// <description>Gets commits for a specific branch or starting from a SHA</description>
    /// <columns>
    /// <column name="Sha" type="string">Commit SHA</column>
    /// <column name="Message" type="string">Commit message</column>
    /// <column name="AuthorName" type="string">Author name</column>
    /// <column name="AuthorDate" type="DateTimeOffset?">Author date</column>
    /// </columns>
    /// </example>
    /// </examples>
    /// </virtual-constructor>
    /// <virtual-constructor>
    /// <virtual-param>Repository owner</virtual-param>
    /// <virtual-param>Repository name</virtual-param>
    /// <examples>
    /// <example>
    /// <from>
    /// <environmentVariables>
    /// <environmentVariable name="GITHUB_TOKEN" isRequired="true">GitHub personal access token</environmentVariable>
    /// </environmentVariables>
    /// #github.branches(string owner, string repo)
    /// </from>
    /// <description>Gets branches for a repository</description>
    /// <columns>
    /// <column name="Name" type="string">Branch name</column>
    /// <column name="CommitSha" type="string">Latest commit SHA</column>
    /// <column name="Protected" type="bool">Whether branch is protected</column>
    /// <column name="RepositoryOwner" type="string">Repository owner</column>
    /// <column name="RepositoryName" type="string">Repository name</column>
    /// </columns>
    /// </example>
    /// </examples>
    /// </virtual-constructor>
    /// <virtual-constructor>
    /// <virtual-param>Repository owner</virtual-param>
    /// <virtual-param>Repository name</virtual-param>
    /// <examples>
    /// <example>
    /// <from>
    /// <environmentVariables>
    /// <environmentVariable name="GITHUB_TOKEN" isRequired="true">GitHub personal access token</environmentVariable>
    /// </environmentVariables>
    /// #github.releases(string owner, string repo)
    /// </from>
    /// <description>Gets releases for a repository</description>
    /// <columns>
    /// <column name="Id" type="long">Release ID</column>
    /// <column name="TagName" type="string">Tag name</column>
    /// <column name="Name" type="string">Release name</column>
    /// <column name="Body" type="string">Release body/notes</column>
    /// <column name="Url" type="string">Release URL</column>
    /// <column name="Draft" type="bool">Whether draft</column>
    /// <column name="Prerelease" type="bool">Whether prerelease</column>
    /// <column name="AuthorLogin" type="string">Author's login</column>
    /// <column name="CreatedAt" type="DateTimeOffset">Creation date</column>
    /// <column name="PublishedAt" type="DateTimeOffset?">Publish date</column>
    /// <column name="AssetsCount" type="int">Number of assets</column>
    /// </columns>
    /// </example>
    /// </examples>
    /// </virtual-constructor>
    /// </virtual-constructors>
    public GitHubSchema() 
        : base(SchemaName, CreateLibrary())
    {
        _api = null;
        
        AddSource<RepositoriesSource>(RepositoriesTableName);
        AddTable<RepositoriesTable>(RepositoriesTableName);
        
        AddSource<IssuesSource>(IssuesTableName);
        AddTable<IssuesTable>(IssuesTableName);
        
        AddSource<PullRequestsSource>(PullRequestsTableName);
        AddTable<PullRequestsTable>(PullRequestsTableName);
        
        AddSource<CommitsSource>(CommitsTableName);
        AddTable<CommitsTable>(CommitsTableName);
        
        AddSource<BranchesSource>(BranchesTableName);
        AddTable<BranchesTable>(BranchesTableName);
        
        AddSource<ReleasesSource>(ReleasesTableName);
        AddTable<ReleasesTable>(ReleasesTableName);
    }

    internal GitHubSchema(IGitHubApi api)
        : base(SchemaName, CreateLibrary())
    {
        _api = api;
        
        AddSource<RepositoriesSource>(RepositoriesTableName);
        AddTable<RepositoriesTable>(RepositoriesTableName);
        
        AddSource<IssuesSource>(IssuesTableName);
        AddTable<IssuesTable>(IssuesTableName);
        
        AddSource<PullRequestsSource>(PullRequestsTableName);
        AddTable<PullRequestsTable>(PullRequestsTableName);
        
        AddSource<CommitsSource>(CommitsTableName);
        AddTable<CommitsTable>(CommitsTableName);
        
        AddSource<BranchesSource>(BranchesTableName);
        AddTable<BranchesTable>(BranchesTableName);
        
        AddSource<ReleasesSource>(ReleasesTableName);
        AddTable<ReleasesTable>(ReleasesTableName);
    }

    /// <summary>
    /// Gets the table name based on the given data source and parameters.
    /// </summary>
    /// <param name="name">Data Source name</param>
    /// <param name="runtimeContext">Runtime context</param>
    /// <param name="parameters">Parameters to pass to data source</param>
    /// <returns>Requested table metadata</returns>
    public override ISchemaTable GetTableByName(string name, RuntimeContext runtimeContext, params object[] parameters)
    {
        return name.ToLowerInvariant() switch
        {
            RepositoriesTableName => new RepositoriesTable(),
            IssuesTableName => new IssuesTable(),
            PullRequestsTableName => new PullRequestsTable(),
            CommitsTableName => new CommitsTable(),
            BranchesTableName => new BranchesTable(),
            ReleasesTableName => new ReleasesTable(),
            _ => throw new NotSupportedException($"Table {name} not supported.")
        };
    }

    /// <summary>
    /// Gets the data source based on the given data source and parameters.
    /// </summary>
    /// <param name="name">Data source name</param>
    /// <param name="runtimeContext">Runtime context</param>
    /// <param name="parameters">Parameters to pass data to data source</param>
    /// <returns>Data source</returns>
    public override RowSource GetRowSource(string name, RuntimeContext runtimeContext, params object[] parameters)
    {
        var api = _api ?? new GitHubApi(runtimeContext.EnvironmentVariables["GITHUB_TOKEN"]);
        
        return name.ToLowerInvariant() switch
        {
            RepositoriesTableName => parameters.Length switch
            {
                0 => new RepositoriesSource(api, runtimeContext),
                1 => new RepositoriesSource(api, runtimeContext, Convert.ToString(parameters[0])),
                _ => throw new ArgumentException($"Invalid number of parameters for {name}")
            },
            IssuesTableName => new IssuesSource(api, runtimeContext, 
                Convert.ToString(parameters[0])!, 
                Convert.ToString(parameters[1])!),
            PullRequestsTableName => new PullRequestsSource(api, runtimeContext, 
                Convert.ToString(parameters[0])!, 
                Convert.ToString(parameters[1])!),
            CommitsTableName => parameters.Length switch
            {
                2 => new CommitsSource(api, runtimeContext, 
                    Convert.ToString(parameters[0])!, 
                    Convert.ToString(parameters[1])!),
                3 => new CommitsSource(api, runtimeContext, 
                    Convert.ToString(parameters[0])!, 
                    Convert.ToString(parameters[1])!,
                    Convert.ToString(parameters[2])),
                _ => throw new ArgumentException($"Invalid number of parameters for {name}")
            },
            BranchesTableName => new BranchesSource(api, runtimeContext, 
                Convert.ToString(parameters[0])!, 
                Convert.ToString(parameters[1])!),
            ReleasesTableName => new ReleasesSource(api, runtimeContext, 
                Convert.ToString(parameters[0])!, 
                Convert.ToString(parameters[1])!),
            _ => throw new NotSupportedException($"Table {name} not supported.")
        };
    }

    /// <summary>
    /// Gets raw constructor information for a specific data source method.
    /// </summary>
    /// <param name="methodName">Name of the data source method</param>
    /// <param name="runtimeContext">Runtime context</param>
    /// <returns>Array of constructor information for the specified method</returns>
    public override SchemaMethodInfo[] GetRawConstructors(string methodName, RuntimeContext runtimeContext)
    {
        return methodName.ToLowerInvariant() switch
        {
            RepositoriesTableName => [CreateRepositoriesMethodInfo(), CreateRepositoriesWithOwnerMethodInfo()],
            IssuesTableName => [CreateIssuesMethodInfo()],
            PullRequestsTableName => [CreatePullRequestsMethodInfo()],
            CommitsTableName => [CreateCommitsMethodInfo(), CreateCommitsWithShaMethodInfo()],
            BranchesTableName => [CreateBranchesMethodInfo()],
            ReleasesTableName => [CreateReleasesMethodInfo()],
            _ => throw new NotSupportedException(
                $"Data source '{methodName}' is not supported by {SchemaName} schema. " +
                $"Available data sources: {string.Join(", ", RepositoriesTableName, IssuesTableName, PullRequestsTableName, CommitsTableName, BranchesTableName, ReleasesTableName)}")
        };
    }

    /// <summary>
    /// Gets raw constructor information for all data source methods in the schema.
    /// </summary>
    /// <param name="runtimeContext">Runtime context</param>
    /// <returns>Array of constructor information for all methods</returns>
    public override SchemaMethodInfo[] GetRawConstructors(RuntimeContext runtimeContext)
    {
        return
        [
            CreateRepositoriesMethodInfo(),
            CreateRepositoriesWithOwnerMethodInfo(),
            CreateIssuesMethodInfo(),
            CreatePullRequestsMethodInfo(),
            CreateCommitsMethodInfo(),
            CreateCommitsWithShaMethodInfo(),
            CreateBranchesMethodInfo(),
            CreateReleasesMethodInfo()
        ];
    }

    private static SchemaMethodInfo CreateRepositoriesMethodInfo()
    {
        var constructorInfo = new ConstructorInfo(
            originConstructorInfo: null!,
            supportsInterCommunicator: false,
            arguments: []);

        return new SchemaMethodInfo(RepositoriesTableName, constructorInfo);
    }
    
    private static SchemaMethodInfo CreateRepositoriesWithOwnerMethodInfo()
    {
        var constructorInfo = new ConstructorInfo(
            originConstructorInfo: null!,
            supportsInterCommunicator: false,
            arguments: [("owner", typeof(string))]);

        return new SchemaMethodInfo(RepositoriesTableName, constructorInfo);
    }

    private static SchemaMethodInfo CreateIssuesMethodInfo()
    {
        var constructorInfo = new ConstructorInfo(
            originConstructorInfo: null!,
            supportsInterCommunicator: false,
            arguments: [
                ("owner", typeof(string)),
                ("repo", typeof(string))
            ]);

        return new SchemaMethodInfo(IssuesTableName, constructorInfo);
    }

    private static SchemaMethodInfo CreatePullRequestsMethodInfo()
    {
        var constructorInfo = new ConstructorInfo(
            originConstructorInfo: null!,
            supportsInterCommunicator: false,
            arguments: [
                ("owner", typeof(string)),
                ("repo", typeof(string))
            ]);

        return new SchemaMethodInfo(PullRequestsTableName, constructorInfo);
    }

    private static SchemaMethodInfo CreateCommitsMethodInfo()
    {
        var constructorInfo = new ConstructorInfo(
            originConstructorInfo: null!,
            supportsInterCommunicator: false,
            arguments: [
                ("owner", typeof(string)),
                ("repo", typeof(string))
            ]);

        return new SchemaMethodInfo(CommitsTableName, constructorInfo);
    }
    
    private static SchemaMethodInfo CreateCommitsWithShaMethodInfo()
    {
        var constructorInfo = new ConstructorInfo(
            originConstructorInfo: null!,
            supportsInterCommunicator: false,
            arguments: [
                ("owner", typeof(string)),
                ("repo", typeof(string)),
                ("sha", typeof(string))
            ]);

        return new SchemaMethodInfo(CommitsTableName, constructorInfo);
    }

    private static SchemaMethodInfo CreateBranchesMethodInfo()
    {
        var constructorInfo = new ConstructorInfo(
            originConstructorInfo: null!,
            supportsInterCommunicator: false,
            arguments: [
                ("owner", typeof(string)),
                ("repo", typeof(string))
            ]);

        return new SchemaMethodInfo(BranchesTableName, constructorInfo);
    }

    private static SchemaMethodInfo CreateReleasesMethodInfo()
    {
        var constructorInfo = new ConstructorInfo(
            originConstructorInfo: null!,
            supportsInterCommunicator: false,
            arguments: [
                ("owner", typeof(string)),
                ("repo", typeof(string))
            ]);

        return new SchemaMethodInfo(ReleasesTableName, constructorInfo);
    }

    private static MethodsAggregator CreateLibrary()
    {
        var methodsManager = new MethodsManager();

        var library = new GitHubLibrary();

        methodsManager.RegisterLibraries(library);

        return new MethodsAggregator(methodsManager);
    }
}
