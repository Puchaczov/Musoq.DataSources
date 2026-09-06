# Batched Search workflow benchmark v1

`W08-S05` records an archived equivalent-workflow comparison for the literal
`search.many` execution path. The measured result unit is
`labeled_occurrence`: one normalized row per selected literal occurrence and
pattern identifier. The benchmark is an observation harness, not a claim that
multi-pattern search is novel or that either implementation is universally
faster. Its external baseline is historical provenance only and is not an
active benchmark or release gate.

## Registered workloads

The deterministic fixture has four UTF-8 text files, including nested paths
and Unicode text. It runs the prefix pattern sets with 1, 10 and 100 unique
literal identifiers against two distributions:

- `sparse`: four hit lines in total, with every selected literal on each hit
  line;
- `dense`: every physical line contains every selected literal.

Every cell has one recorded warmup and seven randomized paired trials. The
fixture digest and normalized result hash are retained in the scope report.

## Historical compared workflows

The managed candidate invokes the actual internal
`SearchManyLiteralScan`/Aho–Corasick path once for the eligible scope and then
normalizes labeled rows for the equivalent sink.

The external baseline invokes one `rg` process per trial with
`--json --no-config --no-ignore --fixed-strings` and one `-e` argument per
registered literal. Its JSON postprocessor assigns a pattern identifier by
exact literal text. This labeled baseline is intentionally restricted to
unique literals; duplicate literal labels require semantics that `rg`'s JSON
submatches do not identify and are not silently treated as equivalent.

## Timing interpretation

Candidate scan time surrounds eligible traversal, file opening, decoding and
matching. Native `rg` traversal/read/matching time is not observable through
the subprocess boundary, so the report records its searched input bytes and
does not invent a native scan duration.

End-to-end investigation time surrounds the complete invocation through
normalized row sorting, serialization and hashing. For `rg`, this includes
process setup, JSON collection/parsing and literal-to-label postprocessing.
Filesystem cache state is `unknown` on the unprivileged measurement host and
is never described as cold.

The historical executable command was:

```text
dotnet run --project Musoq.DataSources.Search.Benchmarks\Musoq.DataSources.Search.Benchmarks.csproj --configuration Release --no-build -- measure-batched
```

That comparison-only command is no longer available. Current managed-only
batched coverage uses the Search test project and the retained
`measure-match-batches` benchmark command.

The exact seven-trial observations, tool identity, hashes, per-trial scan and
end-to-end costs, and automaton node/transition/output-reference growth are
retained in `docs/campaigns/search/evidence/W08-S05/report.json`.
