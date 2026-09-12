# Git reference benchmarks

These scenarios qualify the streaming Git reference readers locally and offline. They cover local tags, stashes,
nested references, and live remote-tag advertisements from temporary bare repositories. No scenario contacts a hosted
remote, fetches objects, or changes client refs.

## Corpus profiles

`GitReferenceBenchmarkCorpusFactory` generates corpora below the operating-system temporary directory:

```text
<temp>/musoq-git-reference-benchmarks/references-v9/<profile>/<loose|packed>/
```

Each corpus contains a working repository, a bare remote, a client with only `origin` configuration, a manifest, a
captured `remote-advertisement.txt`, and streaming-generated expected checksums.

| Profile | Tags | Annotated tags | Stashes |
| --- | ---: | ---: | ---: |
| `smoke` | 1,000 | 100 | 8 |
| `verify` | 100,000 | 5,000 | 256 |
| `scale` | 1,000,000 | 10,000 | 1,024 |

Both loose-ref and packed-ref variants are generated. Tags are distributed deterministically across 64 target commits;
stash reflogs are generated through local Git plumbing. The scale profile qualifies million-reference metadata and
parser behavior, not multi-gigabyte historical object storage.

## Verification

Run from the repository root with a locally installed Git CLI:

```powershell
dotnet run --project Musoq.DataSources.Git.Benchmarks/Musoq.DataSources.Git.Benchmarks.csproj --configuration Release --no-restore -- references-verify smoke
dotnet run --project Musoq.DataSources.Git.Benchmarks/Musoq.DataSources.Git.Benchmarks.csproj --configuration Release --no-restore -- references-verify verify
dotnet run --project Musoq.DataSources.Git.Benchmarks/Musoq.DataSources.Git.Benchmarks.csproj --configuration Release --no-restore -- references-verify scale
```

Verification fails on checksum/count mismatches, incorrect peel metadata, client ref or object mutation, process leaks,
cancellation failures, unbounded source buffers, missing early termination, or a missing local captured advertisement.
The verifier also checks `TAKE 0` before a Git process is started and replays the captured advertisement through the
remote parser.

## Benchmark layers

The BenchmarkDotNet classes are layered so a query result collection is never used as the direct-reader sink:

- `GitReferenceReaderBenchmarks` consumes direct reader records through rolling checksums.
- `GitReferenceSourceBenchmarks` consumes bounded source chunks and records source diagnostics.
- `GitRemoteTagSourceBenchmarks` uses only the local bare remote.
- `GitReferenceCompiledQueryBenchmarks` exercises compiled Musoq SQL with bounded `TAKE` windows where evaluator
  materialization would otherwise dominate.

Local reference backends are run separately as `auto`, `git-cli`, and `libgit2`. Remote tags are always Git CLI because
the live remote advertisement contract has no LibGit2 fallback. `FrozenLegacyGitReferenceReaders` intentionally uses
materializing LibGit2/nested access and is retained only as a frozen comparison boundary.

## Report-only profiling

The profiler emits JSON and does not enforce wall-clock, allocation, RSS, throughput, or regression thresholds:

```powershell
dotnet run --project Musoq.DataSources.Git.Benchmarks/Musoq.DataSources.Git.Benchmarks.csproj --configuration Release --no-restore -- references-profile verify
```

Each result includes the corpus fingerprint, Git/runtime/OS/CPU context, backend and projection mode, first and warm
durations, managed allocation and GC counters, parent/child working set, process start/complete/stop counts, cleanup
status, rolling checksums, rows examined/emitted/filtered/skipped, maximum buffered rows, and the complete source
metrics dictionary. `references-profile` output is report data only and is not a CI gate.

## Deferred scope

Multi-gigabyte history corpora, hosted-remote authentication, network-dependent tests, frozen numeric baselines,
throughput comparisons, RSS/allocation thresholds, and benchmark CI gates remain deliberately deferred. The generated
corpora and metrics schema provide stable inputs for adding those measurements later without changing the streaming
reader contract.
