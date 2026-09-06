# Search diagnostic repair catalog v1

Status: implemented and verified for `W15-S02`.

This catalog is the repair boundary for the current Search schema. It keeps
the original query, request JSON and raw text as data, identifies the earliest
boundary that rejected them, and proposes one smallest repair. It does not
invent a Search method, silently change a pattern dialect, repeat broad
discovery, or turn an incomplete scan into a negative answer.

## Repair protocol

1. Preserve the original source name, alias, argument text, request JSON and
   raw pattern. Record the exact diagnostic code, phase and location before
   editing anything.
2. Repair the smallest layer named by the diagnostic: query binding, source
   arguments, JSON syntax, pattern dialect, transport escaping, filesystem
   access or resource budget.
3. Use only the eight registered source names and the constructor signatures
   shown in the capability card. Do not respond to an unknown name by trying
   `search.find`, `search.scan`, `search.discover` or another unregistered
   method.
4. Re-run the same bounded scope. A source-access, mutation, output or budget
   failure is typed failure evidence, not an empty successful result.
5. Claim scope-wide absence only after a complete `search.counts`/`search.audit`
   result says that the eligible scope was exhausted and its counters are
   exact.

## Stable Search diagnostic catalog

Every code below is emitted by the Search implementation and is checked
against `SearchDiagnosticCodes.All`. The argument/location is part of the
repair signal; the message alone is not a machine contract.

| Code | Phase | Relevant argument or location | Why it fires | Small repair | Documentation heading |
|---|---|---|---|---|---|
| `SEARCH-SYNTAX-001` | `Syntax` | `pattern` | A regex is malformed or uses a construct outside the portable non-backtracking dialect. | Correct the pattern for the declared dialect or select literal mode; do not silently switch dialects. | [`Pattern mode and regex dialect`](#pattern-mode-and-regex-dialect) |
| `SEARCH-SYNTAX-002` | `Syntax` | `recordFraming` | A bounded multiline record delimiter is malformed. | Close the delimiter pair or explicitly allow an unterminated EOF record. | [`Record framing`](#record-framing) |
| `SEARCH-SYNTAX-003` | `Syntax` or `Argument` | `pattern` / `patternJson` | A byte-pattern request has invalid JSON, hex, mask, version or cross-field shape. | Use version 1 JSON with an even-length hex `bytes` string and an optional same-length hex `mask`. | [`Request JSON and byte patterns`](#request-json-and-byte-patterns) |
| `SEARCH-ARGUMENT-001` | `Argument` | `arguments`, `root`, `literal`, `request`, `patternJson`, `case`, `encoding` or `recordFraming` | A source has the wrong arity/type or an option has an unsupported value. | Match the reflected constructor exactly and use a documented scalar value; do not add an unreflected option. | [`Source arguments, aliases and names`](#source-arguments-aliases-and-names) |
| `SEARCH-SOURCE-001` | `SourceAccess` | `root` / path | The requested file or directory does not exist, so no complete scope can be established. | Correct the path or create/restore the intended fixture, then retry. | [`Scope, paths and completion`](#scope-paths-and-completion) |
| `SEARCH-SOURCE-002` | `SourceAccess` | failing file `path` | An eligible input could not be opened. | Check permissions and availability; retry only after the input is readable. | [`Scope, paths and completion`](#scope-paths-and-completion) |
| `SEARCH-SOURCE-003` | `SourceAccess` | failing file `path` | An opened input could not be read or decoded completely. | Check the file and declared encoding, then retry from the beginning. | [`Scope, paths and completion`](#scope-paths-and-completion) |
| `SEARCH-SOURCE-004` | `SourceAccess` | `encoding` | The encoding is unsupported or disagrees with the input BOM. | Use `auto`, a declared UTF-8/UTF-16 mode, or raw `bytes` when text decoding is unsafe. | [`Transport escapes and raw text`](#transport-escapes-and-raw-text) |
| `SEARCH-SOURCE-005` | `SourceAccess` | changed file `path` | The source changed, disappeared or was replaced while it was being read. | Retry against stable/immutable input; retain the failed run as incomplete. | [`Scope, paths and completion`](#scope-paths-and-completion) |
| `SEARCH-RESOURCE-001` | `Resource` | `requestJson`, `pattern`, `recordBytes`, scope lists, file/read/match/output budgets | A declared safety limit was reached or exceeded. | Reduce the named value or split the work into bounded searches; never pretend a capped scan is complete. | [`Scope, paths and completion`](#scope-paths-and-completion) |
| `SEARCH-OUTPUT-001` | `Output` | result consumer boundary | Search could not publish trustworthy rows to the host result consumer. | Retry and inspect the host consumer; do not salvage a partial row stream as a complete answer. | [`Scope, paths and completion`](#scope-paths-and-completion) |

`SEARCH-ARGUMENT-001` is intentionally reused for several argument-boundary
failures. Read `Diagnostic.Location.ArgumentName` (and `Offset` for request
JSON) before choosing the repair. Search does not fabricate reserved `MQ*`
codes for these plugin-owned failures.

## Source arguments, aliases and names

The current reflected surface is deliberately small:

| Source | Exact arguments | Result unit |
|---|---|---|
| `search.paths` | `(root)` | eligible path |
| `search.matches`, `search.lines`, `search.files`, `search.counts`, `search.audit` | `(root, literal)` | occurrence, physical line, positive file, exact count, or terminal summary |
| `search.many` | `(root, requestJson)` | labeled literal occurrence |
| `search.bytes` | `(root, patternJson)` | raw-byte occurrence/window |

An unknown source name is a host/schema boundary error such as
`SourceNotFoundException`, not a new `SEARCH-*` code. An unknown output column
is a planner/binding diagnostic such as `UnsupportedRequiredColumn`, not an
invitation to invent a column or method. An alias is query-local: if the
query uses `m.Path`, bind the source as `... m` and keep the alias stable in
the projection. Repair to a name present in metadata, never to a guessed
`search.find` or `search.discover` call.

## Pattern mode and regex dialect

Literal mode is the safe default for punctuation and ordinary text. A glob is
not a Search pattern mode. For regex, use the documented portable
non-backtracking dialect. A backreference, unsupported anchor or malformed
class must remain an explicit syntax failure; replacing it with a weaker
pattern without telling the caller conceals a semantic change.

For `search.many`, `mode` must be a supported request value and the current
source contract is literal-only. If a repair needs regex, use a source and
request shape that actually exposes regex semantics; do not change a
`search.many` literal request into an unregistered `search.regex` method.

## Record framing

Multiline regex records have explicit delimiters and byte limits. A missing
end delimiter, oversized record or ambiguous EOF policy is a framing/resource
failure. Repair the framing declaration or choose physical-line scope. Keep
the original record text available when a tolerant composition is intended;
strict parsing should fail only when strict failure is the declared intent.

## Request JSON and byte patterns

`search.many` and `search.bytes` take one scalar JSON string. Unknown
properties, duplicate properties, invalid modes, duplicate pattern IDs,
invalid bounds and malformed JSON are rejected at the request boundary. A
byte request uses explicit hex text, not an SQL numeric literal such as
`0x54` or `84`; JSON and SQL escaping are transport layers, not pattern
rewrites.

## SQL LIKE versus filesystem globs

The two wildcard languages have different owners:

| Intent | Correct language | Example |
|---|---|---|
| Filter a projected path with SQL | SQL `LIKE` | `p.Path like '%.cs'` |
| Select files in a Search scope | scope glob in the versioned request | `"include":["*.cs"]` |

`*.cs` passed to SQL `LIKE` is not the same as `%.cs`; `%` and `_` are SQL
wildcards, while `*` and `?` belong to the filesystem-glob policy. Repair only
the layer that was mutated and do not move a user pattern between languages.

## Transport escapes and raw text

Keep raw user intent separate from transport syntax. For a logical Windows
path `C:\logs\build`, a SQL text literal carries doubled backslashes:
`'C:\\logs\\build'`. A host-language string that constructs that query may
need one additional source-language layer. The same rule applies to a JSON
request embedded in SQL: escape JSON first, then escape the SQL literal.

For a regex word boundary, the logical pattern is `\bTODO\b`; preserve those
backslashes through JSON/SQL transport instead of turning `\b` into `b` or a
backspace. Repair quoting and escaping at the layer named by the failure, not
the user's actual path or pattern.

## Case selection

Search defaults to case-sensitive matching. `sensitive` and `insensitive` are
the supported case values where the request shape exposes case policy; an
invented value such as `unicode` is an argument error. If the simple source
does not expose an option, select a request shape that does or preserve exact
case. Do not silently fold the user's literal and report a different search.

## Mutation repair table

These are deliberately small mutations of valid Search work. The repaired
form keeps the intended root, result unit and pattern; the proof is a focused
runtime or transport assertion, not a second broad discovery pass.

| Case | Mutation | Observed code or signal | Minimal repair | Proof / heading |
|---|---|---|---|---|
| `A08` | A Windows path is inserted as a single-layer SQL literal. | `SEARCH-SOURCE-001` after a wrongly resolved root, or the host parser before Search is called. | Escape the SQL transport layer while retaining the logical path. | [`Transport escapes and raw text`](#transport-escapes-and-raw-text) |
| `A09` | `\b` is written through an ordinary string layer and becomes a backspace or loses its slash. | `SEARCH-SYNTAX-001` or a semantic mismatch in the regex result. | Preserve logical `\b...\b` and escape once per JSON/SQL layer. | [`Pattern mode and regex dialect`](#pattern-mode-and-regex-dialect) |
| `A10` | A reflected source/argument/column name is misspelled, or an alias is referenced before it is bound. | `SourceNotFoundException`, `UnsupportedRequiredColumn`, or the host compiler's actual binding code; no new `SEARCH-*` code is fabricated. | Use the name and alias present in metadata; keep the result unit unchanged. | [`Source arguments, aliases and names`](#source-arguments-aliases-and-names) |
| `A11` | A required root, literal or request argument is omitted. | `SEARCH-ARGUMENT-001` at `arguments`, `root`, `literal`, `request` or `patternJson`. | Restore only the missing reflected argument for that source. | [`Source arguments, aliases and names`](#source-arguments-aliases-and-names) |
| `A12` | A filesystem glob such as `*.cs` is used as a SQL `LIKE` pattern. | Semantic mismatch; no Search diagnostic is expected because both values are strings. | Use `LIKE '%.cs'` for SQL filtering, or keep `*.cs` in scope-glob JSON. | [`SQL LIKE versus filesystem globs`](#sql-like-versus-filesystem-globs) |
| `A13` | A regex uses an unsupported construct or a `glob` mode is invented. | `SEARCH-SYNTAX-001` for regex, or `SEARCH-ARGUMENT-001` for an invalid request mode. | Use the portable regex subset, literal mode, or the documented source shape; do not silently weaken it. | [`Pattern mode and regex dialect`](#pattern-mode-and-regex-dialect) |
| `A18` | A case-sensitive default is assumed to be case-insensitive, or an unsupported case value is supplied. | `SEARCH-ARGUMENT-001` at `case`, or an intentional zero-match result under the sensitive default. | Use exact case or the supported `insensitive` request option. | [`Case selection`](#case-selection) |

## Completeness boundary

`SEARCH-SOURCE-001` through `SEARCH-SOURCE-005`, resource failures and output
failures all prevent a complete scope claim. A typed error, a missing row or
an empty `matches`/`files`/`lines` result does not prove absence. Preserve the
failure code and path, retry a stable bounded scope, and use `search.counts`
and `search.audit` when the answer needs zero-hit files or terminal exactness.

Related contracts:
[`search-capability-card-v1.md`](search-capability-card-v1.md),
[`search-request-contract-v1.md`](search-request-contract-v1.md),
[`search-match-semantics-v1.md`](search-match-semantics-v1.md),
[`search-regex-backend-v1.md`](search-regex-backend-v1.md),
[`search-scope-contract-v1.md`](search-scope-contract-v1.md), and
[`search-completion-contract-v1.md`](search-completion-contract-v1.md).
