using System.Threading.Tasks;
using System.Xml;
using System;
using System.Collections.Generic;

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

    public static async Task<string?> GetLicensesNamesFromNuspecAsync(XmlDocument xmlDoc, XmlNamespaceManager namespaceManager)
    {
        var licenseNodes = xmlDoc.SelectSingleNode("/nu:package/nu:metadata/nu:license", namespaceManager);

        if (licenseNodes == null) return "[]";
       
        var spdxEvaluator = new SpdxLicenseExpressionEvaluator();
        var licensesNames = await spdxEvaluator.GetLicenseIdentifiersAsync(licenseNodes.InnerText);

        return System.Text.Json.JsonSerializer.Serialize(licensesNames);
    }
    
    public static async Task<string?> GetLicenseContentFromNuspecAsync(XmlDocument xmlDoc, XmlNamespaceManager namespaceManager)
    {
        var licenseNodes = xmlDoc.SelectSingleNode("/nu:package/nu:metadata/nu:license", namespaceManager);

        if (licenseNodes == null) return null;
        
        var spdxEvaluator = new SpdxLicenseExpressionEvaluator();
        var licensesNames = await spdxEvaluator.GetLicenseIdentifiersAsync(licenseNodes.InnerText);
        var licensesContent = new List<string>();
        
        foreach (var license in licensesNames)
        {
            var content = await spdxEvaluator.GetLicenseContentAsync(license);
            licensesContent.Add(content);
        }
        
        return System.Text.Json.JsonSerializer.Serialize(licensesContent);
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
            // Consider logging the exception here if needed
            return null;
        }
    }
}