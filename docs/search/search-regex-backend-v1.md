# Search regex backend v1

Status: selected and implemented as the internal backend and line-oriented
scanner for `W07-S01` and `W07-S02`, with typed capture rows added by
`W07-S03`, an explicit bounded multiline mode added by `W07-S04`, and
adversarial execution/benchmark coverage added by `W07-S05`. Explicit
start/end framing for bounded multiline records was added by `W09-S03`.

## Selected dialect

Search uses the .NET `Regex` engine with
`RegexOptions.NonBacktracking | RegexOptions.CultureInvariant`. The selected
profile is named `portable-nonbacktracking-v1`. Case-insensitive compilation
adds `RegexOptions.IgnoreCase`; the current invariant word-boundary policy is
part of the cache key and is applied by the later scanner integration.

The backend supports ordinary escaped/literal text, character classes and
ranges, Unicode categories, capturing and non-capturing groups, alternation,
repetition, and anchors/word-boundary expressions supported by the selected
non-backtracking engine. A one-second match timeout is attached to every
compiled instance.

## Rejection and resource policy

Lookaround, backreferences, balancing groups, conditionals, atomic groups,
contiguous-match anchors (`\\G`) and other constructs rejected by the selected
non-backtracking engine are typed `SEARCH-SYNTAX-001` pattern failures. The
diagnostic names the unsupported construct, names the portable
non-backtracking dialect, and directs the caller to rewrite the pattern or use
literal mode. Search never retries the pattern as literal text, silently
enables a backtracking engine, or changes dialects.

Malformed syntax uses the same typed syntax category and identifies the
portable dialect. Patterns are bounded to 65,536 characters and 256 nested
groups before regex construction. The pattern-size and nesting checks bound
individual compilation work; the construction path also records a five-second
compilation budget as a resource failure if that budget is exceeded.

Compiled regexes are immutable and shared only for an exact semantic key:
pattern text, case mode and whole-word mode. A locked 128-entry least-recently-
used cache prevents unbounded retained compiled state and makes concurrent
requests for one resident key share one instance. Eviction may require a later
recompilation; it never changes matching semantics.

## Record scanning

Regex matching runs independently over each complete physical record. Reader
blocks are accumulated until an LF delimiter or end-of-file, so the scanner
does not invent a fixed overlap for arbitrary regex across byte or character
chunks. `^` and `$` therefore apply to the record, not to a reader-block
boundary. The LF delimiter is not searchable; a CR immediately before LF is
treated as the CRLF terminator, while a lone CR remains record content. Line
rows retain their original terminator when one was read.

A regex record is bounded to 1,048,576 UTF-16 characters. Exceeding that limit
is a typed resource failure before the record is passed to the regex engine.
An empty file has no physical records; an empty record between LF delimiters
is still evaluated. The runtime's `Regex.Matches` enumeration supplies
leftmost-first, source-order alternatives and advances after zero-width
matches, while the one-second backend timeout remains attached to each
compiled instance. A timed-out record is reported as a typed resource failure.

## Explicit bounded multiline records

The default record remains one physical line. The internal
`BoundedMultiline` mode without framing treats one complete file as one regex
record, including LF characters, so an explicit pattern can match across
physical lines. Reader blocks are accumulated until EOF and are never exposed
as regex boundaries. The record is limited to 1,048,576 original bytes for
mapped files (or the selected encoding's byte count for already-decoded test
readers); exceeding the limit fails with a typed `recordBytes` resource
diagnostic before regex execution. This compatibility form evaluates a
nonempty unterminated EOF record and does not truncate it to fit a buffer. An
empty file still produces no record.

An internal `SearchRecordFraming` can be supplied with bounded multiline mode
for logs or blocks. Its start and end delimiters are exact physical-line
values (the LF/CRLF terminator is structural and is not part of the delimiter),
matched with ordinal comparison. Text outside a start/end pair is not a logical record; an
end delimiter without a start, or a nested start, is a typed
`SEARCH-SYNTAX-002` framing failure. The default requires the end delimiter;
`AllowUnterminatedEof` explicitly opts into emitting the open payload as an
EOF record. Delimiters are excluded from regex input and from the logical
record byte budget, while payload lines retain their original physical line
number and source coordinates.

Each framed record carries an internal origin, UTF-16 `Start`/`EndExclusive`
range, first physical line number, and termination state. Regex matching uses
the record payload, but match rows continue to expose the existing occurrence
schema; line and record units are not conflated. A framed payload is bounded to
1,048,576 original bytes and is processed as a complete record before regex
execution.

This is a deliberately bounded mode, not an unbounded streaming-regex claim.
The public two-argument literal source constructors and the default
physical-line contract remain unchanged.

## Backend and dialect compatibility

The following table is the compatibility boundary for the current Search
implementation. A later profile must be introduced as a separately named
request option with its own pattern, record, timeout and output limits; it
cannot be reached by fallback from the selected profile.

| Profile | Engine/options | Record scope | Captures | Advanced constructs | Availability |
|---|---|---|---|---|---|
| `portable-nonbacktracking-v1` | .NET `Regex` with `NonBacktracking` and `CultureInvariant`; optional `IgnoreCase` | Physical line by default; explicit bounded multiline records up to 1 MiB | Typed `SearchCapture` values with the final successful repeated-group capture | Lookaround, backreferences, balancing groups, conditionals, atomic groups and `\\G` are rejected with a typed dialect diagnostic | Selected and exercised |
| `backtracking` | .NET backtracking engine | Not defined by the Search contract | Not defined by the Search contract | No implicit access; enabling it would require a separately versioned profile and independent resource limits | Not exposed |
| literal | Existing literal matcher | Physical text records and existing literal source row units | Empty | Not applicable | Separate literal mode |

The safe profile is the only regex profile compiled by Search. The
`W07-S05` adversarial suite exercises a nested-alternation pattern, translates
a deliberately short timeout into the typed resource diagnostic, cancels
while dense match output is being accepted, and confirms that cache eviction
and test reset release compiled instances from cache ownership. The
`measure-regex` benchmark executable records seven cold-compilation and seven
warm-cache trials for pathological and dense-capture workloads; its timing is
an observational safety report, not a cross-engine superiority claim.

## Capture rows

When the `Captures` projection is requested, each regex match exposes a typed
collection rather than pattern-dependent top-level columns. Group zero is
omitted. Named groups retain `GroupName`; unnamed groups use null, and names
accepted more than once share the engine's `GroupIndex`. The selected
non-backtracking profile retains the final successful capture for a repeated
group with `CaptureIndex = 0`; it does not use a silent backtracking fallback
to recover capture history. An unmatched group emits a `Success = false`
placeholder with null text and spans, while a successful empty capture emits
`Success = true`, empty text and zero-length spans. Byte offsets use the same
lossless decoder mapping as the containing match, while UTF-16 columns are
relative to the physical record.

## Verification

`SearchRegexBackendTests` verifies supported non-backtracking options and
source-order alternatives, malformed syntax, lookaround/backreference/
balancing/conditional/atomic rejection, oversized patterns, excessive nesting,
repeated-key compilation, concurrent same-key sharing and cache pressure.
The existing diagnostic validation entry point now uses this backend, so
validation and future execution cannot silently select different regex
semantics.

`SearchRegexMatchingTests` verifies record anchors, source-order and greedy
alternatives, cross-record isolation, zero-width progress, irregular reader
blocks, line grouping, Unicode whole-word boundaries, empty input and the
maximum record bound.

`SearchRegexMultilineTests` verifies cross-buffer multiline matching, the
original-byte record limit, unterminated EOF records and a zero-width match at
the final record boundary. It also verifies exact framed delimiters spanning
reader blocks, delimiter-like payload, origin/range metadata, strict versus
allowed EOF framing and framed-record byte limits.

`SearchRegexAdversarialTests` verifies safe-profile behavior on pathological
input, timeout translation and cancellation during dense match output. The
`measure-regex` benchmark command records the corresponding correctness,
allocation and timing observations with the selected dialect and cache policy.

`SearchRegexCaptureTests` verifies optional unmatched versus successful empty
groups, multiple groups, repeated-group behavior, duplicate names,
projection-elision, UTF-8 byte spans and UTF-16 coordinates. The typed
collection is exposed as a stable `Captures` property and can be expanded by
the host's `CROSS APPLY` property expansion.

Source authority: `S14`, `S22`, `S23`, `S24`; semantic contract:
[`search-match-semantics-v1.md`](search-match-semantics-v1.md).
