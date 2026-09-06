# W06-S03 review

## Verdict

Approved for completion.

Reviewer: separate `grill-code` review pass (no independent subagent was available).

## Scope checked

- The request model carries explicit sensitive/insensitive and whole-word
  policy values without changing the existing two-argument SQL constructor
  defaults.
- Sensitive matching remains the exact UTF-16 code-unit fast path; insensitive
  matching uses invariant one-code-unit folding and does not depend on the
  current culture, normalization, or one-to-many expansions.
- Whole-word classification is explicit and scalar-aware: letters, decimal
  digits, connector punctuation and combining marks are word scalars, while
  punctuation and symbols are boundaries. LF remains the physical record edge.
- KMP fallback preserves leftmost non-overlapping selection while allowing a
  boundary-rejected candidate to leave a later candidate discoverable.
- Candidate spans retain UTF-16/source-byte coordinates through the policy path,
  including reader-block boundaries and supplementary UTF-8 scalars.
- A review finding for a high surrogate at the end of a reader block was fixed
  with deferred-character state and a regression test covering both emoji and a
  supplementary letter across the block boundary.
- Required Turkish I, German sharp-s, Greek sigma, underscore, digit,
  combining-mark, non-ASCII identifier and culture-drift cases are covered by
  focused tests. There is no native backend advertised by this repository, so
  no unsupported managed/native combination is silently represented as parity.

## Findings and disposition

One medium correctness finding was found and fixed before completion: a pending
whole-word candidate could be rejected when its next supplementary scalar was
split between reader blocks. The matcher now defers that high surrogate until
the next code unit or successful EOF, and the focused regression passes.

No material correctness, ownership, diagnostic, compatibility, resource,
performance or scope-boundary finding remains open.

## Boundary

The current public Search SQL constructors remain two-argument and therefore
retain sensitive, non-whole-word behavior; the versioned request parser that
will expose these options is outside this scope. No native backend, sibling
repository, release, publication, installation or raw-byte search surface was
changed.
