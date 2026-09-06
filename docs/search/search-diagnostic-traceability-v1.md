# Search diagnostic traceability recipe v1

This recipe uses one labeled `search.many` request to gather the lexical
evidence needed to review a diagnostic's declaration, emission, test coverage
and documentation. It produces a traceability finding with these fields:

| Field | Meaning |
| --- | --- |
| `Code` | Repository-relative path containing the finding. |
| `Category` | The category encoded by the pattern label, such as `declaration`, `emission`, `test` or `docs`. |
| `Origin` | Recipe-level provenance derived from the explicit path manifest: `src/` is `code`, `tests/` is `test`, and `docs/` is `documentation`. |
| `Evidence` | `lexical-mention` or `lexical-comment`, based on the containing line. |

`Origin` is not a `SearchMatch` or `search.many` source column. The current
many-row schema exposes coordinates, pattern identity, match text and related
evidence fields, but not repository provenance. The recipe therefore derives
origin from a caller-owned path manifest and must not present that derived
classification as a Search guarantee. Paths outside the manifest are
classified as `other` and require explicit handling by the caller.

## Labeled batch

The request contains stable pattern identifiers. The identifier prefix is the
recipe category and the suffix identifies the diagnostic:

```json
{
  "version": 1,
  "patterns": [
    { "id": "declaration-1001", "pattern": "public const string MQ1001", "mode": "literal" },
    { "id": "emission-1001", "pattern": "EmitDiagnostic(\"MQ1001\")", "mode": "literal" },
    { "id": "test-1001", "pattern": "MQ1001_ShouldHaveCoverage", "mode": "literal" },
    { "id": "docs-1001", "pattern": "MQ1001", "mode": "literal" }
  ]
}
```

The executable query keeps the projection explicit:

```sql
select m.Path, m.PatternId, m.LineNumber, m.Utf16Column, m.MatchText
from search.many(root, request) m
order by m.Path, m.PatternId, m.LineNumber, m.Utf16Column
```

When the request is embedded in a SQL string, escape both the root and the
JSON request for that SQL literal. JSON escaping alone does not guarantee that
backslashes survive SQL literal parsing.

## Classification and proof boundary

For each row, split `PatternId` into category and diagnostic identifier, map
the path through the explicit manifest, and read the containing line only to
label lexical evidence. A line beginning with `//` is a lexical comment; it
is still a finding, but it cannot satisfy a declaration or other proof by
itself. Other rows are lexical mentions, not language-structure assertions.

Group findings by diagnostic identifier and require all four independent
relations before reporting proven coverage:

```text
proven(diagnostic) =
    declaration in code, excluding comments
    and emission in code, excluding comments
    and test in test, excluding comments
    and docs in documentation, excluding comments
```

The result must retain missing relations rather than converting an incomplete
group into a negative conclusion. The repository scope must be complete before
absence is treated as meaningful; a lexical search cannot prove that no other
declaration, emission, test or documentation exists outside the scanned path
manifest. Likewise, these findings do not parse language syntax, resolve
symbols or prove that a test exercises the intended runtime behavior.

`SearchDiagnosticTraceabilityRecipeTests` is the executable example. Its
fixture proves a complete `1001` chain, leaves `1002` incomplete, and includes
a comment-only `1002` declaration so that lexical mention, comment evidence,
derived origin and proven coverage remain separate assertions.

Source references: `S14`, `S15`, `S16`; related contracts:
[`search-evidence-recipes-v1.md`](search-evidence-recipes-v1.md) and
[`search-source-contract-v1.md`](search-source-contract-v1.md).
