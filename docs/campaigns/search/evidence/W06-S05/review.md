# W06-S05 review

## Verdict

Approved for completion.

Reviewer: separate `grill-code` review pass (no independent subagent was available).

## Scope checked

- `SearchProjectionRequirements` derives only the optional decoded-text work
  from `SourceExecutionContext.AllColumns`. An empty column set preserves the
  complete typed-row behavior used by direct source callers. The current
  literal schema has no `Captures` field; capture projection remains the
  explicitly later W07 work and no unsupported column was invented here.
- Occurrence sinks continue to consume every matcher span and preserve match
  ordinals, UTF-16 coordinates, original-byte coordinates and multiplicity.
  Compact occurrence rows omit only `MatchText`.
- Line sinks keep line completion and occurrence counting active when
  `LineText` is not requested. The compact path still tracks LF boundaries,
  physical line numbers and line byte offsets, while avoiding the line-text
  `StringBuilder`.
- Full decoded input is still read through the existing strict reader path.
  The W06-S04 binary preflight, late-marker behavior, decoder validation,
  reader disposal and cancellation checks remain in the source boundary.
- Direct and compiled projection tests compare the same files and queries for
  row count, multiplicity, line boundaries and coordinates. The decoding
  fixture test separately verifies complete ASCII, UTF-8 and UTF-16 results.
- `measure-decoding` records one warmup and seven complete trials for each
  encoding cohort. It is clearly labeled as a standalone managed diagnostic;
  cache state is unknown and no cross-backend or superiority claim is made.

## Findings and disposition

No material correctness, semantic, diagnostic, compatibility, resource,
performance or scope-boundary finding remains open.

The benchmark intentionally measures the isolated managed feasibility runner,
not a production Search-vs-baseline claim. Production projection behavior is
covered by direct and compiled datasource tests; the benchmark's role in this
scope is to preserve separate, reproducible decoding observations without
pretending that an external baseline or controlled cache state exists.

The required test wording mentions captures, but the current literal Search
row model does not expose a capture column. This is recorded as a deliberate
boundary and left for W07's regex/capture scopes.

## Boundary

Only the owning Search source, tests, benchmark diagnostic, Search
documentation and W06-S05 evidence were changed. No sibling repository,
release, publication, installation, native backend or raw-byte search surface
was changed.
