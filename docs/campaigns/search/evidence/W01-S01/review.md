# W01-S01 independent review

Verdict: approved

Reviewer: separate `grill-code` review pass

## Scope reviewed

- `docs/search/search-source-contract-v1.md`
- `docs/search/search-source-contract-v1.json`
- `scripts/search/Test-SearchSourceContract.ps1`

## Findings

No material correctness, ownership-boundary or provenance findings were found.

The contract keeps occurrence, line, file, count, path, byte-occurrence and
multi-pattern rows as explicit different relational units. It states the
nullable/default behavior for `SELECT *`, keeps original-byte and UTF-16
coordinates distinct, retains zero-hit files only in the complete count source,
and requires fresh audit execution rather than global last-run state. The
independent harness verifies projection multiplicity, two hits on one line,
zero-hit files, equal text under distinct pattern IDs and fresh audit identity.

The initial focused run exposed and fixed only a PowerShell empty-string test
fixture binding issue. The harness now passes its empty-file case and reads the
golden expected cardinalities from the machine-readable contract.

## Residual boundaries

- This scope deliberately defines a proposed contract; no Search datasource or
  `search.*` method is claimed to be installed or executable.
- Exact overlap/regex semantics, scope/ignore behavior, terminal completion
  rules and request validation remain owned by subsequent W01 scopes.
- `Origin`, byte/text mapping and optional capture/context fields are specified
  for future implementation but are not runtime proof in this documentation
  scope.
- The full suite retains the existing 31 skips and existing NU1902/NU1903
  package-audit warnings; this scope changed no product or package code.
- PSScriptAnalyzer is not installed; PowerShell parser validation and the
  focused contract harness passed.

## Verification

- `pwsh -NoProfile -NonInteractive -File scripts/search/Test-SearchSourceContract.ps1`: exit 0; 7 scenarios; 52 assertions.
- `dotnet test --configuration Release`: exit 0; 20 project summaries; 1,284 discovered, 1,253 passed, 0 failed, 31 skipped.
- `git diff --check`: pending final staged-scope check before commit.
