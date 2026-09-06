# W08-S04 review

Verdict: approved

Reviewer: separate grill-code review pass (no independent subagent was available)

## Scope reviewed

This scope characterizes actual compiled-query binding for bounded CTE rows
around the existing `search.paths` and scalar-JSON `search.many` sources. It
adds no production or Core-engine change. The tests cover metadata without
runtime values, bounded CTE materialization, duplicate path identity, per-row
request isolation and both non-scalar argument directions. The documentation
records only scalar forms demonstrated by those tests and keeps collection
binding as unsupported.

## Findings

- No blocking correctness, ownership or maintainability findings were
  identified.
- `search.many` accepts CTE-produced scalar `Root` and `Request` values through
  `cross apply`; the source receives the values belonging to the current CTE
  row.
- A `take 1` CTE input produces only the expected single correlated scan's
  occurrences. `UNION ALL` duplicate path rows remain distinct, and the
  correlated output preserves both copies.
- Two CTE rows with different request JSON values retain independent pattern
  labels. No mutable request or settings state is shared between rows.
- Passing a CTE/table alias directly as `root` or `request` does not create a
  table-valued overload. The query reaches source opening and fails with
  `MQ7010_DataSourceOpenFailed`, wrapping the Search scalar-argument
  diagnostic.
- Metadata description remains static without runtime root or request values;
  the verified constructor shape is two `string` arguments and the current
  bounded `SearchMatch` row surface.

## Validation reviewed

- Search focused Release tests: 195 discovered, 192 passed, 3 existing
  platform-conditional skips, 0 failed.
- Exact repository-wide Release tests: 21 projects, 1,479 discovered, 1,445
  passed, 34 classified skips, 0 failed.
- Search request contract validator: 4 scenarios, 45 assertions, passed.
- Search match semantics validator: 6 scenarios, 49 assertions, passed.
- Search production build with warnings-as-errors: 0 warnings, 0 errors.
- `git diff --check`: passed.

## Known boundary

The tested contract remains scalar: CTE expressions may provide scalar root and
request values per row, but a relation, row alias or collection is not
implicitly serialized into `requestJson`. Named constructor binding, a
collection-valued source signature, regex execution, completion options and
broader future occurrence fields remain separate scope boundaries.
