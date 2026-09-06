# W01-S03 independent review

Verdict: approved

Reviewer: separate `grill-code` review pass

## Scope reviewed

- `docs/search/search-scope-contract-v1.md`
- `docs/search/search-scope-contract-v1.json`
- `scripts/search/Test-SearchScopeSemantics.ps1`

## Findings

No material correctness, security-boundary, ownership-boundary or provenance
findings were found.

The proposal keeps lexical containment ahead of convenience filters and
requires physical containment when links are followed. It makes root
resolution, traversal, include/exclude ordering, ignore precedence, hidden
entries, link cycles, hard-link path identity and inaccessible-entry outcomes
explicit. The fingerprint includes the effective policy, loaded ignore hashes
and a sorted candidate decision manifest, so a result can be explained and
reproduced without treating SQL result order as traversal order.

The independent table-driven harness covers nested ignore negation, ignored
parent pruning, lexical versus physical containment, case comparison,
hard-link path identity, descendant traversal under extension filters and
inaccessible-entry policy. It passes all six scenarios and 44 assertions.

## Residual boundaries

- This is a proposed scope contract; no Search filesystem traversal is claimed
  to be installed or implemented by this scope.
- Actual filesystem-native case detection, physical link resolution, file-ID
  behavior and access-error diagnostics require runtime tests in later scopes.
- The exact request schema, completion/failure surface and typed result rows
  remain later W01/W02 work.
- The full suite retains the existing 31 expected skips and existing
  NU1902/NU1903 package-audit warnings; this scope changed no product or
  package code.
- PSScriptAnalyzer is not installed; PowerShell parser validation and the
  focused scope harness passed.

## Verification

- `pwsh -NoProfile -NonInteractive -File scripts/search/Test-SearchScopeSemantics.ps1`: exit 0; 6 scenarios; 44 assertions.
- `dotnet test --configuration Release`: exit 0; 20 project summaries; 1,284 discovered, 1,253 passed, 0 failed, 31 skipped.
- `git diff --check`: pending final staged-scope check before commit.
