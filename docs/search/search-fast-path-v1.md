# Search literal fast path v1

This document records the first measured tuning boundary for the implemented
single-literal Search sources. It is an implementation note for `W05-S05`, not
a product-wide ripgrep parity claim. The external comparison described below
is archived evidence only and is not an active benchmark or release gate.

## Tuned ownership

The source creates one `LiteralMatcher` and one match-span staging list per
scan, then resets and reuses them for each eligible file. The matcher state is
reset before every file, so a literal suffix at one file boundary cannot join
with a prefix in the next file. Row staging is lazy: a sink that emits no rows
does not allocate a row list merely because an eligible file was visited.

The span list remains bounded by the largest reader block's matches. Published
row chunks still receive an owned array before staging storage is cleared, and
the pooled character buffer remains owned by the enclosing scan.

## Measurement cohorts

The historical benchmark executable measured the same UTF-8 literal result
unit against a pinned ripgrep invocation for three deterministic cohorts:

- `B02` — no requested literal, so the expected occurrence count is zero;
- `B03` — sparse literal hits, one matching line in every seventeen;
- `B04` — dense literal hits, four non-overlapping occurrences per line.

Each cohort has seven randomized paired trials in both an unwarmed-invocation
and a post-warmup-invocation phase. The filesystem cache is labeled `unknown`
on the unprivileged Windows host. The benchmark records fixture digest,
eligible bytes, normalized result hash/count, allocations, working set and
raw durations.

The managed feasibility runner attributes traversal, read/decode, matching plus
candidate row construction, and final result materialization. The external
ripgrep process exposes only its process/bridge and JSON normalization boundary;
its native traversal, read and matching stages are not inferred from outside
the process.

## Optional details and allocation regression

The focused Search tests exercise the same occurrence input through the
occurrence sink, which does not request line text, and the line sink, which does.
They assert that optional line text changes materialization but not the
occurrence set, and that single-literal no-match allocations remain flat as the
input grows. These are semantic and allocation-slope guards, not an absolute
machine-specific latency threshold.

The historical measurement is deliberately provisional. It does not ratify
the frozen parity targets, controlled cold-cache behavior, native Rust
execution, regex performance or service/client latency. Current qualification
uses managed-only Search tests and benchmarks.
