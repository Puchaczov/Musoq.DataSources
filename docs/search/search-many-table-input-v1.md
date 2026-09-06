# Search many table-input investigation v1

Status: investigated in `W08-S04` against the current compiled Musoq engine.
The result does not add a collection-valued Search constructor or change the
public source contract. The compatibility transport remains two scalar string
arguments:

```sql
search.many(root, requestJson)
```

## Tested binding shapes

The Search test project compiled and executed the following forms:

| Shape | Result |
|---|---|
| Literal scalar `root` and literal scalar `requestJson` | Supported. This is the existing positional `search.many` surface. |
| CTE row with scalar `Root` and `Request` columns, passed through `cross apply search.many(i.Root, i.Request)` | Supported. The source receives the scalar values from the current CTE row. |
| A bounded CTE input using `take 1` before the correlated `cross apply` | Supported. One materialized input row produces one correlated source invocation; the test result contains the expected single scan's occurrences. |
| `UNION ALL` CTE rows containing the same `Path` value | Supported by the engine's relational semantics. Duplicate input rows remain distinct and the correlated output preserves both path identities. |
| Separate CTE rows with different scalar request JSON values | Supported. Each row keeps its own pattern label and does not inherit the other row's request. |
| A CTE/table alias supplied directly as either `search.many` argument | Not supported. Compilation accepts the expression shape, but source opening fails with `MQ7010_DataSourceOpenFailed`; the Search source reports that `root` or `request` is not a scalar string. |

The CTE cases use the existing `search.paths` source as a relational seed, so
the results cover actual compiled-query execution rather than a parser-only
or hand-built row-source test. `search.paths` continues to provide relative
eligible-file paths and does not make those paths a new `search.many` overload.

## Supported example

The following is the tested scalar-correlation shape. `root` and `request` are
ordinary SQL string expressions projected by the CTE:

```sql
with inputs as (
    select '<root>' as Root, '<request-json>' as Request
    from search.paths('<root>') p
    take 1
)
select m.Path, m.PatternId, m.MatchIndex
from inputs i
cross apply search.many(i.Root, i.Request) m
order by m.Path, m.MatchIndex
```

This is still scalar transport. A relation, row alias or collection is not
implicitly serialized into `requestJson`. Callers needing multiple labeled
patterns should continue to serialize the bounded versioned request object in
one scalar JSON value.

## Metadata boundary

`search.many` metadata is available without runtime root or request values. The
tested descriptor has row type `SearchMatch`, the current ten-column bounded
surface (`Path`, `PatternId`, `MatchIndex`, `ByteOffset`, `ByteLength`,
`LineNumber`, `Utf16Column`, `Utf16Length`, `MatchText`, `Captures`), and two
`string` constructor arguments. Metadata discovery does not resolve the root
or open content.

The investigation deliberately does not document named constructor binding,
collection-valued arguments, regex execution, completion options or broader
future occurrence fields. Those remain separate scope boundaries.

Source references: `S14`, `S15`, `S20`, `S23`.
