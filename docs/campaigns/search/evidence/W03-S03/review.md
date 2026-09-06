# W03-S03 independent review

Verdict: approved

Reviewer: separate `grill-code` review pass (no independent subagent was
available)

## Scope reviewed

- `Musoq.DataSources.Search.Tests/RipgrepComparator.cs`
- `Musoq.DataSources.Search.Tests/RipgrepComparatorTests.cs`
- configured ripgrep executable identity and captured version/help output
- W03-S02 reference-scanner boundary and existing Search row semantics

## Findings

No material correctness, ownership-boundary, process-lifecycle, or comparator
contract findings remain for this scope.

The comparator starts the configured executable with `UseShellExecute=false`
and `ProcessStartInfo.ArgumentList`, drains stdout and stderr independently,
waits for process completion and retains the exact argv and exit state. It
uses one `--json --no-config --no-ignore --fixed-strings` invocation with one
`-e` argument per pattern, so multi-pattern baselines do not fan out into
avoidable per-pattern processes.

The JSONL parser accepts ripgrep's begin, match, context, end and summary
message kinds, requires the structured fields used for normalization, decodes
base64 byte values, rejects malformed/truncated output and rejects missing or
duplicate summaries. Exit 0 (matches), exit 1 (no matches) and exit 2 or other
operational failures remain distinct. Normalization preserves relative path,
line, absolute byte offset, submatch byte span and value representation while
labeling byte-offset, line-grouping, missing-pattern-id and base64 dialect
differences explicitly.

The tests exercise the actual pinned executable for version/build/help,
matched multi-pattern output with three submatches from one process, no-match
exit 1, missing-root operational exit 2 and binary JSON base64 output. The
test-owned harness creates and removes only uniquely named temporary roots and
does not modify production Search code or any external checkout.

## Residual boundaries

- This is a test-owned comparator/baseline harness, not a production backend
  selection or Search execution path. W03-S04 owns measured backend choice.
- The pinned executable is supplied by the campaign's local environment
  configuration or explicit test environment override; a machine without
  ripgrep cannot run these campaign integration tests.
- The comparator intentionally uses fixed-string, non-context, line-oriented
  JSON output. Regex, multiline, encoding, ignore-policy and richer dialect
  comparisons remain later scopes.
- The repository-wide suite retains its existing 31 classified skips and
  package advisories; no package was published.

## Verification

- `dotnet test Musoq.DataSources.Search.Tests\\Musoq.DataSources.Search.Tests.csproj --configuration Release --no-restore`: exit 0; 42 passed, 0 failed, 0 skipped.
- `dotnet test --configuration Release`: exit 0; 21 project summaries; 1,326 discovered, 1,295 passed, 0 failed, 31 skipped.
- `dotnet build Musoq.DataSources.Search.Tests\\Musoq.DataSources.Search.Tests.csproj --configuration Release --no-restore`: exit 0; 0 warnings, 0 errors.
- configured `rg.exe --version`: exit 0; ripgrep 15.2.0, revision `e89fff89ac`.
- configured `rg.exe --help`: exit 0; structured JSON, multi-pattern, binary/text and fixed-string options retained.
- `git diff --check`: passed before evidence and commit finalization.
