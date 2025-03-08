using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Musoq.DataSources.Roslyn.Components.NuGet;
using Newtonsoft.Json;

namespace Musoq.DataSources.Roslyn.Components;

internal class RetrieveCommonResourcesVisitor(
    CommonResources commonResources, 
    INuGetRetrievalService nuGetRetrievalService,
    string? customApiEndpoint
) : ICommonResourcesVisitor
{

    public async Task VisitLicensesAsync(CancellationToken cancellationToken)
    {
        var mappedProperties = new Dictionary<string, string?>();
        var property = "LicensesNames";
        var resolvedValue = await RetrieveMetadataJsonArrayAsync(property, cancellationToken);
        mappedProperties[property] = resolvedValue;
        var licensesNames = JsonConvert.DeserializeObject<string[]>(resolvedValue ?? "[]")!;
        var properties = new Queue<string>();
        var licenses = new List<ProjectLicense>();
        
        foreach (var license in licensesNames)
        {
            properties.Enqueue(nameof(ProjectLicense.LicenseUrl));
            properties.Enqueue(nameof(ProjectLicense.LicenseContent));
        
            for (; properties.Count != 0;)
            {
                property = properties.Dequeue();
                resolvedValue = await RetrieveMetadataAsync(property, cancellationToken);
                mappedProperties[property] = resolvedValue;
            }
            
            licenses.Add(new ProjectLicense
            {
                License = license,
                LicenseUrl = mappedProperties[nameof(ProjectLicense.LicenseUrl)],
                LicenseContent = mappedProperties[nameof(ProjectLicense.LicenseContent)]
            });
        }
        
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
        string? resolvedValue = null;
        
        if (commonResources.PackagePath is not null)
        {
            resolvedValue = await nuGetRetrievalService.GetMetadataFromPathAsync(
                commonResources,
                propertyName,
                cancellationToken);
        }

        if (resolvedValue is not null) 
            return resolvedValue;
        
        try
        {
            resolvedValue = await nuGetRetrievalService.GetMetadataFromNugetOrgAsync(
                "https://api.nuget.org",
                commonResources,
                propertyName,
                cancellationToken);
        }
        catch
        {
            resolvedValue = null;
        }
        
        if (resolvedValue is not null  || string.IsNullOrEmpty(customApiEndpoint)) 
            return resolvedValue;

        try
        {
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

        return resolvedValue;
    }
    
    private async Task<string?> RetrieveMetadataJsonArrayAsync(string propertyName, CancellationToken cancellationToken)
    {
        string? resolvedValue = null;
        
        if (commonResources.PackagePath is not null)
        {
            resolvedValue = await nuGetRetrievalService.GetMetadataFromPathAsync(
                commonResources,
                propertyName,
                cancellationToken);
        }
        
        if (resolvedValue is not "[]")
            return resolvedValue;
        
        try
        {
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
        
        if (resolvedValue is not "[]" || string.IsNullOrEmpty(customApiEndpoint)) 
            return resolvedValue;
        
        try
        {
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
        
        return resolvedValue;
    }
}