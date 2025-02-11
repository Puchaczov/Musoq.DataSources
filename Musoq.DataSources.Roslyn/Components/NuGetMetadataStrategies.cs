using HtmlAgilityPack;
using System.Xml;

namespace Musoq.DataSources.Roslyn.Components
{
    internal static class NuGetMetadataStrategies
    {
        public static string? GetLicenseUrlFromNuspec(XmlDocument xmlDoc, XmlNamespaceManager namespaceManager)
        {
            return GetValue(xmlDoc, namespaceManager, "//nu:metadata/nu:licenseUrl");
        }

        public static string? GetProjectUrlFromNuspec(XmlDocument xmlDoc, XmlNamespaceManager namespaceManager)
        {
            return GetValue(xmlDoc, namespaceManager, "//nu:metadata/nu:projectUrl");
        }

        public static string? GetLicenseFromNuspec(XmlDocument xmlDoc, XmlNamespaceManager namespaceManager)
        {
            return GetValue(xmlDoc, namespaceManager, "//nu:metadata/nu:license");
        }

        public static string? GetTitleFromNuspec(XmlDocument xmlDoc, XmlNamespaceManager namespaceManager)
        {
            return GetValue(xmlDoc, namespaceManager, "//nu:metadata/nu:title");
        }

        public static string? GetAuthorsFromNuspec(XmlDocument xmlDoc, XmlNamespaceManager namespaceManager)
        {
            return GetValue(xmlDoc, namespaceManager, "//nu:metadata/nu:authors");
        }

        public static string? GetOwnersFromNuspec(XmlDocument xmlDoc, XmlNamespaceManager namespaceManager)
        {
            return GetValue(xmlDoc, namespaceManager, "//nu:metadata/nu:owners");
        }

        public static string? GetRequireLicenseAcceptanceFromNuspec(XmlDocument xmlDoc, XmlNamespaceManager namespaceManager)
        {
            return GetValue(xmlDoc, namespaceManager, "//nu:metadata/nu:requireLicenseAcceptance");
        }

        public static string? GetDescriptionFromNuspec(XmlDocument xmlDoc, XmlNamespaceManager namespaceManager)
        {
            return GetValue(xmlDoc, namespaceManager, "//nu:metadata/nu:description");
        }

        public static string? GetSummaryFromNuspec(XmlDocument xmlDoc, XmlNamespaceManager namespaceManager)
        {
            return GetValue(xmlDoc, namespaceManager, "//nu:metadata/nu:summary");
        }

        public static string? GetReleaseNotesFromNuspec(XmlDocument xmlDoc, XmlNamespaceManager namespaceManager)
        {
            return GetValue(xmlDoc, namespaceManager, "//nu:metadata/nu:releaseNotes");
        }

        public static string? GetCopyrightFromNuspec(XmlDocument xmlDoc, XmlNamespaceManager namespaceManager)
        {
            return GetValue(xmlDoc, namespaceManager, "//nu:metadata/nu:copyright");
        }

        public static string? GetLanguageFromNuspec(XmlDocument xmlDoc, XmlNamespaceManager namespaceManager)
        {
            return GetValue(xmlDoc, namespaceManager, "//nu:metadata/nu:language");
        }

        public static string? GetTagsFromNuspec(XmlDocument xmlDoc, XmlNamespaceManager namespaceManager)
        {
            return GetValue(xmlDoc, namespaceManager, "//nu:metadata/nu:tags");
        }

        private static string? GetValue(XmlDocument xmlDoc, XmlNamespaceManager namespaceManager, string xpath)
        {
            try
            {
                var node = xmlDoc.SelectSingleNode(xpath, namespaceManager);
                return node?.InnerText;
            }
            catch
            {
                return null;
            }
        }

        public static string? GetLicenseUrlFromHtml(HtmlDocument doc)
        {
            return ExtractUrl(doc, "//a[@id='licenseUrl']", "href");
        }

        public static string? GetProjectUrlFromHtml(HtmlDocument doc)
        {
            return ExtractUrl(doc, "//a[@id='projectUrl']", "href");
        }

        private static string? ExtractUrl(HtmlDocument doc, string xpath, string attributeName)
        {
            var node = doc.DocumentNode.SelectSingleNode(xpath);
            return node?.Attributes[attributeName]?.Value;
        }
    }
}
