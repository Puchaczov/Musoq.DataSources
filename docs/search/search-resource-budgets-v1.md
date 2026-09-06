# Search resource budgets v1

Search keeps work limits separate from query limits and output materialization.
`TAKE` remains a query limit: reaching it can produce `QuerySatisfied`, but it
does not make the unvisited scope exhaustive.

The internal request seam accepts `SearchResourceLimits`. The default preserves
the existing unbounded scope/output behavior while retaining the already
declared pattern, record and context ceilings. A caller that supplies a limit
gets a strict failure when it is exhausted; Search never silently truncates a
successful result.

The stable budget codes are:

| Budget | Failure code |
| --- | --- |
| Total eligible input bytes | `read-bytes` |
| One eligible file's bytes | `file-bytes` |
| Eligible file count | `file-count` |
| Pattern count | `pattern-count` |
| Pattern length or aggregate UTF-8 size | `pattern-size` |
| Regex compilation time | `compile-cost` |
| Physical or bounded record bytes | `record-bytes` |
| Retained context bytes | `context-bytes` |
| Staged result materialization bytes | `output-cap` |
| Matcher occurrence count | `match-count` |

The terminal summary reports `terminalReason=budget-exhausted` and the
specific budget code in `FailureCode`. It therefore cannot be mistaken for
`ScopeExhausted` or an exhaustive negative answer. When an explicit
`partialPolicy=allow` is selected on the internal request seam and rows were
already observed, a work-budget failure is reported as `Partial`; otherwise
the default is `Failed`. Both outcomes remain incomplete and have
`countsExact=false`.

Output materialization is bounded before a row is added to a sink's staging
buffer. Row estimates include retained match text, captures and context, so a
small match can still be rejected when its result amplification would exceed
the in-flight output cap. The cap is not a row-count shortcut and does not
change occurrence multiplicity.

Pattern, record and context budgets are validated before filesystem work when
the request can establish the limit from its arguments. File and total-byte
budgets reserve the eligible file lengths before content processing. Reader,
sink and pooled-buffer cleanup remains protected by the existing `using` and
`finally` boundaries when a budget fails.

Source basis: `S07`, `S14`, `S15`, `S18`; implementation scope: `W12-S03`.
