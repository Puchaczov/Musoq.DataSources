# Search live-source mutation contract v1

Search reads a live filesystem tree and does not promise an atomic snapshot.
`ScopeExhausted` means that the eligible paths observed by that execution were
processed; it does not mean that the tree could not change immediately before
or after an individual read.

For each eligible file, the default Search path records a bounded observation
before reading and checks it again before completing the file. The observation
includes canonical path, length, last-write time, creation time and, on
Windows, the volume/file identity obtained from the open handle. Other
platforms retain the metadata checks without inventing a portable inode value.
An observed source change, replacement, deletion or loss of readability is a
typed `SEARCH-SOURCE-005` failure with terminal reason `source-changed`; the
execution is incomplete and `CountsExact` is false. Ordinary open and decoder
failures retain their existing typed source-open/source-read diagnostics.

The boundary is deliberately conservative rather than an atomicity claim. A
mutation after the final check cannot be prevented by a path-backed scan, so a
caller that requires an atomic answer must provide an immutable revision or
otherwise disclosed stable input. Rows produced before a detected failure are
observed prefix evidence only and must not be treated as exhaustive.

Context expansion has a stronger evidence-specific check. Its handle retains a
content hash, creation metadata and the available file identity, validates the
source before reading, streams only the bounded requested window, and validates
again before returning. Appending, truncating, renaming, deleting or replacing
the source therefore rejects an older handle instead of returning newer or
unverified context. Decoder/read failures are returned as typed source-read
failures and never produce a successful expansion.

The mutation and recovery tests use deterministic reader boundaries to exercise
append, truncate, rename, replacement, permission/open denial, decoder failure,
read failure and stale-evidence paths without relying on timing races.

Source basis: `S07`, `S14`, `S15`, `S18`; implementation scope: `W12-S05`.
