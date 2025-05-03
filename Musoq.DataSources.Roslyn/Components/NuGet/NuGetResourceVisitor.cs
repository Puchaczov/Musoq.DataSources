using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace Musoq.DataSources.Roslyn.Components.NuGet;

internal class NuGetResourceVisitor(
    NuGetResource commonResources, 
    INuGetRetrievalService nuGetRetrievalService,
    string? customApiEndpoint,
    IReadOnlyDictionary<string, HashSet<string>> bannedPropertiesValues,
    ResolveValueStrategy resolveValueStrategy,
    ILogger logger
) : INuGetResourceVisitor
{
    private static readonly HashSet<string> EmptyBannedPropertiesValues = [];
    
    public async Task VisitLicensesAsync(CancellationToken cancellationToken)
    {
        var property = "LicensesNames";
        var resolvedValue = await RetrieveMetadataJsonArrayAsync(property, cancellationToken);
        var licensesNames = JsonConvert.DeserializeObject<string[]>(resolvedValue ?? "[]")!;

        for (var index = 0; index < licensesNames.Length; index++)
        {
            var licenseName = licensesNames[index];
            commonResources.LookingForLicense = licenseName;
            commonResources.LookingForLicenseIndex = index;
            
            commonResources.AddLicense(new NuGetLicense
            {
                License = licenseName
            });

            var properties = new Queue<(string Property, Action<string?, NuGetLicense> ModifyAction)>();
            properties.Enqueue((nameof(NuGetLicense.LicenseUrl), (value, license) => license.LicenseUrl = value));
            properties.Enqueue((nameof(NuGetLicense.LicenseContent), (value, license) => license.LicenseContent = value));

            for (; properties.Count != 0;)
            {
                (property, var modifyAction) = properties.Dequeue();
                resolvedValue = await RetrieveMetadataAsync(property, cancellationToken);
                commonResources.ModifyLicenseProperty(resolvedValue, modifyAction);
            }
        }

        commonResources.LookingForLicense = null;
        commonResources.LookingForLicenseIndex = null;
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
        var bannedPropertyValues = bannedPropertiesValues.GetValueOrDefault(propertyName, EmptyBannedPropertiesValues);
        
        string? resolvedValue = null;
        if (commonResources.PackagePath is not null)
        {
            resolvedValue = await nuGetRetrievalService.GetMetadataFromPathAsync(
                commonResources,
                propertyName,
                cancellationToken);

            if (resolvedValue is not null && bannedPropertyValues.Contains(resolvedValue))
            {
                resolvedValue = null;
            }
        }
        if (resolvedValue is null)
        {
            try
            {
                resolvedValue = await nuGetRetrievalService.GetMetadataFromNugetOrgAsync(
                    "https://www.nuget.org",
                    commonResources,
                    propertyName,
                    cancellationToken);

                if (resolvedValue is not null && bannedPropertyValues.Contains(resolvedValue))
                {
                    resolvedValue = null;
                }
            }
            catch(Exception exc)
            {
                logger.LogError(exc, "Failed to retrieve metadata from NuGet.org for property {PropertyName} ({PackageName}, {Version})", propertyName, commonResources.PackageName, commonResources.PackageVersion);
                
                resolvedValue = null;
            }
        }
        if (resolvedValue is null && !string.IsNullOrEmpty(customApiEndpoint))
        {
            try
            {
                resolvedValue = await nuGetRetrievalService.GetMetadataFromCustomApiAsync(
                    customApiEndpoint,
                    commonResources,
                    propertyName,
                    cancellationToken);

                if (resolvedValue is not null && bannedPropertyValues.Contains(resolvedValue))
                {
                    resolvedValue = null;
                }
            }
            catch (Exception exc)
            {
                logger.LogError(exc, "Failed to retrieve metadata from custom API for property {PropertyName} ({PackageName}, {Version})", propertyName, commonResources.PackageName, commonResources.PackageVersion);
                
                resolvedValue = null;
            }
        }
        return resolvedValue;
    }
    
    private async Task<string?> RetrieveMetadataJsonArrayAsync(string propertyName, CancellationToken cancellationToken)
    {
        var bannedPropertyValues = bannedPropertiesValues.GetValueOrDefault(propertyName, EmptyBannedPropertiesValues);

        string? resolvedValue = null;
        if (commonResources.PackagePath is not null)
        {
            resolvedValue = await nuGetRetrievalService.GetMetadataFromPathAsync(
                commonResources,
                propertyName,
                cancellationToken);

            if (resolvedValue is not null && bannedPropertyValues.Contains(resolvedValue))
            {
                resolvedValue = null;
            }
        }

        switch (resolveValueStrategy)
        {
            case ResolveValueStrategy.UseNugetOrgApiOnly:
                if (resolvedValue is "[]" or null)
                {
                    try
                    {
                        resolvedValue = await nuGetRetrievalService.GetMetadataFromNugetOrgAsync(
                            "https://www.nuget.org",
                            commonResources,
                            propertyName,
                            cancellationToken);

                        if (resolvedValue is not null && bannedPropertyValues.Contains(resolvedValue))
                        {
                            resolvedValue = null;
                        }
                    }
                    catch (Exception exc)
                    {
                        logger.LogError(exc, "Failed to retrieve metadata from NuGet.org for property {PropertyName} ({PackageName}, {Version})", propertyName, commonResources.PackageName, commonResources.PackageVersion);
                
                        resolvedValue = null;
                    }
                }
                break;
            case ResolveValueStrategy.UseCustomApiOnly:
                if (resolvedValue is "[]" or null && !string.IsNullOrEmpty(customApiEndpoint))
                {
                    try
                    {
                        resolvedValue = await nuGetRetrievalService.GetMetadataFromCustomApiAsync(
                            customApiEndpoint,
                            commonResources,
                            propertyName,
                            cancellationToken);
                
                        if (resolvedValue is not null && bannedPropertyValues.Contains(resolvedValue))
                        {
                            resolvedValue = null;
                        }
                    }
                    catch (Exception exc)
                    {
                        logger.LogError(exc, "Failed to retrieve metadata from custom API for property {PropertyName} ({PackageName}, {Version})", propertyName, commonResources.PackageName, commonResources.PackageVersion);
                
                        resolvedValue = null;
                    }
                }
                break;
            case ResolveValueStrategy.UseNugetOrgApiAndCustomApi:
                if (resolvedValue is "[]" or null)
                {
                    try
                    {
                        resolvedValue = await nuGetRetrievalService.GetMetadataFromNugetOrgAsync(
                            "https://www.nuget.org",
                            commonResources,
                            propertyName,
                            cancellationToken);

                        if (resolvedValue is not null && bannedPropertyValues.Contains(resolvedValue))
                        {
                            resolvedValue = null;
                        }
                    }
                    catch (Exception exc)
                    {
                        logger.LogError(exc, "Failed to retrieve metadata from NuGet.org for property {PropertyName} ({PackageName}, {Version})", propertyName, commonResources.PackageName, commonResources.PackageVersion);
                
                        resolvedValue = null;
                    }
                }
            
                if (resolvedValue is "[]" or null && !string.IsNullOrEmpty(customApiEndpoint))
                {
                    try
                    {
                        resolvedValue = await nuGetRetrievalService.GetMetadataFromCustomApiAsync(
                            customApiEndpoint,
                            commonResources,
                            propertyName,
                            cancellationToken);
                
                        if (resolvedValue is not null && bannedPropertyValues.Contains(resolvedValue))
                        {
                            resolvedValue = null;
                        }
                    }
                    catch (Exception exc)
                    {
                        logger.LogError(exc, "Failed to retrieve metadata from custom API for property {PropertyName} ({PackageName}, {Version})", propertyName, commonResources.PackageName, commonResources.PackageVersion);
                
                        resolvedValue = null;
                    }
                }
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(resolveValueStrategy), resolveValueStrategy, null);
        }
            
        return resolvedValue;
    }
}