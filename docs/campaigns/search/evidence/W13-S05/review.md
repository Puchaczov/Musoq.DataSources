# W13-S05 review

Review method: separate grill-code review pass (no independent subagent was available).

Verdict: approved

## Scope reviewed

This scope adds a deterministic byte-pipeline benchmark and correctness
verification for exact signatures, masked signatures, and bounded
match-to-parse work. The review covered declared result units, oracle
independence, dense false positives, large-file coverage, optional-window
semantics, timing boundaries, output hashes, temporary-fixture lifetime,
allocation reporting, and the distinction from ripgrep parity.

## Findings

- No blocking correctness, ownership, resource-isolation, or provenance
  findings were identified.
- Exact and masked cells use the actual compiled `SearchBytePattern` and
  `SearchByteScanner` path. A separate byte matcher computes non-overlapping
  expected offsets and normalized hashes before any measured result is
  accepted.
- The fixture is two deterministic 2 MiB files with dense `CA` candidates,
  exact `CA FE` candidates, malformed records, valid records, and a truncated
  tail. The measured output proves that masking expands candidate cardinality
  and that invalid candidates are not silently counted as parsed records.
- The match-to-parse cell materializes only the requested bounded window,
  records complete versus clipped windows, and separates candidate,
  successful-parse, and parse-failure counts. The first two cells omit the
  window and verify that no window values are produced.
- Every measured cell has one recorded warmup and seven complete trials. Raw
  duration, allocation, working-set, count, terminal-state, and normalized
  hash data are retained in the scope report. Filesystem cache state is
  explicitly `unknown`.
- The parser used for the timing cell is a benchmark-local bounded fixture
  parser. It is intentionally not presented as compiled Musoq `Interpret`,
  service, or client timing; W13-S04 separately qualifies the SQL composition.
- Each run owns a unique temporary fixture directory and disposes it after
  verification or measurement. No mutable cross-query or process-global
  benchmark state is introduced.

## Residual boundaries

- This scope records structured binary observations, not a Search-versus-`rg`
  parity claim. Byte occurrences and parsed records are not compared with
  ripgrep matching lines.
- The benchmark is an in-process Search scanner plus a local bounded parser;
  datasource chunk, compiled-query, service, and client layers remain outside
  this timing boundary.
- The benchmark does not promote the frozen parity or latency thresholds; it
  records raw observations for the registered binary workflows.

## Verification

- Byte-pipeline correctness test: 1 passed, 0 skipped, 0 failed.
- Byte-pipeline verifier: 4 MiB eligible input, 12,483 exact candidates,
  73,716 masked candidates, 8,048 parsed records, 65,668 parse failures, and
  both complete and incomplete windows.
- Byte-pipeline measurement: three cells, one warmup and seven complete
  trials per cell; all output hashes and terminal states matched the oracle.
- Search Release suite: 334 total, 331 passed, 3 classified platform/reparse
  skips, 0 failed.
- Search and benchmark Release builds with warnings-as-errors: 0 warnings,
  0 errors.
- Contract harnesses: 27 scenarios and 314 assertions, all passed.
- Owning-repository Release suite: 1,618 total, 1,584 passed, 34 classified
  skips, 0 failed, exit code 0.
- `git diff --check` passed after the implementation and documentation
  changes.

## Known non-blocking baseline warnings

The repository-wide Release command retains existing package-vulnerability,
generated-code, and compiler warnings outside this scope. They do not change
the byte benchmark implementation or its correctness gate.
