using System.Threading.Tasks;
using System.Xml;
using System;
using System.Collections.Generic;
using System.Threading;
using System.IO;

namespace Musoq.DataSources.Roslyn.Components.NuGet.Helpers;

internal static class NuspecHelpers
{
    public static async Task<string?> GetProjectUrlFromNuspecAsync(XmlDocument xmlDoc, XmlNamespaceManager namespaceManager)
    {
        return await Task.FromResult(GetValue(xmlDoc, namespaceManager, "/nu:package/nu:metadata/nu:projectUrl"));
    }

    public static async Task<string?> GetTitleFromNuspecAsync(XmlDocument xmlDoc, XmlNamespaceManager namespaceManager)
    {
        return await Task.FromResult(GetValue(xmlDoc, namespaceManager, "/nu:package/nu:metadata/nu:title"));
    }

    public static async Task<string?> GetAuthorsFromNuspecAsync(XmlDocument xmlDoc, XmlNamespaceManager namespaceManager)
    {
        return await Task.FromResult(GetValue(xmlDoc, namespaceManager, "/nu:package/nu:metadata/nu:authors"));
    }

    public static async Task<string?> GetOwnersFromNuspecAsync(XmlDocument xmlDoc, XmlNamespaceManager namespaceManager)
    {
        return await Task.FromResult(GetValue(xmlDoc, namespaceManager, "/nu:package/nu:metadata/nu:owners"));
    }

    public static async Task<string?> GetRequireLicenseAcceptanceFromNuspecAsync(XmlDocument xmlDoc, XmlNamespaceManager namespaceManager)
    {
        return await Task.FromResult(GetValue(xmlDoc, namespaceManager, "/nu:package/nu:metadata/nu:requireLicenseAcceptance"));
    }

    public static async Task<string?> GetDescriptionFromNuspecAsync(XmlDocument xmlDoc, XmlNamespaceManager namespaceManager)
    {
        return await Task.FromResult(GetValue(xmlDoc, namespaceManager, "/nu:package/nu:metadata/nu:description"));
    }

    public static async Task<string?> GetSummaryFromNuspecAsync(XmlDocument xmlDoc, XmlNamespaceManager namespaceManager)
    {
        return await Task.FromResult(GetValue(xmlDoc, namespaceManager, "/nu:package/nu:metadata/nu:summary"));
    }

    public static async Task<string?> GetReleaseNotesFromNuspecAsync(XmlDocument xmlDoc, XmlNamespaceManager namespaceManager)
    {
        return await Task.FromResult(GetValue(xmlDoc, namespaceManager, "/nu:package/nu:metadata/nu:releaseNotes"));
    }

    public static async Task<string?> GetCopyrightFromNuspecAsync(XmlDocument xmlDoc, XmlNamespaceManager namespaceManager)
    {
        return await Task.FromResult(GetValue(xmlDoc, namespaceManager, "/nu:package/nu:metadata/nu:copyright"));
    }

    public static async Task<string?> GetLanguageFromNuspecAsync(XmlDocument xmlDoc, XmlNamespaceManager namespaceManager)
    {
        return await Task.FromResult(GetValue(xmlDoc, namespaceManager, "/nu:package/nu:metadata/nu:language"));
    }

    public static async Task<string?> GetTagsFromNuspecAsync(XmlDocument xmlDoc, XmlNamespaceManager namespaceManager)
    {
        return await Task.FromResult(GetValue(xmlDoc, namespaceManager, "/nu:package/nu:metadata/nu:tags"));
    }

    public static async Task<string?> GetLicensesNamesFromNuspecAsync(XmlDocument xmlDoc, XmlNamespaceManager namespaceManager, NuGetResource commonResources, INuGetPropertiesResolver nuGetPropertiesResolver, CancellationToken cancellationToken)
    {
        var licenseNode = xmlDoc.SelectSingleNode("/nu:package/nu:metadata/nu:license", namespaceManager);

        if (licenseNode == null) return "[]";
        
        var typeAttribute = licenseNode.Attributes?["type"]?.Value;
        
        if (string.Equals(typeAttribute, "file", StringComparison.OrdinalIgnoreCase))
        {
            var licenseFilePath = licenseNode.InnerText;
            var fullPath = Path.Combine(Path.GetDirectoryName(commonResources.PackagePath) ?? string.Empty, licenseFilePath);

            if (!File.Exists(fullPath)) return "[]";
            
            var licenseContent = await File.ReadAllTextAsync(fullPath, cancellationToken);
            return System.Text.Json.JsonSerializer.Serialize(await nuGetPropertiesResolver.GetLicensesNamesAsync(licenseContent, cancellationToken));
        }

        // type="expression" or not specified
        var licensesNames = await SpdxLicenseExpressionEvaluator.GetLicenseIdentifiersAsync(licenseNode.InnerText);
        return System.Text.Json.JsonSerializer.Serialize(licensesNames);
    }
    
    public static async Task<string?> GetLicenseContentFromNuspecAsync(XmlDocument xmlDoc, XmlNamespaceManager namespaceManager, NuGetResource commonResources, CancellationToken cancellationToken)
    {
        var licenseNode = xmlDoc.SelectSingleNode("/nu:package/nu:metadata/nu:license", namespaceManager);

        if (licenseNode == null) return null;
        
        var typeAttribute = licenseNode.Attributes?["type"]?.Value;
        
        if (string.Equals(typeAttribute, "file", StringComparison.OrdinalIgnoreCase))
        {
            // Handle license with type="file"
            var licenseFilePath = licenseNode.InnerText;
            var fullPath = Path.Combine(Path.GetDirectoryName(commonResources.PackagePath) ?? string.Empty, licenseFilePath);

            if (!File.Exists(fullPath)) return null;
            
            var licenseContent = await File.ReadAllTextAsync(fullPath, cancellationToken);
            return System.Text.Json.JsonSerializer.Serialize(new List<string> { licenseContent });
        }

        if (string.Equals(typeAttribute, "expression", StringComparison.OrdinalIgnoreCase))
        {
            // I don't want to use SPDX license here, preferably get it from repository later.
            return null;
        }

        return null;
    }

    public static Task<string?> GetLicenseUrlFromNuspecAsync(XmlDocument xmlDoc, XmlNamespaceManager namespaceManager)
    {
        return Task.FromResult(GetValue(xmlDoc, namespaceManager, "/nu:package/nu:metadata/nu:licenseUrl"));
    }

    private static string? GetValue(XmlDocument xmlDoc, XmlNamespaceManager namespaceManager, string xpath)
    {
        try
        {
            var node = xmlDoc.SelectSingleNode(xpath, namespaceManager);
            return node?.InnerText;
        }
        catch (Exception)
        {
            return null;
        }
    }
}