# W16-S02 review

Review method: separate grill-code review pass (no independent subagent was available).

## Verdict

Approved for the registered current-platform observations and their explicit
residual inventory. The observed ratio targets pass for the 12 comparable
cells, but the record does not claim complete B01-B23 parity, controlled
filesystem cold/warm behavior or universal speed superiority.

## Findings

- The literal runner covers B02, B03 and B04 with the frozen `occurrence`
  result unit. The batched runner covers B11 with the frozen
  `labeled_occurrence` unit, one ripgrep process per trial and unique literal
  labels. These are the only paired workflows promoted into the ratio set.
- Each observed cell retains seven paired trials, positive equal scanned input
  bytes, equal normalized result hashes/counts, process order and terminal
  state. The expected no-match ripgrep exit code 1 is accepted only for B02;
  other matching baselines exit 0.
- Raw JSON output, empty stderr, benchmark build output, runner seeds, fixture
  digests, ripgrep revision/binary hash, candidate assembly identity and host
  metadata are retained and SHA-256 pinned.
- The observed candidate-to-baseline median ratio geometric mean is
  0.17622871462594739 and the maximum cell ratio is 0.410659575113502. These
  pass the registered 1.25 aggregate and 2.0 per-cell targets only for the
  applicable observed cells.
- Regex and decoding runs are retained as candidate-only diagnostics. They do
  not supply an equivalent ripgrep baseline and are therefore not parity
  measurements.
- B05-B09 and B16 have no registered equivalent runner. All nine remaining
  mandatory parity cohorts are listed as residuals rather than omitted.
- No production Search source changed. The scope adds only the normalized
  result index, its documentation, evidence artifacts and contract tests.

## Verification

- Search benchmark Release build: 0 warnings, 0 errors.
- Benchmark `verify`: exit code 0.
- Literal `measure`: 6 cells, 84 paired samples, seven trials per candidate
  per cell, exit code 0.
- Batched `measure-batched`: 6 cells, 42 paired trials, seven trials per cell
  plus one excluded warm-up, exit code 0.
- Regex diagnostic: 28 candidate-only trials, exit code 0.
- Decoding diagnostic: 21 candidate-only trials, exit code 0.
- Parity result contract tests: 3 passed, 0 failed, 0 skipped.

## Residuals and release blocker

Filesystem cache state was unknown because it was not controlled or verified;
the observations are not cold or controlled-warm filesystem evidence. Six
mandatory parity cohorts lack a registered baseline/candidate runner, and
three have candidate-only diagnostics. The ratio result is consequently an
observed-cell result, not a complete matrix gate or product-wide performance
claim. No package was published, pushed, released or installed, and no
sibling repository was edited.
