# Search agent friction regressions v1

This catalog turns the deterministic first-attempt probes into regression
assertions. It deliberately records `observed: false` for every entry: the
checkout has no fresh-agent runner, so anticipated friction is not reported as
an empirical failure. The machine-readable catalog is
[`search-agent-friction-regressions-v1.json`](search-agent-friction-regressions-v1.json).

The regressions protect the boundaries most likely to be lost during repair:
occurrence versus line/file cardinality, metadata-only paths, transport
escaping, glob versus LIKE dialects, unsupported semantics, unreadable or
budget-limited inputs, cancellation, residual predicates and untrusted text.
Each future observation must retain the complete trace and independent
evidence coordinates before it can be counted as a failure or a successful
repair.

## Holdout freeze

[`search-holdout-freeze-v1.json`](search-holdout-freeze-v1.json) commits the
holdout task identity and a seed commitment, but stores no hidden bytes,
answers or validator source. Materialization is external-runner-only and must
use fresh variants, fresh contexts and at least three trials per task. The
commitment is a boundary record, not a claim that holdout trials have run.
