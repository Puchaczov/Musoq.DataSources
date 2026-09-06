# Raw-byte pipeline benchmark v1

`W13-S05` measures the binary value of Search separately from text-search
parity. It does not compare byte occurrences or parsed records with `rg`
matching lines, and it makes no claim that binary matching alone is absent
from `rg`. The added value under observation is structured byte evidence and a
bounded match-to-interpret workflow.

## Registered workflows

The deterministic fixture contains two 2 MiB binary files. It plants dense
`CA` candidates, exact `CA FE` candidates, false magic, invalid lengths,
valid bounded records, and a truncated tail record. The independent oracle
checks non-overlapping offsets, matched bytes, parse counts, parse failures,
window completeness, and normalized output hashes.

The executable records these workflows independently:

- `exact-signature`: exact `CA FE` occurrences without a byte window;
- `masked-signature`: masked `CA ??` occurrences without a byte window;
- `match-to-parse`: masked candidates with a 16-byte after-window, followed by
  bounded record interpretation.

The first two workflows prove that omitting `window` keeps window metadata
absent. The third records all candidates, successful parsed records, parse
failures, complete windows, incomplete windows, raw durations, allocations,
working set, eligible bytes, and normalized result hashes.

The parse stage is a benchmark-local bounded record parser with the same
magic/length/trailer fixture semantics. It isolates byte-window-to-structured-
record cost; it is not presented as a compiled Musoq query or service timing.
The Search-to-`Interpret` query composition is qualified separately by
`W13-S04`.

## Commands

Correctness-only verification:

```text
dotnet run --project Musoq.DataSources.Search.Benchmarks\Musoq.DataSources.Search.Benchmarks.csproj --configuration Release --no-build -- verify-byte-pipelines
```

Seven-trial measurement:

```text
dotnet run --project Musoq.DataSources.Search.Benchmarks\Musoq.DataSources.Search.Benchmarks.csproj --configuration Release --no-build -- measure-byte-pipelines
```

The benchmark labels filesystem cache state `unknown` unless it was
controlled and observed. Its stopwatch includes traversal, file reads, byte
matching, optional window materialization, interpretation, output
normalization, and hashing; it is not a service or client latency claim.
