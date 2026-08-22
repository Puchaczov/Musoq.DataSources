# SeparatedValues query-scoped row qualification baseline

Qualification date: 2026-08-19

Verdict: **FAIL — keep query-scoped rows explicit opt-in.**

The carrier-only seam passed its throughput, allocation, class-lifetime, and warmed JIT architecture gates. The end-to-end numeric source did not: allocation was 11.31% to 90.86% higher than legacy, and six of the nine warmed compiled-query scenarios exceeded their allowed regression limit. Therefore the conditional default-activation wave was not created. `new SeparatedValuesSchema()` remains legacy and `new SeparatedValuesSchema(true)` remains the explicit opt-in.

## Environment

- Repository: `D:\repos\Musoq.DataSources`, `feature/runtime_v2`, Wave 5 input commit `8ca4257`
- Musoq package train: `17.0.7-alpha.1`
- Configuration: Release, x64
- OS: Windows 11 25H2, build `10.0.26200.9168`
- CPU: Intel Core Ultra 9 285K, 24 physical and 24 logical cores
- SDK: .NET SDK `10.0.303`
- Runtime: .NET `10.0.11`, x64 RyuJIT x86-64-v3, concurrent workstation GC
- BenchmarkDotNet: `0.15.8`, `ShortRun` (1 launch, 3 warmups, 3 measured iterations), MemoryDiagnoser
- Power plan reported by BenchmarkDotNet: High performance
- Source execution: `separatedvalues.max_parallelism=1`
- Compiled metadata qualification: `separatedvalues.inference_max_time_ms=1000`

An initial third compiled report was rejected because one cold compilation exhausted the production default 10 ms metadata inference budget and consequently had no timing statistics. The benchmark-only ceiling was raised to 1,000 ms, the 132-identity smoke oracle passed again, and all three compiled reports were regenerated. No old and replacement compiled samples were mixed.

## Fixtures and matrix

The source matrix uses 2,048-row comma-separated files at 2, 8, 32, and 64 fields. String records use `value-{row}-{column}`, with every thirteenth record short by one field. Nullable numeric records use deterministic integer values and an empty field when `(row + column) % 17 == 0`, except for column zero. Each file has a `Column0` through `ColumnN` header. Selective projection reads `Column0`, high rejection accepts only `Column0 = 'value-1999-0'`, and early take accepts 16 rows. A separate unterminated quoted record is used as the failure oracle.

The source cohort contains 24 benchmark methods at four widths: 96 identities per report. It compares legacy object-array rows, query structs, and query classes for full rows, selective projection, high rejection, aggregation, early take, nullable numeric input, string carrier-only materialization, and numeric carrier-only materialization.

The compiled matrix uses 512-row files and nine scenarios:

- nullable numeric full rows at 2, 8, 32, and 64 fields;
- nullable string full rows at 8 fields;
- nullable numeric selective projection at 8 fields;
- nullable string high rejection at 8 fields;
- nullable numeric aggregation at 8 fields;
- nullable numeric early take at 8 fields.

Each scenario has legacy/query warm execution and legacy/query cold compile-plus-first-run: 36 identities per report. Setup requires identical legacy/query row count, checksum, and ordering hash. The source setup additionally requires equivalent struct/class outcomes and equivalent failure type/message. `query-row-smoke` executed all 96 source and 36 compiled identities before timed qualification.

## Reproduction

Build and run the deterministic smoke oracle:

```powershell
dotnet build Musoq.DataSources.SeparatedValues.Benchmark\Musoq.DataSources.SeparatedValues.Benchmark.csproj -c Release --nologo --verbosity minimal
dotnet run --project Musoq.DataSources.SeparatedValues.Benchmark\Musoq.DataSources.SeparatedValues.Benchmark.csproj -c Release --no-build -- query-row-smoke
```

Capture three independent source and compiled reports. Use a fresh artifact directory for each run:

```powershell
1..3 | ForEach-Object { dotnet run --project Musoq.DataSources.SeparatedValues.Benchmark\Musoq.DataSources.SeparatedValues.Benchmark.csproj -c Release --no-build -- --filter "*SeparatedValuesQueryScopedSourceMaterializationBenchmarks*" --job short --memory --exporters json --artifacts "BenchmarkDotNet.Artifacts/query-row-wave6/source-$_" }
1..3 | ForEach-Object { dotnet run --project Musoq.DataSources.SeparatedValues.Benchmark\Musoq.DataSources.SeparatedValues.Benchmark.csproj -c Release --no-build -- --filter "*SeparatedValuesQueryScopedCompiledExecutionBenchmarks*" --job short --memory --exporters json --artifacts "BenchmarkDotNet.Artifacts/query-row-wave6/compiled-$_" }
```

Capture the warmed 8-field numeric struct disassembly:

```powershell
$env:COMPlus_TieredCompilation='0'
$env:COMPlus_JitDisasm='*MaterializeNumericRows*'
$env:COMPlus_JitDisasmAssemblies='Musoq.DataSources.SeparatedValues.Benchmark'
dotnet run --project Musoq.DataSources.SeparatedValues.Benchmark\Musoq.DataSources.SeparatedValues.Benchmark.csproj -c Release --no-build -- jit-query-row *> BenchmarkDotNet.Artifacts\query-row-wave6\query-row-jit-disasm.txt
```

Run the gate with each generated `*-report-full-compressed.json` file:

```powershell
dotnet run --project Musoq.DataSources.SeparatedValues.Benchmark\Musoq.DataSources.SeparatedValues.Benchmark.csproj -c Release --no-build -- gate-query-rows --source-report <source-1.json> --source-report <source-2.json> --source-report <source-3.json> --compiled-report <compiled-1.json> --compiled-report <compiled-2.json> --compiled-report <compiled-3.json> --disassembly BenchmarkDotNet.Artifacts\query-row-wave6\query-row-jit-disasm.txt
```

The gate rejects fewer than three reports, missing/invalid statistics, duplicate identities, and different scenario sets before evaluating medians. Raw BenchmarkDotNet reports and JIT dumps remain ignored.

## Median results

Times are median-of-report means in nanoseconds per operation. Allocations are median allocated bytes per operation.

### Numeric carrier-only materialization

| Fields | Legacy time | Legacy alloc | Struct time | Struct alloc | Class time | Class alloc | Struct throughput | Class ceiling | Verdict |
|---:|---:|---:|---:|---:|---:|---:|---:|---:|:---|
| 2 | 11,600.5 | 180,224 | 768.2 | 0 | 4,605.3 | 49,152 | 15.1006x | 65,536 | PASS |
| 8 | 41,604.5 | 573,440 | 3,078.2 | 0 | 10,046.6 | 98,304 | 13.5159x | 114,688 | PASS |
| 32 | 158,394.6 | 2,146,304 | 61,113.1 | 0 | 77,435.6 | 294,912 | 2.5918x | 311,296 | PASS |
| 64 | 341,291.6 | 4,243,456 | 127,452.4 | 0 | 155,789.1 | 557,056 | 2.6778x | 573,440 | PASS |

All four widths exceed 2x throughput, reduce struct-carrier overhead by 100%, allocate zero bytes for the struct carrier, and keep class allocation below the one-carrier-per-accepted-row ceiling.

### End-to-end nullable numeric CSV

| Fields | Legacy time | Legacy alloc | Query-struct time | Query-struct alloc | Allocation reduction | Time ratio | Verdict |
|---:|---:|---:|---:|---:|---:|---:|:---|
| 2 | 657,518.8 | 1,458,134 | 1,005,101.9 | 1,623,058 | -11.31% | 1.5286x | FAIL |
| 8 | 1,529,080.5 | 1,777,703 | 2,125,941.3 | 2,427,875 | -36.57% | 1.3903x | FAIL |
| 32 | 4,559,098.7 | 3,242,834 | 8,966,670.8 | 5,878,327 | -81.27% | 1.9668x | FAIL |
| 64 | 6,820,712.5 | 5,316,344 | 16,374,421.9 | 10,146,791 | -90.86% | 2.4007x | FAIL |

Every width fails the required 20% allocation reduction. Negative reduction means the query-struct path allocated more than legacy.

### Warm compiled-query execution

| Scenario | Legacy time | Legacy alloc | Query time | Query alloc | Time ratio | Limit | Verdict |
|:---|---:|---:|---:|---:|---:|---:|:---|
| NullableNumeric2Full | 774,131.7 | 1,604,388 | 792,742.1 | 1,713,367 | 1.0240x | 1.03x | PASS |
| NullableNumeric8Full | 1,924,199.6 | 1,739,244 | 2,877,157.3 | 2,059,243 | 1.4952x | 1.03x | FAIL |
| NullableNumeric32Full | 2,561,663.3 | 2,506,550 | 4,991,194.5 | 3,117,399 | 1.9484x | 1.03x | FAIL |
| NullableNumeric64Full | 4,321,700.0 | 3,525,720 | 6,161,457.0 | 4,709,917 | 1.4257x | 1.03x | FAIL |
| NullableString8Full | 1,007,778.6 | 1,552,537 | 1,032,767.2 | 1,716,205 | 1.0248x | 1.05x | PASS |
| NullableNumeric8Selective | 764,486.2 | 1,605,880 | 863,033.7 | 1,714,185 | 1.1289x | 1.03x | FAIL |
| NullableString8HighRejection | 799,119.5 | 1,556,907 | 812,402.6 | 1,557,959 | 1.0166x | 1.05x | PASS |
| NullableNumeric8Aggregation | 775,352.2 | 1,585,336 | 818,027.5 | 1,590,711 | 1.0550x | 1.03x | FAIL |
| NullableNumeric8EarlyTake | 562,474.5 | 377,516 | 581,679.9 | 380,006 | 1.0341x | 1.03x | FAIL |

### JIT architecture

PASS. The warmed concrete `MaterializeNumericRows<BenchmarkNumericRow8, BenchmarkNumericMaterializer8>` assembly region contains no `CORINFO_HELP_BOX`, `callvirt`, interface-dispatch marker, or `VIRTUAL_FUNC_PTR` marker.

## Activation decision

The overall gate fails. Query-scoped row support remains available only through `new SeparatedValuesSchema(true)`. The parameterless constructor remains disabled, `new SeparatedValuesSchema(false)` remains the explicit legacy path, and no default-activation commit is permitted from this evidence.

## Native-only qualification — 2026-08-20

Verdict: **PASS — mandatory query-scoped transfer is qualified.**

The 2026-08-19 failure above is retained as the historical pre-remediation result. Its constructor and activation statements are no longer current: commits `2309f52` and `2a2efef` removed the boolean constructor and production legacy-row implementation. The final production schema is native-only; the object-array comparator used below is the independent benchmark-only `FrozenByteNativeLegacySchema`.

### Qualification inputs

- DataSources input commit: `2a2efef`
- Live core comparator commit: `c8bb574ca`
- Initial core snapshot recorded before this migration: `9992d47a`; the checkout was advanced externally during qualification, so the live reports were regenerated from `c8bb574ca`. This migration made no tracked core changes.
- Musoq package train: `17.0.7-alpha.1`
- Configuration/runtime: Release, x64, .NET SDK `10.0.303`, .NET runtime `10.0.11`
- BenchmarkDotNet: `0.15.8`, `ShortRun`, MemoryDiagnoser
- DataSources reports: three independent 96-identity source matrices and three independent 36-identity compiled matrices
- Core reports: three independent matching source matrices and three independent matching compiled matrices
- Fixture shape, row counts, job configuration, runtime fingerprint, checksums, ordering hashes, null/empty behavior, and failure oracles were required to match before medians were evaluated.

The live core gate passed all 30 of its checks before the comparative DataSources gate was evaluated. The final comparative gate then passed all 39 checks without changing a threshold.

### Reproduction

Capture each report into a fresh artifact directory; do not overlap benchmark processes:

```powershell
1..3 | ForEach-Object {
    dotnet run --project Musoq.DataSources.SeparatedValues.Benchmark\Musoq.DataSources.SeparatedValues.Benchmark.csproj -c Release --no-build -- --filter "*SeparatedValuesQueryScopedSourceMaterializationBenchmarks*" --job short --memory --exporters json --artifacts "BenchmarkDotNet.Artifacts/native-only-final-20260820/source-$_"
}
1..3 | ForEach-Object {
    dotnet run --project Musoq.DataSources.SeparatedValues.Benchmark\Musoq.DataSources.SeparatedValues.Benchmark.csproj -c Release --no-build -- --filter "*SeparatedValuesQueryScopedCompiledExecutionBenchmarks*" --job short --memory --exporters json --artifacts "BenchmarkDotNet.Artifacts/native-only-final-20260820/compiled-$_"
}
```

The production JIT regions are both named `Read`; constrain the assembly instead of using the obsolete synthetic `MaterializeNumericRows` filter from the historical section:

```powershell
$env:COMPlus_TieredCompilation='0'
$env:COMPlus_ReadyToRun='0'
$env:COMPlus_JitDisasm='Read'
$env:COMPlus_JitDisasmAssemblies='Musoq.DataSources.SeparatedValues'
dotnet Musoq.DataSources.SeparatedValues.Benchmark\bin\Release\net10.0\Musoq.DataSources.SeparatedValues.Benchmark.dll jit-query-row *> BenchmarkDotNet.Artifacts\native-only-final-20260820\query-row-jit-disasm.txt
```

Run the final gate with all three report paths in each cohort:

```powershell
dotnet run --project Musoq.DataSources.SeparatedValues.Benchmark\Musoq.DataSources.SeparatedValues.Benchmark.csproj -c Release --no-build -- gate-query-rows `
  --source-report <datasources-source-1.json> --source-report <datasources-source-2.json> --source-report <datasources-source-3.json> `
  --compiled-report <datasources-compiled-1.json> --compiled-report <datasources-compiled-2.json> --compiled-report <datasources-compiled-3.json> `
  --core-source-report <core-source-1.json> --core-source-report <core-source-2.json> --core-source-report <core-source-3.json> `
  --disassembly BenchmarkDotNet.Artifacts\native-only-final-20260820\query-row-jit-disasm.txt
```

Raw reports and JIT dumps remain ignored. The frozen baseline implementation is tracked in `Musoq.DataSources.SeparatedValues.Benchmark/FrozenByteNativeLegacySchema.cs`.

### Final median results

Times are median-of-report means in nanoseconds per operation. Allocations are median allocated bytes per operation.

#### Numeric carrier-only materialization

| Fields | Frozen legacy time | Frozen legacy alloc | Struct time | Struct alloc | Class time | Class alloc | Struct throughput | Verdict |
|---:|---:|---:|---:|---:|---:|---:|---:|:---|
| 2 | 11,556.8 | 180,224 | 763.2 | 0 | 4,610.1 | 49,152 | 15.1428x | PASS |
| 8 | 39,262.0 | 573,440 | 3,079.6 | 0 | 9,523.4 | 98,304 | 12.7491x | PASS |
| 32 | 159,049.2 | 2,146,304 | 60,465.5 | 0 | 76,955.7 | 294,912 | 2.6304x | PASS |
| 64 | 332,699.6 | 4,243,456 | 119,745.4 | 0 | 150,819.9 | 557,056 | 2.7784x | PASS |

Every width exceeds the 2x throughput requirement, removes 100% of struct-carrier allocation, and keeps class allocation below one carrier per accepted row.

#### End-to-end nullable numeric CSV and live core comparison

| Fields | Frozen legacy time | Frozen legacy alloc | Query time | Query alloc | Alloc reduction | Core query time | Core query alloc | Time/core | Alloc/core | Verdict |
|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|:---|
| 2 | 645,520.6 | 1,424,146 | 492,178.7 | 79,231 | 94.44% | 812,158.8 | 464,178 | 0.6060x | 0.1707x | PASS |
| 8 | 1,116,740.2 | 1,775,861 | 702,171.3 | 283,818 | 84.02% | 1,460,110.4 | 1,430,635 | 0.4809x | 0.1984x | PASS |
| 32 | 4,197,418.0 | 3,306,217 | 1,863,705.6 | 1,099,608 | 66.74% | 4,735,943.8 | 5,294,193 | 0.3935x | 0.2077x | PASS |
| 64 | 5,992,725.5 | 5,378,110 | 3,303,933.6 | 2,187,615 | 59.32% | 8,840,047.9 | 10,680,410 | 0.3737x | 0.2048x | PASS |

All widths exceed the 20% end-to-end allocation reduction requirement. DataSources time and allocation are each below 80% of the matching live core query-row result at every width.

#### Warm compiled-query execution

| Scenario | Frozen legacy time | Frozen legacy alloc | Native query time | Native query alloc | Time ratio | Limit | Verdict |
|:---|---:|---:|---:|---:|---:|---:|:---|
| NullableNumeric2Full | 660,267.7 | 1,546,649 | 426,158.1 | 308,297 | 0.6454x | 1.03x | PASS |
| NullableNumeric8Full | 1,870,578.4 | 1,705,696 | 530,793.0 | 461,138 | 0.2838x | 1.03x | PASS |
| NullableNumeric32Full | 3,049,319.9 | 2,504,840 | 1,157,990.6 | 1,078,360 | 0.3798x | 1.03x | PASS |
| NullableNumeric64Full | 3,060,990.1 | 3,587,627 | 2,012,261.4 | 1,889,636 | 0.6574x | 1.03x | PASS |
| NullableString8Full | 805,409.1 | 1,551,533 | 560,892.5 | 399,868 | 0.6964x | 1.05x | PASS |
| NullableNumeric8Selective | 659,839.4 | 1,572,388 | 441,859.5 | 333,946 | 0.6696x | 1.03x | PASS |
| NullableString8HighRejection | 682,365.5 | 1,556,546 | 463,553.6 | 314,059 | 0.6793x | 1.05x | PASS |
| NullableNumeric8Aggregation | 642,117.0 | 1,552,084 | 453,478.6 | 281,026 | 0.7062x | 1.03x | PASS |
| NullableNumeric8EarlyTake | 521,785.2 | 409,805 | 425,234.6 | 279,280 | 0.8150x | 1.03x | PASS |

#### Live core gate

| Fields | Carrier throughput | Numeric CSV allocation reduction | Verdict |
|---:|---:|---:|:---|
| 2 | 15.7281x | 54.39% | PASS |
| 8 | 14.0999x | 47.96% | PASS |
| 32 | 2.7999x | 45.25% | PASS |
| 64 | 2.7639x | 44.18% | PASS |

The core warmed scenario ratios were `0.7634x`, `0.9444x`, `0.9176x`, `0.8450x`, `0.8110x`, `0.9578x`, `1.0374x`, `0.9385x`, and `1.0079x`; all remained within their 1.03x or string-heavy 1.05x limits. Core's warmed concrete struct disassembly also passed.

### Production JIT architecture

PASS. The captured `SeparatedValuesFieldReader.Read<int?>` specialization for the nullable eight-field struct carrier and `SeparatedValuesTypedValueReader.Read<int?>` both contain no nullable reflection, runtime type-array allocation, object-array allocation, boxing helper, reflection/expression call, delegate creation, `callvirt`, interface-dispatch marker, or virtual-function-pointer marker.

### Final activation decision

Mandatory query-scoped row transfer is qualified. Production SeparatedValues no longer contains an executable object-array row path; unsupported metadata or a planner-selected legacy transfer fails before opening the source instead of retrying through legacy rows.
