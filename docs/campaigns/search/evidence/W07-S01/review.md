# W07-S01 review

## Verdict

Approved for completion.

Reviewer: separate `grill-code` review pass (no independent subagent was available).

## Scope checked

- `SearchRegexBackend` selects the declared `portable-nonbacktracking-v1`
  profile with invariant culture, optional invariant case-insensitivity and a
  one-second match timeout. It never retries rejected patterns in literal mode
  or silently changes to a backtracking engine.
- Unsupported lookaround, backreferences, contiguous-match anchors, atomic
  groups, conditionals and balancing groups are identified before construction
  where possible. Engine-level unsupported constructs and malformed patterns
  become typed Search syntax diagnostics that name the dialect and a rewrite or
  literal-mode remedy.
- Pattern length and nesting are checked before regex construction. The
  construction path records a five-second compilation budget and turns an
  over-budget construction into a typed resource failure; the bounded input
  and nesting checks are the pre-construction safeguards because the .NET
  `Regex` constructor has no cancellation API.
- The cache key contains pattern text, case mode and whole-word mode. A lock
  protects an immutable `Regex` value and a 128-entry LRU, so concurrent
  requests for one resident key share one instance and retained compiled state
  is bounded.
- Tests cover the selected options, capture support, source-order alternatives,
  malformed syntax, all declared unsupported-construct examples, oversized
  patterns, a 4,096-branch alternation, excessive nesting, repeated semantic
  keys, concurrent same-key requests, eviction and cache pressure.
- The existing regex-validation entry point delegates to the backend, keeping
  validation semantics aligned with the future execution path. The public
  literal source constructors and SQL/XML surface were not widened; line
  execution and capture-row exposure remain W07-S02 and W07-S03.

## Findings and disposition

No material correctness, semantic, diagnostic, compatibility, resource or
scope-boundary finding remains open.

The compilation budget is intentionally documented as a post-construction
guard: .NET does not expose cancellation for dynamic `Regex` construction.
The pre-construction length and nesting limits, plus the selected
non-backtracking engine and bounded cache, are the enforceable resource
controls in this scope. W07-S02 must preserve the same backend and reject
before scanning any source content.

## Boundary

Only the owning Search source, tests, Search documentation and W07-S01
evidence were changed. No sibling repository, release, publication,
installation or public regex execution surface was changed.
