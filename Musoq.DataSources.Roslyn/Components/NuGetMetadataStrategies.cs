using System.IO;
using HtmlAgilityPack;
using System.Xml;

namespace Musoq.DataSources.Roslyn.Components
{
    internal class NuGetMetadataStrategies(string packagePath)
    {
        public static string? GetLicenseUrlFromNuspec(XmlDocument xmlDoc, XmlNamespaceManager namespaceManager)
        {
            return GetValue(xmlDoc, namespaceManager, "/nu:package/nu:metadata/nu:licenseUrl");
        }

        public static string? GetProjectUrlFromNuspec(XmlDocument xmlDoc, XmlNamespaceManager namespaceManager)
        {
            return GetValue(xmlDoc, namespaceManager, "/nu:package/nu:metadata/nu:projectUrl");
        }

        public static string? GetLicenseFromNuspec(XmlDocument xmlDoc, XmlNamespaceManager namespaceManager)
        {
            return GetValue(xmlDoc, namespaceManager, "/nu:package/nu:metadata/nu:license");
        }

        public static string? GetTitleFromNuspec(XmlDocument xmlDoc, XmlNamespaceManager namespaceManager)
        {
            return GetValue(xmlDoc, namespaceManager, "/nu:package/nu:metadata/nu:title");
        }

        public static string? GetAuthorsFromNuspec(XmlDocument xmlDoc, XmlNamespaceManager namespaceManager)
        {
            return GetValue(xmlDoc, namespaceManager, "/nu:package/nu:metadata/nu:authors");
        }

        public static string? GetOwnersFromNuspec(XmlDocument xmlDoc, XmlNamespaceManager namespaceManager)
        {
            return GetValue(xmlDoc, namespaceManager, "/nu:package/nu:metadata/nu:owners");
        }

        public static string? GetRequireLicenseAcceptanceFromNuspec(XmlDocument xmlDoc, XmlNamespaceManager namespaceManager)
        {
            return GetValue(xmlDoc, namespaceManager, "/nu:package/nu:metadata/nu:requireLicenseAcceptance");
        }

        public static string? GetDescriptionFromNuspec(XmlDocument xmlDoc, XmlNamespaceManager namespaceManager)
        {
            return GetValue(xmlDoc, namespaceManager, "/nu:package/nu:metadata/nu:description");
        }

        public static string? GetSummaryFromNuspec(XmlDocument xmlDoc, XmlNamespaceManager namespaceManager)
        {
            return GetValue(xmlDoc, namespaceManager, "/nu:package/nu:metadata/nu:summary");
        }

        public static string? GetReleaseNotesFromNuspec(XmlDocument xmlDoc, XmlNamespaceManager namespaceManager)
        {
            return GetValue(xmlDoc, namespaceManager, "/nu:package/nu:metadata/nu:releaseNotes");
        }

        public static string? GetCopyrightFromNuspec(XmlDocument xmlDoc, XmlNamespaceManager namespaceManager)
        {
            return GetValue(xmlDoc, namespaceManager, "/nu:package/nu:metadata/nu:copyright");
        }

        public static string? GetLanguageFromNuspec(XmlDocument xmlDoc, XmlNamespaceManager namespaceManager)
        {
            return GetValue(xmlDoc, namespaceManager, "/nu:package/nu:metadata/nu:language");
        }

        public static string? GetTagsFromNuspec(XmlDocument xmlDoc, XmlNamespaceManager namespaceManager)
        {
            return GetValue(xmlDoc, namespaceManager, "/nu:package/nu:metadata/nu:tags");
        }

        public string? GetLicenseContentFromNuspec(XmlDocument xmlDoc, XmlNamespaceManager namespaceManager)
        {
            var licenseFileName = GetValue(xmlDoc, namespaceManager, "/nu:package/nu:metadata/nu:packageLicenseFile");

            if (string.IsNullOrEmpty(licenseFileName)) return null;
            
            var licenseFilePath = Path.Combine(packagePath, licenseFileName);
            
            return File.Exists(licenseFilePath) ? File.ReadAllText(licenseFilePath) : null;
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
    }
}
