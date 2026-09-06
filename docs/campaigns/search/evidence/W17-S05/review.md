# W17-S05 review

Review method: separate grill-code review pass (no independent subagent was available).

## Verdict

Approved for W17-S05 completion.

## Findings

- The terminal scope is limited to core-campaign closure evidence, release-readiness checks and the campaign record. No Search provider implementation, legacy provider, shared traversal code, sibling checkout, publication target or normal user installation was changed.
- The final focused Search suite passed 377 tests with 3 expected platform skips and 0 failures. The final owning-repository Release suite passed all 21 project summaries with 1,634 passed, 34 classified skips and 0 failures.
- The dedicated correctness, parity, latency, resource, neutral-choice, first-success, friction and aggregate agent-report checks passed 26/26 tests. Their result documents retain the observed-current-platform and not-measured external/agent boundaries.
- The package matrix records the exact evaluated Search package 8.0.3-alpha.7, both NuGet artifacts, release metadata, all four RID archives and their SHA-256 digests. Release registry, package identity, license snapshot, archive-license and release-smoke checks passed.
- Every core scope from W00-S01 through W17-S04 has one reachable trailer-bearing completion commit and passed evidence. W17-S05 is the only scope being completed by this commit; the campaign record is included so a later DS-CORE receipt can verify the terminal producer revision without a self-referential hash.
- The evidence does not turn measured cells into a universal faster-than-ripgrep claim, does not infer transport streaming from in-process timings, and does not claim agent usability results when no fresh-context runner exists.

## Residuals

- Nine parity cohorts remain explicit residuals; filesystem cache state is unknown and only the recorded comparable cells are qualified.
- Cold client end-to-end and warm service end-to-end latency remain external, not-run boundaries.
- Agent first-success, repair, neutral-choice, token and confidence trials remain unexecuted because no authorized fresh-context runner is available.
- Three non-Windows platform profiles and a separate full fuzz engine remain unavailable in this checkout.
- The installed Musoq 0.40.0-alpha.14 clean-host check retains the W17-S03 host assembly-resolution residual; this datasource scope does not claim generated-query execution on every host.
- The package artifacts and machine-local build outputs remain ignored local evidence. Nothing was pushed, published, released or installed into the normal profile.

## Review exception

The first archive-license invocation used the release root instead of its `plugins` directory and failed before inspecting an archive. The invocation was corrected to the directory required by the validator and the affected gate then passed; no artifact or source bytes changed.
