# W10-S02 review

Review method: separate grill-code review pass (no independent subagent was available).

## Verdict

Approved. The implementation is bounded to the Search projection/materialization contract and does not introduce a new public API, alter the Search row shapes, or change the `search.many` rejection boundary.

## Review findings

- `SearchSourcePlanner` normalizes and filters the request's required fields against the declared row schema, then copies the same accepted field set into both `SourcePlanResult` and `SourceExecutionPlan`.
- `SearchProjectionRequirements` treats the planned accepted fields as authoritative over Core's complete inferred `AllColumns` schema. Residual-only `MatchText` and `Context` requirements therefore survive until source emission, while direct source callers retain the existing `AllColumns` fallback.
- Match and line sinks construct their optional values before `Emit`. The emitted rows freeze context data, so projection elision cannot leave a required field to be populated by a later mutable-file read. The explicit internal `ExpandContext` evidence seam remains separate and still verifies source identity before and after its opt-in read.
- The planner remains conservative: order pushdown and `search.many` pushdown are not claimed, and accepted metadata predicates remain row-level filters without filesystem traversal pruning.
- The new compiled-query and direct-source tests cover residual text filtering, context required only by residual ordering, wildcard output, repeated path-only occurrences, plan-column precedence, and unchanged coordinates/multiplicity.

No blocking correctness, semantic, ownership, or maintainability findings remain.

## Validation reviewed

- `dotnet build .\Musoq.DataSources.Search\Musoq.DataSources.Search.csproj --configuration Release --no-restore -warnaserror`: passed with 0 warnings and 0 errors.
- Projection-focused Search tests: 10 passed, 0 failed, 0 skipped.
- Full Search suite: 239 discovered, 236 passed, 0 failed, 3 existing platform-conditional skips.
- Exact owning-repository Release suite: 1,523 discovered, 1,489 passed, 0 failed, 34 classified skips.
- `git diff --check`: passed.

The full-suite log retains any compiler/package warnings; they are pre-existing repository/environment diagnostics, not test failures, and the exact suite exited 0.
