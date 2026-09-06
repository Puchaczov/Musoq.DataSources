# Search case and word-boundary policy v1

Status: implemented for the current managed text sources in `W06-S03`. The
existing two-argument SQL constructors retain their sensitive, non-whole-word
defaults. The internal request seam carries the options until the later
versioned request parser exposes them.

## Case comparison

`sensitive` is ordinal exact UTF-16 code-unit comparison. `insensitive` is
ordinal Unicode case-insensitive comparison using invariant one-code-unit
folding. It never uses the current culture, normalizes composed/decomposed
text, or expands one code unit into multiple code units. Therefore:

- Turkish `I`/`i` follows invariant ordinal behavior; dotted `İ` and dotless
  `ı` are not silently treated as `i`.
- Greek capital sigma, normal sigma and final sigma compare as the same case
  family under the invariant ordinal-insensitive profile.
- German `ß` is not expanded to `SS`; the two strings remain different-length
  literals, and uppercase `ẞ` is not implicitly normalized to `ß`.

Case-insensitive matching keeps the same leftmost, non-overlapping selection
and the same UTF-16/source-byte coordinates as sensitive matching. The row's
`MatchText` remains the requested literal in the current source contract.

## Whole-word comparison

`wholeWord` is opt-in and uses Unicode scalar categories. A word scalar is a
letter, decimal digit, connector punctuation, non-spacing mark or spacing
combining mark. A match is eligible only when its first scalar and the scalar
before it have different word status (or the match is at a physical-record
edge), and its last scalar and the scalar after it likewise have different
status. Underscores and digits are word-like; punctuation and symbols such as
emoji are not. Combining marks remain attached to a word and therefore block
an otherwise shorter whole-word match immediately before the mark.

Matching is still code-unit based for literal selection, but whole-word
validation rejects a match that starts or ends inside a surrogate pair. A
candidate rejected by a boundary check does not suppress a later overlapping
candidate that can satisfy the boundary policy. Physical LF boundaries remain
record edges; CR is classified as ordinary non-word content unless followed
by LF under the line policy.

Unknown case values are rejected as invalid request arguments. No native
backend is advertised by the current source, so there is no unsupported
managed/native combination to hide or silently reinterpret.

Source authority: `S14`, `S20`, `S22`, `S24`; semantic basis:
[`search-match-semantics-v1.md`](search-match-semantics-v1.md).
