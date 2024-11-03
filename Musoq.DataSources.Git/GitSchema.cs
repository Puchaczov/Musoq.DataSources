using System;
using LibGit2Sharp;
using Musoq.Schema;
using Musoq.Schema.DataSources;
using Musoq.Schema.Managers;

namespace Musoq.DataSources.Git;

/// <summary>
/// Provides schema to work with Git data source
/// </summary>
public class GitSchema : SchemaBase
{
    private const string SchemaName = "Git";

    private readonly Func<string, Repository> _createRepository;
    private Repository? _repository;
    
    /// <virtual-constructors>
    /// <virtual-constructor>
    /// <examples>
    /// <example>
    /// <from>
    /// <environmentVariables></environmentVariables>
    /// #git.repository(string path)
    /// </from>
    /// <description>Allows to perform queries on the given Git repository path.</description>
    /// <columns>
    /// <column name="Path" type="string">Repository path</column>
    /// <column name="WorkingDirectory" type="string">Working directory path</column>
    /// <column name="Branches" type="BranchEntity[]">Repository branches</column>
    /// <column name="Tags" type="TagEntity[]">Repository tags</column>
    /// <column name="Commits" type="CommitEntity[]">Repository commits</column>
    /// <column name="Head" type="BranchEntity">Current HEAD branch</column>
    /// <column name="Configuration" type="ConfigurationEntityKeyValue[]">Repository configuration</column>
    /// <column name="Information" type="RepositoryInformationEntity">Repository information</column>
    /// <column name="Stashes" type="StashEntity[]">Repository stashes</column>
    /// </columns>
    /// </example>
    /// </examples>
    /// </virtual-constructor>
    /// </virtual-constructors>
    /// <additional-tables>
    /// <additional-table>
    /// <description>Represents a Git branch</description>
    /// <columns type="BranchEntity">
    /// <column name="FriendlyName" type="string">Branch friendly name</column>
    /// <column name="CanonicalName" type="string">Branch canonical name</column>
    /// <column name="IsRemote" type="bool">Is remote branch</column>
    /// <column name="IsTracking" type="bool">Is tracking another branch</column>
    /// <column name="IsCurrentRepositoryHead" type="bool">Is current repository HEAD</column>
    /// <column name="TrackedBranch" type="BranchEntity">Tracked branch</column>
    /// <column name="BranchTrackingDetails" type="BranchTrackingDetailsEntity">Branch tracking details</column>
    /// <column name="Tip" type="CommitEntity">Branch tip commit</column>
    /// <column name="Commits" type="CommitEntity[]">Branch commits</column>
    /// <column name="UpstreamBranchCanonicalName" type="string">Upstream branch canonical name</column>
    /// <column name="RemoteName" type="string">Remote name</column>
    /// </columns>
    /// </additional-table>
    /// <additional-table>
    /// <description>Represents a Git commit</description>
    /// <columns type="CommitEntity">
    /// <column name="Sha" type="string">Commit SHA</column>
    /// <column name="Message" type="string">Commit message</column>
    /// <column name="MessageShort" type="string">Short commit message</column>
    /// <column name="Author" type="string">Author name</column>
    /// <column name="AuthorEmail" type="string">Author email</column>
    /// <column name="Committer" type="string">Committer name</column>
    /// <column name="CommitterEmail" type="string">Committer email</column>
    /// <column name="CommittedWhen" type="DateTimeOffset">Commit date and time</column>
    /// </columns>
    /// </additional-table>
    /// <additional-table>
    /// <description>Represents a Git tag</description>
    /// <columns type="TagEntity">
    /// <column name="FriendlyName" type="string?">Tag friendly name</column>
    /// <column name="CanonicalName" type="string?">Tag canonical name</column>
    /// <column name="Message" type="string?">Tag message</column>
    /// <column name="IsAnnotated" type="bool">Is annotated tag</column>
    /// <column name="Annotation" type="AnnotationEntity">Tag annotation</column>
    /// </columns>
    /// </additional-table>
    /// <additional-table>
    /// <description>Represents a Git stash</description>
    /// <columns type="StashEntity">
    /// <column name="Message" type="string">Stash message</column>
    /// <column name="Index" type="CommitEntity">Index state</column>
    /// <column name="WorkTree" type="CommitEntity">Work tree state</column>
    /// <column name="UntrackedFiles" type="CommitEntity">Untracked files state</column>
    /// </columns>
    /// </additional-table>
    /// <additional-table>
    /// <description>Represents Git configuration entry</description>
    /// <columns type="ConfigurationEntityKeyValue">
    /// <column name="Key" type="string">Configuration key</column>
    /// <column name="Value" type="string">Configuration value</column>
    /// <column name="ConfigurationLevel" type="string">Configuration level</column>
    /// </columns>
    /// </additional-table>
    /// <additional-table>
    /// <description>Represents Git repository information</description>
    /// <columns type="RepositoryInformationEntity">
    /// <column name="Path" type="string">Repository path</column>
    /// <column name="WorkingDirectory" type="string">Working directory path</column>
    /// <column name="IsBare" type="bool">Is bare repository</column>
    /// <column name="IsHeadDetached" type="bool">Is HEAD detached</column>
    /// <column name="IsHeadUnborn" type="bool">Is HEAD unborn</column>
    /// <column name="IsShallow" type="bool">Is shallow repository</column>
    /// </columns>
    /// </additional-table>
    /// <additional-table>
    /// <description>Represents Git branch tracking details</description>
    /// <columns type="BranchTrackingDetailsEntity">
    /// <column name="AheadBy" type="int?">Commits ahead count</column>
    /// <column name="BehindBy" type="int?">Commits behind count</column>
    /// </columns>
    /// </additional-table>
    /// <additional-table>
    /// <description>Represents Git file differences</description>
    /// <columns type="DifferenceEntity">
    /// <column name="Path" type="string">Changed file path</column>
    /// <column name="Exists" type="bool">File exists in new version</column>
    /// <column name="ChangeKind" type="string">Kind of change</column>
    /// <column name="OldPath" type="string">Old file path</column>
    /// <column name="OldMode" type="string">Old file mode</column>
    /// <column name="NewMode" type="string">New file mode</column>
    /// <column name="OldSha" type="string">Old file SHA</column>
    /// <column name="NewSha" type="string">New file SHA</column>
    /// <column name="OldContent" type="string">Old file content</column>
    /// <column name="NewContent" type="string">New file content</column>
    /// <column name="OldContentBytes" type="byte[]">Old file content bytes</column>
    /// <column name="NewContentBytes" type="byte[]">New file content bytes</column>
    /// </columns>
    /// </additional-table>
    /// <additional-table>
    /// <description>Represents Git tag annotation</description>
    /// <columns type="AnnotationEntity">
    /// <column name="Message" type="string?">Annotation message</column>
    /// <column name="Name" type="string?">Annotation name</column>
    /// <column name="Sha" type="string?">Annotation SHA</column>
    /// <column name="Tagger" type="TaggerEntity?">Tagger information</column>
    /// </columns>
    /// </additional-table>
    /// <additional-table>
    /// <description>Represents Git tag tagger</description>
    /// <columns type="TaggerEntity">
    /// <column name="Name" type="string?">Tagger name</column>
    /// <column name="Email" type="string?">Tagger email</column>
    /// <column name="WhenSigned" type="DateTimeOffset">When tag was signed</column>
    /// </columns>
    /// </additional-table>
    /// <additional-table>
    /// <description>Represents Git patch</description>
    /// <columns type="PatchEntity">
    /// <column name="LinesAdded" type="int">Lines added</column>
    /// <column name="LinesDeleted" type="int">Lines deleted</column>
    /// <column name="Content" type="string">Gets the full patch file of this diff</column>
    /// </columns>
    /// </additional-table>
    /// <additional-table>
    /// <description>Represents Git patch entry changes</description>
    /// <columns type="PatchEntryChangesEntity">
    /// <column name="LinesAdded" type="int">Lines added</column>
    /// <column name="LinesDeleted" type="int">Lines deleted</column>
    /// <column name="Content" type="string">Gets the patch corresponding to these changes</column>
    /// <column name="Path" type="string">Gets the path of a file</column>
    /// <column name="OldMode" type="string">Gets the old mode</column>
    /// <column name="Mode" type="string">Gets the mode</column>
    /// <column name="IsBinaryComparison" type="string">Determines if at least one side of the comparison holds binary content</column>
    /// </columns>
    /// </additional-table>
    /// </additional-tables>
    public GitSchema()
        : base(SchemaName.ToLowerInvariant(), CreateLibrary())
    {
        _createRepository = path => _repository ??= new Repository(path);
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
        switch (name.ToLowerInvariant())
        {
            case "repository":
                return new RepositoryTable();
        }

        return base.GetTableByName(name, runtimeContext, parameters);
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
        switch (name.ToLowerInvariant())
        {
            case "repository":
                return new RepositoryRowsSource((string) parameters[0], runtimeContext.EndWorkToken, _createRepository);
        }

        return base.GetRowSource(name, runtimeContext, parameters);
    }
    
    private static MethodsAggregator CreateLibrary()
    {
        var methodsManager = new MethodsManager();
        var library = new GitLibrary();
        
        methodsManager.RegisterLibraries(library);
        
        return new MethodsAggregator(methodsManager);
    }
}