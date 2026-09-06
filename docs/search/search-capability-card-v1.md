# Search capability card v1

Status: implemented and verified for `W15-S01`.

Use this card to choose the narrowest Search source for a bounded local
file/directory query. The `search` schema must be installed in the host; this
card does not claim that every Musoq deployment exposes every optional
source. The generated Search XML and runtime source descriptions are the
authority for the current signatures and columns.

## Decision matrix

| Need | Source | Shape and result unit | Cost/completeness boundary | Optional when |
|---|---|---|---|---|
| Names of eligible files | `search.paths(root)` | One `Path`/`EntryKind` row per eligible regular file | Metadata traversal only; does not open content | The query only needs identity or a manifest |
| Positive file existence | `search.files(root, literal)` | One row per eligible file containing the literal | Reads content; empty output is only “no positive match” for the scanned candidate set | A literal predicate defines the desired file set |
| Physical lines/records | `search.lines(root, literal)` | One row per matching physical line with `LineText` and `OccurrenceCount` | Reads matching lines; literal is a candidate prefilter, not universal record coverage | The next step parses or audits matching lines |
| Match locations | `search.matches(root, literal)` | One row per non-overlapping occurrence with line and UTF-16 coordinates | Reads content; byte coordinates are present only when losslessly mapped | A report needs exact occurrence locations or snippets |
| Labeled multi-pattern findings | `search.many(root, requestJson)` | One row per `(Path, PatternId, MatchIndex)` from a bounded scalar JSON request | Reads content once per request; current request contract is literal-only and scalar | Several related literal patterns need stable labels |
| Exact totals | `search.counts(root, literal)` | One row per eligible completed file, including zero-hit files | Full eligible-file content scan; `Complete` marks exact completion | A negative or zero result must include zero-hit files |
| Raw binary signatures/windows | `search.bytes(root, patternJson)` | One row per raw-byte occurrence with optional bounded bytes/window | Raw-byte scan; JSON pattern and window bounds are explicit; no text or UTF-16 inference | Text decoding would be unsafe or a byte window is required |
| Terminal completeness evidence | `search.audit(root, literal)` | One fresh terminal summary with outcome, scope and exactness flags | Executes a count scan and reports whether scope/counters are complete | A report must justify a negative or partial result |

## Selection rules

- Start with `paths` for names and manifests, `files` for positive existence,
  `lines` for record interpretation, `matches` for locations, `counts` for
  totals and zero-hit files, and `bytes` for binary input.
- Use `many` only when labeled literal patterns are needed. Its request is a
  versioned scalar JSON value, not a table or an implicit regex dialect.
- Keep Search provenance and coordinates beside derived values. A `lines`
  row is one physical line; `OccurrenceCount` is multiplicity and must not be
  mistaken for repeated parser input.
- An empty `matches`, `files` or `lines` result is not by itself an exhaustive
  negative answer. Use `counts` and inspect `audit` when completeness matters;
  require `Complete`, `ScopeExhausted` and `CountsExact` before making a
  scope-wide absence claim.
- `matches` and `many` expose decoded UTF-16 positions. `bytes` exposes raw
  byte offsets/windows and intentionally leaves text coordinates null.

## Dialect and failure costs

All eight sources use the two-argument scalar-root convention except
`search.paths(root)`. `many` and `bytes` require versioned JSON request
strings; malformed or duplicate request properties are rejected. Text
interpretation is a separate query composition step: use `Parse` for strict
failure, `TryParse` with `OUTER APPLY` to preserve malformed candidates, and
`PartialParse` when diagnostic fields are needed without projecting its
dictionary as a query result.

Scope filters, encoding/case policy, context windows, record framing and
resource limits affect scan cost and evidence status. They are optional only
when the query does not need the corresponding behavior; they do not broaden
the source's completeness contract. A failed or partial scan remains typed
failure evidence rather than a successful empty result.

Source snapshot: the card names the eight `SearchSchema` virtual constructors
and their current runtime descriptions. Related contracts:
[`search-source-contract-v1.md`](search-source-contract-v1.md),
[`search-request-contract-v1.md`](search-request-contract-v1.md),
[`search-completion-contract-v1.md`](search-completion-contract-v1.md), and
[`search-coordinate-policy-v1.md`](search-coordinate-policy-v1.md).
