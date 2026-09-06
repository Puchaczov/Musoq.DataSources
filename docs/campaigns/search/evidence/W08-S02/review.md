# W08-S02 review

Verdict: approved

Reviewer: separate grill-code review pass (no independent subagent was available)

## Scope and boundary

- `SearchManyLiteralMatcher` builds one bounded Aho-Corasick automaton for the
  request's literal patterns.
- Candidate selection is applied independently per pattern index, preserving
  cross-pattern overlap and equal text under distinct IDs.
- `SearchManyLiteralScan` owns one scope enumeration and one eligible content
  scan per file. It reuses the existing streaming scanner and coordinate path.
- The existing single-pattern scanner is adapted through an internal matcher
  interface only; public Search schema metadata and SQL constructor binding are
  unchanged.

## Review checks

- Prefix and suffix patterns retain all labeled spans; same-pattern overlaps are
  filtered using that pattern's own end boundary.
- Duplicate pattern text is not deduplicated; terminal outputs retain each
  request label.
- Newline resets automaton state, preventing a literal match from crossing
  physical records. Whole-word checks use the existing Unicode word policy.
- UTF-16 line/column state and mapped byte ranges remain in the existing
  `MatchSpan` coordinate model, including reader block boundaries.
- Invalid regex-mode items are rejected by the literal-only engine before scope
  enumeration or reader creation; there is no fallback to literal semantics.
- Cancellation is checked during block processing and delegated through the
  existing scanner/reader disposal path. The automaton and coordinate rings are
  bounded by the request's maximum pattern length.
- The callback scan coordinator does not materialize a repository-wide result
  collection; matches are handed off as they are observed.
- Focused tests cover the required prefix, duplicate-label, overlap,
  chunk-boundary and large-set cases, plus whole-word behavior, pre-I/O mode
  rejection, and one traversal/one reader open per eligible file.

## Findings and disposition

No blocking findings. The implementation is intentionally an internal
literal-set foundation: public `search.many` metadata, query binding and
non-literal execution remain later W08 scopes and are not advertised here.
