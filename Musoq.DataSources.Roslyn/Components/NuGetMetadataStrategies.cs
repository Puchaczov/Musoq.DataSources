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
