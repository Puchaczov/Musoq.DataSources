using Musoq.DataSources.SeparatedValues.Playground;

if (args is ["manual-profile"])
    return SeparatedValuesManualPerformanceProbe.Run();

if (args is ["prepare-large", var directory, var sizeText] &&
    int.TryParse(sizeText, out var sizeGiB) &&
    sizeGiB > 0)
    return SeparatedValuesLargeProfile.Prepare(directory, sizeGiB);

if (args is ["profile-large", var manifest, var shape, var workerText, var cacheMode] &&
    int.TryParse(workerText, out var workers) &&
    workers >= 0)
    return SeparatedValuesLargeProfile.Run(manifest, shape, workers, cacheMode);

if (args is ["large-profile-smoke"])
    return SeparatedValuesLargeProfile.Smoke();

if (args.Length == 0 || args is ["--help"] or ["-h"])
{
    Console.WriteLine("Usage:");
    Console.WriteLine("  dotnet run --project Musoq.DataSources.SeparatedValues.Playground -- manual-profile");
    Console.WriteLine("  dotnet run --project Musoq.DataSources.SeparatedValues.Playground -- prepare-large <directory> <size-gib>");
    Console.WriteLine("  dotnet run --project Musoq.DataSources.SeparatedValues.Playground -- profile-large <manifest> <shape> <workers> <cache-mode>");
    Console.WriteLine("  dotnet run --project Musoq.DataSources.SeparatedValues.Playground -- large-profile-smoke");
    Console.WriteLine();
    Console.WriteLine("Large shapes: projected-one-long, projected-two-numerics, projected-late-column-100,");
    Console.WriteLine("              projected-low-cardinality-string, projected-high-cardinality-string,");
    Console.WriteLine("              projected-predicate-same-column, runtime-sum,");
    Console.WriteLine("              runtime-low-cardinality-group-by, runtime-count-star, raw-ceiling");
    Console.WriteLine("Cache modes: buffered-unprimed, warm, windows-unbuffered-ceiling");
    return 0;
}

Console.Error.WriteLine($"Unknown argument: {string.Join(' ', args)}");
Console.Error.WriteLine("Use --help for usage.");
return 2;
