# W06-S01 review

## Verdict

Approved for completion.

Reviewer: separate `grill-code` review pass (no independent subagent was available).

## Scope checked

- The default file reader uses the canonical `auto` policy and does not infer
  UTF-16 from NUL density, locale, file names or a text sample.
- BOM probing reads only a bounded four-byte prefix, recognizes UTF-8 and both
  UTF-16 BOMs, rejects UTF-32 BOMs, and positions the file stream after a
  validated BOM.
- Explicit `utf8`, `utf8-bom`, `utf16-le-bom` and `utf16-be-bom` modes require
  the declared BOM shape and cannot be silently switched by
  `StreamReader`'s automatic detection.
- UTF-8 and UTF-16 decoders use `throwOnInvalidBytes: true`; partial
  multibyte and odd UTF-16 tails surface as typed source-read failures rather
  than replacement characters.
- Encoding mismatches and unsupported declarations use the stable
  `SEARCH-SOURCE-004` diagnostic before content decoding. Invalid content uses
  the existing typed `SEARCH-SOURCE-003` read boundary.
- File streams are disposed if resolution or reader construction fails, and
  normal reader disposal remains owned by the scanner. The injectable decoded
  `TextReader` seam is not misrepresented as a byte-policy boundary.
- The internal request model carries the policy without changing the existing
  public two-argument SQL constructor metadata; transport of the future
  versioned request option remains a later scope.
- Tests cover mixed BOMs, no-BOM strict UTF-8, explicit mode agreement and
  missing/mismatched BOMs, no-BOM non-heuristic behavior, partial UTF-8 and
  UTF-16 tails, unsupported UTF-32 BOMs and canonical value validation.

## Findings and disposition

No material correctness, ownership, diagnostic, compatibility or scope-boundary
finding required a follow-up change. Original-byte offsets are intentionally
not implemented in this scope; the policy document keeps that mapping as the
subsequent coordinate scope.

## Boundary

Only the owning Search reader/request/diagnostic implementation, Search tests,
and Search documentation/evidence were changed. No sibling repository, public
request parser, release, publication, installation or binary-search surface
was modified.
