# W08-S05 review

Verdict: approved

Reviewer: separate grill-code review pass (no independent subagent was available)

## Scope reviewed

This scope adds a registered benchmark for the actual managed literal
`SearchManyLiteralScan` path and a one-process `rg` JSON baseline. It covers
one, ten and one hundred unique literals over sparse and dense UTF-8 fixtures,
normalizes both workflows to labeled occurrences, records scan and complete
investigation costs, and measures Aho–Corasick automaton structure. It does
not change matching semantics or claim multipattern search as a novel feature.

## Findings

- No blocking correctness, ownership or maintainability findings were
  identified.
- The managed candidate uses one eligible scope traversal and preserves the
  requested pattern identifier, UTF-16 coordinate and matched text in the
  normalized occurrence identity.
- The external baseline uses one fresh `rg` process per trial with one `-e`
  argument per literal. Its JSON submatches are labeled by exact literal text;
  the unique-literal limitation is explicit in the benchmark and documentation.
- All six cells have equivalent result counts and SHA-256 normalized output,
  one recorded warmup and seven randomized complete paired trials.
- Managed scan timing is measurable inside the process. Native `rg` scan time
  is not observable across the process boundary, so the report retains its
  searched bytes and reports the complete process/parse/labeling timing.
- Automaton node, transition and output-reference counts increase across the
  registered 1/10/100 pattern sets; construction allocation observations are
  retained without presenting them as a precise retained-heap measurement.

## Validation reviewed

- Batched benchmark-focused tests: 3 passed, 0 skipped, 0 failed.
- Search focused Release suite: 198 total, 195 passed, 3 platform-conditional
  skips, 0 failed.
- Exact repository-wide Release suite: 21 projects, 1,482 total, 1,448
  passed, 34 classified skips, 0 failed.
- Search production and benchmark builds with warnings-as-errors: 0 warnings,
  0 errors.
- The benchmark executable emitted six complete cells with seven measured
  trials each, recorded warmups, exact fixture digests and ripgrep metadata.
- `git diff --check`: passed.

## Known boundary

The measurements characterize equivalent literal workflows only. They do not
qualify regex, duplicate-literal label inference, filesystem-cold behavior,
datasource chunk delivery, compiled-query latency, service/client latency or a
production performance threshold. The structural automaton counters are an
internal benchmark seam and are not part of the published datasource API.
