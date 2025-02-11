using HtmlAgilityPack;
using System.Collections.Generic;

namespace Musoq.DataSources.Roslyn.Components
{
    internal class CommonResources
    {
        private readonly object _syncRoot = new();
        private readonly Dictionary<string, HtmlDocument> _htmlDocuments = new();
        private readonly string? _packagePath;
        
        public string? PackagePath
        {
            get
            {
                lock (_syncRoot)
                {
                    return _packagePath;
                }
            }
            init
            {
                if (value == null) return;
                lock (_syncRoot)
                {
                    // If it has never been set, assign it
                    // otherwise do not overwrite
                    _packagePath ??= value;
                }
            }
        }

        private string? _licenseUrl;
        public string? LicenseUrl
        {
            get
            {
                lock (_syncRoot)
                {
                    return _licenseUrl;
                }
            }
            set
            {
                if (value == null) return;
                lock (_syncRoot)
                {
                    _licenseUrl ??= value;
                }
            }
        }

        private string? _projectUrl;
        public string? ProjectUrl
        {
            get
            {
                lock (_syncRoot)
                {
                    return _projectUrl;
                }
            }
            set
            {
                if (value == null) return;
                lock (_syncRoot)
                {
                    _projectUrl ??= value;
                }
            }
        }
        public bool TryGetHtmlDocument(string url, out HtmlDocument? doc)
        {
            lock (_syncRoot)
            {
                return _htmlDocuments.TryGetValue(url, out doc);
            }
        }

        public void AddHtmlDocument(string url, HtmlDocument doc)
        {
            lock (_syncRoot)
            {
                _htmlDocuments[url] = doc;
            }
        }
    }
}
