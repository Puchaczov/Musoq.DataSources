# Search completion and budget contract v1

Status: contract v1 from `W01-S04`. `W12-S01` implements its terminal
accounting model and source-side summary for the file-backed text Search
sources. SQL terminal-summary transport and consumer delivery remain owned by
`W12-S02` and later scopes.

The machine-readable contract and reference corpus are in
[`search-completion-contract-v1.json`](search-completion-contract-v1.json). The
independent test is
[`Test-SearchCompletionSemantics.ps1`](../../scripts/search/Test-SearchCompletionSemantics.ps1).

## Four terminal outcomes

| Outcome | Complete | Scope exhausted | Meaning |
|---|---:|---:|---|
| `QuerySatisfied` | yes | no | An explicit positive or limited demand, such as `TAKE`, was met before the whole eligible scope was processed. |
| `ScopeExhausted` | yes | yes | Every eligible input was processed, including a scope with no eligible files or no matches. |
| `Failed` | no | no | No successful complete answer can be accepted; a typed cause is required. |
| `Partial` | no | no | Explicit partial policy preserved observed evidence, but a limit or failure prevented the complete contract. |

`ScopeExhausted` is the only outcome that supports an exhaustive negative
answer for the observed execution scope. A live-filesystem scope is not an
atomic snapshot: a source mutation during processing produces an incomplete
typed failure, and a caller requiring snapshot semantics must use immutable
input. `QuerySatisfied` may be a correct limited answer without being a full
scan. `Failed` and `Partial` are never silently converted to an empty or
successful result.

The terminal summary is authoritative and is delivered exactly once after any
observed rows, or as the first and only result when there are no rows. It
contains the outcome, stable reason, scope fingerprint, completion flags,
observed counters and failure path/code. Occurrence rows do not carry an
`IsComplete` flag: a consumer must inspect the terminal summary and reject a
strict failure or an unapproved partial prefix.

## Strict defaults and explicit partial results

The defaults are:

| Policy | Default |
|---|---|
| Failure policy | `strict` |
| Partial results | `reject` |
| Request/file validation | `full-input` |
| Binary handling | `skip-and-report` |
| Timeout | fail |
| Cancellation | fail |
| Output cap | fail |
| `TAKE` | query satisfaction |

Unexpected read errors, timeouts, cancellation and exhausted work or delivery
budgets produce `Failed` by default. An explicit `partialPolicy=allow` may
retain an observed prefix as `Partial` when at least one row was observed and
the terminal summary identifies the reason. `Partial` still has
`complete=false`, `scopeExhausted=false` and `countsExact=false`.

## Validation mode

`full-input` is the default. The complete request is validated before the root
is opened, and each candidate file's required classification is resolved before
that file's rows are accepted. A late binary marker discovered during this
mode is therefore either a visible policy skip or a failure before that file's
rows are accepted.

An explicitly selected `observed-prefix` mode may emit evidence before a
candidate file is fully validated. If a late marker or read failure then
appears, the terminal outcome is `Failed` under strict policy or `Partial`
under explicit partial policy. Already emitted evidence is never retracted
invisibly, and an unfinished candidate can never support `ScopeExhausted`.

Prefix counters describe only observed rows. They are not exact totals for the
scope. Both modes require exactly one terminal summary.

## Budgets

`TAKE` is a query limit, not a scan budget. Reaching an explicit `TAKE` yields
`QuerySatisfied` with `terminalReason=take-reached`; the scope may still have
unvisited candidates and total occurrence counts are not exact.

Output, read-byte, file-count, elapsed-time and in-flight limits are work or
delivery budgets. Exhausting one is a named failure, never a successful
truncation. The default output-cap result is `Failed` with
`failureCode=output-cap`. With `partialPolicy=allow` and an observed prefix it
is `Partial`, not `QuerySatisfied` or `ScopeExhausted`.

The implementation tracks visited, eligible, opened, completed and failed
files separately, along with skipped binary files, bytes and occurrences.
`ScopeExhausted` counters are exact for the resolved execution (without
claiming an atomic live-filesystem snapshot). `QuerySatisfied`, `Failed` and
`Partial` counters are observed-prefix measurements except that emitted-row
counts remain exact for the rows actually observed.

## Zero-match and failure outcomes

| Situation | Outcome | Reason | Interpretation |
|---|---|---|---|
| Resolved scope has no eligible files | `ScopeExhausted` | `no-eligible-files` | Complete zero result inside the declared scope. |
| Eligible files complete with no occurrence | `ScopeExhausted` | `no-match` | No occurrence in the processed eligible scope. |
| Binary files excluded by declared policy | `ScopeExhausted` | `binary-skipped-by-policy` | Complete policy-selected text scope; skipped count is visible. |
| Missing root | `Failed` | `root-missing` | Not equivalent to zero eligible files; scope was not resolved. |
| Unreadable file | `Failed` by default | `unreadable-file` | No complete answer; path and phase are retained. |
| Late binary marker after observed rows | `Failed` by default | `binary-late-marker` | Prefix is not a successful answer; explicit partial policy may yield `Partial`. |
| Timeout or cancellation | `Failed` by default | `timeout` or `cancelled` | Work did not complete; never report an empty success. |
| Output cap | `Failed` by default | `budget-exhausted` | Explicit partial policy may yield `Partial` when evidence exists. |

The three zero-match cases are intentionally different from root and read
failures. A missing or inaccessible root does not masquerade as zero files:
`scopeResolved` and `countsExact` remain false.

## Required reference cases

The JSON corpus and harness cover no eligible files, no match, missing root,
strict unreadable input, late binary markers under both validation modes,
strict timeout, strict cancellation, accepted `TAKE`, strict output cap and
explicitly accepted output-cap partial results. Each case checks the terminal
summary cardinality, outcome, completeness flags, counter exactness and prefix
acceptance.

## Boundary

This contract complements the source row contract and the W01-S02 match
semantics. It does not add a second query language or promise host/CLI summary
delivery. Runtime accounting and cancellation are implemented at the source
boundary; end-to-end terminal transport remains later implementation work.

Source references: `S14`, `S15`, `S20`, `S22`, `S23`.
