# Pattern pushdown coverage

Runtime: **17.0.14-alpha.2**. Package versions follow the current release manifest.
Both operator families filter the same SQL-visible values before accepted paging.
Reviewed native adapters evaluate those values before expensive work; remaining
predicates use materialized rows. Explicit typed bindings define eligibility, and
native accessors preserve the existing row getters' null and formatting behavior.
JSON and separated-values use their resolved typed schema contracts instead of fixed row bindings.

LIKE supports exact (`'abc'`), prefix (`'abc%'`), suffix (`'%abc'`), and contains
(`'%abc%'`) shapes and their negations. Core transfers only ASCII LIKE literals in
this release; Unicode LIKE patterns stay residual. Unicode row values still follow
Core's matching behavior. RLIKE accepts constant .NET regex patterns, including inline
flags and negation. A null value is false before negation for both families.

## All 16 datasource packages

| Datasource | Supported source rows | Exclusions and boundaries |
|---|---|---|
| Archives | Entry `Key`, `LinkTarget` | Metadata only; no extraction or entry content reads |
| CANBus | All message/signal string metadata | `separatedvalues` frame rows have no direct string columns; nested signal members remain residual |
| Docker | All strings on containers, images, networks, volumes | Materialized daemon responses; no extra daemon request per getter |
| FlatFile | `file.Line` | LineNumber is numeric; decoded line text is matched, not a file enumeration pattern |
| Git | All strings on repository, commits, branches, tags, stashes, remotetags, filehistory, status, remotes, blame | Nested collections remain residual; predicate-only commit messages and native reader fields are retained |
| GitHub | All strings on repositories, issues, pullrequests, commits, branchcommits, branches, releases | Materialized API responses; no additional API-call getters |
| Jira | All strings on issues, projects, comments | Cached SDK/interface fields; no extra user/project lookup. Constant-null SDK fields preserve null semantics |
| JSON | Every resolved string column of `file` | Numeric, Boolean, structured and coercing declared types stay residual; incompatible required declared types conservatively prevent pushdown |
| OS | Strings listed below, including file paths | Process `MainWindowTitle`, `Directory`, `FileName` stay residual: live window/module inspection is not stable materialized metadata and can require process privileges. DLL rows expose nested FileInfo, not direct strings |
| Roslyn | `solution.Id` | Project collection filters remain separate. Nested APPLY members and project strings are not solution-row patterns |
| Search | Every direct string on matches, many, bytes, lines, files, counts, paths, audit | Retain predicate-only MatchText/LineText. Raw-byte text fields remain null. Completed count/audit rows are filtered after accounting; TAKE is not a scan budget |
| SeparatedValues | Every resolved string on comma, tab, semicolon, delimited; declared string contracts included | Enum, character, numeric and other converted columns stay residual. Literal dotted headers are resolved exactly |
| System | None | Dual/range source rows have no queryable strings |
| Time | None | Interval rows have no queryable strings |
| OpenAI | None | Single-row schema exposes functions, not string source columns; model calls stay Core-owned |
| Ollama | None | Single-row schema exposes functions, not string source columns; model calls stay Core-owned |

Credentials and other secret runtime settings are never pattern-column bindings.
Column-valued patterns, cross-source expressions, nested APPLY members, OR containing
patterns, unsupported capability versions, and LIKE wildcard shapes containing `_`
or internal `%` stay residual. A classified needle containing a wildcard is rejected.
Residual predicates or ordering prevent premature paging. Filesystem wildcard
selection never substitutes for row-value LIKE/RLIKE evaluation.

## Execution stages and work avoided

An accepted predicate retains its original `RowFiltering` application record even
when its execution moves earlier. Each conjunct has one execution owner. Readers
without a reviewed adapter retain row-stage filtering. Residual predicates or
ordering prevent accepted paging; API window ownership is transferred only after
all accepted pattern and comparison work can run before the window.

| Provider/source | Early stage | Work avoided and retained boundaries |
|---|---|---|
| OS `file`, `files` | Directory entry / canonical path: `Name`, `FileName`, `FullPath`, `Extension`, `DirectoryPath`, `DirectoryName` | Rejected files create neither FileInfo nor FileEntity. Admitted enumeration entries retain their metadata cache. `DirectoryName` still means the parent of the containing directory, as the existing getter defines it. |
| OS `directories` | Discovered path: `Name`, `FullName`, `Extension`, `Root` | Avoid DirectoryInfo/DirectoryEntity construction for rejected rows. Rejecting a parent never prunes descendant traversal; existing BFS order is retained. File traversal retains DFS order. |
| OS `metadata` | `FullName` before opening, format detection or tag extraction | Excluded files are not opened and cannot cause content-read errors. Metadata category `DirectoryName`, `TagName`, `Description` remain row-stage. Included files keep their existing error policy. |
| Search `matches`, `many`, `lines`, `files`, `bytes` | Exact emitted relative `Path`, before observation, binary inspection, opening and decoding | Scope traversal/security checks and file-count limits still cover all scope-eligible candidates. Content-byte budgets cover admitted files. Visited/admitted/rejected counts are distinct; effective scope fingerprints include the early predicate and execution culture. |
| Search `counts`, `audit`, `paths` | Row-stage | Counts/audit always complete their scope, including TAKE 0; patterns filter completed rows. `paths` has no content reading to avoid and keeps its existing metadata stage. |
| Git commits/branches/tags/stashes | Native fields before full snapshots; CLI predicate fields before unrelated protocol fields | Rejected records avoid optional rich-field loads and wrapper construction; CLI tails are consumed without decoding. Predicate fields themselves remain necessary. No grep translation, extra Git commands, repository opens or history traversals are added. |
| Git repository/remotes/status/remotetags/blame | Native identity, scalar record or completed hunk before row wrappers | Required repository/status/blame/remote-advertisement work remains. Nested identity tokens and lazy members are retained. |
| Git filehistory | `CommitSha`, `FilePath`, `ChangeType`, `OldPath` inside the constructor-selected window, before wrappers | Constructor skip/take still selects the same intrinsic window. Author/time metadata is deferred until an admitted projection needs it. `Author`/`AuthorEmail` predicates retain row-stage ownership. Raw protocol bytes and diff traversal remain necessary. |
| Archives | Current IEntry `Key`, `LinkTarget`, before EntryWrapper | Preserve original raw indexes and disposal. Archive-format traversal/decompression remains necessary; filtering does not extract entries. |
| Docker | Returned DTO before row wrapper | Same daemon requests. All four row types share raw string extraction with existing getters. |
| GitHub/Jira default clients | Returned SDK object before row wrapper | Same request parameters and SDK retrieval. Raw page counts control termination/offsets even for empty filtered pages. Stop later page requests once a safe accepted output window is satisfied. GitHub PR detail requests and Jira comment retrieval remain as before. Alternate clients without the optional internal adapter retain row filtering. |
| JSON | Existing record predicate stage plus sparse decoded-string reuse | Decode an overlapping pattern/output string once per property occurrence. Preserve discovery's duplicate-field rejection and the record processor's existing overwrite semantics. No second additional record parse; unescaped output still uses the existing pool, escaped output still bypasses it. |
| SeparatedValues | Existing field walk plus sparse decoded-string reuse | Reuse only overlapping projected string fields. Keep framing, width checks, declared conversions, sampled validation, schema drift and existing enum conditions, including rejected rows where validation already runs. |
| Other packages / OS sources | Existing row-stage | Capabilities and exclusions in the matrix below are unchanged. |

Native state and decoded reuse belong to one execution/worker, never to the bound
plan's row data. Reuse slots clear on rejection, skip, materializer completion,
failure, cancellation and disposal. There is no reuse allocation for a query with
no overlapping string fields. Initial structured-schema discovery/validation is
unchanged and can still read the source before execution.

`Pattern.*` diagnostics are fixed counters, not retained row histories:
`Candidates`, `RejectedCandidates`, `ConstructedRows`, `ContentOpens`,
`DecodedStrings`, `ApiPages`, `RichFieldLoads`, `ReuseSlots`, `ReusedStrings`.
Counters describe the adapter stage: Git NUL decodes include consumed decoded
protocol tokens; structured decoding/reuse counts cover the overlap slots, not
all unrelated string work. Counts from parallel workers are aggregated. Search
also records `Search.VisitedCandidates`, `Search.EligibleFiles` and
`Search.PathRejectedFiles`. No filesystem glob, Git grep, JQL or remote-search
translation implements LIKE/RLIKE.

`EarlyPatternTests` exercise work avoidance, raw-page progression, request counts,
constructor windows, string reuse and cleanup. Existing `NativePatternTests`
continue to execute every supported binding against Core, including fallback
clients. See [the validation record](PatternPushdownValidation.md) for exact gates,
measurements, skip reasons and remaining upstream limits.

## Static string bindings with execution coverage

The table contains **308 source-column entries across 54 source methods** (aliases
count separately). Dynamic JSON/separated-values columns are additional and follow
the rules above. `NativePatternTests` in each provider executes every advertised
entry against Core `Operators` with pushdown disabled at the row baseline. Each
entry covers four LIKE shapes and RLIKE, each positive and negated. Tests request a
different output field to verify retention of the predicate-only column.

| Schema | Source | String columns |
|---|---|---|
| Archives | file | `Key`, `LinkTarget` |
| can | messages | `Comment`, `Name`, `Transmitter` |
| can | signals | `Comment`, `MessageName`, `Multiplexing`, `Name`, `Unit` |
| csharp | solution | `Id` |
| docker | containers | `Command`, `FlattenPorts`, `ID`, `Image`, `ImageID`, `NetworkSettings`, `State`, `Status` |
| docker | images | `ID`, `ParentID` |
| docker | networks | `ConfigFrom`, `Driver`, `ID`, `IPAM`, `Name`, `Scope` |
| docker | volumes | `CreatedAt`, `Driver`, `Mountpoint`, `Name`, `Scope`, `UsageData` |
| Flat | file | `Line` |
| git | blame | `Author`, `AuthorEmail`, `CommitSha`, `Committer`, `CommitterEmail`, `OriginalFilePath`, `Summary` |
| git | branches | `CanonicalName`, `FriendlyName`, `RemoteName`, `UpstreamBranchCanonicalName` |
| git | commits | `Author`, `AuthorEmail`, `Committer`, `CommitterEmail`, `Message`, `MessageShort`, `Sha` |
| git | filehistory | `Author`, `AuthorEmail`, `ChangeType`, `CommitSha`, `FilePath`, `OldPath` |
| git | remotes | `Name`, `PushUrl`, `Url` |
| git | remotetags | `CanonicalName`, `FriendlyName`, `ObjectSha`, `PeeledSha`, `RemoteName`, `RemoteUrl` |
| git | repository | `Path`, `WorkingDirectory` |
| git | stashes | `Message`, `Selector`, `Sha` |
| git | status | `FilePath`, `IndexStatus`, `State`, `WorkDirStatus` |
| git | tags | `CanonicalName`, `FriendlyName`, `Message`, `TargetSha` |
| github | branchcommits | `AuthorEmail`, `AuthorLogin`, `AuthorName`, `CommitterEmail`, `CommitterLogin`, `CommitterName`, `Message`, `ParentShas`, `Sha`, `ShortSha`, `Url`, `VerificationReason` |
| github | branches | `CommitSha`, `CommitUrl`, `Name`, `RepositoryName`, `RepositoryOwner` |
| github | commits | `AuthorEmail`, `AuthorLogin`, `AuthorName`, `CommitterEmail`, `CommitterLogin`, `CommitterName`, `Message`, `ParentShas`, `Sha`, `ShortSha`, `Url`, `VerificationReason` |
| github | issues | `ActiveLockReason`, `AssigneeLogin`, `Assignees`, `AuthorLogin`, `Body`, `ClosedByLogin`, `Labels`, `MilestoneTitle`, `RepositoryUrl`, `State`, `StateReason`, `Title`, `Url` |
| github | pullrequests | `ActiveLockReason`, `AssigneeLogin`, `Assignees`, `AuthorLogin`, `BaseRef`, `BaseRepository`, `BaseSha`, `Body`, `HeadRef`, `HeadRepository`, `HeadSha`, `Labels`, `MergeableState`, `MergeCommitSha`, `MergedByLogin`, `MilestoneTitle`, `State`, `Title`, `Url` |
| github | releases | `AuthorLogin`, `Body`, `Name`, `TagName`, `TarballUrl`, `TargetCommitish`, `Url`, `ZipballUrl` |
| github | repositories | `CloneUrl`, `DefaultBranch`, `Description`, `FullName`, `Language`, `License`, `Name`, `OwnerLogin`, `SshUrl`, `Url`, `Visibility` |
| jira | comments | `Author`, `AuthorDisplayName`, `Body`, `Id`, `IssueKey`, `UpdateAuthor`, `UpdateAuthorDisplayName`, `VisibilityGroup`, `VisibilityRole` |
| jira | issues | `AffectsVersions`, `Assignee`, `AssigneeDisplayName`, `Components`, `Description`, `Environment`, `FixVersions`, `Id`, `Key`, `Labels`, `OriginalEstimate`, `ParentKey`, `Priority`, `ProjectKey`, `RemainingEstimate`, `Reporter`, `ReporterDisplayName`, `Resolution`, `SecurityLevel`, `Status`, `Summary`, `TimeSpent`, `Type`, `Url` |
| jira | projects | `AvatarUrl`, `Category`, `CategoryDescription`, `Description`, `Id`, `Key`, `Lead`, `Name`, `Url` |
| os | cultures | `CultureTypes`, `DecimalSeparator`, `DisplayName`, `EnglishName`, `LongDatePattern`, `LongTimePattern`, `Name`, `NativeName`, `NumberGroupSeparator`, `ParentName`, `ShortDatePattern`, `ShortTimePattern` |
| os | currentculture | `CurrentCulture`, `CurrentUICulture`, `DecimalSeparator`, `LongDatePattern`, `LongTimePattern`, `NumberGroupSeparator`, `ShortDatePattern`, `ShortTimePattern` |
| os | directories | `Extension`, `FullName`, `Name`, `Root` |
| os | dirscompare | `DestinationFileRelative`, `SourceFileRelative` |
| os | drives | `DriveFormat`, `DriveType`, `Name`, `RootDirectory` |
| os | encodings | `BodyName`, `EncodingName`, `HeaderName`, `Name`, `WebName` |
| os | environmentvariables | `Name`, `Target` |
| os | file | `DirectoryName`, `DirectoryPath`, `Extension`, `FileName`, `FullPath`, `Name` |
| os | fileattributes | `Name` |
| os | files | `DirectoryName`, `DirectoryPath`, `Extension`, `FileName`, `FullPath`, `Name` |
| os | metadata | `Description`, `DirectoryName`, `FullName`, `TagName` |
| os | pathinfo | `DirectoryName`, `Extension`, `FileName`, `FullPath`, `InputPath`, `Root` |
| os | runtime | `CurrentDirectory`, `DotNetVersion`, `FrameworkDescription`, `OSArchitecture`, `OSDescription`, `ProcessArchitecture` |
| os | specialfolders | `Name`, `Path` |
| os | timezones | `DaylightName`, `DisplayName`, `Id`, `StandardName` |
| os | zip | `FullName`, `Name` |
| search | bytes | `LineText`, `MatchText`, `Origin`, `Path`, `PatternId` |
| search | counts | `Origin`, `Path`, `PatternId` |
| search | files | `Origin`, `Path`, `PatternId` |
| search | lines | `LineText`, `Origin`, `Path`, `PatternId` |
| search | many | `MatchText`, `Path`, `PatternId` |
| search | matches | `MatchText`, `Path`, `PatternId` |
| search | paths | `EntryKind`, `Origin`, `Path` |
| os | processes | `MachineName`, `ProcessName` |
| search | audit | `Root`, `ScanId`, `ScopeFingerprint`, `TerminalReason`, `FailureCode`, `FailurePath` |

Process tests use a controlled process-row fixture. Audit tests execute real scans
for every column/shape/negation with TAKE 0 and TAKE 1 and assert complete accounting;
random ScanId values are checked by matching behavior rather than compared across runs.
API tests cover both schema and direct source-constructor entry points and assert the
exact request count, so a getter cannot silently add remote calls.

`Alpha14IntegrationTests` compares compiled queries with provider pushdown enabled
and disabled. It covers dynamic/declared strings, quoted dotted column names,
missing/null/empty values, escaping, Unicode, inline flags, conjunctions, unprojected
fields, ordering, residual work, paging, cancellation, and repeated/concurrent runs.
`PatternMatcherTests` checks matching across cultures, culture changes on a shared
bound predicate, unsupported shapes and disposal on early TAKE.

Capability/application assertions are part of execution tests: every accepted
conjunct has exactly one matching `RowFiltering` application. Coverage records are
retained in `artifacts/pattern-pushdown/coverage/`; manual process/audit cases are in
the corresponding provider's `NativePatternTests`. See [validation](PatternPushdownValidation.md)
for commands, exact totals, skips, package artifacts and upstream reproducers.

Current Core integration and release qualification: [CoreAlpha2ReleaseValidation.md](CoreAlpha2ReleaseValidation.md). Earlier validation remains historical evidence.
