# W11-S02 review

Review method: separate grill-code review pass (no independent subagent was available).

Decision: pass.

## Scope and implementation

- The production change specializes only the sensitive, non-whole-word, one-character literal path with `ReadOnlySpan<char>.IndexOf(char)`.
- The existing stateful matcher remains the path for longer literals, case-insensitive matching, whole-word matching, newline-containing literals, and block tails.
- The internal constructor switch exists only to compare the pre-specialization branch in the benchmark and boundary tests; it is not a published API.
- No custom SIMD or architecture-specific intrinsic was added. The BCL span primitive supplies the runtime implementation and its scalar fallback.

## Correctness and lifecycle review

- The specialized loop advances the same absolute character offset, line number, UTF-16 column, cancellation checks, and optional byte-coordinate mapping as the prior branch.
- Tests compare optimized and scalar paths across irregular reader chunks, an unaligned input slice, a near-end slice, surrogate-containing text, newlines, absent matches, adjacent matches, and dense matches.
- The benchmark checks both result count and position hash against an independent `String.IndexOf` oracle for every warmup and measured trial.
- The change does not alter source planning, row cardinality, output ordering, resource ownership, or cancellation contracts.

## Performance review

- The literal benchmark uses a 240,000-line in-memory corpus, seven randomized-order trials, and four measured iterations per trial.
- Stopwatch and `GC.GetAllocatedBytesForCurrentThread` cover matcher construction, the complete in-memory scan, and result count/position-hash normalization; traversal, decoding, and external output serialization are excluded and explicitly labeled.
- Per-invocation total allocations are retained for both paths, including the no-match workload used to check the nonmatching fast path.
- The one-character sparse workload is the only workload directly exercising the new branch. Longer-pattern and multi-pattern measurements are retained as controls, not as universal speed claims.
- The existing multi-pattern benchmark was refreshed for six sparse/dense cells and remains observational; no multi-pattern implementation change is promoted by this scope.

## Limitations

- The run was on an x64 host. ARM64 was not emulated or directly executed; portability is supported by using the BCL primitive and scalar stateful fallback.
- CPU-feature absence was represented by the unchanged scalar comparison path rather than by disabling host hardware features at process level.
- Benchmark timings are local kernel observations and are not a release or end-to-end performance claim.
