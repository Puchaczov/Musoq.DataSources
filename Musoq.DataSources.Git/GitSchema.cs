using System;
using System.IO;
using System.Security;
using LibGit2Sharp;
using Musoq.Schema;
using Musoq.Schema.DataSources;
using Musoq.Schema.Managers;
using Musoq.Schema.Reflection;

namespace Musoq.DataSources.Git;

/// <description>
///     Provides schema to work with Git repositories.
/// </description>
/// <short-description>
///     Provides schema to work with Git repositories.
/// </short-description>
/// <project-url>https://github.com/Puchaczov/Musoq.DataSources</project-url>
public class GitSchema : SchemaBase
{
    private const string SchemaName = "Git";
    private const string RepositoryTable = "repository";
    private const string TagsTable = "tags";
    private const string CommitsTable = "commits";
    private const string BranchesTable = "branches";
    private const string FileHistoryTable = "filehistory";
    private const string StatusTable = "status";
    private const string RemotesTable = "remotes";
    private const string BlameTable = "blame";
    private readonly Func<string, Repository> _createRepository;

    /// <virtual-constructors>
    ///     <virtual-constructor>
    ///         <examples>
    ///             <example>
    ///                 <from>
    ///                     <environmentVariables></environmentVariables>
    ///                     #git.repository(string path)
    ///                 </from>
    ///                 <description>Allows to perform queries on the given Git repository path.</description>
    ///                 <columns>
    ///                     <column name="Path" type="string">Repository path</column>
    ///                     <column name="WorkingDirectory" type="string">Working directory path</column>
    ///                     <column name="Branches" type="BranchEntity[]">Repository branches</column>
    ///                     <column name="Tags" type="TagEntity[]">Repository tags</column>
    ///                     <column name="Commits" type="CommitEntity[]">Repository commits</column>
    ///                     <column name="Head" type="BranchEntity">Current HEAD branch</column>
    ///                     <column name="Configuration" type="ConfigurationEntityKeyValue[]">Repository configuration</column>
    ///                     <column name="Information" type="RepositoryInformationEntity">Repository information</column>
    ///                     <column name="Stashes" type="StashEntity[]">Repository stashes</column>
    ///                     <column name="Self" type="RepositoryEntity">This instance</column>
    ///                 </columns>
    ///             </example>
    ///         </examples>
    ///     </virtual-constructor>
    ///     <virtual-constructor>
    ///         <examples>
    ///             <example>
    ///                 <from>
    ///                     <environmentVariables></environmentVariables>
    ///                     #git.tags(string path)
    ///                 </from>
    ///                 <description>Allows to query tags directly from a Git repository.</description>
    ///                 <columns>
    ///                     <column name="FriendlyName" type="string?">Tag friendly name</column>
    ///                     <column name="CanonicalName" type="string?">Tag canonical name</column>
    ///                     <column name="Message" type="string?">Tag message</column>
    ///                     <column name="IsAnnotated" type="bool">Is annotated tag</column>
    ///                     <column name="Annotation" type="AnnotationEntity">Tag annotation</column>
    ///                     <column name="Commit" type="CommitEntity?">Tag commit</column>
    ///                 </columns>
    ///             </example>
    ///         </examples>
    ///     </virtual-constructor>
    ///     <virtual-constructor>
    ///         <examples>
    ///             <example>
    ///                 <from>
    ///                     <environmentVariables></environmentVariables>
    ///                     #git.commits(string path)
    ///                 </from>
    ///                 <description>Allows to query commits directly from a Git repository.</description>
    ///                 <columns>
    ///                     <column name="Sha" type="string">Commit SHA</column>
    ///                     <column name="Message" type="string">Commit message</column>
    ///                     <column name="MessageShort" type="string">Short commit message</column>
    ///                     <column name="Author" type="string">Author name</column>
    ///                     <column name="AuthorEmail" type="string">Author email</column>
    ///                     <column name="Committer" type="string">Committer name</column>
    ///                     <column name="CommitterEmail" type="string">Committer email</column>
    ///                     <column name="CommittedWhen" type="DateTimeOffset">Commit date and time</column>
    ///                     <column name="Self" type="CommitEntity">This instance</column>
    ///                 </columns>
    ///             </example>
    ///         </examples>
    ///     </virtual-constructor>
    ///     <virtual-constructor>
    ///         <examples>
    ///             <example>
    ///                 <from>
    ///                     <environmentVariables></environmentVariables>
    ///                     #git.branches(string path)
    ///                 </from>
    ///                 <description>Allows to query branches directly from a Git repository.</description>
    ///                 <columns>
    ///                     <column name="FriendlyName" type="string">Branch friendly name</column>
    ///                     <column name="CanonicalName" type="string">Branch canonical name</column>
    ///                     <column name="IsRemote" type="bool">Is remote branch</column>
    ///                     <column name="IsTracking" type="bool">Is tracking another branch</column>
    ///                     <column name="IsCurrentRepositoryHead" type="bool">Is current repository HEAD</column>
    ///                     <column name="TrackedBranch" type="BranchEntity">Tracked branch</column>
    ///                     <column name="BranchTrackingDetails" type="BranchTrackingDetailsEntity">Branch tracking details</column>
    ///                     <column name="Tip" type="CommitEntity">Branch tip commit</column>
    ///                     <column name="Commits" type="CommitEntity[]">Branch commits</column>
    ///                     <column name="UpstreamBranchCanonicalName" type="string">Upstream branch canonical name</column>
    ///                     <column name="RemoteName" type="string">Remote name</column>
    ///                     <column name="ParentBranch" type="BranchEntity">Parent branch</column>
    ///                     <column name="Self" type="BranchEntity">This instance</column>
    ///                 </columns>
    ///             </example>
    ///         </examples>
    ///     </virtual-constructor>
    ///     <virtual-constructor>
    ///         <examples>
    ///             <example>
    ///                 <from>
    ///                     <environmentVariables></environmentVariables>
    ///                     #git.filehistory(string path, string filePattern)
    ///                 </from>
    ///                 <description>Allows to query the history of file changes in a Git repository.</description>
    ///                 <columns>
    ///                     <column name="CommitSha" type="string">Commit SHA</column>
    ///                     <column name="Author" type="string">Author name</column>
    ///                     <column name="AuthorEmail" type="string">Author email</column>
    ///                     <column name="CommittedWhen" type="DateTimeOffset">Commit date and time</column>
    ///                     <column name="FilePath" type="string">Changed file path</column>
    ///                     <column name="ChangeType" type="string">Type of change (Added, Modified, Deleted, Renamed)</column>
    ///                     <column name="OldPath" type="string">Previous file path (for renamed files)</column>
    ///                 </columns>
    ///             </example>
    ///             <example>
    ///                 <from>
    ///                     <environmentVariables></environmentVariables>
    ///                     #git.filehistory(string path, string filePattern, int take)
    ///                 </from>
    ///                 <description>
    ///                     Allows to query the history of file changes in a Git repository, limited to the first N
    ///                     changes.
    ///                 </description>
    ///                 <columns>
    ///                     <column name="CommitSha" type="string">Commit SHA</column>
    ///                     <column name="Author" type="string">Author name</column>
    ///                     <column name="AuthorEmail" type="string">Author email</column>
    ///                     <column name="CommittedWhen" type="DateTimeOffset">Commit date and time</column>
    ///                     <column name="FilePath" type="string">Changed file path</column>
    ///                     <column name="ChangeType" type="string">Type of change (Added, Modified, Deleted, Renamed)</column>
    ///                     <column name="OldPath" type="string">Previous file path (for renamed files)</column>
    ///                 </columns>
    ///             </example>
    ///             <example>
    ///                 <from>
    ///                     <environmentVariables></environmentVariables>
    ///                     #git.filehistory(string path, string filePattern, int skip, int take)
    ///                 </from>
    ///                 <description>
    ///                     Allows to query the history of file changes in a Git repository, skipping the first N
    ///                     changes and taking the next M changes.
    ///                 </description>
    ///                 <columns>
    ///                     <column name="CommitSha" type="string">Commit SHA</column>
    ///                     <column name="Author" type="string">Author name</column>
    ///                     <column name="AuthorEmail" type="string">Author email</column>
    ///                     <column name="CommittedWhen" type="DateTimeOffset">Commit date and time</column>
    ///                     <column name="FilePath" type="string">Changed file path</column>
    ///                     <column name="ChangeType" type="string">Type of change (Added, Modified, Deleted, Renamed)</column>
    ///                     <column name="OldPath" type="string">Previous file path (for renamed files)</column>
    ///                 </columns>
    ///             </example>
    ///         </examples>
    ///     </virtual-constructor>
    ///     <virtual-constructor>
    ///         <examples>
    ///             <example>
    ///                 <from>
    ///                     <environmentVariables></environmentVariables>
    ///                     #git.status(string path)
    ///                 </from>
    ///                 <description>Allows to query the working directory status of a Git repository.</description>
    ///                 <columns>
    ///                     <column name="FilePath" type="string">File path</column>
    ///                     <column name="State" type="string">File state</column>
    ///                     <column name="IndexStatus" type="string">Index status</column>
    ///                     <column name="WorkDirStatus" type="string">Working directory status</column>
    ///                 </columns>
    ///             </example>
    ///         </examples>
    ///     </virtual-constructor>
    ///     <virtual-constructor>
    ///         <examples>
    ///             <example>
    ///                 <from>
    ///                     <environmentVariables></environmentVariables>
    ///                     #git.remotes(string path)
    ///                 </from>
    ///                 <description>Allows to query Git remotes from a repository.</description>
    ///                 <columns>
    ///                     <column name="Name" type="string">Remote name</column>
    ///                     <column name="Url" type="string">Remote URL</column>
    ///                     <column name="PushUrl" type="string">Push URL</column>
    ///                 </columns>
    ///             </example>
    ///         </examples>
    ///     </virtual-constructor>
    ///     <virtual-constructor>
    ///         <examples>
    ///             <example>
    ///                 <from>
    ///                     <environmentVariables></environmentVariables>
    ///                     #git.blame(string repositoryPath, string filePath)
    ///                 </from>
    ///                 <description>Returns hunk-based blame information for a file at HEAD revision.</description>
    ///                 <columns>
    ///                     <column name="StartLineNumber" type="int">First line of hunk (1-based)</column>
    ///                     <column name="EndLineNumber" type="int">Last line of hunk (1-based)</column>
    ///                     <column name="LineCount" type="int">Number of lines in hunk</column>
    ///                     <column name="CommitSha" type="string">SHA of commit that last modified these lines</column>
    ///                     <column name="Author" type="string">Author name</column>
    ///                     <column name="AuthorEmail" type="string">Author email</column>
    ///                     <column name="AuthorDate" type="DateTimeOffset">When author made the change</column>
    ///                     <column name="Committer" type="string">Committer name</column>
    ///                     <column name="CommitterEmail" type="string">Committer email</column>
    ///                     <column name="CommitterDate" type="DateTimeOffset">When commit was applied</column>
    ///                     <column name="Summary" type="string">First line of commit message</column>
    ///                     <column name="OriginalStartLineNumber" type="int?">Original line number if moved/copied</column>
    ///                     <column name="OriginalFilePath" type="string?">
    ///                         Original file path if moved/copied (null if same
    ///                         file)
    ///                     </column>
    ///                     <column name="Lines" type="BlameLineEntity[]">Line details with content (lazy loaded)</column>
    ///                     <column name="Self" type="BlameHunkEntity">This instance</column>
    ///                 </columns>
    ///             </example>
    ///             <example>
    ///                 <from>
    ///                     <environmentVariables></environmentVariables>
    ///                     #git.blame(string repositoryPath, string filePath, string revision)
    ///                 </from>
    ///                 <description>Returns hunk-based blame information for a file at a specific revision.</description>
    ///                 <columns>
    ///                     <column name="StartLineNumber" type="int">First line of hunk (1-based)</column>
    ///                     <column name="EndLineNumber" type="int">Last line of hunk (1-based)</column>
    ///                     <column name="LineCount" type="int">Number of lines in hunk</column>
    ///                     <column name="CommitSha" type="string">SHA of commit that last modified these lines</column>
    ///                     <column name="Author" type="string">Author name</column>
    ///                     <column name="AuthorEmail" type="string">Author email</column>
    ///                     <column name="AuthorDate" type="DateTimeOffset">When author made the change</column>
    ///                     <column name="Committer" type="string">Committer name</column>
    ///                     <column name="CommitterEmail" type="string">Committer email</column>
    ///                     <column name="CommitterDate" type="DateTimeOffset">When commit was applied</column>
    ///                     <column name="Summary" type="string">First line of commit message</column>
    ///                     <column name="OriginalStartLineNumber" type="int?">Original line number if moved/copied</column>
    ///                     <column name="OriginalFilePath" type="string?">
    ///                         Original file path if moved/copied (null if same
    ///                         file)
    ///                     </column>
    ///                     <column name="Lines" type="BlameLineEntity[]">Line details with content (lazy loaded)</column>
    ///                     <column name="Self" type="BlameHunkEntity">This instance</column>
    ///                 </columns>
    ///             </example>
    ///         </examples>
    ///     </virtual-constructor>
    /// </virtual-constructors>
    /// <additional-tables>
    ///     <additional-table>
    ///         <description>Represents a Git branch</description>
    ///         <columns type="BranchEntity">
    ///             <column name="FriendlyName" type="string">Branch friendly name</column>
    ///             <column name="CanonicalName" type="string">Branch canonical name</column>
    ///             <column name="IsRemote" type="bool">Is remote branch</column>
    ///             <column name="IsTracking" type="bool">Is tracking another branch</column>
    ///             <column name="IsCurrentRepositoryHead" type="bool">Is current repository HEAD</column>
    ///             <column name="TrackedBranch" type="BranchEntity">Tracked branch</column>
    ///             <column name="BranchTrackingDetails" type="BranchTrackingDetailsEntity">Branch tracking details</column>
    ///             <column name="Tip" type="CommitEntity">Branch tip commit</column>
    ///             <column name="Commits" type="CommitEntity[]">Branch commits</column>
    ///             <column name="UpstreamBranchCanonicalName" type="string">Upstream branch canonical name</column>
    ///             <column name="RemoteName" type="string">Remote name</column>
    ///             <column name="ParentBranch" type="BranchEntity">Parent branch</column>
    ///             <column name="Self" type="BranchEntity">This instance</column>
    ///         </columns>
    ///     </additional-table>
    ///     <additional-table>
    ///         <description>Represents a Git commit</description>
    ///         <columns type="CommitEntity">
    ///             <column name="Sha" type="string">Commit SHA</column>
    ///             <column name="Message" type="string">Commit message</column>
    ///             <column name="MessageShort" type="string">Short commit message</column>
    ///             <column name="Author" type="string">Author name</column>
    ///             <column name="AuthorEmail" type="string">Author email</column>
    ///             <column name="Committer" type="string">Committer name</column>
    ///             <column name="CommitterEmail" type="string">Committer email</column>
    ///             <column name="CommittedWhen" type="DateTimeOffset">Commit date and time</column>
    ///             <column name="Self" type="CommitEntity">This instance</column>
    ///         </columns>
    ///     </additional-table>
    ///     <additional-table>
    ///         <description>Represents a Git tag</description>
    ///         <columns type="TagEntity">
    ///             <column name="FriendlyName" type="string?">Tag friendly name</column>
    ///             <column name="CanonicalName" type="string?">Tag canonical name</column>
    ///             <column name="Message" type="string?">Tag message</column>
    ///             <column name="IsAnnotated" type="bool">Is annotated tag</column>
    ///             <column name="Annotation" type="AnnotationEntity">Tag annotation</column>
    ///             <column name="Commit" type="CommitEntity?">Tag commit</column>
    ///         </columns>
    ///     </additional-table>
    ///     <additional-table>
    ///         <description>Represents a Git stash</description>
    ///         <columns type="StashEntity">
    ///             <column name="Message" type="string">Stash message</column>
    ///             <column name="Index" type="CommitEntity">Index state</column>
    ///             <column name="WorkTree" type="CommitEntity">Work tree state</column>
    ///             <column name="UntrackedFiles" type="CommitEntity">Untracked files state</column>
    ///         </columns>
    ///     </additional-table>
    ///     <additional-table>
    ///         <description>Represents Git configuration entry</description>
    ///         <columns type="ConfigurationEntityKeyValue">
    ///             <column name="Key" type="string">Configuration key</column>
    ///             <column name="Value" type="string">Configuration value</column>
    ///             <column name="ConfigurationLevel" type="string">Configuration level</column>
    ///         </columns>
    ///     </additional-table>
    ///     <additional-table>
    ///         <description>Represents Git repository information</description>
    ///         <columns type="RepositoryInformationEntity">
    ///             <column name="Path" type="string">Repository path</column>
    ///             <column name="WorkingDirectory" type="string">Working directory path</column>
    ///             <column name="IsBare" type="bool">Is bare repository</column>
    ///             <column name="IsHeadDetached" type="bool">Is HEAD detached</column>
    ///             <column name="IsHeadUnborn" type="bool">Is HEAD unborn</column>
    ///             <column name="IsShallow" type="bool">Is shallow repository</column>
    ///         </columns>
    ///     </additional-table>
    ///     <additional-table>
    ///         <description>Represents Git branch tracking details</description>
    ///         <columns type="BranchTrackingDetailsEntity">
    ///             <column name="AheadBy" type="int?">Commits ahead count</column>
    ///             <column name="BehindBy" type="int?">Commits behind count</column>
    ///         </columns>
    ///     </additional-table>
    ///     <additional-table>
    ///         <description>Represents Git file differences</description>
    ///         <columns type="DifferenceEntity">
    ///             <column name="Path" type="string">Changed file path</column>
    ///             <column name="Exists" type="bool">File exists in new version</column>
    ///             <column name="ChangeKind" type="string">Kind of change</column>
    ///             <column name="OldPath" type="string">Old file path</column>
    ///             <column name="OldMode" type="string">Old file mode</column>
    ///             <column name="NewMode" type="string">New file mode</column>
    ///             <column name="OldSha" type="string">Old file SHA</column>
    ///             <column name="NewSha" type="string">New file SHA</column>
    ///             <column name="OldContent" type="string">Old file content</column>
    ///             <column name="NewContent" type="string">New file content</column>
    ///             <column name="OldContentBytes" type="byte[]">Old file content bytes</column>
    ///             <column name="NewContentBytes" type="byte[]">New file content bytes</column>
    ///         </columns>
    ///     </additional-table>
    ///     <additional-table>
    ///         <description>Represents Git tag annotation</description>
    ///         <columns type="AnnotationEntity">
    ///             <column name="Message" type="string?">Annotation message</column>
    ///             <column name="Name" type="string?">Annotation name</column>
    ///             <column name="Sha" type="string?">Annotation SHA</column>
    ///             <column name="Tagger" type="TaggerEntity?">Tagger information</column>
    ///         </columns>
    ///     </additional-table>
    ///     <additional-table>
    ///         <description>Represents Git tag tagger</description>
    ///         <columns type="TaggerEntity">
    ///             <column name="Name" type="string?">Tagger name</column>
    ///             <column name="Email" type="string?">Tagger email</column>
    ///             <column name="WhenSigned" type="DateTimeOffset">When tag was signed</column>
    ///         </columns>
    ///     </additional-table>
    ///     <additional-table>
    ///         <description>Represents Git patch</description>
    ///         <columns type="PatchEntity">
    ///             <column name="LinesAdded" type="int">Lines added</column>
    ///             <column name="LinesDeleted" type="int">Lines deleted</column>
    ///             <column name="Content" type="string">Gets the full patch file of this diff</column>
    ///             <column name="Changes" type="PatchEntryChangesEntity[]">Gets the changes in this patch</column>
    ///         </columns>
    ///     </additional-table>
    ///     <additional-table>
    ///         <description>Represents Git patch entry changes</description>
    ///         <columns type="PatchEntryChangesEntity">
    ///             <column name="LinesAdded" type="int">Lines added</column>
    ///             <column name="LinesDeleted" type="int">Lines deleted</column>
    ///             <column name="Content" type="string">Gets the patch corresponding to these changes</column>
    ///             <column name="Path" type="string">Gets the path of a file</column>
    ///             <column name="OldMode" type="string">Gets the old mode</column>
    ///             <column name="Mode" type="string">Gets the mode</column>
    ///             <column name="IsBinaryComparison" type="string">
    ///                 Determines if at least one side of the comparison holds
    ///                 binary content
    ///             </column>
    ///         </columns>
    ///     </additional-table>
    ///     <additional-table>
    ///         <description>Represents a merge base in a git repository</description>
    ///         <columns type="MergeBaseEntity">
    ///             <column name="MergeBaseCommit" type="CommitEntity">Merge base commit</column>
    ///             <column name="FirstBranch" type="BranchEntity">First branch</column>
    ///             <column name="SecondBranch" type="BranchEntity">Second branch</column>
    ///         </columns>
    ///     </additional-table>
    ///     <additional-table>
    ///         <description>Represents a blame hunk (contiguous group of lines sharing the same attribution)</description>
    ///         <columns type="BlameHunkEntity">
    ///             <column name="StartLineNumber" type="int">First line of hunk (1-based)</column>
    ///             <column name="EndLineNumber" type="int">Last line of hunk (1-based)</column>
    ///             <column name="LineCount" type="int">Number of lines in hunk</column>
    ///             <column name="CommitSha" type="string">SHA of commit that last modified these lines</column>
    ///             <column name="Author" type="string">Author name</column>
    ///             <column name="AuthorEmail" type="string">Author email</column>
    ///             <column name="AuthorDate" type="DateTimeOffset">When author made the change</column>
    ///             <column name="Committer" type="string">Committer name</column>
    ///             <column name="CommitterEmail" type="string">Committer email</column>
    ///             <column name="CommitterDate" type="DateTimeOffset">When commit was applied</column>
    ///             <column name="Summary" type="string">First line of commit message</column>
    ///             <column name="OriginalStartLineNumber" type="int?">Original line number if moved/copied</column>
    ///             <column name="OriginalFilePath" type="string?">Original file path if moved/copied (null if same file)</column>
    ///             <column name="Lines" type="BlameLineEntity[]">Line details with content (lazy loaded)</column>
    ///             <column name="Self" type="BlameHunkEntity">This instance</column>
    ///         </columns>
    ///     </additional-table>
    ///     <additional-table>
    ///         <description>Represents a single line within a blame hunk</description>
    ///         <columns type="BlameLineEntity">
    ///             <column name="LineNumber" type="int">Line number (1-based)</column>
    ///             <column name="Content" type="string">Actual line content</column>
    ///             <column name="Self" type="BlameLineEntity">This instance</column>
    ///         </columns>
    ///     </additional-table>
    /// </additional-tables>
    public GitSchema()
        : base(SchemaName.ToLowerInvariant(), CreateLibrary())
    {
        _createRepository = path => new Repository(path);
    }

    /// <summary>
    ///     Gets the table name based on the given data source and parameters.
    /// </summary>
    /// <param name="name">Data Source name</param>
    /// <param name="runtimeContext">Runtime context</param>
    /// <param name="parameters">Parameters to pass to data source</param>
    /// <returns>Requested table metadata</returns>
    public override ISchemaTable GetTableByName(string name, RuntimeContext runtimeContext, params object[] parameters)
    {
        switch (name.ToLowerInvariant())
        {
            case RepositoryTable:
                return new RepositoryTable();
            case TagsTable:
                return new TagsTable();
            case CommitsTable:
                return new CommitsTable();
            case BranchesTable:
                return new BranchesTable();
            case FileHistoryTable:
                return new FileHistoryTable();
            case StatusTable:
                return new StatusTable();
            case RemotesTable:
                return new RemotesTable();
            case BlameTable:
                return new BlameTable();
        }

        return base.GetTableByName(name, runtimeContext, parameters);
    }

    /// <inheritdoc />
    public override SchemaMethodInfo[] GetRawConstructors(string methodName, RuntimeContext runtimeContext)
    {
        return methodName.ToLowerInvariant() switch
        {
            RepositoryTable => [CreateRepositoryMethodInfo()],
            TagsTable => [CreateTagsMethodInfo()],
            CommitsTable => [CreateCommitsMethodInfo()],
            BranchesTable => [CreateBranchesMethodInfo()],
            FileHistoryTable => CreateFileHistoryMethodInfos(),
            StatusTable => [CreateStatusMethodInfo()],
            RemotesTable => [CreateRemotesMethodInfo()],
            BlameTable => CreateBlameMethodInfos(),
            _ => throw new NotSupportedException(
                $"Data source '{methodName}' is not supported by {SchemaName} schema. " +
                $"Available data sources: {RepositoryTable}, {TagsTable}, {CommitsTable}, {BranchesTable}, {FileHistoryTable}, {StatusTable}, {RemotesTable}, {BlameTable}")
        };
    }

    /// <inheritdoc />
    public override SchemaMethodInfo[] GetRawConstructors(RuntimeContext runtimeContext)
    {
        return
        [
            CreateRepositoryMethodInfo(),
            CreateTagsMethodInfo(),
            CreateCommitsMethodInfo(),
            CreateBranchesMethodInfo(),
            ..CreateFileHistoryMethodInfos(),
            CreateStatusMethodInfo(),
            CreateRemotesMethodInfo(),
            ..CreateBlameMethodInfos()
        ];
    }

    private static SchemaMethodInfo CreateRepositoryMethodInfo()
    {
        var constructorInfo = new ConstructorInfo(
            null!,
            false,
            [
                ("path", typeof(string))
            ]
        );

        return new SchemaMethodInfo(RepositoryTable, constructorInfo);
    }

    private static SchemaMethodInfo CreateTagsMethodInfo()
    {
        var constructorInfo = new ConstructorInfo(
            null!,
            false,
            [
                ("path", typeof(string))
            ]
        );

        return new SchemaMethodInfo(TagsTable, constructorInfo);
    }

    private static SchemaMethodInfo CreateCommitsMethodInfo()
    {
        var constructorInfo = new ConstructorInfo(
            null!,
            false,
            [
                ("path", typeof(string))
            ]
        );

        return new SchemaMethodInfo(CommitsTable, constructorInfo);
    }

    private static SchemaMethodInfo CreateBranchesMethodInfo()
    {
        var constructorInfo = new ConstructorInfo(
            null!,
            false,
            [
                ("path", typeof(string))
            ]
        );

        return new SchemaMethodInfo(BranchesTable, constructorInfo);
    }

    private static SchemaMethodInfo[] CreateFileHistoryMethodInfos()
    {
        var simpleConstructorInfo = new ConstructorInfo(
            null!,
            false,
            [
                ("path", typeof(string)),
                ("filePattern", typeof(string))
            ]
        );

        var takeConstructorInfo = new ConstructorInfo(
            null!,
            false,
            [
                ("path", typeof(string)),
                ("filePattern", typeof(string)),
                ("take", typeof(int))
            ]
        );

        var skipTakeConstructorInfo = new ConstructorInfo(
            null!,
            false,
            [
                ("path", typeof(string)),
                ("filePattern", typeof(string)),
                ("skip", typeof(int)),
                ("take", typeof(int))
            ]
        );

        return
        [
            new SchemaMethodInfo(FileHistoryTable, simpleConstructorInfo),
            new SchemaMethodInfo(FileHistoryTable, takeConstructorInfo),
            new SchemaMethodInfo(FileHistoryTable, skipTakeConstructorInfo)
        ];
    }

    private static SchemaMethodInfo CreateStatusMethodInfo()
    {
        var constructorInfo = new ConstructorInfo(
            null!,
            false,
            [
                ("path", typeof(string))
            ]
        );

        return new SchemaMethodInfo(StatusTable, constructorInfo);
    }

    private static SchemaMethodInfo CreateRemotesMethodInfo()
    {
        var constructorInfo = new ConstructorInfo(
            null!,
            false,
            [
                ("path", typeof(string))
            ]
        );

        return new SchemaMethodInfo(RemotesTable, constructorInfo);
    }

    private static SchemaMethodInfo[] CreateBlameMethodInfos()
    {
        var simpleConstructorInfo = new ConstructorInfo(
            null!,
            false,
            [
                ("repositoryPath", typeof(string)),
                ("filePath", typeof(string))
            ]
        );

        var revisionConstructorInfo = new ConstructorInfo(
            null!,
            false,
            [
                ("repositoryPath", typeof(string)),
                ("filePath", typeof(string)),
                ("revision", typeof(string))
            ]
        );

        return
        [
            new SchemaMethodInfo(BlameTable, simpleConstructorInfo),
            new SchemaMethodInfo(BlameTable, revisionConstructorInfo)
        ];
    }

    /// <summary>
    ///     Gets the data source based on the given data source and parameters.
    /// </summary>
    /// <param name="name">Data source name</param>
    /// <param name="runtimeContext">Runtime context</param>
    /// <param name="parameters">Parameters to pass data to data source</param>
    /// <returns>Data source</returns>
    public override RowSource GetRowSource(string name, RuntimeContext runtimeContext, params object[] parameters)
    {
        var path = (string)parameters[0];

        if (!DirectoryOrFile(path)) throw new InvalidOperationException($"The path '{path}' is not a directory");

        var directoryInfo = new DirectoryInfo(path);

        if (!DirectoryContainsGitFolder(path) && directoryInfo.Name != ".git")
            throw new InvalidOperationException($"The path '{path}' does not contain a Git repository");

        switch (name.ToLowerInvariant())
        {
            case RepositoryTable:
                return new RepositoryRowsSource((string)parameters[0], _createRepository, runtimeContext.EndWorkToken);
            case TagsTable:
                return new TagsRowsSource((string)parameters[0], _createRepository, runtimeContext);
            case CommitsTable:
                return new CommitsRowsSource((string)parameters[0], _createRepository, runtimeContext);
            case BranchesTable:
                return new BranchesRowsSource((string)parameters[0], _createRepository, runtimeContext);
            case FileHistoryTable:
                var skip = parameters.Length > 3 ? (int)parameters[2] : 0;
                var take = parameters.Length > 3 ? (int)parameters[3] :
                    parameters.Length > 2 ? (int)parameters[2] : int.MaxValue;
                return new FileHistoryRowsSource((string)parameters[0], (string)parameters[1], skip, take,
                    _createRepository, runtimeContext.EndWorkToken);
            case StatusTable:
                return new StatusRowsSource((string)parameters[0], _createRepository, runtimeContext);
            case RemotesTable:
                return new RemotesRowsSource((string)parameters[0], _createRepository, runtimeContext);
            case BlameTable:
                var repositoryPath = (string)parameters[0];
                var filePath = (string)parameters[1];
                var revision = parameters.Length > 2 ? (string)parameters[2] : "HEAD";
                return new BlameRowsSource(repositoryPath, filePath, revision, _createRepository,
                    runtimeContext.EndWorkToken);
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

    private static bool DirectoryOrFile(string path)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(path)) return false;

            var attr = File.GetAttributes(path);

            return (attr & FileAttributes.Directory) == FileAttributes.Directory;
        }
        catch (Exception ex) when (ex is SecurityException or UnauthorizedAccessException or DirectoryNotFoundException
                                       or FileNotFoundException)
        {
            return false;
        }
    }

    private static bool DirectoryContainsGitFolder(string path)
    {
        try
        {
            return Directory.Exists(Path.Combine(path, ".git"));
        }
        catch (Exception ex) when (ex is SecurityException or UnauthorizedAccessException or DirectoryNotFoundException
                                       or FileNotFoundException)
        {
            return false;
        }
    }
}