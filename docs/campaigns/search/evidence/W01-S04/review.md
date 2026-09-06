# W01-S04 independent review

Verdict: approved

Reviewer: separate `grill-code` review pass

## Scope reviewed

- `docs/search/search-completion-contract-v1.md`
- `docs/search/search-completion-contract-v1.json`
- `scripts/search/Test-SearchCompletionSemantics.ps1`

## Findings

No material correctness, completion-state, security-boundary, ownership-boundary
or provenance findings were found.

The contract gives each terminal outcome a distinct meaning and prevents a
limited `TAKE` result from claiming scope exhaustion. It preserves complete
zero-match answers for both empty eligible scopes and exhausted no-match
scopes, while keeping missing roots and read failures typed and incomplete.
Strict failure is the default for operational and budget failures; the
explicit partial policy can retain observed evidence only as `Partial` with
inexact counters. The exactly-once terminal summary rule also covers scans
that emit no occurrence rows.

The full-input versus observed-prefix decision is explicit. A late binary
marker in full-input mode is a visible policy-selected skip, while a marker
after observed rows cannot become silent success. The independent resolver
and corpus cover strict and opt-in partial behavior for unreadable input,
late binary markers, timeout, cancellation and output caps, plus accepted
`TAKE` and both zero-match cases. All four scenarios and 124 assertions pass.

## Residual boundaries

- This is a proposed outcome and budget contract; no Search terminal-summary
  transport or runtime outcome model is claimed to be implemented.
- Actual filesystem, cancellation, buffering, binary classification and
  output-cap enforcement require later runtime tests.
- The exact request schema and host/CLI delivery surface remain later scopes;
  this contract does not invent an engine integration.
- The full suite retains the existing 31 expected skips and existing
  NU1902/NU1903 package-audit warnings; this scope changed no product or
  package code.
- PSScriptAnalyzer is not installed; PowerShell parser validation and the
  focused completion harness passed.

## Verification

- `pwsh -NoProfile -NonInteractive -File scripts/search/Test-SearchCompletionSemantics.ps1`: exit 0; 4 scenarios; 124 assertions.
- `dotnet test --configuration Release`: exit 0; 20 project summaries; 1,284 discovered, 1,253 passed, 0 failed, 31 skipped.
- `git diff --check`: pending final staged-scope check before commit.
