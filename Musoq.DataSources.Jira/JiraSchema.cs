using Musoq.DataSources.Jira.Sources.Comments;
using Musoq.DataSources.Jira.Entities;
using Musoq.DataSources.Jira.Sources.Issues;
using Musoq.DataSources.Jira.Sources.Projects;
using Musoq.Schema;
using Musoq.Schema.DataSources;
using Musoq.Schema.Managers;
using Musoq.Schema.Optimization;
using Musoq.Schema.Reflection;

namespace Musoq.DataSources.Jira;

/// <description>
///     Provides schema to work with Jira issues, projects, and comments.
///     Supports predicate pushdown for efficient JQL-based filtering.
/// </description>
/// <short-description>
///     Query Jira data with SQL-style queries.
/// </short-description>
/// <project-url>https://github.com/Puchaczov/Musoq.DataSources</project-url>
public class JiraSchema : SchemaBase
{
    private const string SchemaName = "jira";

    private const string IssuesTableName = "issues";
    private const string ProjectsTableName = "projects";
    private const string CommentsTableName = "comments";

    private readonly IJiraApi? _api;

    /// <virtual-constructors>
    ///     <virtual-constructor>
    ///         <virtual-param>Project key (e.g., PROJ)</virtual-param>
    ///         <examples>
    ///             <example>
    ///                 <from>
    ///                     <environmentVariables>
    ///                         <environmentVariable name="JIRA_URL" isRequired="true">
    ///                             Jira instance URL (e.g.,
    ///                             https://yourcompany.atlassian.net)
    ///                         </environmentVariable>
    ///                         <environmentVariable name="JIRA_USERNAME" isRequired="true">Jira username or email</environmentVariable>
    ///                         <environmentVariable name="JIRA_API_TOKEN" isRequired="true">Jira API token</environmentVariable>
    ///                     </environmentVariables>
    ///                     #jira.issues(string projectKey)
    ///                 </from>
    ///                 <description>
    ///                     Gets issues for a specific project. Supports predicate pushdown for Status, Type,
    ///                     Priority, Assignee, Reporter, and date filters.
    ///                 </description>
    ///                 <columns>
    ///                     <column name="Key" type="string">Issue key (e.g., PROJ-123)</column>
    ///                     <column name="Id" type="string">Issue ID</column>
    ///                     <column name="Summary" type="string">Issue summary/title</column>
    ///                     <column name="Description" type="string">Issue description</column>
    ///                     <column name="Type" type="string">Issue type (Bug, Story, Task, etc.)</column>
    ///                     <column name="Status" type="string">Issue status</column>
    ///                     <column name="Priority" type="string">Issue priority</column>
    ///                     <column name="Resolution" type="string">Issue resolution</column>
    ///                     <column name="Assignee" type="string">Assignee username</column>
    ///                     <column name="AssigneeDisplayName" type="string">Assignee display name</column>
    ///                     <column name="Reporter" type="string">Reporter username</column>
    ///                     <column name="ReporterDisplayName" type="string">Reporter display name</column>
    ///                     <column name="ProjectKey" type="string">Project key</column>
    ///                     <column name="CreatedAt" type="DateTimeOffset?">Creation date</column>
    ///                     <column name="UpdatedAt" type="DateTimeOffset?">Last update date</column>
    ///                     <column name="ResolvedAt" type="DateTimeOffset?">Resolution date</column>
    ///                     <column name="DueDate" type="DateTime?">Due date</column>
    ///                     <column name="Labels" type="string">Labels (comma-separated)</column>
    ///                     <column name="Components" type="string">Components (comma-separated)</column>
    ///                     <column name="FixVersions" type="string">Fix versions (comma-separated)</column>
    ///                     <column name="AffectsVersions" type="string">Affected versions (comma-separated)</column>
    ///                     <column name="OriginalEstimateSeconds" type="long?">Original estimate in seconds</column>
    ///                     <column name="RemainingEstimateSeconds" type="long?">Remaining estimate in seconds</column>
    ///                     <column name="TimeSpentSeconds" type="long?">Time spent in seconds</column>
    ///                     <column name="ParentKey" type="string">Parent issue key (for subtasks)</column>
    ///                     <column name="Votes" type="long?">Number of votes</column>
    ///                     <column name="Url" type="string">Issue URL</column>
    ///                 </columns>
    ///             </example>
    ///         </examples>
    ///     </virtual-constructor>
    ///     <virtual-constructor>
    ///         <virtual-param>JQL query string</virtual-param>
    ///         <examples>
    ///             <example>
    ///                 <from>
    ///                     <environmentVariables>
    ///                         <environmentVariable name="JIRA_URL" isRequired="true">Jira instance URL</environmentVariable>
    ///                         <environmentVariable name="JIRA_USERNAME" isRequired="true">Jira username or email</environmentVariable>
    ///                         <environmentVariable name="JIRA_API_TOKEN" isRequired="true">Jira API token</environmentVariable>
    ///                     </environmentVariables>
    ///                     #jira.issues(string jql)
    ///                 </from>
    ///                 <description>Gets issues matching a JQL query</description>
    ///                 <columns>
    ///                     <column name="Key" type="string">Issue key</column>
    ///                     <column name="Summary" type="string">Issue summary</column>
    ///                     <column name="Status" type="string">Issue status</column>
    ///                 </columns>
    ///             </example>
    ///         </examples>
    ///     </virtual-constructor>
    ///     <virtual-constructor>
    ///         <examples>
    ///             <example>
    ///                 <from>
    ///                     <environmentVariables>
    ///                         <environmentVariable name="JIRA_URL" isRequired="true">Jira instance URL</environmentVariable>
    ///                         <environmentVariable name="JIRA_USERNAME" isRequired="true">Jira username or email</environmentVariable>
    ///                         <environmentVariable name="JIRA_API_TOKEN" isRequired="true">Jira API token</environmentVariable>
    ///                     </environmentVariables>
    ///                     #jira.projects()
    ///                 </from>
    ///                 <description>Gets all projects accessible to the user</description>
    ///                 <columns>
    ///                     <column name="Id" type="string">Project ID</column>
    ///                     <column name="Key" type="string">Project key</column>
    ///                     <column name="Name" type="string">Project name</column>
    ///                     <column name="Description" type="string">Project description</column>
    ///                     <column name="Lead" type="string">Project lead username</column>
    ///                     <column name="Category" type="string">Project category</column>
    ///                 </columns>
    ///             </example>
    ///         </examples>
    ///     </virtual-constructor>
    ///     <virtual-constructor>
    ///         <virtual-param>Issue key (e.g., PROJ-123)</virtual-param>
    ///         <examples>
    ///             <example>
    ///                 <from>
    ///                     <environmentVariables>
    ///                         <environmentVariable name="JIRA_URL" isRequired="true">Jira instance URL</environmentVariable>
    ///                         <environmentVariable name="JIRA_USERNAME" isRequired="true">Jira username or email</environmentVariable>
    ///                         <environmentVariable name="JIRA_API_TOKEN" isRequired="true">Jira API token</environmentVariable>
    ///                     </environmentVariables>
    ///                     #jira.comments(string issueKey)
    ///                 </from>
    ///                 <description>Gets comments for a specific issue</description>
    ///                 <columns>
    ///                     <column name="Id" type="string">Comment ID</column>
    ///                     <column name="IssueKey" type="string">Parent issue key</column>
    ///                     <column name="Body" type="string">Comment body</column>
    ///                     <column name="Author" type="string">Author username</column>
    ///                     <column name="AuthorDisplayName" type="string">Author display name</column>
    ///                     <column name="CreatedAt" type="DateTimeOffset?">Creation date</column>
    ///                     <column name="UpdatedAt" type="DateTimeOffset?">Update date</column>
    ///                 </columns>
    ///             </example>
    ///         </examples>
    ///     </virtual-constructor>
    /// </virtual-constructors>
    public JiraSchema()
        : base(SchemaName, CreateLibrary())
    {
        _api = null;

        AddSource<IssuesSource>(IssuesTableName);
        AddTable<IssuesTable>(IssuesTableName);

        AddSource<ProjectsSource>(ProjectsTableName);
        AddTable<ProjectsTable>(ProjectsTableName);

        AddSource<CommentsSource>(CommentsTableName);
        AddTable<CommentsTable>(CommentsTableName);
    }

    /// <summary>
    ///     Internal constructor for testing with mock API.
    /// </summary>
    internal JiraSchema(IJiraApi api)
        : base(SchemaName, CreateLibrary())
    {
        _api = api;

        AddSource<IssuesSource>(IssuesTableName);
        AddTable<IssuesTable>(IssuesTableName);

        AddSource<ProjectsSource>(ProjectsTableName);
        AddTable<ProjectsTable>(ProjectsTableName);

        AddSource<CommentsSource>(CommentsTableName);
        AddTable<CommentsTable>(CommentsTableName);
    }

    /// <summary>
    ///     Gets the table metadata by name.
    /// </summary>
    public override ISchemaTable GetTableByName(
        string name,
        SourceMetadataContext metadataContext,
        params object[] parameters)
    {
        return name.ToLowerInvariant() switch
        {
            IssuesTableName => new IssuesTable(),
            ProjectsTableName => new ProjectsTable(),
            CommentsTableName => new CommentsTable(),
            _ => throw new NotSupportedException($"Table {name} not supported.")
        };
    }

    /// <summary>
    ///     Gets the row source by name.
    /// </summary>
    public override RowSource<T> GetRowSource<T>(
        string name,
        SourceExecutionContext executionContext,
        params object[] parameters)
    {
        var api = _api ?? CreateApi(executionContext);

        return name.ToLowerInvariant() switch
        {
            IssuesTableName => parameters.Length switch
            {
                1 => EnsureSourceType<T, IJiraIssue>(
                    name,
                    new IssuesSource(api, executionContext, Convert.ToString(parameters[0])!)),
                _ => throw new ArgumentException(
                    $"Invalid number of parameters for {name}. Expected 1 (project key or JQL).")
            },
            ProjectsTableName => EnsureSourceType<T, IJiraProject>(
                name,
                new ProjectsSource(api, executionContext)),
            CommentsTableName => parameters.Length switch
            {
                1 => EnsureSourceType<T, IJiraComment>(
                    name,
                    new CommentsSource(api, executionContext, Convert.ToString(parameters[0])!)),
                _ => throw new ArgumentException($"Invalid number of parameters for {name}. Expected 1 (issue key).")
            },
            _ => throw new NotSupportedException($"Table {name} not supported.")
        };
    }

    public override SourceDescriptor DescribeSource(
        string name,
        SourceDescribeContext context,
        params object[] parameters)
    {
        var table = GetTableByName(name, context.MetadataContext, parameters);

        return new SourceDescriptor
        {
            Identity = context.Identity,
            Columns = table.Columns,
            RowType = table.Metadata.TableEntityType,
            Diagnostics = [],
            ContractDiagnostics = []
        };
    }

    public override IReadOnlyList<SourceRuntimeSettingRequirement> DescribeSourceRuntimeSettings(
        string name,
        SourceRuntimeSettingsDescribeContext context,
        params object[] parameters)
    {
        return _api is not null
            ? []
            :
            [
                new SourceRuntimeSettingRequirement(
                    "JIRA_URL",
                    true,
                    false,
                    SourceRuntimeSettingPhase.Execution,
                    "Jira instance URL."),
                new SourceRuntimeSettingRequirement(
                    "JIRA_USERNAME",
                    true,
                    false,
                    SourceRuntimeSettingPhase.Execution,
                    "Jira username or email."),
                new SourceRuntimeSettingRequirement(
                    "JIRA_API_TOKEN",
                    true,
                    true,
                    SourceRuntimeSettingPhase.Execution,
                    "Jira API token.")
            ];
    }

    public override SourcePlanResult TryPlanSource(string name, SourcePlanRequest request, params object[] parameters)
    {
        return SourcePlanResult.RejectAll(request);
    }

    /// <summary>
    ///     Gets raw constructor information for a specific data source method.
    /// </summary>
    public override SchemaMethodInfo[] GetRawConstructors(
        string methodName,
        SourceMetadataContext metadataContext)
    {
        return methodName.ToLowerInvariant() switch
        {
            IssuesTableName => [CreateIssuesMethodInfo()],
            ProjectsTableName => [CreateProjectsMethodInfo()],
            CommentsTableName => [CreateCommentsMethodInfo()],
            _ => throw new NotSupportedException(
                $"Data source '{methodName}' is not supported by {SchemaName} schema. " +
                $"Available data sources: {string.Join(", ", IssuesTableName, ProjectsTableName, CommentsTableName)}")
        };
    }

    /// <summary>
    ///     Gets raw constructor information for all data source methods.
    /// </summary>
    public override SchemaMethodInfo[] GetRawConstructors(SourceMetadataContext metadataContext)
    {
        return
        [
            CreateIssuesMethodInfo(),
            CreateProjectsMethodInfo(),
            CreateCommentsMethodInfo()
        ];
    }

    public override SchemaMethodInfo[] GetConstructors()
    {
        return
        [
            CreateIssuesMethodInfo(),
            CreateProjectsMethodInfo(),
            CreateCommentsMethodInfo()
        ];
    }

    private static IJiraApi CreateApi(SourceExecutionContext executionContext)
    {
        var jiraUrl = executionContext.SourceRuntimeSettings.TryGetValue("JIRA_URL", out var url)
            ? url
            : throw new InvalidOperationException("JIRA_URL runtime setting is required.");

        var username = executionContext.SourceRuntimeSettings.TryGetValue("JIRA_USERNAME", out var user)
            ? user
            : throw new InvalidOperationException("JIRA_USERNAME runtime setting is required.");

        var apiToken = executionContext.SourceRuntimeSettings.TryGetValue("JIRA_API_TOKEN", out var token)
            ? token
            : throw new InvalidOperationException("JIRA_API_TOKEN runtime setting is required.");

        return new JiraApi(jiraUrl, username, apiToken);
    }

    private static SchemaMethodInfo CreateIssuesMethodInfo()
    {
        var constructorInfo = new ConstructorInfo(
            null!,
            false,
            [("projectKeyOrJql", typeof(string))]);

        return new SchemaMethodInfo(IssuesTableName, constructorInfo);
    }

    private static SchemaMethodInfo CreateProjectsMethodInfo()
    {
        var constructorInfo = new ConstructorInfo(
            null!,
            false,
            []);

        return new SchemaMethodInfo(ProjectsTableName, constructorInfo);
    }

    private static SchemaMethodInfo CreateCommentsMethodInfo()
    {
        var constructorInfo = new ConstructorInfo(
            null!,
            false,
            [("issueKey", typeof(string))]);

        return new SchemaMethodInfo(CommentsTableName, constructorInfo);
    }

    private static MethodsAggregator CreateLibrary()
    {
        var methodsManager = new MethodsManager();
        var library = new JiraLibrary();
        methodsManager.RegisterLibraries(library);
        return new MethodsAggregator(methodsManager);
    }
}
