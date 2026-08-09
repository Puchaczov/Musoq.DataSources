# JSON and SeparatedValues breaking changes

The dynamic structured-source rework intentionally removes the legacy readers instead of maintaining two behavioral contracts.

## JSON

- Replace `#json.file(dataPath, schemaPath)` with `#json.file(dataPath)`. The complete top-level schema is inferred from the JSON file.
- Stream-backed JSON input and external schema files are no longer accepted.
- Input is strict UTF-8, with an optional UTF-8 BOM.
- Comments, trailing commas, duplicate properties, multiple root documents, malformed roots, and primitive elements in a root array now fail.
- Conversion failures now fail instead of silently becoming null.

```sql
-- Before
select Name from #json.file('./people.json', './people.schema.json')

-- Now
select Name from #json.file('./people.json')
```

## SeparatedValues

- Public input is a UTF-8 file path through `comma`, `tab`, or `semicolon`; stream-backed and archive cross-apply input is removed.
- Encoding, culture, codec, format, and trim read modifiers are removed. Values use invariant inference and whitespace is preserved.
- Missing files, invalid UTF-8, malformed quoting, empty or duplicate headers, header overflow, and conversion failures now fail.
- Header names are no longer normalized. Query their exact spelling and use bracket quoting for special names.
- Headerless width and inferred types come from the complete source rather than its first record.
- An unquoted empty field is null; a quoted empty field remains an empty string.

```sql
-- A header named "Account name" is preserved.
select [Account name]
from #separatedvalues.comma('./accounts.csv', true, 0)
```

To query a JSON or separated-values archive entry, extract it to a strict UTF-8 file and pass that path to the datasource. Explicit `TABLE` coupling remains available when fixed types are preferable, but is no longer required to establish a schema.
