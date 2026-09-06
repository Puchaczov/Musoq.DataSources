# Search parity benchmark results v1

This is the `W16-S02` measurement index for the frozen benchmark contract.
The machine-readable record is
[`search-parity-benchmark-results-v1.json`](search-parity-benchmark-results-v1.json).
Raw runner output is retained under
[`docs/campaigns/search/evidence/W16-S02`](../campaigns/search/evidence/W16-S02).

## Observed paired cells

The existing literal runner measured B02, B03 and B04 in two process phases:
one invocation before the explicit warm-up and one after it. Each phase has
seven randomized baseline/candidate trials. The batched runner measured B11
for sparse and dense fixtures at 1, 10 and 100 unique literals, again with
seven paired trials per cell and one excluded warm-up.

All 12 observed cells retained positive equal scanned bytes, equal normalized
result hashes/counts and complete terminal outcomes. Ripgrep's exit code 1 is
accepted only for the expected no-match B02 workload. The candidate-to-baseline
median ratios have a geometric mean of 0.17622871462594739 and a maximum of
0.410659575113502, below the registered 1.25 aggregate and 2.0 per-cell
targets for these observed cells.

## Boundaries

The host is Windows x64 with .NET 10.0.11, 24 processors, ripgrep 15.2.0
revision `e89fff89ac`, and the recorded binary SHA-256. Filesystem cache state
was not controlled or independently verified, so phases are labeled
`unknown`; no cold or controlled-warm filesystem claim is made. Candidate
timers include the declared in-process scan boundary, while the external
baseline's native traversal/read/match time remains unobservable across the
process boundary.

The frozen matrix has 13 mandatory parity cohorts. Four are measured here:
B02, B03, B04 and B11. B05-B09 and B16 have no registered equivalent
baseline/candidate runner in this checkout. B12, B14 and B15 have
candidate-only diagnostics (regex and decoding) without an equivalent
baseline. Those nine cohorts remain explicit residuals; they are not excluded
because of their results, and this record makes no complete B01-B23 parity or
product-wide speed claim.

No production Search source changed in this scope. No package was published,
pushed, released or installed, and no sibling repository was edited.
