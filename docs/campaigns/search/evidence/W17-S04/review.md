# W17-S04 review

Review method: separate grill-code review pass (no independent subagent was available).

## Verdict

Approved for W17-S04 completion.

## Findings

- The scope is limited to a release-facing migration/claim index and its
  executable documentation checks. No Search provider implementation,
  `os.files`, `flat.file`, shared traversal code, package registry entry or
  publication target was changed.
- The release note derives the public method list from the actual
  `SearchSchema` metadata and the package version from `scripts/release/packages.json`.
  It links the capability, dialect, scope, coordinate, parsing, recipe and
  measured-performance records rather than copying or broadening their claims.
- Unsupported-feature questions have explicit safe answers for incomplete
  negatives, regex execution, table-valued requests, byte/text coordinates,
  parsing, host assembly resolution, legacy-provider compatibility,
  benchmark generalization and publication state.
- The note separates the internal regex backend from the currently
  literal-only public `search.many` execution and preserves the host
  assembly-loading residual recorded by W17-S03.
- The focused Search suite, the three new docs-to-runtime/claim tests and the
  full owning suite pass. The full suite reports 21 project summaries,
  1,634 passed, 34 expected skips and 0 failures.

## Residuals

- The release note is a claim boundary, not publication authorization. Package
  publication and host release remain separate workflows.
- Cross-host generated-query execution remains subject to the installed-host
  assembly-resolution residual documented by W17-S03.
- The performance section intentionally retains observed-cell and
  in-process-only boundaries; it does not claim universal Search superiority,
  cold CLI timing or completed agent evaluation.
