# W16-S04 review

Review method: separate grill-code review pass (no independent subagent was available).

## Verdict

Approved. The scope provides an executable, deterministic stress harness and
contract tests for repeated scans, growing match sets, retained evidence,
cache pressure, cancellation, resource limits and concurrent user isolation.
The resource conclusion is appropriately limited to an in-process observation
on the recorded host.

## Findings

- The fixture identity is deterministic and pinned. Repeated scans retain one
  normalized result hash across 20 trials, while explicit-file growth cases
  verify exact row counts of 1, 8, 64 and 256.
- Retained evidence materializes context on every trial and records source
  cleanup. The terminal summaries distinguish successful scope exhaustion
  from cancelled and budget-exhausted work.
- Regex cache pressure reaches the declared 128-entry bound, observes
  eviction and recompilation, serializes a shared-key compilation, and
  returns to an empty cache after reset.
- Cancellation is exercised mid-read eight times. Each attempt reports the
  typed cancelled failure state and a disposed reader. The match-count budget
  path reports `SearchResourceLimitException` with `match-count` and also
  disposes its reader.
- Eight concurrent user contexts run four rounds with distinct patterns and
  scope fingerprints. The assertions reject excluded paths, another user's
  pattern, or cross-context result contamination.
- `SearchFileParallelCoordinator` composes completion waits through task
  continuations and preserves cancellation propagation, avoiding the
  synchronous wait-handle path while retaining worker cleanup semantics.
- The Release benchmark build completed with 0 warnings and 0 errors. The
  three focused contract tests passed 3/3, the Search suite passed 371/374
  with 3 existing platform-conditional skips, and the owning 21-project
  suite passed 1,628/1,662 with 0 failures and 34 classified skips.
- The raw JSON, empty stderr, benchmark build log, Search log, full-suite log,
  fixture digest and source hashes are retained as evidence.

## Resource boundary and residuals

After forced collection, the observed process handle count was 390 versus a
baseline of 385, within the declared slack of eight. Managed heap, working
set and private memory are reported for diagnosis but are not treated as
portable limits. The concurrent worker shape is warmed before the baseline
because runtime/thread-pool handles are included in the process count; this
keeps the observation from confusing first-use worker activation with a
Search-owned leak.

This is in-process datasource qualification. Long-lived service behavior,
cross-process cleanup and production-load limits remain external residuals.
No package was published, pushed, released or installed, and no sibling
repository was edited.
