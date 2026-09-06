# Search benchmark harness v1

Status: frozen historical contract from `W03-S05`. The canonical
machine-readable definition is
[`search-benchmark-harness-v1.json`](search-benchmark-harness-v1.json).
The external ripgrep baseline and Rust feasibility candidate described by
this archive are provenance only; they are not active benchmark or release
gates. Current benchmark execution is managed-only.

This contract freezes what a Search performance result means before any
optimization claim is made. It does not claim that every layer or cohort is
implemented. The W03-S04 feasibility spike measured the in-process matching
layer only; its raw observations remain in the campaign evidence.

## Measurement layers

The harness keeps these boundaries separate:

1. `matching_kernel_equivalent_in_process_sink` — traversal, open/read,
   decoding, matching, result mapping and output serialization/hash.
2. `datasource_chunks` — the scanner plus datasource chunk production and
   writer boundary.
3. `compiled_query` — datasource work plus query compilation and execution.
4. `warm_service_end_to_end` — a request through a ready service, with first
   result and terminal latency recorded separately.
5. `cold_client_end_to_end` — client/service startup, configuration, transport,
   query execution and terminal output.

Scanner-only results must not be presented as service or client results.
Process coldness and filesystem cache coldness are independent axes.

## Frozen measurement rules

- Historical rule: the baseline and candidate receive the same registered eligible input
  manifest, pattern set, semantics and result unit.
- Historical rule: the baseline is one pinned ripgrep invocation per trial for the complete
  registered pattern set; its version, revision and binary SHA-256 are saved.
- Every cell records positive eligible bytes, normalized result hash/count,
  timing boundaries, process/cache labels and complete terminal state.
- Each cell has at least seven complete paired trials. Warmups are excluded
  from statistics but recorded. Pair order is randomized and recorded.
- Report the median per cell and the geometric mean of registered median
  ratios. Preserve raw durations and variance; do not remove outliers after
  seeing results without a pre-registered environmental-fault rule.
- Failed, cancelled, partial and budget-limited runs are evidence of an
  outcome, never successful measurements.

The frozen negative validator rejects mismatched scope or output units, absent
baseline/candidate records, zero-byte scans, incomplete runs/trials, unequal
normalized outputs, missing version or hash identity, insufficient trials and
an unknown filesystem cache state labeled as cold.

## Thresholds and coverage

The initial parity targets are proposed thresholds, not measurements: a 1.25
geomean median ratio, a 2.0 per-cell median ratio, at most 100 ms warm-service
p95 overhead and at most 500 ms cold-client p95 overhead. Semantic parity and
registered coverage are gates before these targets are interpreted.

Core coverage requires cohorts `B01` through `B23`; `B24` is optional. Each
cohort's result unit is fixed in the JSON contract. Matching lines, line
counts and occurrences cannot be substituted for one another.

## Development and completion commands

The Search test project is the normal development loop. The full repository
test command is retained for scope completion and explicitly high-impact
changes. The retained benchmark commands are managed-only measurements:
`measure-decoding`, `measure-regex`, `measure-literal`,
`measure-io-backends`, `measure-match-batches`, `measure-parallel-cost`,
`measure-delivery-latency`, `measure-resource-stability`,
`verify-byte-pipelines` and `measure-byte-pipelines`. The former `verify`,
`measure` and `measure-batched` external-comparison paths are historical and
are no longer active release prerequisites. The archived `W05-S05` `B02`,
`B03` and `B04` observations remain provisional evidence and do not promote
the proposed thresholds.

Source basis: `S20`–`S25`; campaign matrix:
[`docs/campaigns/search/benchmark-matrix.json`](../../docs/campaigns/search/benchmark-matrix.json).
