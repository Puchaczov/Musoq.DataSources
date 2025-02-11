using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
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

        public static async Task<HtmlDocument> TraverseToLicenseUrlAsync(string url, HttpClient httpClient, CancellationToken cancellationToken)
        {
            var response = await httpClient.GetAsync(url, cancellationToken);
            response.EnsureSuccessStatusCode();
            var html = await response.Content.ReadAsStringAsync(cancellationToken);

            var newDoc = new HtmlDocument();
            newDoc.LoadHtml(html);
            
            return newDoc;
        }
        
        public static async Task<HtmlDocument> TraverseToProjectUrlAsync(string url, HttpClient httpClient, CancellationToken cancellationToken)
        {
            var response = await httpClient.GetAsync(url, cancellationToken);
            response.EnsureSuccessStatusCode();
            var html = await response.Content.ReadAsStringAsync(cancellationToken);

            var newDoc = new HtmlDocument();
            newDoc.LoadHtml(html);
            
            return newDoc;
        }
        
        public static async Task<HtmlDocument> TraverseToLicenseContentAsync(string url, HttpClient httpClient, CancellationToken cancellationToken)
        {
            var response = await httpClient.GetAsync(url, cancellationToken);
            response.EnsureSuccessStatusCode();
            var html = await response.Content.ReadAsStringAsync(cancellationToken);

            var newDoc = new HtmlDocument();
            newDoc.LoadHtml(html);
            
            // Find with xpath, element "a" that link points to https://licenses.nuget.org/* and get its href attribute
            var licenseUrl = newDoc.DocumentNode.SelectSingleNode("//a[contains(@href, 'https://licenses.nuget.org')]")?.Attributes["href"]?.Value;

            if (licenseUrl is not null)
            {
                var licenseResponse = await httpClient.GetAsync(licenseUrl, cancellationToken);
                
                licenseResponse.EnsureSuccessStatusCode();
                var licenseHtml = await licenseResponse.Content.ReadAsStringAsync(cancellationToken);
                
                newDoc.LoadHtml(licenseHtml);
                
                return newDoc;
            }
            
            newDoc.LoadHtml(string.Empty);
            
            return newDoc;
        }

        public static string? GetLicenseUrlFromHtml(HtmlDocument doc)
        {
            return ExtractUrl(doc, "//a[@id='licenseUrl']", "href");
        }

        public static string? GetProjectUrlFromHtml(HtmlDocument doc)
        {
            return ExtractUrl(doc, "//a[@id='projectUrl']", "href");
        }
        
        public static string? GetLicenseContentFromHtml(HtmlDocument doc)
        {
            return doc.DocumentNode.SelectSingleNode("//div[@id='licenseContent']")?.InnerText;
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
