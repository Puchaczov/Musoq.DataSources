using System;
using System.IO;
using System.Xml;

namespace Musoq.DataSources.Roslyn
{
    public class NuGetPackageMetadataRetriever : INuGetPackageMetadataRetriever
    {
        private readonly INuGetCachePathResolver _nuGetCachePathResolver;

        public NuGetPackageMetadataRetriever(INuGetCachePathResolver nuGetCachePathResolver)
        {
            _nuGetCachePathResolver = nuGetCachePathResolver;
        }

        public (string LicenseUrl, string ProjectUrl) GetMetadata(string packageName, string packageVersion)
        {
            var nuGetCachePath = _nuGetCachePathResolver.Resolve();
            var packagePath = Path.Combine(nuGetCachePath, packageName.ToLower(), packageVersion);
            var nuspecFilePath = Path.Combine(packagePath, $"{packageName}.nuspec");

            if (!File.Exists(nuspecFilePath))
            {
                return (null, null);
            }

            try
            {
                var xmlDoc = new XmlDocument();
                xmlDoc.Load(nuspecFilePath);

                var namespaceManager = new XmlNamespaceManager(xmlDoc.NameTable);
                namespaceManager.AddNamespace("nu", "http://schemas.microsoft.com/packaging/2013/05/nuspec.xsd");

                string licenseUrl = GetValue(xmlDoc, namespaceManager, "//nu:metadata/nu:licenseUrl");
                string projectUrl = GetValue(xmlDoc, namespaceManager, "//nu:metadata/nu:projectUrl");

                return (licenseUrl, projectUrl);
            }
            catch
            {
                return (null, null);
            }
        }

        private string GetValue(XmlDocument xmlDoc, XmlNamespaceManager namespaceManager, string xpath)
        {
            var node = xmlDoc.SelectSingleNode(xpath, namespaceManager);
            return node?.InnerText;
        }
    }
}
