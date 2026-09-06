# Search many-pattern summaries v1

Status: implemented for the scalar-JSON, literal-only `search.many` source in
`W08-S03`. The source emits typed occurrence rows; Boolean and threshold
answers are ordinary SQL aggregates over that result. It does not add a
second summary DSL or a mutable last-run cache.

## Request and row unit

The source shape is:

```sql
search.many(root, requestJson)
```

`requestJson` is the bounded versioned request from
[`search-request-contract-v1.json`](search-request-contract-v1.json). The
current execution scope accepts literal pattern entries and preserves each
`PatternId`. Regex-labeled entries, `take`, partial-result mode and
observed-prefix validation are rejected before scope enumeration until their
owned execution/completion scopes are implemented.

One row represents one occurrence of one labeled pattern. `MatchIndex` is
zero-based independently within `(Path, PatternId)`. Equal literal text under
different labels therefore produces separate rows. SQL projection and
aggregation do not change that source row unit.

## Tested summary shapes

The following queries use the ordinary SQL supported by the repository. They
assume `request` contains labels `todo` and `fixme`.

Per-file and per-pattern occurrence counts:

```sql
select m.Path as Path, m.PatternId as PatternId,
       Count(m.MatchIndex) as OccurrenceCount
from search.many(root, request) m
group by m.Path, m.PatternId
```

Files with any hit are the grouped paths from the occurrence source:

```sql
select m.Path as Path, true as AnyHit
from search.many(root, request) m
group by m.Path
```

Files containing all requested labels, or at least `k` distinct labels, use
the label count rather than the raw occurrence count. This avoids counting a
second occurrence of one label as a different requested term:

```sql
select m.Path as Path, Count(distinct m.PatternId) as PatternCount
from search.many(root, request) m
group by m.Path
having Count(distinct m.PatternId) = 2
```

Replace `= 2` with `>= k` for an at-least-`k` query. A missing label means the
file is absent from the complete all-label result; it is not fabricated as a
zero row by `search.many`.

Files with none of the requested labels are obtained by preserving the
eligible path relation and anti-joining the occurrence relation:

```sql
select p.Path
from search.paths(root) p
left outer join search.many(root, request) m on p.Path = m.Path
where m.Path is null
```

`search.paths` supplies the zero-hit file identity. A file is classified as
none only after the `search.many` scan has completed for every eligible file;
an empty or failed occurrence prefix is never a successful negative answer.
The current source intentionally processes the complete eligible scope for
these SQL aggregates, so no early-stop optimization is claimed here.

Source references: `S14`, `S15`, `S20`, `S23`.
