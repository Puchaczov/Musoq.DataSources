# W13-S01 review

Review method: separate grill-code review pass (no independent subagent was available).

Verdict: approved

## Scope reviewed

This scope adds the internal v1 byte-pattern compiler, its typed diagnostic
catalog entry and focused tests, plus the proposed byte-pattern contract
documentation. The review covered transport validation, duplicate and unknown
JSON properties, explicit hex parsing, nibble wildcards, explicit bit masks,
endianness behavior, resource bounds, immutable compiled storage, source
contract wording and the existing diagnostic inventory.

## Findings

- No blocking correctness, ownership, compatibility, resource-isolation or
  documentation findings were identified.
- The parser accepts only a versioned JSON object with explicit `bytes` and an
  optional same-length `mask`; numeric JSON values and `0x`-prefixed strings do
  not become byte arrays.
- Byte order is preserved exactly as written. There is no host, little-endian
  or big-endian conversion path, and an `endianness` option receives an
  actionable typed diagnostic.
- Wildcard nibbles compile into masks, while an explicit mask is a separate
  mutually exclusive notation. The compiled values and masks are copied into
  read-only memory with equal lengths.
- Input size is bounded before JSON parsing, malformed JSON and duplicate keys
  are rejected, and syntax/argument invalid forms use the stable
  `SEARCH-SYNTAX-003` diagnostic with the `pattern` argument location.
- The proposed `search.bytes` source remains explicitly uninstalled in the
  source contract. Constructor metadata, SQL examples and binary scanning are
  deferred to later W13 scopes rather than being advertised prematurely.

## Residual boundaries

- This scope validates and compiles patterns only; it does not implement file
  scanning or SQL source registration. W13-S02 owns matching and output
  cardinality.
- All-zero masks are valid wildcard patterns by design. A later scanner must
  apply the existing bounded output and match-count policy to their potentially
  high cardinality.
- Diagnostic spans identify the pattern argument/property boundary rather than
  exposing the raw JSON text or leaking untrusted input into exception messages.

## Verification

- Byte-pattern focused tests: 9 passed, 0 skipped, 0 failed.
- Search Release suite: 321 total, 318 passed, 3 platform-conditional skips,
  0 failed.
- Contract harnesses: 27 scenarios and 314 assertions, all passed.
- Exact repository-wide Release suite: 21 projects, 1,605 total tests,
  1,571 passed, 34 classified skips, 0 failed.
- Search test-project Release build with warnings-as-errors: 0 warnings,
  0 errors.
- `git diff --check`: passed; the changed contract JSON parsed successfully.
- No production process execution, package publication, push, release, install,
  sibling-repository edit or public Search constructor change was introduced.

## Known non-blocking baseline warnings

The repository-wide command retains existing package-vulnerability, generated
code and compiler warnings outside this scope. They did not cause failures and
were not changed as part of W13-S01.
