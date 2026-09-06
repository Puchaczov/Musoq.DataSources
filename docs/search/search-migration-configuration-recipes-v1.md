# Search migration and configuration recipes v1

These recipes compare bounded lexical candidates while preserving the
distinction between an observed mention and proven code/configuration
evidence. They do not parse a programming language or configuration format.

## Labeled migration candidates

Use one `search.many` request with stable labels for the deprecated and
replacement APIs:

```json
{
  "version": 1,
  "patterns": [
    { "id": "deprecated", "pattern": "OldApi()", "mode": "literal" },
    { "id": "replacement", "pattern": "NewApi()", "mode": "literal" }
  ]
}
```

Project the occurrence identity and coordinates explicitly:

```sql
select m.Path, m.PatternId, m.MatchIndex,
       m.LineNumber, m.Utf16Column, m.MatchText
from search.many(root, request) m
order by m.Path, m.PatternId, m.LineNumber, m.Utf16Column, m.MatchIndex
```

Classify each row using an explicit evidence rule. A line beginning with
`//` is `lexical-comment`; an API-shaped match inside a quoted string is
`lexical-string`; otherwise it is a `code-mention` candidate. Only the last
category enters the proven code set in this recipe. These are line-level
guards, not a substitute for syntax or symbol analysis.

## Candidate anti-join and proven set difference

The tested SQL anti-join finds deprecated paths with no lexical replacement
path:

```sql
select deprecated.Path
from search.many(root, request) deprecated
left outer join search.many(root, request) replacement
  on deprecated.Path = replacement.Path
 and replacement.PatternId = 'replacement'
where deprecated.PatternId = 'deprecated'
  and replacement.Path is null
group by deprecated.Path
order by deprecated.Path
```

This is a candidate result. It can include a file whose only deprecated hit
is a comment or string literal. To report a proven unmigrated code path, first
filter both labels to `code-mention`, then compute:

```text
proven-deprecated-paths minus proven-replacement-paths
```

Keep occurrence counts before taking a path set difference. Two deprecated
occurrences in one file are two findings even though the file appears once in
the final path set.

## Configuration manifest comparison

Configuration checks need an explicit bounded candidate manifest, for example:

```text
config/app.json
config/legacy.json
config/comments.json
config/missing.json
```

Use `search.paths(root)` to resolve which manifest entries are eligible files,
then use a labeled `search.many` request for the key. A real key occurrence is
`configuration-key`; a comment-only occurrence remains `lexical-comment`.
The result should retain at least:

```text
Path, Candidate, Proven, OccurrenceCount, Status, Evidence
```

`Candidate` means Search observed a lexical finding for the manifest path;
`Proven` means the evidence rule accepted the finding as the requested key.
Use `Status = proven` for an accepted key, `missing-key` for an eligible file
with no accepted key, and `missing-file` for a manifest path absent from the
resolved eligible set. Duplicate keys keep their occurrence count instead of
being collapsed as if one occurrence had been observed.

## Completeness gate

Do not turn a missing row into a negative migration or configuration claim
until the relevant scope is complete. The companion `search.audit(root,
pattern)` row must report `Complete`, `ScopeExhausted` and `CountsExact` before
the recipe can use set difference or assign `missing-key`/`missing-file` as a
scope conclusion. A missing root, read failure, cancellation or other
incomplete audit preserves failure evidence and blocks the negative claim.

`SearchMigrationConfigurationRecipeTests` covers API comments, API-shaped
string literals, duplicate deprecated occurrences, lexical anti-join output,
proven code set difference, duplicate configuration keys, comment-only keys,
an absent manifest file and an incomplete audit. The fixture uses exact
expected findings rather than treating query compilation as proof.

Source references: `S14`, `S15`, `S16`; related contracts:
[`search-source-contract-v1.md`](search-source-contract-v1.md),
[`search-completion-contract-v1.md`](search-completion-contract-v1.md) and
[`search-many-summaries-v1.md`](search-many-summaries-v1.md).
