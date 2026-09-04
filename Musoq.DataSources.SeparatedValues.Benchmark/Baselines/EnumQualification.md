# SeparatedValues enum qualification

The enum gate is a deterministic `MediumRun` over 8,192 records. It warms the
production descriptor decoder and three allocation-free comparators, then runs
three isolated reports and uses the median ratio. Final-table allocation is
outside the measured loops.

```powershell
dotnet run -c Release --no-build --project Musoq.DataSources.SeparatedValues.Benchmark -- gate-enums
```

The gate qualified locally on 2026-09-03 with these three reports:

| report | numeric ratio | symbolic ratio | flags ratio | fixed-operation allocation |
| --- | ---: | ---: | ---: | ---: |
| 1 | 1.1339x | 0.9850x | 0.5750x | 0 B |
| 2 | 0.9300x | 0.9867x | 0.5724x | 0 B |
| 3 | 0.9084x | 0.9786x | 0.5759x | 0 B |
| median | 0.9300x | 0.9850x | 0.5750x | 0 B |

The thresholds are `<=1.03x` for numeric primitive parsing, `<=1.10x` for
hash-plus-exact UTF-8 symbolic comparison, `<=1.02x` for primitive masks, and
at most 1,024 bytes of fixed-operation allocation noise. The report is a
qualification record for this checkout and machine, not a universal storage
throughput claim.
