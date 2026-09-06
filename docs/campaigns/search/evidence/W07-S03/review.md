# W07-S03 review

## Verdict

Approved for completion.

Reviewer: separate `grill-code` review pass (no independent subagent was available).

## Scope checked

- `SearchCapture` is a typed, immutable row with group name, stable engine
  group number, capture ordinal, participation state, text and nullable byte/
  UTF-16 spans. Group zero is excluded and unnamed groups use a null name.
- `SearchMatch.Captures` is a stable collection-valued property. The schema
  exposes it without creating pattern-dependent top-level columns, and the
  compiled `CROSS APPLY` coverage verifies that the collection is expandable.
- Capture materialization is projection-aware. Literal and compact match
  scans retain their existing cheap path and receive a shared empty collection;
  regex capture objects are created only when `Captures` is requested (or a
  direct caller supplies no projection metadata).
- The selected portable non-backtracking regex profile's actual behavior is
  preserved: repeated groups expose the final successful capture only, while
  accepted duplicate names share the engine group number. There is no silent
  backtracking fallback. Unmatched groups produce a false/null placeholder;
  successful empty groups produce a true/empty, zero-length result.
- Capture byte spans use the existing lossless decoder coordinate mapping and
  UTF-16 spans are relative to the physical record. Read-only capture lists
  do not expose mutable scanner state, and cancellation/error behavior remains
  owned by the existing scanner path.
- Focused tests cover optional unmatched versus successful empty groups,
  multiple groups, repeated groups, duplicate names, projection elision,
  UTF-8 byte spans and UTF-16 coordinates. The exact Release repository suite
  is also green.

## Findings and disposition

No material correctness, semantic, diagnostic, compatibility, resource or
scope-boundary finding remains open.

The safe profile intentionally does not reconstruct backtracking capture
history. That behavior is documented and tested; an advanced backtracking
profile remains a separately gated future concern rather than an implicit
fallback.

## Boundary

Only the owning Search source, tests, Search documentation and W07-S03
evidence were changed. No sibling repository, release, publication,
installation or unrelated campaign state was staged.
