Review method: separate grill-code review pass (no independent subagent was available).

# W12-S04 review

## Scope reviewed

- `Musoq.DataSources.Search/SearchDiagnostics.cs`
- `Musoq.DataSources.Search/SearchManyRequestParser.cs`
- `Musoq.DataSources.Search/SearchSourceVersion.cs`
- `Musoq.DataSources.Search.Tests/SearchUntrustedDataTests.cs`
- `Musoq.DataSources.Search.Tests/RipgrepComparator.cs`
- `Musoq.DataSources.Search.Tests/RipgrepComparatorTests.cs`
- `docs/search/search-untrusted-data-v1.md`

## Findings

No blocking correctness, lifecycle, concurrency, contract, security or
ownership findings remain.

Production Search has no external-process invocation. The only process code is
the test-only ripgrep comparator. It disables shell execution, appends every
argument through `ProcessStartInfo.ArgumentList`, terminates the path/options
boundary with `--`, starts both asynchronous output drains before waiting, and
has a bounded timeout with process-tree cleanup. A regression test writes more
than a pipe buffer to both stdout and stderr and completes successfully.

Request property names and rejected enum values are presentation data. Their
diagnostic display escapes C0/C1 controls and Unicode terminal-format
characters, bounds the rendered value, and retains no raw request excerpt in
the diagnostic. Diagnostic reasons and evidence-source exception messages use
the same bounded display helper. The machine-facing diagnostic path remains a
bounded exact value, while `DisplayPath` is the explicit presentation-safe
variant. Search rows likewise preserve source path and content data rather than
rewriting it for terminal output.

Ignore files are parsed as matching rules only. They do not authorize access,
execute text, suppress containment checks, or establish a safety claim about a
candidate file. Existing traversal and containment decisions remain separate.

## Required-case coverage

- leading-dash filename identity remains exact in Search rows;
- control characters in a malicious request key/value cannot create a line,
  prompt or terminal escape in diagnostic presentation;
- quoted, newline and escape-bearing machine paths remain exact while their
  display form is bounded and safe;
- large simultaneous stdout/stderr streams do not deadlock the process oracle;
- process timeout and kill-tree behavior prevent an unbounded wait.

## Residual boundaries

- Windows rejects newline and double-quote filename components. The Windows
  row test therefore uses a leading-dash filename; synthetic diagnostic-path
  coverage exercises quote/newline/control preservation, and Unix retains the
  literal newline filename case.
- `SearchDiagnosticLocation.Path` is intentionally machine data and may contain
  bounded control characters. Terminal/UI consumers must use `DisplayPath`; no
  production Search terminal renderer currently exists.
- The ripgrep process oracle is test infrastructure, not a production Search
  execution feature. Search does not claim that an external tool is available
  or use ignore files as a security boundary.

## Verification

- warnings-as-errors Search build: exit 0; 0 warnings; 0 errors;
- untrusted-data and process-boundary focused tests: 10 passed, 0 failed,
  0 skipped;
- complete Search test project: 298 discovered, 295 passed, 0 failed, 3
  existing platform-conditional skips;
- source contract harness: exit 0; 7 scenarios; 52 assertions;
- completion semantics harness: exit 0; 4 scenarios; 124 assertions;
- request contract harness: exit 0; 4 scenarios; 45 assertions;
- match semantics harness: exit 0; 6 scenarios; 49 assertions;
- scope semantics harness: exit 0; 6 scenarios; 44 assertions;
- exact owning-repository Release suite: exit 0; 21 projects; 1,582
  discovered, 1,548 passed, 0 failed, 34 classified skips;
- separate review verdict: approved.
