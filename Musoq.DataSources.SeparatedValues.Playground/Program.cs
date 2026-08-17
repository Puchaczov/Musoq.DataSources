using Musoq.DataSources.SeparatedValues.Playground;

if (args is ["manual-profile"])
    return SeparatedValuesManualPerformanceProbe.Run();

if (args.Length == 0 || args is ["--help"] or ["-h"])
{
    Console.WriteLine("Usage: dotnet run --project Musoq.DataSources.SeparatedValues.Playground -- [manual-profile]");
    return 0;
}

Console.Error.WriteLine($"Unknown argument: {string.Join(' ', args)}");
Console.Error.WriteLine("Use --help for usage.");
return 2;
