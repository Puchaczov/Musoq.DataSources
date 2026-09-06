# Search match semantics v1

Status: proposed design contract for `W01-S02`. This document freezes the
meaning of matching before a backend is selected. It does not claim that the
future Search datasource or these modes are already installed.

The canonical truth-table corpus is
[`search-match-semantics-v1.json`](search-match-semantics-v1.json), and
[`Test-SearchMatchSemantics.ps1`](../../scripts/search/Test-SearchMatchSemantics.ps1)
checks the declared cases with an independent small oracle.

## Defaults

| Option | Default |
|---|---|
| Text mode | `literal` |
| Case | `sensitive` |
| Whole word | `false` |
| Encoding | `auto` |
| Record scope | physical line |
| Selection | leftmost, non-overlapping per pattern |
| Regex profile | portable non-backtracking subset |
| Unsupported regex construct | reject before opening the root |
| Fallback | none |

The default literal mode treats punctuation and regex metacharacters as
ordinary text. Regex is explicit. Case-sensitive matching is ordinal and no
Unicode normalization is implicit. Case-insensitive matching, when explicitly
requested, is ordinal Unicode case-insensitive matching and never uses the
current culture.

## Literal and multi-pattern selection

For one pattern, emit the leftmost match, then continue searching after its
end. A later match that overlaps the selected match is not emitted. A match
with `ByteLength = 0` is handled by the zero-width rule below.

Patterns in `search.many` are independent. Each labeled pattern gets its own
non-overlapping scan, so a match from `a` never suppresses an overlapping match
from `ab`. Equal pattern strings under distinct `PatternId` values remain
distinct logical patterns. Duplicate IDs are rejected before any root read.

An empty literal is invalid and is rejected before any root read. This is an
input error, not a zero-width search.

## Portable regex profile

The proposed regex profile is a non-backtracking portable subset supporting
escaped/literal characters, character classes and ranges, Unicode categories,
capturing and non-capturing groups, alternation, repetition, record anchors
and word boundaries. Constructs that require backtracking or depend on an
engine-specific extension are unsupported; the initial set includes
backreferences, lookahead/lookbehind, balancing groups, conditionals and
atomic groups.

Regex selection is earliest start first. If alternatives begin at the same
position, their source order wins: `a|ab` on `ab` returns `a`, while `ab|a`
returns `ab`. This is leftmost-first, not POSIX leftmost-longest.

Unsupported or malformed syntax is a typed request failure before content is
read. The implementation must not retry as literal, switch to a backtracking
engine, or silently use another dialect.

## Zero-width matches

Zero-width regex matches are emitted with a zero byte length. After emitting
one, the next search start advances by one Unicode scalar unless the match is
already at the end of the record. An end-position match is emitted at most
once, and an implementation must never loop on a zero-width result.

The default record is a physical line. An empty physical record is valid; an
empty file has no physical records unless the later line-source contract
explicitly changes that rule. `^` and `$` therefore apply to the start and end
of a physical record, not to arbitrary internal buffer boundaries.

## Whole-word and Unicode behavior

Whole-word matching is opt-in. A word scalar is a letter, decimal digit,
connector punctuation, non-spacing mark or spacing-combining mark. A boundary
exists at a record edge or where that word status changes. Combining marks stay
attached to the word for boundary purposes.

Matching does not normalize composed and decomposed Unicode. For example,
`café` and `cafe` followed by U+0301 are different literals. No current-culture
case or boundary behavior is allowed.

## Encoding behavior

`auto` honors a supported UTF-8/UTF-16 BOM and otherwise interprets input as
UTF-8. Explicit `utf8`, `utf8-bom`, `utf16-le-bom` and `utf16-be-bom` modes
must agree with a present BOM and the actual decoding. Invalid bytes are a
typed failure; replacement characters are not a successful fallback.

All byte coordinates remain offsets and lengths in the original input bytes.
UTF-16 columns and lengths are separate fields. Raw byte matching belongs to
`search.bytes` and is not silently converted into text-regex semantics.

## Ordering

Within one pattern, selected rows are in increasing source start. A same-start
tie uses increasing end. For `many`, path and byte start precede input pattern
order. These are source traversal rules, not an SQL ordering guarantee; a
caller must use `ORDER BY` when result ordering is part of the query contract.

## Required truth tables

The reference corpus covers:

- a suffix-overlapping `aba` in `ababa`, plus cross-pattern `a`/`ab` overlap;
- same-start regex alternatives and leftmost-first source order;
- duplicate IDs versus equal text with distinct IDs;
- empty literal rejection before reading;
- record anchors and zero-width start/end matches;
- composed/decomposed Unicode and combining-mark word boundaries.

The expected spans in the corpus are UTF-16 code-unit spans for text records;
the runtime row contract separately reports original-byte offsets and lengths.

## Authority and boundary

The Runtime-v2 plugin guide establishes static source metadata, typed row
contracts and chunked row production. Structured search output distinguishes
match messages, submatch spans and summary counters. The Aho-Corasick reference
demonstrates that overlapping, leftmost-first and leftmost-longest matching are
different semantics, so no algorithm is selected merely because it can search
multiple patterns.

This document is a proposed semantic contract, not a claim that ripgrep,
.NET, Aho-Corasick or any other backend is the product implementation. Scope
and ignore policy, completion outcomes and input-schema validation remain in
the other W01 scopes.

The W08-S02 implementation adds an internal Aho-Corasick literal-set matcher
and a one-traversal/one-read scan coordinator. It preserves the contract's
per-pattern selection and identity rules, but does not register a public
`search.many` constructor; regex-labeled requests are rejected by this
literal-only engine before scope I/O until a later execution scope owns them.

Source references: `S14`, `S15`, `S20`, `S22`, `S23`.
