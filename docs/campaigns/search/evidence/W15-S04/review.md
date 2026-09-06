# W15-S04 review

Review method: separate grill-code review pass (no independent subagent was available).

## Verdict

Approved for the repository-owned neutral protocol and scorer. The scope does
not fabricate empirical agent preference data; its unexecuted state and the
missing runner are explicit residuals for later evaluation work.

## Findings

- The protocol selects fifteen benchmark tasks in five balanced categories:
  simple grep, compositional, semantic boundary, resource boundary and cost
  trap. Every task offers Search, `rg` and ordinary tools under the same
  recorded budget and permissions, with randomized arm order per trial.
- Direct lexical/path tasks explicitly allow `rg` to win. Relational tasks
  allow Search or an equivalent ordinary workflow, while semantic and failure
  tasks are scored on the validity of the evidence and terminal state rather
  than on product identity. Search is never a required answer.
- Answer correctness, evidence validity, completeness awareness,
  inappropriate selection and unsafe actions remain separate. The primary
  choice score requires a correct answer, valid evidence, required completion,
  an accepted arm and zero unsafe actions; failed and intervened trials stay
  in the denominator.
- The scorer validates all declared trial fields, arm permutations, duplicate
  trial keys, non-negative measurements and the recorded inappropriate-choice
  flag. It reports category totals, `rg` wins and 95% Wilson intervals, with a
  null interval for zero trials.
- The protocol references the W15-S03 task set without copying prompts or
  answers into the evaluation manifest. Hidden answers, validator source and
  hidden fixture material remain outside evaluated-agent context.

## Verification

- Neutral protocol tests: 5 passed, 0 failed, 0 skipped.
- Scorer smoke run: `not_executed`, 15 selected tasks, 45 required trials,
  0 completed trials and null confidence interval.
- Full Search suite: 359 discovered, 356 passed, 0 failed, 3 skipped.
- Search Release build with warnings as errors: 0 warnings, 0 errors.
- Five registered Search contract harnesses: 27 scenarios and 314 assertions,
  all passed.
- Owning repository Release suite: 1,647 discovered, 1,613 passed, 0 failed,
  34 expected skips, exit code 0.

## Residuals

No fresh-context agent runner is available in this checkout, so no tool-choice
rates, failure samples or empirical confidence intervals are claimed. The
protocol must be supplied with at least three trials per selected task before
its primary score is meaningful; later holdout runs must use fresh hidden
variants. Platform-dependent unreadable and invalid-encoding behavior still
requires platform evidence. No production Search source, sibling repository
or publication state was changed.
