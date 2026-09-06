# Search request contract v1

Status: proposed design contract for `W01-S05`, with the scalar literal subset
implemented by `W08-S01` and `W08-S03`. The bounded request parser, `search.many`
metadata and positional scalar constructor binding are verified for literal
entries. Named-argument binding still requires reflected constructor metadata;
regex execution, partial/completion options and table-valued input remain
later-scope work.

The machine-readable schema and fixtures are in
[`search-request-contract-v1.json`](search-request-contract-v1.json). The
independent validator is
[`Test-SearchRequestContract.ps1`](../../scripts/search/Test-SearchRequestContract.ps1).

## Transport and validation order

`search.many` uses a scalar JSON request string in the proposed compatibility
shape:

```text
search.many(root, request)
```

The request is one object with `version: 1` and a non-empty `patterns` array.
Each pattern has a stable `id`, non-empty `pattern` text and a closed `mode`
enum (`literal` or `regex`). The array is labeled rather than represented as
a SQL map, so equal pattern text under different IDs remains independent.

The implemented parser accepts at most 8,388,608 UTF-16 characters in the
scalar before allocating its UTF-8 reader input. Semantic field limits below
remain independently enforced. Diagnostic offsets identify UTF-8 byte
positions in `requestJson`.

Validation order is:

1. reject duplicate JSON property names at every object depth;
2. parse JSON syntax;
3. reject unknown top-level, option, scope and pattern properties;
4. validate version and enum values;
5. reject duplicate pattern IDs;
6. enforce bounds and cross-field rules;
7. only then access the filesystem.

JSON Schema alone does not reliably enforce duplicate property names or unique
IDs under all binders, so application validation must perform those checks.
Last-wins property binding is not part of this contract. Invalid requests do
not open the supplied root. The parser returns an immutable typed request;
the implemented literal source consumes it only after validation and before
scope enumeration.

## Bounded schema

| Field | Rule |
|---|---|
| `version` | Required constant `1`. |
| `patterns` | Required array of 1–1,024 objects. |
| `patterns[].id` | Required 1–128 character ASCII identifier matching `[A-Za-z0-9][A-Za-z0-9._-]*`. Unique across the request. |
| `patterns[].pattern` | Required non-empty text, maximum 65,536 characters. |
| `patterns[].mode` | Required `literal` or `regex`. |
| `options.case` | `sensitive` or `insensitive`. |
| `options.wholeWord` | Boolean. |
| `options.encoding` | `auto`, `utf8`, `utf8-bom`, `utf16-le-bom` or `utf16-be-bom`. |
| `options.selection` | The frozen `leftmost-first-non-overlapping` value. |
| `options.take` | Integer from 1 through 1,000,000. It remains a query limit, not a scan budget. |
| `options.partialPolicy` | `reject` or `allow`, as defined by W01-S04. |
| `options.validation` | `full-input` or `observed-prefix`, as defined by W01-S04. |
| `options.scope` | Closed scope policy subset; unknown nested options are rejected. |

Unspecified match, scope and completion options inherit the already published
W01-S02, W01-S03 and W01-S04 defaults. The request cannot weaken security
containment or turn an incomplete outcome into success.

## Acceptance examples

The scalar literal examples below are executable against the current Search
source. Regex entries and completion options remain parser-only until their
execution scopes are implemented.

### Positional

```sql
SELECT Path, PatternId, MatchIndex
FROM search.many('./fixture', '{"version":1,"patterns":[{"id":"todo","pattern":"TODO","mode":"literal"}]}')
```

For the fixture with `src/a.txt: TODO TODO`, `src/empty.txt` empty and
`src/none.txt: DONE`, the expected occurrence rows are:

| Path | PatternId | MatchIndex |
|---|---|---:|
| `src/a.txt` | `todo` | 0 |
| `src/a.txt` | `todo` | 1 |

### Named

```sql
SELECT Path, PatternId, MatchIndex
FROM search.many(root: './fixture', request: '{"version":1,"patterns":[{"id":"todo","pattern":"TODO","mode":"literal"}]}')
```

The named form remains a parser-shape fixture. The current Search schema uses
the repository's positional-only constructor metadata, so named binding is not
advertised as executable until reflected constructor metadata is available.

### Two equal patterns with distinct labels

With `todo-primary` and `todo-secondary`, both containing literal `TODO`, the
same two occurrences are emitted independently for each label: four rows in
total. Pattern labels are part of row identity and are not deduplicated.

## Rejection examples

| Case | Expected result |
|---|---|
| Unknown top-level `debug` property | `reject-unknown-property` |
| Duplicate top-level `version` property | `reject-duplicate-json-property` |
| Duplicate `patterns[].id` | `reject-duplicate-pattern-id` |
| Unknown `options.fast` property | `reject-unknown-property` |
| `patterns[].mode: glob` | `reject-invalid-enum` |
| `options.encoding: ascii` | `reject-invalid-enum` |

Every rejection occurs before filesystem access and carries a stable
validation category. Future options require a versioned contract change;
silently accepting an unknown key would make query behavior dependent on the
installed binder.

## Boundary

This contract owns request shape and acceptance examples. Match selection,
scope/path behavior and terminal completion semantics remain the separate
W01-S02, W01-S03 and W01-S04 contracts. Runtime request models and the
scalar-literal `search.many` source are implemented in W08; regex execution,
completion transport and table-valued input remain separately owned work.

Source references: `S14`, `S15`, `S20`, `S22`, `S23`.
