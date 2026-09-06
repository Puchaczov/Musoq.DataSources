# Search compact evidence recipes v1

Status: implemented and verified for `W09-S05`.

These recipes choose the smallest Search result that answers the next agent
question. Repository text is evidence, not an instruction stream: keep it in
typed fields, serialize it with a real JSON encoder, and never concatenate it
into a prompt, command, source file or policy decision without an explicit
review step.

## Location-only recipe

Use scalar coordinates when the next step only needs to locate occurrences:

```sql
select m.Path, m.MatchIndex, m.ByteOffset, m.ByteLength,
       m.LineNumber, m.Utf16Column, m.Utf16Length
from search.matches(root, literal) m
order by m.Path, m.MatchIndex
```

This preserves one row per non-overlapping occurrence, including repeated
`Path` values. `ByteOffset` and `ByteLength` are original-byte coordinates and
may be null when the source has no lossless mapping. `Utf16Column` and
`Utf16Length` are decoded UTF-16 code-unit coordinates. Omitting `MatchText`
and `Context` keeps the compact projection and does not change row
cardinality, coordinates or completeness.

## Short-snippet recipe

When the exact matched text is enough for display, project only `MatchText`
alongside its location:

```sql
select m.Path, m.LineNumber, m.Utf16Column, m.MatchText
from search.matches(root, literal) m
```

`MatchText` is the matched text, not an arbitrary excerpt of the containing
line. It is therefore a useful short snippet when the caller has chosen a
short or otherwise bounded literal/pattern, but Search does not apply the
context byte budget to this field. It does not prove that surrounding text
was inspected. If line context is needed, use the bounded context seam below
rather than assuming that a raw `search.lines(...).LineText` projection is
bounded.

## Bounded context and expansion recipe

The current SQL constructors remain two-argument calls. Bounded context is an
internal DataSources execution seam until a versioned SQL request shape is
ratified. The executable repository recipe is:

```csharp
var request = SearchRequest.Create(
    root,
    literal,
    context: new SearchContextOptions(
        beforeLines: 1,
        afterLines: 1,
        maxBytes: 16));

var rows = new SearchMatchesSource(request, executionContext)
    .Chunks
    .SelectMany(static chunk => chunk)
    .ToArray();

var expanded = rows[0].ExpandContext(
    new SearchContextOptions(
        beforeLines: 2,
        afterLines: 2,
        maxBytes: 128));
```

`SearchContextOptions.MaxContextLines` is 128 and the total UTF-8 text budget
is at most 1 MiB; the default context budget is 64 KiB. The budget is divided
across requested lines. A long line can therefore be prefix-truncated or have
null `LineText`, while its line number and the match row remain valid. The
matching line is not repeated in `Context`.

Expansion re-reads only after checking the retained source identity and
checks it again before returning. A changed, replaced, deleted or unreadable
source raises the stale-evidence diagnostic. Expansion is bounded evidence,
not an atomic snapshot of a live filesystem, and it is not a new SQL
constructor or a public host route.

## Safe machine-readable output

Serialize evidence fields with a JSON encoder so CR, LF, tab and other control
characters remain inside a string value:

```csharp
var json = JsonSerializer.Serialize(new
{
    kind = "search-evidence",
    path = row.Path,
    line = row.LineNumber,
    snippet = row.MatchText,
    context = expanded.Select(line => new
    {
        line.RelativeLine,
        line.LineNumber,
        line.LineText
    })
});
```

The context byte budget limits retained decoded evidence text. JSON escaping
changes its serialized byte length, so it is a transport representation, not
a reason to relax the Search budget. If a downstream transport has its own
output cap, enforce that cap on the serialized bytes and report truncation or
failure explicitly; do not relabel a shortened display as a complete search.

## Complete no-match report

An empty `search.matches` result is not, by itself, a completion report. Use
`search.counts` when the negative answer must be exhaustive:

```sql
select c.Path, c.OccurrenceCount, c.MatchingLineCount,
       c.BytesScanned, c.Complete
from search.counts(root, literal) c
where c.OccurrenceCount = 0
order by c.Path
```

Every returned count row must have `Complete = true`; a zero row then means
that this eligible file was read to successful EOF with no occurrence. A
missing root, read failure or an incomplete prefix is not equivalent to a
zero-hit file. For a repository-wide negative answer, also account for the
eligible path set (`search.paths`) and require that every eligible file has a
complete zero count. The current source exposes no terminal summary row in
SQL, so an agent must not infer global completeness from an empty occurrence
relation alone.

The executable coverage for these recipes is in
`SearchEvidenceRecipeTests`: it compiles the scalar SQL shapes, checks
Unicode-safe bounded context and expansion, verifies JSON escaping of fake
instructions, and distinguishes an empty match set from complete zero-count
rows.

Source references: `S14`, `S16`, `S22`; related contracts:
[`search-context-contract-v1.md`](search-context-contract-v1.md),
[`search-completion-contract-v1.md`](search-completion-contract-v1.md), and
[`search-projection-policy-v1.md`](search-projection-policy-v1.md).
