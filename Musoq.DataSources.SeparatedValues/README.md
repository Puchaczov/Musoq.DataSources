# Musoq.DataSources.SeparatedValues

The SeparatedValues plugin streams UTF-8 delimited files through a bounded,
byte-native pipeline.

Use the strict convenience sources for existing CSV/TSV files:

```sql
from #separatedvalues.comma('data.csv', true, 0)
from #separatedvalues.tab('data.tsv', true, 0)
from #separatedvalues.semicolon('data.scsv', false, 0)
```

For another ASCII delimiter, select it explicitly:

```sql
from #separatedvalues.delimited('data.psv', '|', true, 0)
```

Delimiter and header detection are never guessed. A concrete `TABLE` contract
is authoritative; dynamic sources inspect only a bounded sample (1 MiB, 4,096
records, or 10 ms by default). Headerless contracts bind names by source
ordinal, while dynamic headerless sources use `Column1`, `Column2`, and so on.

The generic source can opt into quote, escape, trimming, comments, null-token,
culture, record-ending, and buffer limits through the documented
`separatedvalues.*` runtime settings. Strict convenience sources retain the
historical grammar and defaults.
