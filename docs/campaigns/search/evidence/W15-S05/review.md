# W15-S05 review

Review method: separate grill-code review pass (no independent subagent was available).

## Verdict

Approved for the repository-owned regression catalog and holdout-freeze
artifacts. Empirical first-success, repair and token targets remain an
explicit release blocker because no fresh-agent runner is available in this
checkout.

## Findings

- The friction catalog contains twelve focused regressions over occurrence
  cardinality, metadata-only paths, escaping, wildcard dialects, unsupported
  semantics, unreadable/budget-limited/cancelled scans, residual ordering and
  untrusted text. Every entry references a real A01-A40 first attempt and is
  marked `observed: false`; the catalog has no fabricated observations.
- The holdout freeze pins the benchmark revision and neutral protocol bytes,
  lists every A01-A40 task, commits a 64-character seed hash and keeps seed
  material, answers, validator source and result records outside this
  repository. It requires fresh variants, fresh contexts and three trials per
  task.
- The focused tests validate task references, regression identity, empty
  observations, exact holdout task coverage, answer absence, seed commitment
  shape and pinned source hashes. The freeze therefore protects against
  accidentally converting development fixtures into holdout evidence.
- No production Search source or runtime semantics changed. The scope only
  adds regression/freeze contracts and their owning tests/documentation.

## Verification

- Friction/holdout focused tests: 3 passed, 0 failed, 0 skipped.
- Full Search suite: 362 discovered, 359 passed, 0 failed, 3 skipped.
- Search Release build with warnings as errors: 0 warnings, 0 errors.
- Five registered Search contract harnesses: 27 scenarios and 314 assertions,
  all passed.
- Owning repository Release suite: 1,650 discovered, 1,616 passed, 0 failed,
  34 expected skips, exit code 0.

## Residuals and release blocker

No fresh-agent runner is available here. Consequently, the required fresh
task-subset trials, observed-friction measurements, target first-success and
repair rates, token comparisons and empirical confidence intervals remain
unexecuted and are not claimed. The holdout is frozen as a boundary contract
only; an external runner must materialize variants and retain independent
traces before those targets can be assessed. Platform-specific unreadable and
invalid-encoding cases likewise require platform evidence. No package was
published, pushed, released or installed, and no sibling repository was edited.
