# Search predicate planning v1

Status: implemented and verified for `W10-S01` through `W10-S04`.

The Search planner accepts only predicates that the corresponding source can
evaluate against the row it emits. Accepted predicates are copied into both
`SourcePlanResult.AcceptedPredicate` and `SourceExecutionPlan.AcceptedPredicate`;
the Search source applies that predicate before writing the row. Everything
else remains in `ResidualPredicate` for Musoq to evaluate.

## Required-field materialization

The planner also copies the source request's required columns into both
`SourcePlanResult.AcceptedColumns` and `SourceExecutionPlan.AcceptedColumns`.
This set is the union Core derives from output, predicates and ordering, so a
field needed only by a residual operation is still materialized before the row
is emitted. Search uses it to retain optional match text, line text, captures
and context; when no plan projection is present, the older `AllColumns` direct
source contract remains the fallback. Rows are immutable snapshots and are not
completed by rereading mutable files after emission.

## Ordering and slicing

Search does not claim global `ORDER BY` pushdown: filesystem traversal order
is not a source-level ordering contract, so descending, multi-file and tied
ordering remains residual for Core. `SKIP` and `TAKE` are accepted only when
the source has no residual predicate or ordering and the window values are
non-negative. The source applies the accepted window after its accepted row
predicate across the complete source stream; this preserves global slice
semantics for a direct source while Core keeps a slice above joins, grouping
or residual work. `TAKE 0` is accepted and emits no rows, without changing
the source's scan/error contract.

Parallel file scheduling is a transport concern, not an SQL ordering promise.
The coordinator names its two internal modes explicitly: file-enumeration
order and completion order. The Search source currently selects the bounded
file-enumeration mode; completion order is available only as an explicit
multiset-preserving candidate and must not be treated as a sorted result. A
caller that requires order still pays the residual `ORDER BY` cost at the
query boundary. Progress callbacks are aggregated at a bounded row interval,
and files that emit no rows do not cause a progress callback.

## Plan inspection and cost transparency

Every Search source plan carries a versioned `search.plan.v1` metadata map in
`SourceExecutionPlan.Properties` and emits the same bounded facts through the
Core planning inspection text. The snapshot reports the selected strategy,
the default effective scope and its deferred root resolution, whether content
is opened or decoded, context-retention requirements, early-stop limits, and
requested/accepted/residual columns, predicates, ordering and slices.

The metadata-only `paths` source explicitly reports that it enumerates
filesystem metadata without opening or decoding content. Text sources report
BOM-aware UTF-8/UTF-16 decoding at execution time. A source-local `TAKE` is
reported as an output window rather than a scan budget; exact counts and
occurrence/line results therefore retain their complete-file requirements.
Inspection is metadata-only: it does not resolve the root, read plugin input
files, or parse the runtime `many` request, whose source-specific operations
remain residual at the normal planner boundary.

## Accepted matrix

| Source | Accepted scalar columns | Retained residual columns |
| --- | --- | --- |
| `matches` | `Path`, `PatternId`, `MatchIndex`, `ByteOffset`, `ByteLength`, `LineNumber`, `Utf16Column`, `Utf16Length` | `MatchText`, `Captures`, `Context` |
| `lines` | `Path`, `Origin`, `PatternId`, `LineNumber`, `ByteOffset`, `OccurrenceCount` | `LineText` |
| `files` | `Path`, `Origin`, `PatternId` | none |
| `counts` | `Path`, `Origin`, `PatternId`, `OccurrenceCount`, `MatchingLineCount`, `BytesScanned`, `Complete` | none |
| `paths` | `Path`, `Origin`, `EntryKind` | none |

The `many` source keeps its existing conservative planner boundary in this
scope. Its source implementation can consume an explicitly supplied accepted
plan, but normal planning continues to reject all source operations there.

The accepted expression forms are scalar comparisons, `IN`/`NOT IN` with
literal members, `IS NOT NULL`, and `AND`/`OR` combinations of those forms.
Unsupported `OR` expressions stay whole and unsupported conjuncts are split
only from an `AND`. Generic `NOT`, content fields and collection-valued fields
therefore remain residual.

## Semantic rules

- Column names are matched case-insensitively after removing the final source
  alias component, so both `Path` and `m.Path` address the same row field.
- The evaluator compares strings with ordinal semantics and numeric values with
  invariant numeric conversion. Reversed operands are evaluated in their
  original order, so `2 < m.LineNumber` has the same result as the reject-all
  path.
- Ordinary comparisons involving `NULL` are `UNKNOWN` and do not select a
  row. `IS NOT NULL` is accepted; `IS NULL` stays residual because a source
  planner has no join-nullability context and pushing it could change a
  `LEFT JOIN`'s NULL-extended rows.
- `NOT IN` with a NULL member is `UNKNOWN` for a non-matching non-NULL value,
  and is therefore not selected by the source filter.
- `counts` predicates are evaluated only after the source has completed the
  exact per-file count row. The planner does not change the count row unit or
  claim early termination.

This is row filtering, not filesystem traversal pruning. A predicate on a
relative `Path` does not authorize a new glob, directory walk policy or
metadata lookup, and accepted filtering must not change the Search source's
coordinates, row ordinals or completion semantics.
