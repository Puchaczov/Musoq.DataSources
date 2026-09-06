# Search byte-pattern contract v1

This document specifies the byte-pattern input compiled by the
`search.bytes` source. The streaming source, constructor metadata, compiled
query behavior and bounded same-read windows are implemented by W13-S03.
Search-to-binary-interpretation composition is verified by W13-S04; see
`search-interpretation-composition-v1.md` for the APPLY shape and failure
evidence contract.

## Transport

The pattern is a single JSON scalar. It is not an SQL integer and is not
inferred from a numeric literal such as `0x90`.

```json
{
  "version": 1,
  "bytes": "48 8b 90 89",
  "mask": "ff ff f0 ff",
  "window": {
    "beforeBytes": 16,
    "afterBytes": 32
  }
}
```

The object has these fields:

| Field | Required | Meaning |
|---|---:|---|
| `version` | yes | Must be the integer `1`. |
| `bytes` | yes | An explicit byte string. Two hex nibbles represent one byte; ASCII whitespace is ignored. |
| `mask` | no | A same-length byte string where one bits compare and zero bits are wildcards. |
| `window` | no | An object requesting bounded bytes before and after each match. |

`window` may contain non-negative integer `beforeBytes` and `afterBytes`
properties; omitted values are zero. The total requested window, including the
matched pattern, is limited to 1 MiB. A request over that limit is rejected
before any input file is opened.

The compiler preserves byte order exactly as written. It never converts a
multi-byte value according to host, little-endian or big-endian rules.

## Byte and mask syntax

`bytes` may be compact (`488b90`) or separated (`48 8b 90`). Each byte has
two positions. A `?` in either position is a wildcard nibble: `4?` compiles
to byte `40` with mask `f0`, `?f` compiles to `0f` with mask `0f`, and `??`
compiles to byte `00` with mask `00`.

An explicit `mask` uses two hex nibbles per byte and can express nibble masks
such as `f0` or `0f`. It cannot be combined with wildcard `?` characters in
`bytes`; callers must choose one mask notation so the request has one
unambiguous source of truth. An all-zero mask is valid and compiles to a
wildcard pattern; the later scanning contract must apply its normal bounded
output/resource policy to that high-cardinality case.

The `0x` prefix, odd-length hex, invalid hex characters, empty values,
length-mismatched masks, unknown properties, duplicate properties and
unsupported versions are rejected before any input file is read. Diagnostics
identify the `pattern` argument and explain the correction.

## Compilation result

Compilation produces two immutable, equal-length sequences:

```text
Bytes[i]  -- literal byte value at position i
Masks[i]  -- bits that must compare at position i
```

A later scanner matches a source byte `value` at position `i` when:

```text
(value & Masks[i]) == (Bytes[i] & Masks[i])
```

No text decoding, Unicode coordinate inference or endianness conversion is
part of this contract.

## Bounded same-read windows

When `window` is present, every occurrence row reports
`WindowStartByteOffset`, `WindowByteLength`, `WindowComplete` and optionally
`WindowBytes`. The window is rooted at the match start and includes the matched
bytes: its requested range is
`[ByteOffset - beforeBytes, ByteOffset + ByteLength + afterBytes)`.

The scanner retains only the bounded before/after region and the current read
block; it does not load the file or reread it once per field. At beginning or
end of file, the range is clipped to available bytes. `WindowComplete` is false
when either requested side was clipped and true only when the full requested
range was available. `WindowBytes` is immutable at the row boundary and is
materialized only when projected. With no `window` request, all four window
fields are null.
