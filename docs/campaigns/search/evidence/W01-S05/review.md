# W01-S05 independent review

Verdict: approved

Reviewer: separate `grill-code` review pass

## Scope reviewed

- `docs/search/search-request-contract-v1.md`
- `docs/search/search-request-contract-v1.json`
- `scripts/search/Test-SearchRequestContract.ps1`

## Findings

No material correctness, validation-order, ownership-boundary or provenance
findings were found.

The request is bounded and versioned, uses an array of labeled patterns rather
than a lossy SQL map, and keeps scalar JSON transport as the explicit
compatibility shape until Runtime-v2 metadata proves collection binding. The
closed schema rejects unknown properties and invalid enums, while independent
application validation rejects duplicate JSON names—including escaped names
that decode to the same property—and duplicate pattern IDs before filesystem
access. The proposed named and positional examples preserve the W01-S01 row
cardinality and equal-text pattern-label identity.

The independent harness covers schema/transport identity, validation ordering,
all six rejection cases and all three acceptance examples. It passes four
scenarios and 45 assertions, including the escaped duplicate-key case.

## Residual boundaries

- This is a proposed request contract; actual source metadata, constructor
  binding, static XML, compiled SQL and filesystem access remain W02 work.
- The PowerShell validator is a reference guard, not the runtime JSON binder;
  runtime validation must preserve duplicate-key detection and reject invalid
  requests before opening roots.
- The exact SQL parameter names and overload metadata are intentionally not
  asserted as installed capabilities by this scope.
- The full suite retains the existing 31 expected skips and existing
  NU1902/NU1903 package-audit warnings; this scope changed no product or
  package code.
- PSScriptAnalyzer is not installed; PowerShell parser validation and the
  focused request harness passed.

## Verification

- `pwsh -NoProfile -NonInteractive -File scripts/search/Test-SearchRequestContract.ps1`: exit 0; 4 scenarios; 45 assertions.
- `dotnet test --configuration Release`: exit 0; 20 project summaries; 1,284 discovered, 1,253 passed, 0 failed, 31 skipped.
- `git diff --check`: pending final staged-scope check before commit.
