# W08-S01 review

## Verdict

Approved for completion.

Reviewer: separate `grill-code` review pass (no independent subagent was available).

## Scope checked

- The parser accepts one bounded scalar JSON string and returns immutable typed
  pattern and option models. It does not add a SQL map or presume collection
  binding.
- A strict UTF-8 reader validates the complete JSON value with comments,
  trailing commas and excessive nesting rejected. A separate structural pass
  detects duplicate property names at every object depth, including escaped
  names and properties inside otherwise-unknown objects.
- Closed top-level, pattern, option and scope property sets are enforced.
  Pattern IDs, pattern text, pattern count, scope lists, `take` and the scalar
  transport are bounded before any filesystem-facing code can be called.
- Contract enums are mapped explicitly rather than through permissive aliases.
  Literal and regex labels may coexist; unsupported mode values are rejected,
  while shared case, encoding, selection, partial-result and validation
  options retain their documented defaults.
- Diagnostic failures retain typed Search exceptions and identify
  `requestJson`; JSON-reader and property failures carry UTF-8 byte offsets.
- Tests cover option and pattern round trips, escaped values, maximum pattern
  text, positional/named SQL scalar payloads, duplicate keys and IDs, unknown
  properties, invalid modes, bounds, malformed JSON and oversized transport.
- The request contract explicitly remains proposed for source metadata and
  execution. W08-S01 records parser implementation without advertising an
  unverified `search.many` constructor; source discovery and shared matching
  remain later W08 scopes.

## Findings and disposition

No material correctness, semantic, diagnostic, compatibility, resource,
performance or scope-boundary finding remains open.

The parser uses two bounded reader passes so duplicate-key rejection remains
independent of closed-schema rejection. This is request-admission work rather
than a scan hot path; no filesystem access or per-file matching cost is added.

## Boundary

Only Search request diagnostics/models/parser, parser tests, the versioned
request contract documentation and W08-S01 evidence were changed. No sibling
repository, engine/CLI source, release, publication, installation or unrelated
campaign state was staged.
