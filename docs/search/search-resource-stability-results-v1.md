# Search resource stability results v1

This is the `W16-S04` resource-stability record for the Search datasource.
The machine-readable contract is
[`search-resource-stability-results-v1.json`](search-resource-stability-results-v1.json).
The raw benchmark output is retained under
[`docs/campaigns/search/evidence/W16-S04`](../campaigns/search/evidence/W16-S04).

## Workloads and observations

The Windows x64 Release runner used a deterministic fixture pinned by its
SHA-256 digest. It exercised:

- 20 repeated scans over 32 files × 64 lines × two matches per line, with
  4,096 rows and one stable normalized result hash;
- four explicit-file growth cases with exactly 1, 8, 64 and 256 rows;
- six retained-evidence scans that materialized context and released readers;
- a 160-pattern regex-cache pressure run against the 128-entry cache,
  including shared-key compilation, eviction/recompilation and reset;
- eight mid-read cancellations, a `maxMatchCount=2` resource-limit failure,
  and eight concurrent user contexts running four rounds each.

Every repeated and growing scan completed with `ScopeExhausted`, exact counts
and no result drift. Cancellation produced the typed cancelled terminal state
with disposed readers. The resource limit produced
`SearchResourceLimitException` with budget code `match-count`. Concurrent
users retained eight distinct scope fingerprints and only observed each
user's visible file and requested pattern.

## Resource interpretation

The harness samples managed heap, total managed allocation, working set,
private memory and supported process handles. After forced collection, the
observed handle count was 390 versus a baseline of 385, within the declared
slack of eight. The benchmark deliberately warms the concurrent worker shape
before taking the baseline because process handle counts include runtime and
thread-pool handles; the metric is therefore evidence for cleanup on this
host, not a portable absolute threshold.

The result supports bounded behavior and cleanup for the exercised in-process
Search paths. It does not establish a universal memory ceiling or qualify a
long-lived service, cross-process execution, or production load profile.

No package was published, pushed, released or installed, and no sibling
repository was edited.
