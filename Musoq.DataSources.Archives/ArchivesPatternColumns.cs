#nullable enable
using System;
using Musoq.DataSources.Common;

namespace Musoq.DataSources.Archives;

/// <summary>Reviewed source-row string getters. These never traverse child tables or perform API calls.</summary>
internal static class ArchivesPatternColumns
{
    internal static readonly PatternColumnBindings<EntryWrapper> EntryWrapper = new(
        (nameof(Archives.EntryWrapper.Key), static row => row.Key),
        (nameof(Archives.EntryWrapper.LinkTarget), static row => row.LinkTarget)
    );

    internal static IPatternColumnBindings? Get(string name) => name.ToLowerInvariant() switch
    {
        "file" => EntryWrapper,
        _ => null
    };
}
