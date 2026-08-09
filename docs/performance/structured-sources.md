# Structured source performance evidence

This is the append-only performance log for the JSON and separated-values rework. Each wave adds a dated section; earlier measurements are not rewritten.

## Wave 0 legacy baseline — 2026-08-05

### Reproduction contract

- Production code SHA: `81e2d4c6ebcf1d380c0c5e434a35ea0c1f784502`
- BenchmarkDotNet: `0.15.8`
- SDK: `.NET SDK 10.0.302`
- Runtime: `.NET 10.0.10`, x64 RyuJIT x86-64-v3, concurrent workstation GC
- OS: Windows 11 `10.0.26200.8894`
- CPU: Intel Core Ultra 9 285K, 24 physical/logical cores
- Power plan selected by BenchmarkDotNet: High performance
- JSON fixture: 100,000 rows, 6,119,592 bytes (5.836 MiB)
- Separated-values fixture: 100,000 rows, 1,649,797 bytes (1.573 MiB)
- Fixture verification checksums: JSON `50,045,000`; separated values `930,054`

No data was downloaded. The benchmark programs generate deterministic bounded fixtures below the system temporary directory. The repository does not contain generated data.

Commands:

```powershell
dotnet run --project Musoq.DataSources.Json.Benchmarks -c Release -- verify
dotnet run --project Musoq.DataSources.SeparatedValues.Benchmark -c Release -- verify

dotnet run --project Musoq.DataSources.Json.Benchmarks -c Release --no-build -- --filter "*JsonLegacySourceBenchmarks*"
dotnet run --project Musoq.DataSources.SeparatedValues.Benchmark -c Release --no-build -- --filter "*SeparatedValuesLegacySourceBenchmarks*"

dotnet run --project Musoq.DataSources.Json.Benchmarks -c Release --no-build -- --filter "*JsonCompiledExecutionBenchmarks*"
dotnet run --project Musoq.DataSources.SeparatedValues.Benchmark -c Release --no-build -- --filter "*SeparatedValuesCompiledExecutionBenchmarks*"
```

Each legacy source command was launched three times, sequentially. Each launch used three warmups and three measured iterations. Values below are the means from each launch, not pooled results.

### Layer baselines

| Format / layer | Launch means | Throughput range | Managed allocation / operation |
|---|---:|---:|---:|
| JSON cached file read | 0.621–0.632 ms | 9,231–9,404 MiB/s | 240 B |
| JSON scalar memory scan | 1.454–1.457 ms | 4,005–4,014 MiB/s | 0 B |
| JSON Newtonsoft token scan | 16.622 / 16.626 / 17.057 ms | 342–351 MiB/s; 5.86–6.02 M rows/s | 28,322,738–28,322,740 B |
| JSON legacy datasource | 119.656 / 124.633 / 121.896 ms | 46.8–48.8 MiB/s; 0.80–0.84 M rows/s | 64,881,138–64,881,760 B |
| CSV cached file read | 0.124 / 0.140 / 0.125 ms | 11,262–12,678 MiB/s | 240 B |
| CSV scalar memory scan | 0.390–0.392 ms | 4,016–4,039 MiB/s | 0 B |
| CSV CsvHelper token scan | 11.290 / 12.023 / 11.125 ms | 130.9–141.4 MiB/s; 8.32–8.99 M rows/s | 11,167,993–11,168,112 B |
| CSV legacy datasource | 9.162 / 9.100 / 8.738 ms | 171.7–180.1 MiB/s; 10.91–11.44 M rows/s | 13,424,107–13,424,203 B |
| JSON frozen legacy adapter | 128.307 ms | 45.5 MiB/s; 0.78 M rows/s | 61.06 MiB |
| CSV frozen legacy adapter | 12.725 ms | 123.6 MiB/s; 7.86 M rows/s | 14.47 MiB |

The parser-only and datasource methods intentionally do different work, so their absolute values are independent ceilings rather than ratios. In particular, the CsvHelper token benchmark reads and hashes every field, while the existing datasource benchmark exercises its current read plan.

The frozen adapters are benchmark-project source copies of the legacy Newtonsoft and CsvHelper materialization behavior. They do not call the production row sources, so later production changes retain a same-process old/new comparison. `LegacyDataSource` measures the production implementation at the baseline SHA; `FrozenLegacyAdapter` is the durable comparison control.

### Complete compiled Musoq execution

| Query shape | JSON mean / allocation | CSV mean / allocation |
|---|---:|---:|
| Count | 118.7 ms / 60.58 MiB | 7.336 ms / 9.00 MiB |
| One-column projection | 130.5 ms / 67.91 MiB | 23.799 ms / 16.34 MiB |
| Full row | 140.8 ms / 74.78 MiB | 33.435 ms / 23.21 MiB |
| Predicate, about 10% selected | 122.1 ms / 63.57 MiB | 7.572 ms / 3.66 MiB |
| Predicate, about 50% selected | 126.0 ms / 69.21 MiB | 7.571 ms / 3.66 MiB |
| Early `take 100` | 120.3 ms / 60.59 MiB | 0.626 ms / 0.58 MiB |
| Group/min/max/average | 130.7 ms / 63.65 MiB | 16.580 ms / 14.66 MiB |

The legacy JSON benchmark uses `Sequence` for numeric predicates and aggregates. Its old schema declares fractional JSON numbers as `decimal`, while Newtonsoft materializes them as `double`; using the declared fractional column in compiled numeric queries throws `InvalidCastException`. The new dynamic reader must remove that mismatch.

### Legacy metadata behavior

| Operation | Mean | Allocation |
|---|---:|---:|
| JSON schema-file read | 163.9 us | 233.79 KiB |
| CSV first-record metadata read | 324.8 us | 561.63 KiB |

These are not exact-discovery measurements: the legacy JSON source reads a separate schema file and the legacy CSV source reads only the first record. There is no process snapshot cache in either source. Cold full discovery and cache-hit measurements start when the snapshot implementation exists; they must not be compared semantically with these two legacy operations.

BenchmarkDotNet reports elapsed CPU-bound operation time and managed allocation. Peak working set is not isolated by these microbenchmarks because the host, generated query assembly, fixture bytes, and benchmark process share one process; large-macro peak memory is measured in the final evidence wave with a dedicated process.

### Dataset policy and explicit macros

Normal fixtures are bounded and generated automatically. A large 1BRC-shaped file is generated only by an explicit command:

```powershell
dotnet run --project Musoq.DataSources.SeparatedValues.Benchmark -c Release -- generate <row-count> <absolute-output-path>
```

The generator estimates output size and refuses to start unless the target drive has the estimate plus 20% headroom. Wave 0 did not generate 100-million-row or one-billion-row files. Large fixtures stay outside the repository and are never a CI input.

## Wave 1 snapshot foundation — 2026-08-05

Wave 1 adds linked cold-path source code only. Neither production reader calls it yet.

Focused validation:

```powershell
dotnet build Musoq.DataSources.Json.Tests/Musoq.DataSources.Json.Tests.csproj -c Release
dotnet test Musoq.DataSources.Json.Tests/Musoq.DataSources.Json.Tests.csproj -c Release --no-build --filter "FullyQualifiedName~Structured"
dotnet build Musoq.DataSources.SeparatedValues/Musoq.DataSources.SeparatedValues.csproj -c Release
```

- 28 snapshot, inference, layout, drift, fingerprint, cancellation, concurrency, retry, and eviction tests passed.
- JSON current datasource: `119.8 ms`, `61.88 MiB` allocated; Wave 0 launch range was `119.656–124.633 ms`.
- CSV current datasource: `8.826 ms`, `12.8 MiB` allocated; Wave 0 launch range was `8.738–9.162 ms`.
- No production reader regression was observed. The shared code adds no package and is compiled into each format assembly through linked source files.

## Wave 2 exact JSON discovery — 2026-08-05

Wave 2 replaces schema-file metadata with an exact, strict UTF-8 scan of the source itself. The production execution reader is intentionally still the legacy bridge in this wave; its replacement is measured separately in Wave 3.

Focused validation:

```powershell
dotnet test Musoq.DataSources.Json.Tests/Musoq.DataSources.Json.Tests.csproj -c Release --logger "console;verbosity=minimal"
dotnet build Musoq.DataSources.Json.Benchmarks/Musoq.DataSources.Json.Benchmarks.csproj -c Release
dotnet run --project Musoq.DataSources.Json.Benchmarks -c Release --no-build -- --filter "*JsonMetadataBenchmarks*"
```

- 72 JSON and shared-structured tests passed.
- The tests cover root objects and arrays, complete late-column union, sparse-row nulls, numeric widening, nested containers, exact casing, duplicate properties, BOM, invalid UTF-8, buffer-spanning tokens, malformed input, cancellation, cache invalidation, dense explicit layouts, and schema drift.
- No schema fixture or schema-file constructor argument remains in the JSON projects.
- No benchmark data was downloaded or generated beyond the existing bounded 100,000-row fixture.

| Exact metadata operation | Mean | Allocation | Throughput |
|---|---:|---:|---:|
| Process-cache hit, including identity/fingerprint validation | 208.8 us | 222.46 KiB | n/a |
| Cold full discovery, 100,000 rows / 5.836 MiB | 16.609 ms | 296.51 KiB | 351.4 MiB/s; 6.02 M rows/s |

The cold allocation remains approximately constant with row count for flat input: property names are decoded and retained once, top-level duplicate detection uses row ordinals, and only the bounded snapshot and partition descriptions survive discovery. The legacy schema-file number above is not a semantic baseline for cold discovery because it reads a different, tiny file and never scans source rows.

## Wave 3 streaming JSON execution — 2026-08-05

Wave 3 removes Newtonsoft and `JsonHelpers` from the production JSON package. A format-specific structural framer now supplies complete root records to `Utf8JsonReader`; hash-bound top-level names let unselected values be skipped without decoding. Accepted predicates run in a first pass over the framed record, so rejected rows allocate no projected arrays, strings, nested containers, or scalar boxes. Skip/take is accepted only with no residual predicate or order.

Focused validation:

```powershell
dotnet test Musoq.DataSources.Json.Tests/Musoq.DataSources.Json.Tests.csproj -c Release --logger "console;verbosity=minimal"
dotnet build Musoq.DataSources.Json.Benchmarks/Musoq.DataSources.Json.Benchmarks.csproj -c Release
dotnet run --project Musoq.DataSources.Json.Benchmarks -c Release --no-build -- --filter "*JsonLegacySourceBenchmarks*"
dotnet run --project Musoq.DataSources.Json.Benchmarks -c Release --no-build -- --filter "*JsonCompiledExecutionBenchmarks*"
```

- 84 focused JSON/shared tests passed.
- Random sparse rows were compared field-for-field with `System.Text.Json` reference results.
- Execution coverage includes escaped names and values, braces inside strings, multi-megabyte buffer-spanning records, nested materialization, natural heterogeneous scalars, invariant numeric types, predicate/residual splitting, dense/zero-column projection, early take, conversion errors, drift, cancellation, and progress.
- The benchmark project keeps its direct Newtonsoft dependency only for the frozen legacy adapter; the production package has no Newtonsoft or `JsonHelpers` reference.

Same-process source layers, 100,000 rows / 5.836 MiB:

| Layer | Mean | Allocation | Rows/s |
|---|---:|---:|---:|
| Zero-column framing and row delivery | 7.973 ms | 2.89 MiB | 12.54 M |
| One selected string column | 14.394 ms | 10.52 MiB | 6.95 M |
| Predicate rejects every row | 13.758 ms | 2.10 MiB | 7.27 M scanned |
| Three-column production source | 19.645 ms | 17.39 MiB | 5.09 M |
| Frozen legacy adapter | 147.348 ms | 61.06 MiB | 0.68 M |

The full-row source is 7.50 times faster than the frozen adapter and allocates 71.5% less. The zero-column path reuses `Array.Empty<object>()`; its remaining allocation is chunk/source/core infrastructure rather than one array per row.

Complete compiled Musoq execution:

| Query shape | Wave 0 | Wave 3 | Speedup | Allocation change |
|---|---:|---:|---:|---:|
| Count | 118.7 ms / 60.58 MiB | 19.883 ms / 10.75 MiB | 5.97x | -82.3% |
| One-column projection | 130.5 ms / 67.91 MiB | 31.249 ms / 18.09 MiB | 4.18x | -73.4% |
| Full row | 140.8 ms / 74.78 MiB | 41.176 ms / 34.11 MiB | 3.42x | -54.4% |
| Predicate, about 10% selected | 122.1 ms / 63.57 MiB | 18.487 ms / 4.65 MiB | 6.60x | -92.7% |
| Predicate, about 50% selected | 126.0 ms / 69.21 MiB | 29.872 ms / 13.66 MiB | 4.22x | -80.3% |
| Early `take 100` | 120.3 ms / 60.59 MiB | 0.791 ms / 2.37 MiB | 152.1x | -96.1% |
| Group/min/max/average | 130.7 ms / 63.65 MiB | 29.639 ms / 15.43 MiB | 4.41x | -75.8% |

All primary warm execution shapes improved substantially and none regressed. The remaining zero-column gap to raw file reading is retained as a measured Wave 8 optimization target rather than hidden by the full-row gains.

## Wave 4 separated-values parser bake-off — 2026-08-05

Wave 4 compares CsvHelper `33.1.0`, Sep `0.15.1`, Sylvan.Data.Csv `1.4.4`, and a focused managed UTF-8 scanner. The custom scanner is benchmark-only in this wave; production cutover starts with exact discovery in Wave 5.

Focused validation and reproduction commands:

```powershell
dotnet build Musoq.DataSources.SeparatedValues.Benchmark/Musoq.DataSources.SeparatedValues.Benchmark.csproj -c Release
dotnet run --project Musoq.DataSources.SeparatedValues.Benchmark -c Release --no-build -- bakeoff-verify
dotnet run --project Musoq.DataSources.SeparatedValues.Benchmark -c Release --no-build -- --filter "*SeparatedValuesParserBakeoffBenchmarks*"
```

The deterministic verifier generated 2,001 randomized rows plus a record crossing the 1 MiB input-buffer boundary. It covered BOM, LF/CRLF, blank-line skipping, quoted delimiters, escaped quotes, quoted and unquoted empty fields, multiline fields, Unicode, whitespace, trailing fields, and variable content. Five separate malformed probes covered unterminated quotes, quotes inside unquoted fields, content after closing quotes, bare carriage returns, and invalid UTF-8.

| Candidate | Valid grammar | Malformed probes rejected | Field spans | Quoted-state distinction | Skipped fields without strings | Eligible |
|---|---:|---:|---:|---:|---:|---:|
| Custom managed UTF-8 | yes | 5 / 5 | yes, UTF-8 | yes | yes | yes |
| Sep 0.15.1 | yes | 0 / 5 | yes, UTF-16 | yes with raw spans | yes | no |
| Sylvan.Data.Csv 1.4.4 | yes | 2 / 5 | yes, UTF-16 | no | yes | no |
| CsvHelper 33.1.0 | yes | 4 / 5 | no | no | no | no |

Candidate throughput used identical 100,000-record ASCII fixtures and equivalent decoded-field length/edge checksums. Blank records were normalized according to the locked contract. The custom reader uses a pooled record/input buffer and allocates no field strings.

| Candidate | 1BRC-shaped, 1.573 MiB | Wide 48-column, 26.963 MiB | Quoted/multiline, 5.224 MiB | Managed allocation / operation |
|---|---:|---:|---:|---:|
| Custom managed UTF-8 | 2.713 ms | 38.775 ms | 9.826 ms | 225–241 B |
| Sep 0.15.1 | 2.405 ms | 30.774 ms | 9.226 ms | 8.1–8.8 KiB |
| Sylvan.Data.Csv 1.4.4 | 2.065 ms | 23.393 ms | 9.503 ms | 3.01–3.02 MiB |
| CsvHelper 33.1.0 | 7.236 ms | 103.700 ms | 21.135 ms | 10.65–149.51 MiB |

The custom scanner reached 579.9 MiB/s and 36.86 million rows/s on the 1BRC-shaped input, 695.4 MiB/s on the wide input, and 531.7 MiB/s on quoted/multiline input. Sep and Sylvan are faster on some valid-only shapes, but the selection rule first rejects non-conforming candidates. None of the maintained libraries satisfies strict malformed-input rejection together with raw fields, quoted-empty distinction, and allocation-free field skipping, so the within-5%-throughput/10%-allocation library preference never applies. The selected implementation is the custom managed UTF-8 scanner.

## Wave 5 exact separated-values discovery — 2026-08-05

Wave 5 moves the selected strict UTF-8 reader into the production package and makes full-file discovery the only metadata path. The benchmark bake-off now calls that production reader directly. Headers remain exact, ordinal, and case-sensitive; headerless width is the maximum width observed anywhere; short rows contribute missing/nullability state; headered overflow rows are malformed. Explicit non-`object` coupled-table types override inference only after their source names have been validated.

Focused validation:

```powershell
dotnet build Musoq.DataSources.SeparatedValues.Tests/Musoq.DataSources.SeparatedValues.Tests.csproj -c Release
dotnet test Musoq.DataSources.SeparatedValues.Tests/Musoq.DataSources.SeparatedValues.Tests.csproj -c Release --no-build --filter "FullyQualifiedName~SeparatedValuesSchemaDiscoveryTests|FullyQualifiedName~SeparatedValuesSchemaHeaderTests|FullyQualifiedName~SeparatedValuesDynamicSchemaTests"
dotnet run --project Musoq.DataSources.SeparatedValues.Benchmark -c Release --no-build -- bakeoff-verify
dotnet run --project Musoq.DataSources.SeparatedValues.Benchmark -c Release --no-build -- --filter "*SeparatedValuesMetadataBenchmarks*"
```

- 40 focused exact-discovery, header, unknown-column, dense-layout, cache, cancellation, drift, planner, file, and coupled-table tests passed.
- The randomized Wave 4 grammar verifier still passes against the production reader: 2,001 valid rows and all five malformed probes.
- No data was downloaded. The metadata benchmark used the existing locally generated 100,000-row, 1.573 MiB fixture.

| Exact metadata operation | Mean | Allocation | Throughput |
|---|---:|---:|---:|
| Process-cache hit, including identity/fingerprint validation | 218.8 us | 222.73 KiB | n/a |
| Cold full discovery, 100,000 rows / 1.573 MiB | 7.161 ms | 231.73 KiB | 219.7 MiB/s; 13.96 M rows/s |

Discovery retains only column/type/nullability state and at most 64 safe partitions; allocation does not grow per row. The temporary Wave 5 execution bridge still uses CsvHelper after validating the current snapshot and layout. Its replacement, including strict conversions and quoted-empty materialization, belongs to Wave 6.

## Wave 6 synchronous separated-values execution — 2026-08-05

Wave 6 replaces the production `StreamReader`/CsvHelper/async-row path with the selected synchronous UTF-8 scanner. Predicate fields are parsed from spans first; projection arrays, boxes, and strings are created only for accepted rows. Output converters and source/output ordinals are bound once per execution. Empty unquoted fields remain null, quoted empty fields remain empty strings, missing trailing fields remain null, and failed explicit conversions now throw with row/column context.

Focused validation:

```powershell
dotnet test Musoq.DataSources.SeparatedValues.Tests/Musoq.DataSources.SeparatedValues.Tests.csproj -c Release --no-restore
dotnet test Musoq.DataSources.Archives.Tests/Musoq.DataSources.Archives.Tests.csproj -c Release
dotnet build Musoq.DataSources.SeparatedValues.Benchmark/Musoq.DataSources.SeparatedValues.Benchmark.csproj -c Release --no-restore
dotnet run --project Musoq.DataSources.SeparatedValues.Benchmark -c Release --no-build -- smoke
dotnet run --project Musoq.DataSources.SeparatedValues.Benchmark -c Release --no-build -- --filter "*SeparatedValuesLegacySourceBenchmarks*"
dotnet run --project Musoq.DataSources.SeparatedValues.Benchmark -c Release --no-build -- --filter "*SeparatedValuesCompiledExecutionBenchmarks*"
```

- 80 focused separated-values tests passed; one opt-in manual profile test remained skipped.
- 11 focused archive tests passed after removal of the separated-values archive cross-apply integration.
- The grammar/runtime suite covers exact dense projection, predicate-before-projection, zero columns, skip/take, quoted delimiters and escapes, multiline fields, blank records, short rows, quoted-empty/null distinction, all supported explicit scalar types, strict conversion errors, drift, cancellation, and progress.
- The production package no longer references CsvHelper, `System.Text.Encoding.CodePages`, or `Musoq.DataSources.AsyncRowsSource`. CsvHelper remains a benchmark-only dependency for the frozen adapter and parser bake-off.
- No data was downloaded. All measurements used the existing deterministic 100,000-row / 1.573 MiB fixture.

Same-process source layers:

| Layer | Mean | Allocation | Rows/s |
|---|---:|---:|---:|
| Zero-column scan and row delivery | 3.333 ms | 2.11 MiB | 30.00 M |
| One selected decimal column | 8.101 ms | 8.21 MiB | 12.34 M |
| Predicate rejects every row | 6.932 ms | 1.29 MiB | 14.43 M scanned |
| Two-column production source | 8.636–10.474 ms | 13.52 MiB | 9.55–11.58 M |
| Frozen legacy CsvHelper adapter | 10.949–11.237 ms | 14.47 MiB | 8.90–9.13 M |

The production/frozen pair was launched three times after the cutover. The new source won in every same-process launch: elapsed time was 6.8–21.9% lower and managed allocation was 6.6% lower. The benchmark method is still named `LegacyDataSource` to preserve the Wave 0 series, but after this wave it invokes the new production implementation; `FrozenLegacyAdapter` remains the unchanged legacy control. The all-rejected path creates no projection rows or field strings; its measured allocation is identity/cache, reader, producer/channel, and other per-source infrastructure.

Complete compiled Musoq execution, recorded as an intermediate result rather than a final performance gate:

| Query shape | Wave 0 | Wave 6 |
|---|---:|---:|
| Count | 7.336 ms / 9.00 MiB | 11.732 ms / 9.75 MiB |
| One-column projection | 23.799 ms / 16.34 MiB | 23.713 ms / 17.09 MiB |
| Full row | 33.435 ms / 23.21 MiB | 37.956 ms / 27.01 MiB |
| Predicate, about 10% selected | 7.572 ms / 3.66 MiB | 9.759 ms / 4.08 MiB |
| Predicate, about 50% selected | 7.571 ms / 3.66 MiB | 22.151 ms / 14.23 MiB |
| Early `take 100` | 0.626 ms / 0.58 MiB | 0.834 ms / 1.31 MiB |
| Group/min/max/average | 16.580 ms / 14.66 MiB | 19.900 ms / 15.45 MiB |

One-column execution is unchanged within noise; the other compiled shapes expose costs still assigned to Waves 7 and 8. In particular, the two Wave 0 predicate benchmarks produced the same time and allocation despite materially different selectivity, so the old 50%-selectivity number is retained as historical evidence but is not assumed to be a correctness-equivalent baseline. The new smoke checks and focused tests validate the distinct result cardinalities. Early-take overhead is currently dominated by discovery identity/cache validation and source/core setup, while full materialization still pays the current core array/boxing and downstream table costs.

## Wave 7 adaptive ordered parallel scans — 2026-08-05

Wave 7 adds format-specific range readers over the safe record boundaries retained by exact discovery. Workers own independent file handles, parser state, converters, and pooled input buffers. A bounded channel per partition is drained in source order; cancellation and the first worker error stop all workers. Accepted skip/take plans and single-object JSON roots remain sequential. Optional `json.max_parallelism` and `separatedvalues.max_parallelism` settings accept `0` or omission for automatic selection and `1` to force sequential execution.

Focused validation and measurement commands:

```powershell
dotnet test Musoq.DataSources.Json.Tests/Musoq.DataSources.Json.Tests.csproj -c Release --logger "console;verbosity=minimal"
dotnet test Musoq.DataSources.SeparatedValues.Tests/Musoq.DataSources.SeparatedValues.Tests.csproj -c Release --logger "console;verbosity=minimal"
dotnet run --project Musoq.DataSources.Json.Benchmarks -c Release --no-build -- --filter "*JsonParallelScanBenchmarks*"
dotnet run --project Musoq.DataSources.Json.Benchmarks -c Release --no-build -- --filter "*JsonParallelCrossoverBenchmarks*"
dotnet run --project Musoq.DataSources.SeparatedValues.Benchmark -c Release --no-build -- --filter "*SeparatedValuesParallelScanBenchmarks*"
dotnet run --project Musoq.DataSources.SeparatedValues.Benchmark -c Release --no-build -- --filter "*SeparatedValuesParallelCrossoverBenchmarks*"
```

- 93 JSON tests and 88 separated-values tests passed; one opt-in separated-values profile probe remained skipped.
- Forced sequential and parallel scans produce identical rows, order, checksums, progress totals, and strict-conversion errors. Stress coverage cancels active workers and observes no deadlock or leaked work.
- The fixed worker scheduler always starts the earliest outstanding partitions, preventing a later bounded channel from blocking the partition currently being drained.
- JSON's framer owns its 1 MiB pooled input buffer, so its `FileStream` buffer is intentionally one byte. Removing the redundant per-worker 1 MiB `FileStream` buffer reduced parallel benchmark allocation from approximately 48 MiB to 1.67–9.30 MiB.
- No data was downloaded. All crossover inputs were bounded deterministic local fixtures.

Worker-count sweep, zero-column scans:

| Format and size | 1 worker | 2 workers | 4 workers | 8 workers | 16 workers | Selected cap |
|---|---:|---:|---:|---:|---:|---:|
| CSV, 500k rows | 13.044 ms | 9.942 ms | 6.052 ms | 4.768 ms | 4.536 ms | — |
| CSV, 2M rows | 49.743 ms | 33.276 ms | 32.404 ms | 34.881 ms | 36.117 ms | 2 |
| JSON, 100k rows | 6.532 ms | 6.224 ms | 3.980 ms | 3.415 ms | 3.036 ms | — |
| JSON, 1M rows | 55.885 ms | 32.900 ms | 25.148 ms | 25.936 ms | 26.850 ms | 4 |

The rule selects the smallest worker count within 3% of best throughput at the largest fixture. CSV therefore caps automatic execution at two workers: 33.276 ms is 2.7% behind the 32.404 ms best result. JSON caps at four workers because four is both the smallest and fastest result at 1M rows.

Two independent crossover launches produced these means:

| Format and fixture | Sequential run 1 / parallel run 1 | Sequential run 2 / parallel run 2 | Decision |
|---|---:|---:|---|
| CSV, 500k rows / 8,248,787 bytes | 14.486 / 10.765 ms | 14.231 / 12.921 ms | rejected; second gain was 9.2% |
| CSV, 750k rows / 12,373,131 bytes | 20.950 / 14.023 ms | 20.259 / 13.390 ms | accepted; gains were 33.1% and 33.9% |
| JSON, 50k rows / 3,054,243 bytes | 3.525 / 3.686 ms | 3.816 / 3.759 ms | rejected |
| JSON, 100k rows / 6,119,592 bytes | 7.541 / 4.567 ms | 6.973 / 4.061 ms | accepted; gains were 39.4% and 41.8% |

The committed automatic crossover constants are 12,000,000 bytes for separated values and 6,000,000 bytes for JSON. Explicit settings still allow any positive worker count, capped by available processors and discovered partitions. These constants are machine-derived policy rather than claims that every storage device has the same crossover; the runtime override remains the escape hatch.

## Wave 8 allocation and hot-path ceiling — 2026-08-05

Wave 8 removes per-row zero-column storage, adds bounded source-local string reuse, specializes the common decimal path, and retunes the format-specific scanners from measured evidence. No unsafe code, core changes, vectorized query execution, downloaded data, or automatic large-file generation was introduced.

Focused validation and measurement commands:

```powershell
dotnet test Musoq.DataSources.Json.Tests/Musoq.DataSources.Json.Tests.csproj -c Release --logger "console;verbosity=minimal"
dotnet test Musoq.DataSources.SeparatedValues.Tests/Musoq.DataSources.SeparatedValues.Tests.csproj -c Release --logger "console;verbosity=minimal"
dotnet build Musoq.DataSources.Json.Benchmarks/Musoq.DataSources.Json.Benchmarks.csproj -c Release
dotnet build Musoq.DataSources.SeparatedValues.Benchmark/Musoq.DataSources.SeparatedValues.Benchmark.csproj -c Release
dotnet run --project Musoq.DataSources.SeparatedValues.Benchmark -c Release --no-build -- bakeoff-verify
dotnet run --project Musoq.DataSources.Json.Benchmarks -c Release --no-build -- --filter "*JsonStringReuseBenchmarks*"
dotnet run --project Musoq.DataSources.SeparatedValues.Benchmark -c Release --no-build -- --filter "*SeparatedValuesStringReuseBenchmarks*"
dotnet run --project Musoq.DataSources.SeparatedValues.Benchmark -c Release --no-build -- --filter "*SeparatedValuesZeroColumnCeilingBenchmarks*"
```

- 100 JSON tests passed.
- 103 separated-values tests passed and the existing opt-in manual profile probe remained skipped.
- The randomized parser verifier again passed 2,001 valid rows and all five malformed probes.
- Direct tests cover compact chunks, predicate-bearing zero projections, string identity under concurrency, cardinality and byte-budget shutdown, decimal edge values, and 10,000 randomized invariant decimals.

### Compact zero-column scans

An accepted zero-column/no-predicate/no-slice plan now reads every source byte and uses the exact immutable discovery snapshot to emit its known row count. Output is represented by `RepeatedValueChunk<T>` over the shared empty row, then wrapped by the existing core `RowChunk<T>`. It allocates neither one array nor one reference slot per source row. Predicate-bearing zero projections still use the strict parser and are tested separately.

| Format / fixture | Raw cached file read | Zero-column source | Raw-throughput fraction | Source allocation |
|---|---:|---:|---:|---:|
| CSV, 2,000,000 rows / 32,995,000 bytes | 5.192 ms | 6.933 ms | 74.9% | 426.7 KiB |
| JSON, 100,000 rows / 6,119,592 bytes | 0.735 ms | 1.125 ms | 65.3% | 336.3 KiB |

The CSV result clears the 70% zero-column file-reading gate and JSON clears its 45% gate. The normal 100,000-row CSV source benchmark now takes 0.727 ms and allocates 300.5 KiB, down from Wave 6's 3.333 ms and 2.11 MiB. Allocation depends on snapshot validation, pooled input, and chunks rather than row count.

### Bounded string reuse and numeric specialization

Each snapshot owns an ordinal-indexed UTF-8 string pool. It retains at most 4,096 distinct values per column and 8 MiB per snapshot, then atomically disables and discards all retained entries if either limit is crossed. The cache reserves the full 8 MiB potential in each snapshot's size estimate, so dynamic reuse cannot bypass the cache's byte ceiling. Escaped JSON/CSV strings keep the strict existing materialization path, and `string.Intern` is never used.

The A/B benchmarks force one worker to isolate reuse cost:

| Format / cardinality | Reuse disabled | Reuse enabled | Effect |
|---|---:|---:|---:|
| CSV low | 9.621 ms / 13.52 MiB | 8.462 ms / 8.95 MiB | 12.0% faster; 33.8% less allocation |
| CSV high | 9.467 ms / 10.47 MiB | 9.466 ms / 10.47 MiB | pool disabled; unchanged |
| JSON low | 20.541 ms / 16.36 MiB | 18.915 ms / 11.78 MiB | 7.9% faster; 28.0% less allocation |
| JSON high | 15.836 ms / 9.49 MiB | 16.017 ms / 9.49 MiB | pool disabled; 1.1% timing noise |

The CSV decimal converter now constructs small plain invariant decimals directly from their UTF-8 significand and scale, falling back to `Utf8Parser` for long, exponent, overflow, and unusual valid forms. A controlled full-source run improved from 8.543 ms after string reuse alone to 7.134 ms with the decimal specialization, a further 16.5% reduction.

### Scanner and parallel tuning

The strict CSV reader keeps a 1 MiB sequential/discovery buffer, uses 256 KiB per parallel partition, and uses 64 KiB only for accepted takes of at most 4,096 rows with no accepted skip. Removing a redundant per-byte cancellation branch restored the Wave 4 parser ceiling while cancellation remains checked per record and buffer refill. Final parser-only results versus the frozen selected scanner are:

| CSV parser fixture | Wave 4 | Wave 8 | Change |
|---|---:|---:|---:|
| 1BRC-shaped | 2.713 ms | 2.682 ms | 1.1% faster |
| Wide 48-column | 38.775 ms | 38.026 ms | 1.9% faster |
| Quoted/multiline | 9.826 ms | 8.937 ms | 9.0% faster |

A CSV `SearchValues` field-scanner experiment was rejected and fully reverted: it moved the three primary cases from 2.713/38.775/9.826 ms to 2.818/58.386/10.173 ms. JSON's structural `SearchValues` framing was retained because it reduced the scalar framer probe from 4.566 to 4.109 ms and all escaping/buffer-boundary tests stayed green.

Parallel benchmarks now project one scalar column because zero-column execution intentionally uses the raw sequential shortcut. Largest-fixture worker sweeps were:

| Format | 1 worker | 2 workers | 4 workers | 8 workers | 16 workers | Automatic cap |
|---|---:|---:|---:|---:|---:|---:|
| CSV, 2M rows | 140.550 ms | 100.323 ms | 120.173 ms | 134.990 ms | 172.472 ms | 2 |
| JSON, 1M rows | 138.175 ms | 107.897 ms | 75.680 ms | 83.078 ms | 100.932 ms | 4 |

The smallest two-run crossover was 100,000 CSV rows / 1,649,797 bytes: 7.381/6.089 ms and 7.655/6.018 ms for one/two workers, gains of 17.5% and 21.4%. CSV therefore uses 1,500,000 bytes for projection-only scans. Accepted predicates retain a 12,000,000-byte crossover because the 100,000-row selective compiled query was faster sequentially. JSON rejected 25,000 rows at only 9.6% gain and accepted 50,000 rows / 3,054,243 bytes twice: 7.231/3.923 ms and 7.616/4.101 ms, gains of 45.8% and 46.2%. Its crossover is therefore 3,000,000 bytes.

### Current source and compiled-query results

Same-process source layers:

| Format / layer | Earlier wave | Wave 8 |
|---|---:|---:|
| JSON zero columns | 7.973 ms / 2.89 MiB | 1.125 ms / 0.33 MiB |
| JSON one string column | 14.394 ms / 10.52 MiB | 6.256 ms / 4.75 MiB |
| JSON all-rejected predicate | 13.758 ms / 2.10 MiB | 6.264 ms / 0.19 MiB |
| JSON three-column source | 19.645 ms / 17.39 MiB | 7.610 ms / 11.92 MiB |
| CSV zero columns | 3.333 ms / 2.11 MiB | 0.727 ms / 0.29 MiB |
| CSV one decimal column | 8.101 ms / 8.21 MiB | 7.001 ms / 8.04 MiB |
| CSV all-rejected predicate | 6.932 ms / 1.29 MiB | 6.066 ms / 0.39 MiB |
| CSV two-column source | 8.636–10.474 ms / 13.52 MiB | 6.642 ms / 5.78 MiB |

Complete compiled Musoq execution:

| Format / query | Reference wave | Wave 8 |
|---|---:|---:|
| JSON count | 19.883 ms / 10.75 MiB | 6.846 ms / 4.97 MiB |
| JSON one column | 31.249 ms / 18.09 MiB | 15.591 ms / 12.32 MiB |
| JSON full row | 41.176 ms / 34.11 MiB | 23.871 ms / 28.36 MiB |
| JSON predicate, about 10% | 18.487 ms / 4.65 MiB | 8.112 ms / 2.35 MiB |
| JSON predicate, about 50% | 29.872 ms / 13.66 MiB | 14.751 ms / 9.84 MiB |
| JSON early `take 100` | 0.791 ms / 2.37 MiB | 0.726 ms / 1.33 MiB |
| JSON grouped aggregates | 29.639 ms / 15.43 MiB | 10.042 ms / 9.68 MiB |
| CSV count | 7.336 ms / 9.00 MiB | 5.813 ms / 4.96 MiB |
| CSV one column | 23.799 ms / 16.34 MiB | 14.993 ms / 12.31 MiB |
| CSV full row | 33.435 ms / 23.21 MiB | 21.661 ms / 22.25 MiB |
| CSV predicate, about 10% | 7.572 ms / 3.66 MiB | 6.975 ms / 3.57 MiB |
| CSV predicate, about 50% | not correctness-comparable | 14.688 ms / 11.90 MiB |
| CSV early `take 100` | 0.626 ms / 0.58 MiB | 0.553 ms / 0.30 MiB |
| CSV grouped aggregates | 16.580 ms / 14.66 MiB | 9.328 ms / 10.14 MiB |

Every correctness-comparable primary warm query is faster than its reference wave. Rejected predicates allocate no projection rows or selected strings; emitted scalar rows still allocate only the positional array, required boxes, and non-reusable strings required by the current core contract.

### Ceiling status

The zero-column file gates are met, but the original parser-only percentage gates are not claimed as met. The strict CSV parser reaches 586.5 MiB/s, 14.8% of the equivalent scalar raw-memory scan; the allocation-free `Utf8JsonReader` token pass reaches 1,269.8 MiB/s, 31.7% of its raw-memory scan. The requested 75% and 50% targets compare grammar/token validation and field traversal with a single arithmetic operation per raw byte. Reaching them requires a materially different parser architecture and likely SIMD/vectorized structural scanning, which conflicts with the locked no-vectorization scope for this rework. The shortfall is retained explicitly for final evidence rather than hidden behind the source and compiled-query gains.

## Wave 9 legacy removal and documentation — 2026-08-05

Wave 9 deletes the unused JsonHelpers project, the first-record separated-values table shim, the obsolete size converter, the old header-normalization helper, and the external JSON schema fixture. JSON and SeparatedValues now have one production path each: strict UTF-8 files with exact source discovery and format-specific synchronous readers. AsyncRowsSource remains because CANBus, Git, GitHub, Jira, Os, and Roslyn actively use it; its accidental JsonHelpers dependency was removed. Roslyn's direct Newtonsoft use is now declared directly instead of arriving transitively through JsonHelpers.

Constructor XML, repository guides, representative queries, archive integration guidance, a structured-source user guide, and explicit breaking-change notes now describe the file-only contract. The frozen Newtonsoft and CsvHelper adapters remain isolated in benchmark projects so Wave 10 can repeat the same-process baseline comparison.

Focused validation commands:

```powershell
dotnet test Musoq.DataSources.Json.Tests/Musoq.DataSources.Json.Tests.csproj -c Release --nologo --verbosity quiet --logger "console;verbosity=minimal"
dotnet test Musoq.DataSources.SeparatedValues.Tests/Musoq.DataSources.SeparatedValues.Tests.csproj -c Release --nologo --verbosity quiet --logger "console;verbosity=minimal"
dotnet test Musoq.DataSources.Archives.Tests/Musoq.DataSources.Archives.Tests.csproj -c Release --nologo --verbosity quiet --logger "console;verbosity=minimal"
dotnet test Musoq.DataSources.AsyncRowsSource.Tests/Musoq.DataSources.AsyncRowsSource.Tests.csproj -c Release --nologo --verbosity quiet --logger "console;verbosity=minimal"
dotnet test Musoq.DataSources.RepresentativeTests/Musoq.DataSources.RepresentativeTests.csproj -c Release --no-restore --nologo --verbosity quiet --logger "console;verbosity=minimal"
```

- JSON: 100 passed.
- SeparatedValues: 102 passed; the existing manual profiling probe was skipped.
- Archives: 11 passed. The existing SharpCompress vulnerability warning remains outside this structured-source change.
- AsyncRowsSource: 4 passed.
- Representative queries: 46 passed; the existing package-artifact-dependent test was skipped.

## Wave 10 final bounded evidence — 2026-08-05

The final bounded measurements use the same machine, SDK, runtime, and deterministic 100,000-row fixtures recorded in Wave 0. The measured structured-source implementation is the tree at `cb9d8c8`; the only subsequent production change normalizes an already-triggered parallel cancellation exception and does not alter a successful scan hot path. Fixture verification again produced JSON checksum `50,045,000` and separated-values checksum `930,054`. No input was downloaded.

Final measurement commands:

```powershell
dotnet run --project Musoq.DataSources.Json.Benchmarks -c Release -- verify
dotnet run --project Musoq.DataSources.SeparatedValues.Benchmark -c Release -- verify
dotnet run --project Musoq.DataSources.Json.Benchmarks -c Release --no-build -- smoke
dotnet run --project Musoq.DataSources.SeparatedValues.Benchmark -c Release --no-build -- smoke

dotnet run --project Musoq.DataSources.Json.Benchmarks -c Release --no-build -- --filter "*JsonLegacySourceBenchmarks*" "*JsonMetadataBenchmarks*" "*JsonCompiledExecutionBenchmarks*"
dotnet run --project Musoq.DataSources.SeparatedValues.Benchmark -c Release --no-build -- --filter "*SeparatedValuesLegacySourceBenchmarks*" "*SeparatedValuesMetadataBenchmarks*" "*SeparatedValuesCompiledExecutionBenchmarks*"
dotnet run --project Musoq.DataSources.SeparatedValues.Benchmark -c Release --no-build -- --filter "*SeparatedValuesParserBakeoffBenchmarks*"
dotnet run --project Musoq.DataSources.SeparatedValues.Benchmark -c Release --no-build -- --filter "*SeparatedValuesZeroColumnCeilingBenchmarks*"
dotnet run --project Musoq.DataSources.SeparatedValues.Benchmark -c Release --no-build -- bakeoff-verify
```

The parser verifier accepted 2,001 randomized valid records, rejected all five malformed probes with the custom scanner, and again found no conforming library alternative.

### Discovery and parser layers

| Format / operation | Mean | Allocation | Result |
|---|---:|---:|---|
| JSON cached discovery | 233.8 us | 222.46 KiB | identity and fingerprint validation |
| JSON cold exact discovery | 17.061 ms | 296.64 KiB | complete 100,000-row scan |
| CSV cached discovery | 235.7 us | 222.81 KiB | identity and fingerprint validation |
| CSV cold exact discovery | 4.817 ms | 231.94 KiB | complete 100,000-row scan |
| JSON scalar raw-memory scan | 1.465 ms | 0 B | about 3,984 MiB/s |
| JSON `Utf8JsonReader` token scan | 4.698 ms | 0 B | about 1,242 MiB/s; 31.2% of raw memory |
| CSV scalar raw-memory scan | 0.392 ms | 0 B | about 4,016 MiB/s |
| CSV conforming custom parser, 1BRC-shaped | 2.676 ms | 249 B | about 588 MiB/s; 14.6% of raw memory |
| CSV conforming custom parser, wide | 37.322 ms | 264 B | faster than Wave 8's 38.026 ms |
| CSV conforming custom parser, quoted/multiline | 8.640 ms | 252 B | faster than Wave 8's 8.937 ms |

The exact-discovery allocation depends on columns, bounded partitions, fingerprints, and the snapshot cache rather than row offsets. The old JSON schema-file and CSV first-record metadata numbers remain semantically incomparable because they did not inspect the source completely.

The original parser-only targets remain unmet: CSV reaches 14.6%, not 75%, and JSON reaches 31.2%, not 50%, of a scalar raw-memory checksum loop. The source-level zero-column gates do pass. JSON reads the 5.836 MiB fixture in 1.124 ms versus 0.642 ms raw, or 57.1% of raw file throughput. On the 2,000,000-row / 32,995,000-byte CSV fixture, an uncontended rerun measured 6.667 ms versus 5.071 ms raw, or 76.1%, with 427.2 KiB allocated independent of row count. A prior launch containing a 17.5 ms outlier and a launch interrupted by an unrelated build were discarded under the noise policy and are not used here.

Closing the parser-only gap would require a different structural scanner, most plausibly SIMD/vectorized parsing, which was explicitly excluded from this rework. The zero-column source results demonstrate that the file, cache-validation, and current-core delivery layers meet their separate gates without such a change.

### Same-process datasource A/B

| Format / source shape | Final mean | Final allocation | Frozen legacy adapter / effect |
|---|---:|---:|---:|
| JSON full three-column source | 7.369 ms | 11.64 MiB | 134.785 ms / 61.06 MiB; 18.29x faster, 80.9% less allocation |
| JSON zero columns | 1.124 ms | 336.3 KiB | no row array or reference per source row |
| JSON one string column | 6.313 ms | 4.75 MiB | only selected values are decoded |
| JSON all-rejected predicate | 6.044 ms | 198.5 KiB | no projection rows or selected strings |
| CSV full two-column source | 6.192 ms | 5.78 MiB | 10.982 ms / 14.47 MiB; 1.77x faster, 60.1% less allocation |
| CSV zero columns, 100,000 rows | 0.719 ms | 300.5 KiB | no row array or reference per source row |
| CSV one decimal column | 5.963 ms | 8.04 MiB | selected scalar boxes remain core-required |
| CSV all-rejected predicate | 5.102-5.186 ms | 1.29 MiB | no projection rows or selected strings |

The isolated CSV rejected-predicate rerun reproduced 1.29 MiB rather than the 0.39 MiB recorded in Wave 8. There is no successful-scan production diff between those measurement points. The extra charge is consistent with BenchmarkDotNet observing a cold/trimmed 1 MiB pooled input-buffer rental; predicate-before-projection tests and the measured hot path still ensure that rejected records create neither output arrays nor selected strings. This corrected total is retained rather than silently copying the earlier number.

### Complete compiled Musoq execution

| Format / query | Wave 0 baseline | Final | Speedup | Allocation change |
|---|---:|---:|---:|---:|
| JSON count | 118.700 ms / 60.58 MiB | 6.071 ms / 4.97 MiB | 19.55x | -91.8% |
| JSON one column | 130.500 ms / 67.91 MiB | 14.548 ms / 12.32 MiB | 8.97x | -81.9% |
| JSON full row | 140.800 ms / 74.78 MiB | 22.438 ms / 28.36 MiB | 6.27x | -62.1% |
| JSON predicate, about 10% | 122.100 ms / 63.57 MiB | 7.245 ms / 2.35 MiB | 16.85x | -96.3% |
| JSON predicate, about 50% | 126.000 ms / 69.21 MiB | 13.393 ms / 9.84 MiB | 9.41x | -85.8% |
| JSON early `take 100` | 120.300 ms / 60.59 MiB | 0.657 ms / 1.33 MiB | 183.13x | -97.8% |
| JSON grouped aggregates | 130.700 ms / 63.65 MiB | 9.050 ms / 9.68 MiB | 14.44x | -84.8% |
| CSV count | 7.336 ms / 9.00 MiB | 5.151 ms / 4.96 MiB | 1.42x | -44.8% |
| CSV one column | 23.799 ms / 16.34 MiB | 14.352 ms / 12.31 MiB | 1.66x | -24.7% |
| CSV full row | 33.435 ms / 23.21 MiB | 18.802 ms / 22.24 MiB | 1.78x | -4.2% |
| CSV predicate, about 10% | 7.572 ms / 3.66 MiB | 6.542 ms / 3.57 MiB | 1.16x | -2.5% |
| CSV predicate, about 50% | not correctness-comparable | 13.148 ms / 11.90 MiB | n/a | n/a |
| CSV early `take 100` | 0.626 ms / 0.58 MiB | 0.471 ms / 0.30 MiB | 1.33x | -48.1% |
| CSV grouped aggregates | 16.580 ms / 14.66 MiB | 8.407 ms / 10.45 MiB | 1.97x | -28.7% |

Every correctness-comparable primary warm timing beats Wave 0 and remains within the 3% Wave 8 regression gate. The first final CSV compiled launch placed `Count` at 7.344 ms; the required complete-matrix rerun measured 5.151 ms and all six neighboring shapes also improved, so the first value is treated as environmental variance rather than selected silently.

### Sampled hot stacks

The benchmark executables provide `profile-source` and `profile-compiled` commands that run a warmed 100,000-row operation repeatedly for at least five seconds. Four traces were collected with the explicit `Microsoft-DotNETCore-SampleProfiler` provider by launching the built executables directly.

```powershell
dotnet-trace collect --providers Microsoft-DotNETCore-SampleProfiler --output <trace-path> -- <benchmark-executable> profile-source
dotnet-trace collect --providers Microsoft-DotNETCore-SampleProfiler --output <trace-path> -- <benchmark-executable> profile-compiled
dotnet-trace report <trace-path> topN --number 20
```

- JSON source: 728 iterations. `JsonRowLayout.Materialize` used 8.94% exclusive samples, UTF-8 structural searches 5.37%, snapshot string reuse 2.75%, `Utf8JsonReader` token consumption 1.86%, and file reads 0.89%.
- JSON compiled full row: 219 iterations. JSON materialization used 5.06%, structural searches 2.83%, string reuse 1.55%, scalar materialization 0.94%, core `Row.Values` access 0.79%, and token consumption 0.73%.
- CSV source: 1,144 iterations. `SeparatedValuesUtf8Reader.TryRead` used 3.23%, field iteration 0.95%, row materialization 0.73%, and buffer refill 0.41%.
- CSV compiled full row: 276 iterations. Value conversion and record reading each used 1.51%, the generated compiled-query projection frame 0.48%, field iteration 0.40%, and buffer refill 0.38%; GC/finalizer and runtime-allocation frames were larger than any single parser frame.

Sampling includes all runtime and adaptive-parallel worker threads, so wait/scheduling frames dominate the process-wide top list and these percentages are not wall-time shares. The actionable application frames show that further datasource work is now format-specific parsing/conversion and materialization. Removing positional `object?[]` allocation, scalar boxing, downstream row access, and related GC costs requires a future core row contract; it is outside these waves.

### Final validation and explicit macro status

Final repository validation uses the one full-suite gate reserved for Wave 10:

```powershell
dotnet restore Musoq.DataSources.sln --nologo --verbosity quiet
dotnet build Musoq.DataSources.sln -c Release --no-restore --nologo --verbosity quiet
dotnet test Musoq.DataSources.sln -c Release --no-build --nologo --verbosity quiet --logger "console;verbosity=minimal"
```

- Restore succeeded with existing SharpCompress and Jira/Newtonsoft vulnerability warnings.
- Release build succeeded. Removing JsonHelpers exposed Roslyn and CANBus imports that had relied on its accidental transitive Newtonsoft reference; both active plugins now declare that dependency directly.
- The first concurrent full test attempt lost an MSBuild node while an unrelated workspace launched 24 build nodes. A clean retry then exposed `TaskCanceledException` escaping two exact cancellation tests. The shared ordered partition runner now normalizes caller cancellation to `OperationCanceledException`; both focused regressions passed.
- The final unchanged full command passed 1,038 tests and skipped 32 intentional playground, external, manual-profile, and artifact-dependent tests across 19 projects.
- Release builds generated JSON and SeparatedValues packages containing only their DLL, runtime configuration, XML documentation, license, and package metadata; benchmark projects are not package contents.

The 100-million-row and one-billion-row CSV macros were not invoked. They require a separately explicit command and absolute output path under the locked dataset policy. Consequently the billion-row no-OOM gate, dedicated-process peak memory, independent large digest, and equivalent-byte JSON macro remain unclaimed rather than fabricated. No large file was generated and no public 1BRC data was downloaded. The explicit sequence remains:

```powershell
dotnet run --project Musoq.DataSources.SeparatedValues.Benchmark -c Release -- generate 100000000 <absolute-output-path>
# Validate the 100M result and disk/memory behavior before explicitly invoking:
dotnet run --project Musoq.DataSources.SeparatedValues.Benchmark -c Release -- generate 1000000000 <absolute-output-path>
```
