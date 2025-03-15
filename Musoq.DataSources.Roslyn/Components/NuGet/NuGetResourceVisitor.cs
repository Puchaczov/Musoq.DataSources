using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace Musoq.DataSources.Roslyn.Components.NuGet;

internal class NuGetResourceVisitor(
    NuGetResource commonResources, 
    INuGetRetrievalService nuGetRetrievalService,
    string? customApiEndpoint
) : INuGetResourceVisitor
{
    private readonly SortedSet<TimingRecord> _timingRecords = new(new TimingRecordComparer());

    public async Task VisitLicensesAsync(CancellationToken cancellationToken)
    {
        var mappedProperties = new Dictionary<string, string?>();
        var property = "LicensesNames";
        var resolvedValue = await RetrieveMetadataJsonArrayAsync(property, cancellationToken);
        mappedProperties[property] = resolvedValue;
        var licensesNames = JsonConvert.DeserializeObject<string[]>(resolvedValue ?? "[]")!;
        var properties = new Queue<string>();
        var licenses = new List<NuGetLicense>();
        
        foreach (var licenseName in licensesNames)
        {
            commonResources.LookingForLicense = licenseName;
            
            properties.Enqueue(nameof(NuGetLicense.LicenseUrl));
            properties.Enqueue(nameof(NuGetLicense.LicenseContent));
        
            for (; properties.Count != 0;)
            {
                property = properties.Dequeue();
                resolvedValue = await RetrieveMetadataAsync(property, cancellationToken);
                mappedProperties[property] = resolvedValue;
            }
            
            licenses.Add(new NuGetLicense
            {
                License = licenseName,
                LicenseUrl = mappedProperties[nameof(NuGetLicense.LicenseUrl)],
                LicenseContent = mappedProperties[nameof(NuGetLicense.LicenseContent)]
            });
        }

        commonResources.LookingForLicense = null;
        commonResources.Licenses = licenses.ToArray();
    }

    public async Task VisitProjectUrlAsync(CancellationToken cancellationToken)
    {
        commonResources.ProjectUrl = await RetrieveMetadataAsync(nameof(commonResources.ProjectUrl), cancellationToken);
    }

    public async Task VisitTitleAsync(CancellationToken cancellationToken)
    {
        commonResources.Title = await RetrieveMetadataAsync(nameof(commonResources.Title), cancellationToken);
    }

    public async Task VisitAuthorsAsync(CancellationToken cancellationToken)
    {
        commonResources.Authors = await RetrieveMetadataAsync(nameof(commonResources.Authors), cancellationToken);
    }

    public async Task VisitOwnersAsync(CancellationToken cancellationToken)
    {
        commonResources.Owners = await RetrieveMetadataAsync(nameof(commonResources.Owners), cancellationToken);
    }

    public async Task VisitRequireLicenseAcceptanceAsync(CancellationToken cancellationToken)
    {
        var resolvedValue = await RetrieveMetadataAsync(nameof(commonResources.RequireLicenseAcceptance), cancellationToken);
        if (!string.IsNullOrEmpty(resolvedValue) && bool.TryParse(resolvedValue, out var parsed))
            commonResources.RequireLicenseAcceptance = parsed;
    }

    public async Task VisitDescriptionAsync(CancellationToken cancellationToken)
    {
        commonResources.Description = await RetrieveMetadataAsync(nameof(commonResources.Description), cancellationToken);
    }

    public async Task VisitSummaryAsync(CancellationToken cancellationToken)
    {
        commonResources.Summary = await RetrieveMetadataAsync(nameof(commonResources.Summary), cancellationToken);
    }

    public async Task VisitReleaseNotesAsync(CancellationToken cancellationToken)
    {
        commonResources.ReleaseNotes = await RetrieveMetadataAsync(nameof(commonResources.ReleaseNotes), cancellationToken);
    }

    public async Task VisitCopyrightAsync(CancellationToken cancellationToken)
    {
        commonResources.Copyright = await RetrieveMetadataAsync(nameof(commonResources.Copyright), cancellationToken);
    }

    public async Task VisitLanguageAsync(CancellationToken cancellationToken)
    {
        commonResources.Language = await RetrieveMetadataAsync(nameof(commonResources.Language), cancellationToken);
    }

    public async Task VisitTagsAsync(CancellationToken cancellationToken)
    {
        commonResources.Tags = await RetrieveMetadataAsync(nameof(commonResources.Tags), cancellationToken);
    }
    
    private async Task<string?> RetrieveMetadataAsync(string propertyName, CancellationToken cancellationToken)
    {
        var sw = Stopwatch.StartNew();
        string? resolvedValue = null;
        var calledMethods = new List<string>();
        try
        {
            if (commonResources.PackagePath is not null)
            {
                calledMethods.Add("GetMetadataFromPathAsync");
                resolvedValue = await nuGetRetrievalService.GetMetadataFromPathAsync(
                    commonResources,
                    propertyName,
                    cancellationToken);
            }
            if (resolvedValue is null)
            {
                try
                {
                    calledMethods.Add("GetMetadataFromNugetOrgAsync");
                    resolvedValue = await nuGetRetrievalService.GetMetadataFromNugetOrgAsync(
                        "https://nuget.org",
                        commonResources,
                        propertyName,
                        cancellationToken);
                }
                catch
                {
                    resolvedValue = null;
                }
            }
            if (resolvedValue is null && !string.IsNullOrEmpty(customApiEndpoint))
            {
                try
                {
                    calledMethods.Add("GetMetadataFromCustomApiAsync");
                    resolvedValue = await nuGetRetrievalService.GetMetadataFromCustomApiAsync(
                        customApiEndpoint,
                        commonResources,
                        propertyName,
                        cancellationToken);
                }
                catch (Exception)
                {
                    resolvedValue = null;
                }
            }
            return resolvedValue;
        }
        finally
        {
            sw.Stop();
            lock (_timingRecords)
            {
                _timingRecords.Add(new TimingRecord("RetrieveMetadataAsync", propertyName, sw.Elapsed, string.Join(", ", calledMethods)));
            }
        }
    }
    
    private async Task<string?> RetrieveMetadataJsonArrayAsync(string propertyName, CancellationToken cancellationToken)
    {
        var sw = Stopwatch.StartNew();
        string? resolvedValue = null;
        var calledMethods = new List<string>();
        try
        {
            if (commonResources.PackagePath is not null)
            {
                calledMethods.Add("GetMetadataFromPathAsync");
                resolvedValue = await nuGetRetrievalService.GetMetadataFromPathAsync(
                    commonResources,
                    propertyName,
                    cancellationToken);
                if (resolvedValue is not "[]")
                {
                    // value found; no further calls
                }
            }
            if (resolvedValue is "[]" || resolvedValue is null)
            {
                try
                {
                    calledMethods.Add("GetMetadataFromNugetOrgAsync");
                    resolvedValue = await nuGetRetrievalService.GetMetadataFromNugetOrgAsync(
                        "https://nuget.org",
                        commonResources,
                        propertyName,
                        cancellationToken);
                }
                catch
                {
                    resolvedValue = null;
                }
            }
            if (resolvedValue is "[]" or null && !string.IsNullOrEmpty(customApiEndpoint))
            {
                try
                {
                    calledMethods.Add("GetMetadataFromCustomApiAsync");
                    resolvedValue = await nuGetRetrievalService.GetMetadataFromCustomApiAsync(
                        customApiEndpoint,
                        commonResources,
                        propertyName,
                        cancellationToken);
                }
                catch (Exception)
                {
                    resolvedValue = null;
                }
            }
            return resolvedValue;
        }
        finally
        {
            sw.Stop();
            lock (_timingRecords)
            {
                _timingRecords.Add(new TimingRecord("RetrieveMetadataJsonArrayAsync", propertyName, sw.Elapsed, string.Join(", ", calledMethods)));
            }
        }
    }

    [DebuggerDisplay("{MethodName} {PropertyName} {Duration} {MethodCalls}")]
    private class TimingRecord(string methodName, string propertyName, TimeSpan duration, string methodCalls)
    {
        public string MethodName { get; } = methodName;
        public string PropertyName { get; } = propertyName;
        public TimeSpan Duration { get; } = duration;
        public string MethodCalls { get; } = methodCalls;
    }

    private class TimingRecordComparer : IComparer<TimingRecord>
    {
        public int Compare(TimingRecord? x, TimingRecord? y)
        {
            if (x is null || y is null)
                return 0;
            var cmp = y.Duration.CompareTo(x.Duration); // sorted descending by duration
            if (cmp == 0)
                cmp = string.Compare(x.MethodName, y.MethodName, StringComparison.Ordinal);
            if (cmp == 0)
                cmp = string.Compare(x.PropertyName, y.PropertyName, StringComparison.Ordinal);
            return cmp;
        }
    }
}