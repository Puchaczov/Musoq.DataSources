using BenchmarkDotNet.Running;
using Musoq.DataSources.Roslyn.Benchmarks;

BenchmarkSwitcher.FromAssembly(typeof(NativeNameSelectionBenchmarks).Assembly).Run(args);
