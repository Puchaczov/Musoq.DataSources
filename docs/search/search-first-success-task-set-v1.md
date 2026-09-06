# Search first-success task set v1

Status: development task set from `W15-S03`. It materializes all 40 task
blueprints in `docs/campaigns/search/agent-benchmark.json` as deterministic
development prompts. The machine-readable contract is
[`search-first-success-task-set-v1.json`](search-first-success-task-set-v1.json).

This is not an agent-evaluation result. Every task has `results: []` until a
fresh-context trial is actually run. The development fixture answers are
visible to the task author; hidden variants must be generated separately and
must not be exposed to an evaluated agent.

## Task contract

Each task has a natural-language prompt, a fixture case, a declared result
unit, an intentionally wrong first query or action, and an independently
checkable success action/answer. The first attempt is intentionally useful as a
failure probe: it may use the wrong source unit, lose labels, make an
incomplete negative claim, or choose a tool that cannot establish the requested
semantic fact.

The primary fixture is the seeded `synthetic-search-v1` manifest already used
by Search tests. Its truth subset has five paths, four matching files and 20
TODO occurrences; its count answer includes the zero-hit file. Binary and
Unicode tasks use the manifest's byte and UTF-16 facts. Small inline fixture
recipes cover punctuation, logs, diagnostics and security boundaries without
depending on the current repository's mutable source inventory.

Schema discovery is intentionally bounded to one capability-card, generated
source-description or task-local-column read. The task set does not authorize
catalogue dumping, invented `search.*` methods, or reading an oracle as agent
context. Current Search sources are literal-oriented; regex-only tasks record
the limitation or use a separately verified regex-capable tool rather than
silently pretending that a literal source is regex execution.

## Measurement contract

Record per trial:

- first valid attempt and repair attempts;
- all discovery/query/verification tool calls;
- input and output token volume separately;
- answer correctness and evidence validity;
- completeness awareness for negative, partial and failed scans;
- inappropriate tool selection, unsafe action attempts and user interventions;
- the observed terminal outcome.

`TAKE` is not a scan budget, and positive early rows do not establish an
exhaustive negative. Failed, partial, cancelled and budget-limited runs are
retained as outcomes, never counted as successful complete answers. Aggregate
development trials by task and category only; do not mix them with hidden
holdout trials or claim the campaign's release gates from this unexecuted set.

## Review boundary

The set defines evaluation inputs and scoring fields. It does not add Search
runtime methods, change source semantics, generate hidden holdouts, or report
agent success. The owning Search suite and the campaign's full validation gate
remain the correctness checks for this scope.
