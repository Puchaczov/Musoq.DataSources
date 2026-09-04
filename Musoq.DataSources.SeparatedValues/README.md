# Musoq.DataSources.SeparatedValues

The SeparatedValues plugin streams UTF-8 delimited files through a bounded,
byte-native pipeline. This version requires the compatible Musoq 17.0.9
runtime-v2 package set: `Parser`, `Plugins`, and `Schema` at `17.0.9-alpha.1`,
with `Evaluator` and `Converter` at `17.0.9-alpha.2`.

Use the strict convenience sources for existing CSV/TSV files:

```sql
from separatedvalues.comma('data.csv', true, 0)
from separatedvalues.tab('data.tsv', true, 0)
from separatedvalues.semicolon('data.scsv', false, 0)
```

For another ASCII delimiter, select it explicitly:

```sql
from separatedvalues.delimited('data.psv', '|', true, 0)
```

Delimiter and header detection are never guessed. A concrete `TABLE` contract
is authoritative; dynamic sources inspect only a bounded sample (1 MiB, 4,096
records, or 10 ms by default). Headerless contracts bind names by source
ordinal, while dynamic headerless sources use `Column1`, `Column2`, and so on.

The generic source can opt into quote, escape, trimming, comments, null-token,
culture, record-ending, and buffer limits through the documented
`separatedvalues.*` runtime settings. Strict convenience sources retain the
historical grammar and defaults.

## Explicit enum columns

SeparatedValues supports first-class enums only when a query declares the
descriptor and couples the source to that `TABLE` contract. Dynamic sampling
never infers an enum from row text:

```sql
enum JobStatus : int {
    Queued = 10,
    Running = 20,
    Finished = 30
};

table Jobs { Status: JobStatus };
couple separatedvalues.comma with table Jobs as Rows;
select Status, EnumName(Status) from Rows('jobs.csv', true, 0);
```

The eight integral backings (`byte`, `sbyte`, `short`, `ushort`, `int`,
`uint`, `long`, and `ulong`) are accepted. A field first uses the existing
invariant integral grammar, so any representable unknown number is preserved
as the primitive carrier and `EnumName` returns `NULL`. Otherwise the exact,
case-sensitive UTF-8 token is matched against every declared member and alias.
Named composites and unnamed numeric flag masks are valid; comma-composed
symbolic flags, wrong casing, overflow, signedness violations, and unknown
names are rejected with bounded source/row/column/descriptor diagnostics.

Enum columns are nullable when the source contains an unquoted empty field or
the configured null token. `IS NULL` and `IS NOT NULL` follow SQL null
semantics; comparisons, membership, and flags terms never match a null value.
`HasAnyFlags` with a zero mask is false, while `HasAllFlags` with a zero mask
is true for a non-null value. Equality/inequality, `IN`/`NOT IN`, null checks,
and direct flags helpers may be pushed into the byte-native scan when their
descriptor fingerprint matches. Other expressions remain Core residuals.

The source boundary reads and returns the primitive carrier (`int?` in the
example), while `EnumType` metadata retains the portable descriptor. No
`System.Enum` value is created and no enum is inferred row by row. Non-empty
read modifiers, implicit conversions, ordering, arithmetic, general bitwise
operations, and binary/text enum fields remain unsupported.

## Native query-scoped rows

SeparatedValues requires query-scoped row transfer. Construct the schema with
the parameterless constructor:

```csharp
var schema = new SeparatedValuesSchema();
```

For recognized sources with valid exact metadata, `DescribeSource` always
advertises `QueryScopedRows | LogicalScalarReads`. The compiled query reads
accepted UTF-8 fields directly into its generated struct or class carrier. It
does not create a production row `object?[]` and does not box primitive fields.
Filtering and accepted `SKIP` run before materialization; accepted `TAKE` stops
input as soon as its limit is reached. Sequential and parallel execution
preserve source order and support zero-field carriers.

`DescribeSource.RowType` intentionally remains `typeof(object[])` because that
is nominal metadata required by the core schema contract. It is not an
executable SeparatedValues transfer path. The required `GetRowSource<T>` entry
point throws `InvalidOperationException` for every recognized SeparatedValues
source before opening the file, including when the execution token is already
cancelled. That exception identifies the source, requested row type, and the
unsupported legacy planner selection. There is no runtime retry.

### Migrating schema construction

Replace the removed boolean constructor:

```csharp
// Before
new SeparatedValuesSchema(enableQueryScopedRows: true);

// After
new SeparatedValuesSchema();
```

`new SeparatedValuesSchema(false)` has no native-only equivalent. Callers that
used it must move to the parameterless constructor and exact supported source
metadata. Direct calls to `GetRowSource<T>` must be replaced by normal compiled
queries so the core can select `GetQueryScopedRowSource<TRow, TMaterializer>`.

Eligible exact field types are:

- `string`;
- `bool` and `char`;
- `byte`, `sbyte`, `short`, `ushort`, `int`, `uint`, `long`, `ulong`, `float`,
  `double`, and `decimal`;
- `DateTime`, `DateTimeOffset`, `DateOnly`, `TimeOnly`, and `TimeSpan`;
- `Guid`;
- nullable forms of every supported value type above.

Source discovery must yield exact, unique metadata, and the generated query
shape must agree with it. A concrete `TABLE` coupled to
`separatedvalues.comma`, `.tab`, `.semicolon`, or `.delimited` supplies the
authoritative names, ordinals, nullability, and declared types. Direct
discovered sources use the exact bounded snapshot. `TABLE` and `COUPLE`
semantics otherwise remain unchanged, including typed and reordered
projections.

Header names map fields by name. Headerless sources retain the one-based
physical names `Column1`, `Column2`, and so on. Qualified query names are
resolved to their unique source name. Projection ordinals in a generated shape
are dense query slots, not file offsets: the immutable execution layout maps
each slot to its physical source ordinal. This is why reordering and sparse
projection continue to read the intended file columns.

Unsupported metadata is rejected during source description with the source
identity and precise eligibility reason. Rejection includes empty or duplicate
names, unresolved intended types, `object` or another unsupported exact type,
unsupported read modifiers, non-dense or ambiguous metadata, and columns that
cannot be matched to the immutable source snapshot. A runtime capability or
shape mismatch throws with the source identity and shape fingerprint.

Core planner decisions that cannot create a query carrier also fail instead of
falling back. Examples include an unsupported target, a declared-entity
requirement, an unavailable descriptor, or failed query-shape creation. Such a
plan reaches the guarded `GetRowSource<T>` entry point and throws before file
access.

Missing fields, short records, and unquoted empty or configured null tokens
produce `null` only for reference or nullable requests. A non-nullable value
request fails with row, column, source, and shape context. A quoted empty string
is a real empty string, distinct from an unquoted null value.

An execution token cancelled before enumeration opens no file and yields no
rows. Mid-stream cancellation, consumer cancellation, and early consumer
disposal stop promptly, release source/reader/block/output leases, and preserve
`OperationCanceledException`. Source-open, reader, conversion, and materializer
failures preserve their inner exception and do not retry. Begin/end progress is
reported exactly once; failures and cancellations are additionally counted by
the `SeparatedValues.ExecutionFailures` and
`SeparatedValues.ExecutionCancellations` diagnostics metrics because the public
progress enum has no failure phase.

## Multi-gigabyte inputs

Use a concrete, typed `TABLE` contract for large production files. A declared
contract validates record width but parses only fields needed by an accepted
predicate or projection. A dynamically sampled source must continue validating
every inferred typed column for every selected record, even when that column is
not projected. A contradiction after the bounded sample is a deterministic
schema-drift error; execution never widens the type mid-query.

Files at least 64 MiB use the parallel block pipeline in automatic mode. The
threshold is a file-size heuristic, not drive detection. Automatic mode uses
the process-wide CPU permit pool and keeps I/O depth independent from worker
count. Set `separatedvalues.max_parallelism=1` for rotational, network,
heavily contended, or otherwise latency-sensitive storage. A positive value
caps workers, while a missing value or `0` selects automatically.

The strict quote-free path recognizes LF and CRLF records for every supported
ASCII delimiter. Blocks containing quotes, escaped quotes, multiline fields,
or custom dialect behavior retain the general grammar path. Both paths preserve
source order. A positive source `SKIP`, a source `TAKE` greater than 4,096, or
their combination can run in parallel when there is no accepted predicate or
residual work; predicate-plus-slice requests and standalone smaller `TAKE`
requests remain sequential.

Input blocks, newline indexes, overflow records, and reordered output are
process-wide bounded. Reordered materialized output has a 256 MiB permit budget
and a 32-result guard. One result estimated above that budget may run
exclusively so a valid long record is not rejected only because of buffering.
Rows remain UTF-8 bytes until accepted output is materialized directly into the
generated query carrier. Production execution has no evaluator-facing
`object?[]` boundary.

## Performance qualification

The query-scoped qualification matrix, reproducible commands, environment,
median results, live core comparison, allocation results, and gate verdict are
checked in at
[QueryScopedRowMaterializationBaseline.md](../Musoq.DataSources.SeparatedValues.Benchmark/Baselines/QueryScopedRowMaterializationBaseline.md).
The document intentionally preserves the failing 2026-08-19 pre-remediation
evidence and appends the passing 2026-08-20 native-only qualification. The
final gate used three independent 96-identity source reports, three independent
36-identity compiled reports, a frozen byte-native legacy comparator, and three
matching live core report pairs. Core passed all 30 of its gates first; the
DataSources/core comparative gate then passed all 39 checks.

The independent comparator is preserved in
[FrozenByteNativeLegacySchema.cs](../Musoq.DataSources.SeparatedValues.Benchmark/FrozenByteNativeLegacySchema.cs).
The core design and qualification methodology are documented in
[Musoq query-scoped dynamic sources](https://github.com/Puchaczov/Musoq/blob/master/docs/query-scoped-dynamic-sources.md).

At 2, 8, 32, and 64 numeric fields, final carrier throughput was 15.1428x,
12.7491x, 2.6304x, and 2.7784x the frozen legacy throughput. Struct carrier
allocation was zero, end-to-end numeric CSV allocation fell by 59.32% to
94.44%, and DataSources query time was 0.3737x to 0.6060x the matching live
core result. Treat these as qualification evidence for the pinned fixture and
environment, not as a universal storage-throughput claim. The baseline links
the frozen comparator and records the complete reproducible commands,
medians, allocation results, core comparison, and production JIT verdict.

Performance numbers are hardware- and cache-state-specific. Do not publish an
absolute GB/s expectation without measuring the target storage and CPU. The
benchmark scheduling matrix covers 1, 2, 4, and 8 MiB blocks; I/O depths 1, 2,
4, and 8; yielded and direct CPU scheduling; and framing, projected numeric,
and quoted/multiline shapes:

```powershell
dotnet run -c Release --project Musoq.DataSources.SeparatedValues.Benchmark -- --filter *SeparatedValuesSchedulingMatrixBenchmarks*
```

The Playground keeps large fixture generation separate from profiling, so
generation cannot silently prime the measured process:

```powershell
dotnet run -c Release --project Musoq.DataSources.SeparatedValues.Playground -- prepare-large D:\sv-profile 8
dotnet run -c Release --project Musoq.DataSources.SeparatedValues.Playground -- profile-large D:\sv-profile\separated-values-large-v1-8589934592.json projected-one-long 0 buffered-unprimed
```

`buffered-unprimed` describes application behavior only; the OS cache state is
not guaranteed. Use the Windows unbuffered raw-ceiling shape when qualifying
the framing ratio on the target NVMe. Keep a new scheduling default only after
median isolated runs satisfy the documented throughput and latency thresholds.

The enum-specific qualification gate is a short, repeatable `MediumRun` over
8,192 records. It runs three isolated reports and compares the production
decoder with primitive parsing, hash-plus-UTF-8 token comparison, and direct
primitive masks; it reports per-report and median ratios plus fixed-operation
allocation noise (limited to 1,024 bytes):

```powershell
dotnet run -c Release --project Musoq.DataSources.SeparatedValues.Benchmark -- gate-enums
```

The gate is intentionally separate from final-table allocation: each measured
loop is warmed, returns only a checksum, and must allocate zero bytes on the
qualified path.
