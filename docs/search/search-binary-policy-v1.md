# Search binary classification policy v1

Status: implemented for the current managed file-backed text sources in
`W06-S04`. The current public SQL constructors retain their existing shape;
binary classification is applied by the owning source before its matcher and
its internal skip count is reserved for the later terminal-summary surface.

## Marker and encoding boundary

The text-source binary marker is the decoded `U+0000` character. A raw zero
byte is not independently sufficient when the selected text encoding is
UTF-16, because ordinary ASCII UTF-16 code units contain zero bytes in one
half of each code unit. Classification therefore scans decoded characters
through the same strict UTF-8/UTF-16 reader policy used by matching.

The decision is `skip-and-report`: a marker is a policy-selected binary file,
not a successful text no-match. The current internal `BinaryFilesSkipped`
counter records the decision for the future terminal summary. No text rows are
accepted from the file.

## Complete validation

The default file-backed source performs a bounded-buffer preflight to the first
decoded NUL or successful EOF, then opens the file again for matching only when
the complete decoded input has no marker. There is no fixed prefix heuristic:
markers at the end of a reader block and markers after an otherwise matching
prefix are detected before the sink can receive a row. This prevents a late
marker from causing already-emitted text evidence to be retracted invisibly.

The probe uses the selected strict encoding. Invalid non-marker input remains a
typed source-read failure; arbitrary raw-byte search belongs to the separate
`search.bytes` contract. A supplied `TextReader` factory is already-decoded
input and remains outside this file-byte classification boundary.

## Required cases

Focused tests cover NUL at the last character of a reader block, the first
character of the next block and the following character; both UTF-16 BOM
endiannesses without false positives from encoded zero bytes; an opaque control
payload; and valid UTF-8 binary content whose NUL appears after a matching
prefix. The latter is skipped before any occurrence row reaches the sink.

Source authority: `S14`, `S20`, `S22`, `S24`; semantic basis:
[`search-completion-contract-v1.json`](search-completion-contract-v1.json) and
[`search-encoding-policy-v1.md`](search-encoding-policy-v1.md).
