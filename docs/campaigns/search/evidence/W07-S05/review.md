# W07-S05 review

## Verdict

Approved for completion.

Reviewer: separate `grill-code` review pass (no independent subagent was available).

## Scope checked

- The selected regex profile remains .NET `RegexOptions.NonBacktracking |
  RegexOptions.CultureInvariant`, with the existing one-second timeout and
  bounded pattern/record/cache limits. No backtracking profile or implicit
  fallback was introduced.
- The adversarial correctness test uses a nested alternation that is
  pathological for a backtracking implementation and verifies that the safe
  profile completes with the correct no-match result.
- A direct scanner timeout test uses a deliberately shorter test-only timeout
  to exercise the production timeout translation path. The result is a typed
  resource diagnostic with the original timeout exception retained.
- Cancellation is checked between emitted matches. The dense-output test
  cancels after 512 accepted matches and verifies that no later partial scan
  completes.
- Cache pressure remains bounded by the existing 128-entry LRU. The new test
  verifies that eviction and reset release compiled instances from cache
  ownership; `Regex` has no disposable API to invoke.
- The benchmark constructs independent expected match/capture hashes and
  records seven cold-compilation plus seven warm-cache trials for pathological
  and dense-capture workloads. All trials completed below the declared
  one-second median safety observation and preserved their expected hashes.
- The compatibility table explicitly distinguishes the selected safe regex
  profile, the unavailable backtracking profile and the separate literal
  mode.

## Findings and disposition

No material correctness, semantic, diagnostic, compatibility, resource,
performance-report or scope-boundary finding remains open.

The timeout test intentionally uses a sub-millisecond budget because the
production one-second budget is not suitable for a deterministic unit-test
trigger. The benchmark and existing backend option test retain coverage of
the production timeout value; no production timeout was changed.

## Boundary

Only Search assembly metadata, benchmark-owned regex measurement code,
Search adversarial tests, Search regex documentation and W07-S05 evidence
were changed. No sibling repository, release, publication, installation or
unrelated campaign state was staged.
