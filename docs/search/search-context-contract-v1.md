# Search context contract v1

`SearchMatch.Context` is optional physical-line evidence attached to an
occurrence. It does not change the `matches` row unit: one source row remains
one non-overlapping occurrence, even when adjacent windows overlap or several
occurrences share one window.

Each immutable `SearchContextLine` contains:

| Field | Meaning |
|---|---|
| `RelativeLine` | Non-zero signed distance from the matching line; negative is before and positive is after. |
| `LineNumber` | Exact one-based physical line number in the source file. |
| `LineText` | Decoded physical line, including its terminator when retained; null when the bounded text budget is exhausted. |

The matching line is not repeated in `Context`. Lines are ordered by physical
line position, and a context window is clipped at beginning-of-file and
end-of-file. CRLF remains part of a retained line when it fits the budget.

The current execution seam accepts internal `SearchContextOptions` with
`BeforeLines`, `AfterLines` and `MaxBytes`. The total line window is bounded at
128 lines and the UTF-8 text budget at 1 MiB. The default context text budget
is 64 KiB. That byte budget is divided across requested context lines so every
emitted window is bounded; a long line is prefix-truncated and its identity is
still retained when text cannot fit. These settings are not yet a new SQL
constructor shape, so the two-argument `search.matches(root, literal)` call
continues to return empty context.

Context work is projection-aware. A query that does not request `Context`
keeps the ordinary occurrence fast path and returns an empty collection. A
query that requests it retains the bounded line ring and emits the same match
count and coordinates. The typed collection can be expanded with
`CROSS APPLY m.Context` once a request has enabled a non-zero context window.

Context-enabled occurrence rows also retain an internal evidence handle. The
handle stores only the source identity and encoding policy, not pooled reader
memory. Its bounded `ExpandContext` execution seam re-reads a requested
physical-line window only after validating the original source version and
validates it again before returning. A changed, replaced, deleted or
unreadable source is rejected as stale; text from a newer source is never
returned for an older row. Decoder or read failures remain typed source-read
failures rather than producing a successful expansion. This seam is not a new
SQL constructor argument or an atomic-snapshot claim for a live filesystem;
the live-source mutation boundary is specified separately in
`search-mutation-contract-v1.md`.
