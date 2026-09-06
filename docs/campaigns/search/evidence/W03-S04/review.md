# W03-S04 independent review

Verdict: approved

Reviewer: separate `grill-code` review pass (no independent subagent was
available)

## Scope reviewed

- `Musoq.DataSources.Search.Benchmarks/ManagedSearchSpike.cs`
- `Musoq.DataSources.Search.Benchmarks/RipgrepSearchSpike.cs`
- `Musoq.DataSources.Search.Benchmarks/Program.cs`
- `Musoq.DataSources.Search.Benchmarks/native/rust-prototype/`
- `Musoq.DataSources.Search.Tests/SearchFeasibilityTests.cs`
- solution/project wiring and W03-S04 evidence artifacts

## Findings

No material correctness, ownership, process-lifecycle, or evidence-integrity
finding remains for this bounded feasibility scope.

The managed candidate uses streamed strict UTF-8 decoding and
`SearchValues<char>` candidate search with ordinal, non-overlapping literal
matching. Traversal is deterministic and the relative path is computed once
per file. Result rows are canonically sorted, serialized and hashed before a
measurement is accepted.

The ripgrep candidate uses the pinned executable with shell-free
`ProcessStartInfo.ArgumentList`, one process for the multi-pattern workload,
JSON output, and separate stdout/stderr draining. Exit 0 and exit 1 remain
distinct. Cancellation starts after both drains are active, kills the process
tree, waits for cleanup, and raises `OperationCanceledException` instead of
returning partial success.

The benchmark surrounds traversal, file open/read, UTF-8 decode, matching,
process/bridge setup, JSON parsing and canonical output hashing. It records
seven measured trials per candidate/workload/phase, deterministic paired order,
raw timings, allocation deltas, working-set observations, bytes, row counts,
output hashes and tool metadata. The filesystem cache is deliberately labeled
`unknown`; no privileged cache flush or unsupported cold-cache claim is made.

The independent corpus oracle and focused tests agree for 3,971 occurrences
over 65 files and 644,463 bytes. The absent-literal workload returns no rows;
the ripgrep no-match result retains exit code 1. Both candidates observe
cancellation without returning a result.

## Decision and residual boundaries

For the currently implemented exact UTF-8 literal dialect, the provisional
backend decision is the managed `Span`/`SearchValues` candidate. On this small
fixture it is faster than the process/JSON baseline, but it allocates lines and
result rows; this is a feasibility result, not the W05 production scanner or a
G05 parity claim.

The Rust `RegexSet`/regex prototype is source-only in this run. `rustc`,
`cargo`, and normal native compiler locations were absent from the locked
Windows x64 host, and no toolchain was installed implicitly. Consequently no
Rust timing, compiled correctness result, or Rust licensing audit is claimed.
The portable regex dialect remains deferred to W07, where its production
selection can be measured with its own semantics.

ARM64 was not evaluated or emulated because the host process and OS are x64.
Ripgrep is retained as a pinned external baseline/prototype, not selected as a
production dependency. The benchmark does not claim controlled cold-cache
results, clean-host behavior, large-corpus parity, or final allocation parity.

## Verification

- `dotnet test Musoq.DataSources.Search.Tests\\Musoq.DataSources.Search.Tests.csproj --configuration Release --no-restore`: exit 0; 45 passed, 0 failed, 0 skipped.
- `dotnet test --configuration Release`: exit 0; 21 test projects; 1,329 discovered, 1,298 passed, 0 failed, 31 expected skips.
- `dotnet build Musoq.DataSources.Search.Benchmarks\\Musoq.DataSources.Search.Benchmarks.csproj --configuration Release --no-restore`: exit 0; 0 warnings, 0 errors.
- benchmark `verify`: exit 0; managed and ripgrep output hashes matched on the multi-pattern and absent-literal vectors; both cancellation observations were true.
- benchmark `measure`: exit 0; four cells, 56 measured samples, seven trials per candidate in every cell.
- `git diff --check`: passed.
