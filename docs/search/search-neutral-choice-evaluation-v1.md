# Neutral Search choice evaluation v1

This scope defines the neutral evaluation protocol for Search, `rg` and
ordinary local tools. It is a scoring contract, not a product-preference
claim. The machine-readable protocol is
[`search-neutral-choice-evaluation-v1.json`](search-neutral-choice-evaluation-v1.json),
and the scorer is [`Measure-SearchNeutralChoice.ps1`](../../scripts/search/Measure-SearchNeutralChoice.ps1).

## Design

The selected slice contains fifteen tasks: three each for direct grep,
compositional work, semantic boundaries, resource boundaries and cost traps.
Each fresh trial receives all three arms under equivalent permissions and
budget, with arm descriptions and order randomized and recorded. The task
selection explicitly allows `rg` to win for direct lexical/path work, accepts
an equivalent ordinary workflow where appropriate, and does not force Search
for relational work when another tool can establish the same evidence.

Answer correctness, evidence validity, completeness awareness and tool-choice
appropriateness are separate fields. An accepted tool still fails the primary
choice score when it produces an incorrect answer, incomplete negative claim
or unsafe action. Failed, partial, cancelled, budget-limited and
user-intervened trials remain in the denominator.

The scorer reports per-trial rows, category totals, `rg` wins, failure counts
and 95% Wilson intervals. With no trial input it emits `not_executed` and a
null interval. This checkout has no fresh-context agent runner, so this scope
retains an empty result set rather than inventing preference measurements.
Hidden variants must be generated separately; development expected answers
and validator source are not evaluation-agent context.
