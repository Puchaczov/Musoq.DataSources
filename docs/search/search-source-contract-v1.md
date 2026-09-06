# Search source contract v1

Status: proposed design contract for `W01-S01`. This document describes the
row units that the `Search` datasource exposes or is intended to expose. The
file-backed text audit operation is implemented by `W12-S02`, and the raw-byte
occurrence source with bounded same-read windows is implemented by `W13-S03`;
this is not evidence that every proposed `search.*` method is already
installed or executable.

The machine-readable form of this contract is
[`search-source-contract-v1.json`](search-source-contract-v1.json). The
PowerShell contract test exercises the golden cardinality examples without
depending on a scanner implementation.

## Contract rules

- A source method names its relational unit. SQL projection does not change
  that unit or deduplicate rows.
- Occurrences, matching lines, matching files, complete per-file counts,
  eligible paths, byte occurrences and coordinated labeled occurrences are
  separate units.
- `Path` identifies an eligible file. `Origin` is null for an ordinary local
  file and is required when a future virtual/container source has a distinct
  origin identity.
- `PatternId` is null for a single-pattern call and required for
  `search.many`. Equal pattern text under different IDs remains independent.
- `ByteOffset` and `ByteLength` always refer to original input bytes. They are
  not UTF-16 positions. `Utf16Column` and `Utf16Length` count UTF-16 code
  units and are named separately.
- Text matching defaults to literal, case-sensitive search with compact row
  details. The exact overlap, regex, encoding and scope policies are frozen by
  the subsequent W01 scopes before implementation.
- `long` counters are 64-bit. A count row is emitted only after its file has
  been completely processed; an incomplete file cannot be reported as an
  exact zero or partial count.

## Proposed call shapes and units

These are design notation only. They must not be advertised as SQL until
constructor metadata, XML and compiled-query tests agree.

| Method | Design call | Row unit | Cardinality | Reads content |
|---|---|---|---|---|
| `matches` | `search.matches(root, pattern)` | One occurrence of one pattern | One row per `(Path, PatternId, MatchIndex)` | Yes |
| `lines` | `search.lines(root, pattern)` | One physical line containing a match | One row per `(Path, PatternId, LineNumber)` | Yes |
| `files` | `search.files(root, pattern)` | One file satisfying a positive match | One row per matching `(Path, PatternId)`; a file is emitted once | Yes |
| `counts` | `search.counts(root, pattern)` | One completely processed eligible file | One row per eligible path, including zero-hit files | Yes |
| `paths` | `search.paths(root)` | One eligible regular-file path | One row per eligible path; no content reads | No |
| `many` | `search.many(root, requestJson)` | One occurrence of one labeled pattern | One row per `(Path, PatternId, MatchIndex)` | Yes |
| `bytes` | `search.bytes(root, patternJson)` | One occurrence of a byte pattern | One row per `(Path, PatternId, MatchIndex)` | Yes |

`search.bytes` accepts a versioned JSON scalar containing the explicit hex
byte-pattern and optional bounded-window schema in `search-byte-pattern-contract-v1.md`; an SQL integer is
not a byte-array literal and no endianness is guessed. `search.many` accepts a
bounded labeled pattern array in a JSON scalar. Its complete input schema is
deliberately ratified by `W01-S05`, rather than inferred from a SQL map.

## Common defaults and outcome accounting

The following defaults apply to this proposal:

| Concern | Default |
|---|---|
| Text mode | `literal` |
| Case | `sensitive` |
| Row details | `compact`; optional match/line text is null unless retained |
| Context | zero lines; `Captures` and `Context` are empty |
| Single-pattern `PatternId` | null |
| Count rows | include a zero row for every eligible file after complete processing |
| Path rows | eligible regular files only; content-open count is zero |
| Scope | resolved and fingerprinted by `W01-S03`; no overload may silently choose another scope |

The scan-level counters are defined independently of emitted row counts:

| Counter | Meaning |
|---|---|
| `EligibleFiles` | Files admitted by the resolved scope policy |
| `FilesRead` | Eligible files opened and processed |
| `FilesMatched` | Distinct matching `(Path, PatternId)` values |
| `MatchingLines` | Distinct matching `(Path, PatternId, LineNumber)` values |
| `Occurrences` | All emitted occurrence rows, including repeated paths |
| `BytesScanned` | Original input bytes scanned |

For a single pattern, `PatternId` is absent from the logical key. For
`many`, counters and cardinalities are pattern-labelled. A physical line that
matches two pattern IDs contributes two logical matching-line values.

Terminal audit outcomes are `QuerySatisfied`, `ScopeExhausted`, `Failed` and
`Partial`. The completion/failure rules are detailed in `W01-S04`; this
contract requires that an ordinary no-match result not be confused with a
missing-root or unreadable-input failure.

## SELECT-star schemas

The tables below list the declared fields. `SELECT *` means every scalar field;
collection-valued fields such as `Captures` and `Context` are explicitly
projected and can then be expanded with `CROSS APPLY`. Selecting only `Path`
never changes the number of source rows.

Notation: `string?`, `long?` and `byte[]?` are nullable; `long` is a signed
64-bit integer; `SearchCapture` and `SearchContextLine` are the typed
collections defined below.

### `matches`

One row is retained for every selected occurrence. `MatchIndex` is a zero-based
ordinal per `(Path, PatternId)` in scan order. Exact match-selection and
overlap rules are deliberately owned by `W01-S02`.

| Field | Type | Default/null rule |
|---|---|---|
| `Path` | `string` | Required stable path identity |
| `Origin` | `string?` | Null for an ordinary local file |
| `PatternId` | `string?` | Null for the single-pattern method |
| `MatchIndex` | `long` | Zero-based ordinal per path/pattern |
| `ByteOffset` | `long?` | Original-byte start when losslessly mapped |
| `ByteLength` | `long?` | Original-byte length when losslessly mapped |
| `LineNumber` | `long?` | One-based physical line when mapped |
| `Utf16Column` | `long?` | Zero-based UTF-16 code-unit column |
| `Utf16Length` | `long?` | UTF-16 code-unit length |
| `MatchText` | `string?` | Null under compact details; retained when requested and decodable |
| `LineText` | `string?` | Null under compact details; retained when requested and decodable |
| `Captures` | `IReadOnlyList<SearchCapture>` | Empty for literals or when not requested |
| `Context` | `IReadOnlyList<SearchContextLine>` | Empty when context is zero |

Each `SearchCapture` represents one capturing-group result, excluding group
zero (the complete match). `GroupIndex` is the one-based engine group number;
`GroupName` is null for an unnamed capturing group. The selected portable
non-backtracking profile retains the final successful capture for a repeated
group, so its `CaptureIndex` is zero; it does not silently switch to a
backtracking engine to recover capture history. Duplicate names that the
engine accepts share their engine group number and follow the same rule.
An unmatched optional group contributes one placeholder row with
`Success = false`, null `Text` and null spans. A successful empty capture has
`Success = true`, `Text = ''` and zero-length spans, so it is not confused
with an unmatched group.

| `SearchCapture` field | Type | Default/null rule |
|---|---|---|
| `GroupName` | `string?` | Null for unnamed groups |
| `GroupIndex` | `int` | One-based engine group number; group zero is omitted |
| `CaptureIndex` | `int` | Zero for the selected safe profile's group result |
| `Success` | `bool` | False only for an unmatched-group placeholder |
| `Text` | `string?` | Null when unmatched; empty for a successful empty capture |
| `ByteOffset` | `long?` | Original-byte start when losslessly mapped |
| `ByteLength` | `long?` | Original-byte length when losslessly mapped |
| `Utf16Column` | `long?` | Zero-based UTF-16 column within the containing record |
| `Utf16Length` | `long?` | Capture length in UTF-16 code units |

`matches` counters are `Occurrences`, distinct matching lines and distinct
matching files. Zero-hit files do not produce occurrence rows.

### `lines`

There is one row for a physical line even when that line contains several
occurrences. `OccurrenceCount` preserves the distinction between line and
occurrence counts.

| Field | Type | Default/null rule |
|---|---|---|
| `Path` | `string` | Required stable path identity |
| `Origin` | `string?` | Null for an ordinary local file |
| `PatternId` | `string?` | Null for the single-pattern method |
| `LineNumber` | `long` | One-based physical line number |
| `ByteOffset` | `long?` | Original-byte start of the line when mapped |
| `LineText` | `string?` | Decoded line by default when retained; null only when not retained or not decodable |
| `OccurrenceCount` | `long` | Number of occurrences for this pattern on this line |

`lines` row count is `MatchingLines`; the sum of `OccurrenceCount` is the
occurrence count. Zero-hit files do not produce line rows.

### `files`

`files` is existential: after the first qualifying occurrence, the file may be
completed for this source without emitting the remaining occurrences.

| Field | Type | Default/null rule |
|---|---|---|
| `Path` | `string` | Required stable path identity |
| `Origin` | `string?` | Null for an ordinary local file |
| `PatternId` | `string?` | Null for the single-pattern method |

Its row count is `FilesMatched`. Zero-hit files do not produce file rows.

### `counts`

`counts` emits exact rows only after complete processing of each eligible file.

| Field | Type | Default/null rule |
|---|---|---|
| `Path` | `string` | Required stable path identity |
| `Origin` | `string?` | Null for an ordinary local file |
| `PatternId` | `string?` | Null for the single-pattern method |
| `OccurrenceCount` | `long` | All occurrences in the file, including zero |
| `MatchingLineCount` | `long` | Physical lines containing one or more occurrences |
| `BytesScanned` | `long` | Original input bytes scanned for the file |
| `Complete` | `bool` | Always true for an emitted exact count row |

The row count is `EligibleFiles` after complete processing. An incomplete file
is not represented as an exact count row.

### `paths`

`paths` performs path discovery only. It includes eligible zero-hit files and
does not open file content.

| Field | Type | Default/null rule |
|---|---|---|
| `Path` | `string` | Required stable path identity |
| `Origin` | `string?` | Null for an ordinary local file |
| `EntryKind` | `string` | `file` for this source |

Its row count is `EligibleFiles`; the content-open counter is zero.

### `many`

`many` uses the occurrence schema of `matches`, but `PatternId` is required
and is part of row identity. It has the fields `Path`, `Origin`, `PatternId`,
`MatchIndex`, `ByteOffset`, `ByteLength`, `LineNumber`, `Utf16Column`,
`Utf16Length`, `MatchText`, `LineText`, `Captures` and `Context`, with the
same types and null rules as `matches` except for required `PatternId`.

The row count is the sum of occurrences for all labels. Equal pattern text
under distinct IDs is not deduplicated. Duplicate IDs are an input-validation
error to be specified by `W01-S05`.

### `bytes`

`bytes` reports raw input spans. Text coordinates are not inferred from raw
bytes.

| Field | Type | Default/null rule |
|---|---|---|
| `Path` | `string` | Required stable path identity |
| `Origin` | `string?` | Null for an ordinary local file |
| `PatternId` | `string?` | Null for the single-pattern method |
| `MatchIndex` | `long` | Zero-based ordinal per path/pattern |
| `ByteOffset` | `long` | Original input byte start |
| `ByteLength` | `long` | Original input byte length |
| `MatchedBytes` | `byte[]?` | Null under compact details; materialized when requested |
| `WindowStartByteOffset` | `long?` | Actual bounded window start after beginning-of-file clipping; null without a window request |
| `WindowByteLength` | `long?` | Actual bounded window length after end-of-file clipping; null without a window request |
| `WindowComplete` | `bool?` | False when a requested side was clipped at a file boundary; null without a window request |
| `WindowBytes` | `byte[]?` | Null under compact details; immutable bounded bytes materialized when requested |
| `LineNumber` | `long?` | Null unless byte-to-text mapping is explicitly supported |
| `Utf16Column` | `long?` | Null for raw byte matches |
| `Utf16Length` | `long?` | Null for raw byte matches |
| `MatchText` | `string?` | Null for raw byte matches |
| `LineText` | `string?` | Null for raw byte matches |
| `Captures` | `IReadOnlyList<SearchCapture>` | Empty |
| `Context` | `IReadOnlyList<SearchContextLine>` | Empty |

The row count is the number of byte-pattern occurrences. Overlap selection is
part of the match-semantics contract. A requested window is rooted at the
match start and includes the match plus the declared before/after bytes. The
total requested range, including the match, is bounded to 1 MiB. It is clipped
at BOF/EOF and marked incomplete rather than silently claiming the requested
range was available. The scanner retains only bounded same-read state and does
not reread a file once per output field.

## Fresh audit summary

The implemented companion operation is `search.audit(root, pattern)`. Every
invocation starts a new count scan and returns one summary row containing the
terminal facts for that invocation:

| Field | Type | Default/null rule |
|---|---|---|
| `Root` | `string` | Requested root representation |
| `ScanId` | `string` | New opaque identifier for each invocation |
| `ScopeFingerprint` | `string` | Stable fingerprint of the requested scope |
| `Outcome` | `SearchOutcome` | One terminal outcome for this invocation |
| `TerminalReason` | `string` | Stable terminal reason |
| `Complete` | `bool` | True only under the declared completion rules |
| `ScopeExhausted` | `bool` | Whether the eligible scope was exhausted |
| `QuerySatisfied` | `bool` | Whether an explicit positive query demand was satisfied |
| `CountsExact` | `bool` | Whether counters are exact for the resolved scope |
| `ScopeResolved` | `bool` | Whether the requested root was resolved |
| `VisitedFiles` | `long` | Number of visited file candidates |
| `EligibleFiles` | `long` | Zero when no files are eligible |
| `FilesOpened` | `long` | Number of content files opened |
| `FilesRead` | `long` | Zero before a file is opened |
| `FilesCompleted` | `long` | Number of files completed by the scan |
| `FilesFailed` | `long` | Number of files that failed during processing |
| `BinaryFilesSkipped` | `long` | Number of files skipped by binary policy |
| `BytesScanned` | `long` | Zero before input is read |
| `FilesMatched` | `long` | Zero when no file has a qualifying occurrence |
| `MatchingLines` | `long` | Zero when no physical line matches |
| `Occurrences` | `long` | Zero when no occurrence matches |
| `ObservedRows` | `long` | Rows observed by the underlying count scan |
| `FailureCode` | `string?` | Null unless a typed failure/partial outcome has a code |
| `FailurePath` | `string?` | Null unless a bounded failure path is available |

The audit method is not a cache, history table or global mutable state. Two
identical invocations may have equal counters, but they have distinct scan
identities and each reports its own execution. The source performs the fresh
scan before emitting its single row; ordinary Search sources retain their
existing strict failure behavior.

## Golden cardinality examples

All examples use this fixture:

```text
src/a.txt:     TODO TODO\n
src/empty.txt: [empty]
src/none.txt:  DONE\n
```

For `search.matches('./fixture', 'TODO')`, the rows are the two occurrences in
`src/a.txt`. Therefore `SELECT Path` returns
`src/a.txt`, `src/a.txt`—two rows, not one. `search.lines` returns one row for
line 1 with `OccurrenceCount = 2`; `search.files` returns one row; and
`search.counts` returns three rows, including zero rows for `src/empty.txt`
and `src/none.txt`. `search.paths` returns all three eligible paths without
opening content.

For `search.many` with labels `todo-primary` and `todo-secondary`, both having
the literal `TODO`, each label independently emits two occurrence rows. The
combined result has four rows; equal text is not a reason to collapse labels.

These examples are executable as a contract test in
[`Test-SearchSourceContract.ps1`](../../scripts/search/Test-SearchSourceContract.ps1).

## Authority and deferred decisions

This scope follows the supplied Runtime-v2 plugin guide: source metadata and
typed rows are static contracts, while row sources emit typed chunks. It also
preserves the source distinctions observed in structured search output:
match events can contain several submatches on one line, summary counters
separate matching lines from matches, and raw byte payloads need explicit
encoding/offset treatment.

The referenced Aho-Corasick documentation demonstrates that overlapping,
leftmost-first and leftmost-longest choices are materially different; the
algorithm is therefore not selected by this document. Literal/regex selection,
scope/ignore behavior, completion outcomes and request validation are
subsequent W01 scopes. No backend behavior is silently promoted to this
contract.

Source references: `S14`, `S15`, `S20`, `S22`, `S23`.
