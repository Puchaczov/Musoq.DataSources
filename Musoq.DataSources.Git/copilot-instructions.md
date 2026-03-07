# Git plugin guide

## Purpose
- Exposes local repositories as Musoq sources for `repository`, `commits`, `branches`, `tags`, `filehistory`, `status`, `remotes`, and `blame`.

## Read first
- `GitSchema.cs`
- root-level `*Table.cs` and `*RowsSource.cs` files
- `Entities/`
- `GitWhereNodeHelper.cs`
- `GitLibrary.cs`
- `Musoq.DataSources.Git.Tests/GitToSqlTests.cs`
- `Musoq.DataSources.Git.Tests/BlameTests.cs`
- `Musoq.DataSources.Git.Tests/GitWhereNodeOptimizationTests.cs`

## Patterns to preserve
- Keep each Git concept in its own entity/table/source pair instead of adding generic catch-all rows.
- Many sources inherit from `AsyncRowsSourceBase`; preserve chunking and cancellation for large-history traversal.
- `GitSchema` method names and overloads define the public query surface and `desc #git` behavior.
- Simple `WHERE` pushdown happens through `GitWhereNodeHelper`; keep optimization behavior aligned with tests.

## Source families
- Direct top-level sources are registered in `GitSchema.GetRowSource()`: `repository`, `tags`, `commits`, `branches`, `filehistory`, `status`, `remotes`, and `blame`.
- `repository` is the root object graph source. Most richer scenarios flow through nested bindable properties on `RepositoryEntity`, such as `Branches`, `Tags`, `Commits`, `Configuration`, and `Stashes`.
- Nested/table-valued entity members matter just as much as direct sources:
	- `RepositoryEntity.Branches`, `Tags`, `Commits`, `Configuration`, `Stashes`
	- `BranchEntity.Commits`
	- `CommitEntity.Parents`
	- `BlameHunkEntity.Lines`
- Library-driven source-like expansion lives in `GitLibrary`, especially `DifferenceBetween(...)`, `PatchBetween(...)`, `SearchForBranches(...)`, `GetBranchSpecificCommits(...)`, `FindMergeBase(...)`, `CommitFrom(...)`, `BranchFrom(...)`, and the `MinCommit` / `MaxCommit` aggregations.

## Where lazy or nested entities matter
- `RepositoryEntity` and `BranchEntity` deliberately expose nested enumerable properties with `[BindablePropertyAsTable]`. If you change those shapes, validate `cross apply` scenarios, not just direct `#git.*(...)` queries.
- `BlameHunkEntity.Lines` is the most important lazy property: it reads blob content on demand, caches the expanded `BlameLineEntity` list, and powers `cross apply h.Lines` queries.
- `CommitEntity.Parents` is another nested traversal point that should stay cheap and null-safe.
- `TagEntity.Commit` is optional because lightweight tags do not always resolve the same way as annotated tags.
- `BranchEntity.ParentBranch` is intentionally defensive and relatively expensive: it computes merge-base candidates and falls back to `main` / `master` on failure. Treat changes there as behavior-sensitive.
- `RepositoryEntity` owns a `LibGit2Sharp.Repository` and disposes it in the finalizer. Avoid introducing extra ownership ambiguity when adding nested entities.

## Simple predicate optimization
- Predicate extraction lives in `GitWhereNodeHelper.ExtractParameters()`.
- Pushdown is applied manually inside `CommitsRowsSource`, `BranchesRowsSource`, `TagsRowsSource`, `StatusRowsSource`, and `RemotesRowsSource`.
- Supported pushdown is intentionally simple:
	- equality on plain fields such as `Author`, `Sha`, `FriendlyName`, `CanonicalName`, `IsRemote`, `IsTracking`, `IsAnnotated`, `Name` / `RemoteName`, `Url`, and `State`
	- commit date comparisons on `CommittedWhen`
	- `AND` composition only
- `OR` nodes are ignored for pushdown, and non-literal expressions are not extracted. Engine-level filtering must still produce correct final results when pushdown does nothing.
- Commit date pushdown assumes the existing newest-first commit traversal. `Since` can short-circuit with `break`, so preserve ordering if you refactor `CommitsRowsSource`.
- `filehistory`, `repository`, and `blame` do not currently use simple predicate pushdown.

## Common pitfalls
- Path validation happens before source creation: queries must point at a repository root directory or a `.git` directory. Non-existent paths and non-repositories should keep failing early in `GitSchema`.
- Test and query paths often need `.Escape()` because Windows paths are passed into Musoq scripts as string literals.
- `filehistory` normalizes absolute paths back to repository-relative paths and matches either file names or full relative paths against the HEAD tree. Preserve that before touching wildcard logic.
- Negative `take` in `filehistory` means “oldest N changes”, implemented by reversing the commit history in memory. Keep tests in sync if you optimize that path.
- `BlameRowsSource` returns empty results for binary blobs and for blame operations that LibGit2Sharp cannot resolve; invalid revisions and missing files still throw.
- `StatusRowsSource` currently emits one-row chunks, unlike the 100-row batching used by most other Git row sources. Do not normalize that casually unless you validate behavior and cancellation.
- `GitWhereNodeHelper` works on raw field names, so aliasing or computed predicates should not be baked into pushdown assumptions.

## Fixture conventions
- Canonical fixtures live in `Musoq.DataSources.Git.Tests/Repositories/*.zip`.
- Most tests unzip into `%TEMP%\mqgt\<testName>` using the caller name as a stable folder key.
- Some tests pass short explicit names like `wbifrr` to keep extracted paths short on Windows.
- Reuse the existing unpack pattern from `GitToSqlTests`, `BlameTests`, and `GitWhereNodeOptimizationTests` rather than inventing a new fixture lifecycle.
- Most helper `Dispose()` implementations intentionally avoid deleting extracted repos immediately; preserve that unless you are cleaning up the whole convention across tests.

## Most representative tests
- `GitToSqlTests.cs` is the broadest integration suite:
	- repository basics and nested `Head` / `Information`
	- `cross apply` over `repository.Branches`, `repository.Tags`, `repository.Commits`, and `commit.Parents`
	- library methods like `DifferenceBetween(...)`, `SearchForBranches(...)`, `GetBranchSpecificCommits(...)`, `MinCommit(...)`, and `MaxCommit(...)`
	- direct-source coverage for `#git.commits`, `#git.branches`, `#git.filehistory`, and `#git.remotes`
- `BlameTests.cs` is the best reference for lazy nested entities, binary-file handling, revision validation, and `cross apply h.Lines`.
- `GitWhereNodeOptimizationTests.cs` is the contract for simple pushdown on commits, tags, and branches.
- `GitSchemaDescribeTests.cs` guards constructor overload counts and `desc #git` / `desc #git.repository(...)` output.

## Integrations
- `LibGit2Sharp`
- `AsyncRowsSource`

## Validate with
- `Musoq.DataSources.Git.Tests/GitToSqlTests.cs`
- `Musoq.DataSources.Git.Tests/BlameTests.cs`
- `Musoq.DataSources.Git.Tests/GitWhereNodeOptimizationTests.cs`
- `Musoq.DataSources.Git.Tests/GitSchemaDescribeTests.cs`