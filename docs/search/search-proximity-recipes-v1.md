# Search bounded proximity recipes v1

This recipe relates ordered lexical occurrences that are close in the same
file. It is evidence about positions in text, not a claim about language
structure, symbol identity, control flow or test behavior.

## Candidate source

Use one labeled `search.many` request and retain the occurrence coordinates
needed by the relation:

```sql
select m.Path, m.PatternId, m.MatchIndex,
       m.LineNumber, m.Utf16Column, m.Utf16Length, m.MatchText
from search.many(root, request) m
order by m.Path, m.PatternId, m.LineNumber, m.Utf16Column, m.MatchIndex
```

The request in the executable example labels the left and right literals as
`left` and `right`. If the JSON is embedded in a SQL string, escape the JSON
backslashes for the SQL literal as well as escaping the root path.

## Window and ordering contract

For a left occurrence and a right occurrence to be related, all of these must
hold:

1. `Path` is equal. Occurrences in adjacent or otherwise related files never
   form a pair.
2. The right span is ordered after the left span. On a later line this is
   determined by `LineNumber`; on the same line the right `Utf16Column` must
   be at or after the left span's end.
3. `LineNumber(right) - LineNumber(left)` is between zero and the inclusive
   `MaxFollowingLines` bound.
4. On the same line, the gap between the left span's end and the right span's
   start is at most the inclusive `MaxSameLineGap` bound.

Line and UTF-16 coordinates are used only for this lexical relation. The
recipe does not reconstruct byte offsets or infer a syntax tree. A missing
right occurrence produces no pair; it is not a zero or a fabricated match.

## All-pairs and nearest-right modes

All-pairs mode emits every right occurrence satisfying the window in stable
`Path`, line, UTF-16-column and `MatchIndex` order. `MaxPairsPerLeft` is a
hard cap: if one left occurrence has more candidates than the cap, the recipe
fails rather than returning an unexplained partial relation. This makes the
combinatorial boundary visible to the caller.

Nearest-right mode emits at most one pair per left occurrence: the first
candidate in that same deterministic order. Its output cap is therefore one
pair per left, while the line and same-line gap bounds still apply. “Nearest”
means nearest in the ordered lexical candidate sequence; it does not mean
semantic or geometric distance.

The result can be represented as:

```text
ProximityPair {
    LeftPath, LeftLine, LeftUtf16Column, LeftMatchIndex,
    RightPath, RightLine, RightUtf16Column, RightMatchIndex,
    LineDistance, SameLineGap
}
```

`SameLineGap` is null for a pair on different lines. Keep both occurrence
identities in downstream output so repeated matches are not collapsed merely
because they share a line.

## Qualification boundary

`SearchProximityRecipeTests` compiles and executes the labeled SQL shape. Its
fixture asserts an independent expected set of six same-file pairs for the
all-pairs window, preserves two same-line right matches in column order,
rejects cross-file pairing, leaves a right-only file without a left pair and
selects the first candidate for nearest-right mode. A second test proves that
an all-pairs cap fails closed and that negative window values are rejected.

The recipe does not claim that a nearby string is the declaration, call,
assertion or documentation associated with another string. Use the diagnostic
traceability recipe when a caller needs explicit categories and evidence
relations instead.

Source references: `S14`, `S15`, `S16`; related contracts:
[`search-source-contract-v1.md`](search-source-contract-v1.md),
[`search-coordinate-policy-v1.md`](search-coordinate-policy-v1.md) and
[`search-many-summaries-v1.md`](search-many-summaries-v1.md).
