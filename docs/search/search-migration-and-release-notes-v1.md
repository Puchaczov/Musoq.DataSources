# Search migration and release notes v1

Status: release-claim index for `W17-S04`. The package metadata and local
artifacts are prepared in this repository; this document does not claim that
the package has been published or that every Musoq host can execute it.

This note is the short migration boundary for `Musoq.DataSources.Search`
`1.0.0-alpha.1`. The package targets `net10.0`, uses the Runtime-v2 plugin
compatibility contract, and is an additive datasource. The existing
`os.files` and `flat.file` datasource behaviors are unchanged; broad shared
traversal refactoring is deliberately outside this release boundary.

## Install and compatibility boundary

Install the reviewed local directory or ZIP with the host's discovered
command shape:

```text
musoq data-sources import <PATH> --format json --name Musoq.DataSources.Search --non-interactive
```

The host must accept the package's runtime family and dependency range, and
the package must be imported into the active host installation. A successful
import or schema listing is not by itself proof that generated query
execution works: host assembly resolution, host package versions and the
actual executable version are part of the smoke evidence. Keep the artifact
digest, source path and post-import metadata with the run.

The release package contains the Search assembly, generated XML, compatibility
metadata and notices for each registered RID archive. Search is managed-only
and currently contributes no native runtime entry. A missing-native test is
therefore not a valid Search failure injection; runtime-family and dependency
conflict negatives remain required.

## Public dialect and source selection

The public schema currently exposes these eight runtime methods. Their exact
signatures and columns come from generated XML and `SearchSchema` metadata:

| Need | Source | Request shape | Result/completeness boundary |
|---|---|---|---|
| Eligible file names | `search.paths(root)` | one scalar root | metadata rows; content is not opened |
| Files containing a literal | `search.files(root, literal)` | root plus scalar literal | positive file rows only |
| Matching physical lines | `search.lines(root, literal)` | root plus scalar literal | one row per matching physical line |
| Match locations | `search.matches(root, literal)` | root plus scalar literal | one row per non-overlapping literal occurrence; text coordinates are UTF-16 units |
| Labeled literal findings | `search.many(root, request)` | root plus scalar versioned JSON | one row per labeled occurrence; current execution is literal-only |
| Exact per-file totals | `search.counts(root, literal)` | root plus scalar literal | includes zero-hit files completed by the scan |
| Raw-byte occurrences | `search.bytes(root, patternJson)` | root plus scalar versioned JSON | raw byte offsets/windows; no text-coordinate inference |
| Terminal evidence | `search.audit(root, literal)` | root plus scalar literal | one fresh summary carrying outcome and completeness flags |

Use the narrowest source for the requested result unit. `search.many` and
`search.bytes` take JSON strings, not SQL numeric literals, table-valued
arguments or an implicit wildcard language. Escape JSON first and then escape
the SQL string literal. SQL `LIKE` patterns and filesystem globs are different
dialects.

The regex backend is implemented and tested as an internal bounded profile,
but regex execution is not advertised as a public Search constructor in this
release. The request parser can describe a `regex` pattern for the forward
contract. The current `search.many` execution rejects it before opening
content because its exposed matcher is literal-only. It must never silently
fall back to literal text. See
[`search-regex-backend-v1.md`](search-regex-backend-v1.md) for the internal
profile and its independent limits.

## Migration recipes

Keep existing providers when their existing row units are the right fit:

1. Use `os.files` for its established filesystem metadata and planner
   behavior.
2. Use `flat.file` when the existing line-oriented file projection is the
   desired input to downstream parsing.
3. Add `search.paths`, `search.lines` or `search.matches` for bounded lexical
   candidate discovery, retaining Search paths and coordinates beside derived
   values.
4. Add `search.many` for stable labels when one scan needs several literal
   candidate patterns; classify comments and strings before calling a finding
   a code or configuration fact.
5. Add `search.counts` and `search.audit` before making a scope-wide negative
   claim. An empty `matches`, `files` or `lines` result is not an exhaustive
   negative, and a failed or partial scan is not a successful empty result.
6. Add `search.bytes` for binary signatures or bounded byte windows. Do not
   infer text encoding, Unicode columns or endianness from a raw-byte row.

Worked examples are in
[`RepresentativeQueries.md`](../../RepresentativeQueries.md) and
[`search-migration-configuration-recipes-v1.md`](search-migration-configuration-recipes-v1.md).
The capability decision matrix is in
[`search-capability-card-v1.md`](search-capability-card-v1.md).

## Coordinates, scope and parsing

Text match coordinates use one-based physical line numbers and zero-based
UTF-16 columns. Losslessly mapped source-byte offsets are nullable; they are
not interchangeable with UTF-16 columns. Raw-byte rows expose byte offsets
and bounded windows while text-coordinate fields remain null. See
[`search-coordinate-policy-v1.md`](search-coordinate-policy-v1.md) and
[`search-byte-pattern-contract-v1.md`](search-byte-pattern-contract-v1.md).

Scope, ignore, hidden-entry, link, encoding, case and resource policies are
part of the scan contract and its explanation. A root-missing or inaccessible
input is typed failure evidence. A negative answer requires an exhausted,
complete scope with exact counters; see
[`search-scope-contract-v1.md`](search-scope-contract-v1.md),
[`search-completion-contract-v1.md`](search-completion-contract-v1.md) and
[`search-binary-policy-v1.md`](search-binary-policy-v1.md).

Search produces lexical candidates. It does not parse a programming language,
prove symbol binding, or turn a comment/string occurrence into a code fact.
Use `Parse`, `TryParse` with `OUTER APPLY`, or `PartialParse` according to the
required failure-preservation policy; see
[`search-log-search-to-parse-v1.md`](search-log-search-to-parse-v1.md) and
[`search-interpretation-composition-v1.md`](search-interpretation-composition-v1.md).

## Measured performance claim boundary

The W16 ripgrep comparison figures below are archived historical evidence
only. Current Search qualification is managed-only and no active release gate
invokes ripgrep or the Rust prototype.

The measured-performance index is
[`search-benchmark-agent-evaluation-results-v1.md`](search-benchmark-agent-evaluation-results-v1.md).
Its current-platform parity record observed 12 comparable cells across four
of 13 mandatory parity cohorts, with candidate-to-ripgrep median geometric
mean `0.17622871462594739` and maximum observed cell ratio
`0.410659575113502`. These are observed cells, not a blanket
"Search is faster than ripgrep" claim. Filesystem cache state was unknown.

The same boundary applies to the supporting records:

- [`search-parity-benchmark-results-v1.md`](search-parity-benchmark-results-v1.md)
  retains the measured cells and nine explicit residual cohorts.
- [`search-delivery-latency-results-v1.md`](search-delivery-latency-results-v1.md)
  measures in-process boundaries; cold CLI and warm-service end-to-end
  boundaries remain host-owned and were not run.
- [`search-resource-stability-results-v1.md`](search-resource-stability-results-v1.md)
  records bounded in-process cleanup observations, not a universal memory or
  long-lived-service limit.
- Agent-evaluation results remain unmeasured without a fresh-context runner;
  no Search preference, repair-rate or confidence-interval claim is made.

## Unsupported-feature questions

| Question | Safe answer |
|---|---|
| Does an empty `search.matches`, `search.files` or `search.lines` result prove that nothing exists? | No. Use `search.counts` and `search.audit` and require complete/exhausted exactness before a scope-wide negative. |
| Does `search.many` currently execute a regex pattern? | No. Regex is parser-only for this source; execution rejects it before content is opened. |
| Can `search.many` accept a table of requests or a table-valued argument? | No. The public shape is a scalar JSON request; table input is a separate unsupported composition. |
| Are `bytes` offsets UTF-16 or decoded text positions? | No. They are original-byte coordinates; text coordinates are not inferred. |
| Does Search parse code or configuration and prove a migration? | No. Search returns lexical candidates; parsing and classification remain explicit downstream steps. |
| Does importing a ZIP prove generated query execution on every host? | No. The host version, package dependencies and assembly-resolution boundary must be smoke-tested. |
| Does adding Search change `os.files` or `flat.file`? | No. This is additive; their existing contracts remain outside the Search implementation boundary. |
| Do the benchmark numbers prove Search is universally faster? | No. They cover the recorded current-platform cells only; cache state and unmeasured cohorts remain explicit. |
| Has this release been published? | No. Local package and release metadata are not publication evidence; publication requires a separate authorized release workflow. |

## Authority and evidence

The runtime source and generated XML are authoritative for the eight public
constructors. The release registry is
[`scripts/release/packages.json`](../../scripts/release/packages.json), and
the package layout/compatibility requirements come from the plugin ZIP
specification. The documented CLI import boundary was verified against the
installed executable in the `W17-S03` clean-host evidence. The owning Search
tests and the docs-to-runtime/unsupported-feature assertions guard this note
against drift.
