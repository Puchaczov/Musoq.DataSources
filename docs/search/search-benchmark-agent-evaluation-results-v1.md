# Search benchmark and agent-evaluation results v1

This is the `W16-S05` publication index for the Search campaign. The
machine-readable record is
[`search-benchmark-agent-evaluation-results-v1.json`](search-benchmark-agent-evaluation-results-v1.json).
It joins the versioned current-platform correctness, parity, latency and
resource records with the neutral agent-evaluation protocol and answer-free
holdout boundary.

## What was measured

The current Windows x64 evidence is internally consistent and keeps result
units separate:

- Correctness passed on the current platform; three other platform profiles
  and a separate full fuzz engine remain unexecuted.
- The parity record has 12 comparable cells across four of 13 mandatory
  parity cohorts. The Search/ripgrep median-ratio geometric mean is
  `0.17622871462594739`, and the largest observed cell ratio is
  `0.410659575113502`. Filesystem cache state is `unknown`. These are
  observed-cell results, not a universal faster-than-rg claim.
- In-process latency records seven trials for direct-source, compiled-query
  cache-miss and compiled-query reuse boundaries. Cold client and warm
  service boundaries were not run because they belong to external hosts.
- Resource stability retains repeated/growing scans, bounded regex-cache
  pressure, cancellation, resource-limit and concurrent-context evidence.
  Post-collection handles were 390 against a warmed baseline of 385, within
  the declared slack of eight. Memory values are diagnostic observations,
  not portable limits.

The aggregate is rebuilt by contract tests from the pinned descriptors and
raw records. Every input path and SHA-256 is recorded in the JSON file, along
with the exact benchmark commands and result-unit rules.

## Agent evaluation boundary

The neutral protocol selects 15 development tasks with three trials each, but
no fresh-context runner is available here, so completed trials remain zero.
The hidden holdout contains 40 tasks with a minimum of three trials each and
stores no answers or fixture material in this repository; it also remains at
zero trials. The twelve friction entries are contract regressions with empty
observations, not empirical agent failures.

Accordingly, this publication makes no Search preference, first-execution
correctness, repair-rate, token-volume or confidence-interval claim. It also
does not promote the frozen performance and latency targets into measurements.
Direct lexical work may appropriately select `rg` or another ordinary tool;
the protocol never forces Search.

No package was published, pushed, released or installed, and no sibling
repository was edited.
