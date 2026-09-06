Review method: separate grill-code review pass (no independent subagent was available).

# W12-S03 review

## Scope reviewed

- `Musoq.DataSources.Search/SearchResourceLimits.cs`
- `Musoq.DataSources.Search/SearchContracts.cs`
- `Musoq.DataSources.Search/SearchDiagnostics.cs`
- `Musoq.DataSources.Search/SearchExecution.cs`
- `Musoq.DataSources.Search/SearchRegexBackend.cs`
- `Musoq.DataSources.Search/SearchRegexScanner.cs`
- `Musoq.DataSources.Search/SearchTextScanner.cs`
- `Musoq.DataSources.Search/SearchTextSinks.cs`
- `Musoq.DataSources.Search/SearchTextSourceBase.cs`
- `Musoq.DataSources.Search/SearchManyRequest.cs`
- `Musoq.DataSources.Search/SearchManyLiteralScan.cs`
- `Musoq.DataSources.Search/SearchManySource.cs`
- `Musoq.DataSources.Search/SearchMatchesSource.cs`
- `Musoq.DataSources.Search/SearchLinesSource.cs`
- `Musoq.DataSources.Search/SearchFilesSource.cs`
- `Musoq.DataSources.Search/SearchCountsSource.cs`
- `Musoq.DataSources.Search.Tests/SearchResourceLimitTests.cs`
- `docs/search/search-resource-budgets-v1.md`

## Findings

No blocking correctness, lifecycle, concurrency, contract, security or
ownership findings remain.

The implementation keeps work budgets separate from query `TAKE` semantics.
File-count, file-byte, total-byte, match-count and staged-output accounting is
shared per execution; the finite counters use atomic reservation so the
parallel Search path cannot independently exceed a limit. Pattern, compile,
record and context limits are validated or enforced with stable budget codes.
The regex physical-line path counts custom lower record limits in decoded bytes,
including a non-ASCII regression case, while the existing default hot path keeps
its established ceiling without an extra per-character budget object.

Result amplification is accounted for before rows enter sink staging. Context
matches reserve a conservative pending-row estimate and release it when the
context is materialized or when the sink is disposed. Reader, pooled-buffer,
sink, pending-context and linked-cancellation resources remain inside `using`
or `finally` boundaries. A budget exception is never converted into a
successful no-match result: strict requests fail, while the explicit internal
partial policy reports an incomplete `Partial` outcome only after observed
rows exist.

The scope's earlier hot-path review concern was corrected: unlimited defaults
do not perform per-match atomic accounting, while finite output and match
limits are shared across worker sinks. Dense matches, each budget family,
cleanup, explicit partial behavior and regex byte counting are covered by the
focused tests.

## Residual boundaries

- File and total-byte budgets reserve `FileInfo.Length` before content
  processing. This is conservative for ordinary immutable files and provides a
  stable pre-read failure boundary; a concurrently mutated file is outside this
  scope's synchronization contract.
- The finite output budget covers rows and pending context retained by Search
  staging before writer handoff. The existing bounded per-file output channels
  remain the downstream backpressure boundary; this scope does not redesign the
  generic host writer contract to attach byte reservations to consumer
  acknowledgement.
- Regex compilation cost measures an actual cache miss. An exact cached pattern
  is reused without recompilation, so no compilation work is charged for that
  invocation.
- The internal request/options seam is tested directly. Public SQL argument
  binding for these new limits remains a later contract boundary.

## Verification

- warnings-as-errors Search build: exit 0; 0 warnings; 0 errors;
- resource-limit focused tests: 7 passed, 0 failed, 0 skipped;
- complete Search test project: 294 discovered, 291 passed, 0 failed, 3
  existing platform-conditional skips;
- source contract harness: exit 0; 7 scenarios; 52 assertions;
- completion semantics harness: exit 0; 4 scenarios; 124 assertions;
- request contract harness: exit 0; 4 scenarios; 45 assertions;
- match semantics harness: exit 0; 6 scenarios; 49 assertions;
- scope semantics harness: exit 0; 6 scenarios; 44 assertions;
- exact owning-repository Release suite: exit 0; 21 projects; 1,578
  discovered, 1,544 passed, 0 failed, 34 classified skips;
- separate review verdict: approved.
