# W11-S03 review

Review method: separate grill-code review pass (no independent subagent was available).

Decision: approved with the buffered sequential backend retained; no mmap production rollout.

## Scope and ownership

- The production Search implementation remains unchanged: it continues to use `FileStream` with `FileOptions.SequentialScan`, `StreamReader`, strict encoding handling, pooled buffers and byte-coordinate tracking.
- The new memory-mapped reader is an evaluation-only probe in the benchmark assembly. It is not exposed by the datasource and cannot silently change backend selection.
- The benchmark compares exact raw-byte counts and SHA-256 hashes for the same stable files. It does not claim that raw I/O alone predicts complete Search latency.

## Correctness and lifecycle review

- The mapped view uses the captured file length rather than a zero-size view, avoiding page-sized reads for tiny files and preventing view overrun beyond the eligible file length.
- Empty files complete without attempting an invalid zero-length mapping.
- Both backends check cancellation before opening and between reads; cancellation is reported as `Cancelled`, never as a complete result.
- Mapping/open failures are reported as `Failed` without a buffered fallback that could hide the selected backend failure.
- The probe disposes the view stream before the mapping and disposes the underlying file when ownership remains with the probe.
- A length or last-write timestamp change is reported as `MutationDetected`; partial bytes are not labeled a stable successful read.
- The mutation test permits the operating system to reject a write against an active map; either that refusal or explicit mutation detection is a safe outcome for this evaluation.

## Performance and safety review

- The final measurement uses seven randomized-order trials over a seven-byte file and a deterministic 16 MiB file, with two measured iterations per trial and allocation counters.
- The latest run reports mmap at 1.693x buffered time on the 16 MiB file and approximately 1.006x buffered allocations; the tiny-file setup is also slower.
- The current fingerprint check is not an immutable snapshot or a proof against every same-size content race. That limitation is explicit and is why the view-stream candidate is not integrated into the production Search contract.
- The probe opens files with sharing that permits the controlled mutation fixture; production Search's narrower sharing and decoding/coordinate requirements are not silently represented as satisfied by this raw-byte experiment.

## Required coverage

The focused tests cover stable tiny and large files, empty files, change during a mapped read, missing-file mapping failure, and cancellation for both backends. The Search suite and repository-wide Release suite also pass after the probe changes.
