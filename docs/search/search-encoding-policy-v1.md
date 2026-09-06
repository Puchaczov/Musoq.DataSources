# Search text encoding policy v1

Status: implemented for the current two-argument text sources in `W06-S01`.
The policy is a source-owned decoding boundary; the versioned JSON request
parser that will transport `options.encoding` remains a later scope.

## Modes

The canonical mode names are:

| Mode | Required input | Decoder behavior |
|---|---|---|
| `auto` | A supported BOM may be present | Honors UTF-8, UTF-16 LE or UTF-16 BE BOMs; without a BOM, uses strict UTF-8. |
| `utf8` | No BOM | Uses strict UTF-8. A UTF-8 BOM is not silently accepted under this exact mode. |
| `utf8-bom` | UTF-8 BOM | Uses strict UTF-8 after consuming the BOM. |
| `utf16-le-bom` | UTF-16 little-endian BOM | Uses strict UTF-16 LE after consuming the BOM. |
| `utf16-be-bom` | UTF-16 big-endian BOM | Uses strict UTF-16 BE after consuming the BOM. |

The current SQL constructors keep their existing two-argument shape and use
`auto`. Internal request construction can select a canonical mode so the
reader policy is executable and testable before the later request parser
exposes the option. Compatibility spellings accepted by the internal policy
are normalized to the canonical values; ambiguous `utf-16` and unsupported
code pages are rejected.

`auto` does not guess an encoding from NUL density, locale, file names or a
short text sample. A UTF-16 file without its BOM is therefore interpreted as
strict UTF-8 and may fail or produce no literal match; it is never silently
reinterpreted as UTF-16.

UTF-32 LE/BE BOMs and a BOM that conflicts with an explicit mode produce the
typed `SEARCH-SOURCE-004` unsupported-encoding diagnostic before text is
decoded. A missing required BOM produces the same diagnostic. Invalid UTF-8
or UTF-16 sequences, including an incomplete multibyte/code-unit tail at EOF,
produce the typed `SEARCH-SOURCE-003` source-read diagnostic. Replacement
fallback is disabled.

The reader probes only the BOM prefix, positions the stream after a validated
BOM, and passes a strict decoder to `StreamReader` with framework BOM
auto-detection disabled. This prevents an explicit UTF-8 request from being
silently switched to UTF-16. A supplied test/host `TextReader` factory is
already decoded input and remains outside this byte-policy boundary.

Original-byte coordinates are defined by the follow-on `W06-S02` coordinate
policy. This encoding scope remains responsible only for establishing the
strict decoded source boundary; raw binary matching remains a separate
`search.bytes` concern and this text policy does not classify or search
arbitrary bytes as text.

## Required coverage

`SearchEncodingTests` covers supported BOMs mixed in one scope, strict UTF-8
without a BOM, explicit mode/BOM agreement, no-BOM non-heuristic behavior,
partial UTF-8 and UTF-16 tails, unsupported UTF-32 BOMs and canonical mode
validation.

Source authority: `S14`, `S20`, `S22`, `S24`; semantic basis:
[`search-match-semantics-v1.md`](search-match-semantics-v1.md).
