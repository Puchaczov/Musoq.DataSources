# Search literal scanner v1

Status: implemented in `W05-S02` for the current single-pattern literal
source.

The production scanner uses the managed primitive selected by the feasibility
work: `System.Buffers.SearchValues<char>` locates candidates matching the
literal's first UTF-16 code unit, and ordinal `ReadOnlySpan<char>.SequenceEqual`
checks candidates that are wholly contained in the current reader block.

Candidates that reach the end of a block use the same non-overlapping KMP
state machine as the scalar path. The retained state is the matched prefix
length, prefix table and bounded start-coordinate ring. It is sufficient to
continue a candidate in the next block without copying a whole file or
materializing a concatenated tail.

## Block ownership

- A complete in-block match is emitted while its start block is being
  consumed.
- A match whose final code unit arrives in a later block is emitted once by
  that later block, after the retained prefix state completes.
- After emission, the matcher resets its matched-prefix state and resumes at
  the match end, enforcing the declared leftmost, non-overlapping literal
  semantics.
- A physical newline ends the current record; a partial literal is discarded
  at that boundary, and literals containing a newline produce no text-record
  spans.
- Skipped non-candidate runs still advance the absolute UTF-16 offset and
  physical-line coordinates, including newline transitions.

The reader owns one bounded pooled character buffer. The matcher owns only
pattern-sized state, and rows retain scalar coordinates and the immutable
literal string; no row retains a reader block or pooled array.

Boundary tests compare matcher spans and source rows with the independent
reference scanner for every split of a hand-written fixture, patterns longer
than each block, adjacent and EOF-ending matches, randomized inputs and every
non-zero chunk size in each randomized input.
