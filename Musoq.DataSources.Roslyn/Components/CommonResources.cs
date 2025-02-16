using HtmlAgilityPack;
using System.Collections.Generic;

namespace Musoq.DataSources.Roslyn.Components;

internal class CommonResources
{
    private readonly object _syncRoot = new();
    private readonly Dictionary<string, HtmlDocument> _htmlDocuments = new();
    private readonly string? _packagePath;
    private string? _licenseUrl;

    private string? _licenseContent;

    private string? _license;
    private string? _authors;

    private string? _projectUrl;
    private string? _title;
    private string? _owners;

    private bool? _requireLicenseAcceptance;

    private string? _description;

    private string? _summary;
    private string? _releaseNotes;
    private string? _copyright;
    private string? _language;
    private string? _tags;
        
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
                _packagePath ??= value;
            }
        }
    }

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

    public string? License
    {
        get
        {
            lock (_syncRoot)
            {
                return _license;
            }
        }
        set
        {
            if (value == null) return;
            lock (_syncRoot)
            {
                _license ??= value;
            }
        }
    }

    public string? LicenseContent
    {
        get
        {
            lock (_syncRoot)
            {
                return _licenseContent;
            }
        }
        set
        {
            if (value == null) return;
            lock (_syncRoot)
            {
                _licenseContent ??= value;
            }
        }
    }

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

    public string? Title
    {
        get
        {
            lock (_syncRoot)
            {
                return _title;
            }
        }
        set
        {
            lock (_syncRoot)
            {
                _title = value;
            }
        }
    }

    public string? Authors
    {
        get
        {
            lock (_syncRoot)
            {
                return _authors;
            }
        }
        set
        {
            lock (_syncRoot)
            {
                _authors = value;
            }
        }
    }

    public string? Owners
    {
        get
        {
            lock (_syncRoot)
            {
                return _owners;
            }
        }
        set
        {
            lock (_syncRoot)
            {
                _owners = value;
            }
        }
    }

    public bool? RequireLicenseAcceptance
    {
        get
        {
            lock (_syncRoot)
            {
                return _requireLicenseAcceptance;
            }
        }
        set
        {
            lock (_syncRoot)
            {
                _requireLicenseAcceptance = value;
            }
        }
    }

    public string? Description
    {
        get
        {
            lock (_syncRoot)
            {
                return _description;
            }
        }
        set
        {
            lock (_syncRoot)
            {
                _description = value;
            }
        }
    }
    public string? Summary
    {
        get
        {
            lock (_syncRoot)
            {
                return _summary;
            }
        }
        set
        {
            lock (_syncRoot)
            {
                _summary = value;
            }
        }
    }

    public string? ReleaseNotes
    {
        get
        {
            lock (_syncRoot)
            {
                return _releaseNotes;
            }
        }
        set
        {
            lock (_syncRoot)
            {
                _releaseNotes = value;
            }
        }
    }

    public string? Copyright
    {
        get
        {
            lock (_syncRoot)
            {
                return _copyright;
            }
        }
        set
        {
            lock (_syncRoot)
            {
                _copyright = value;
            }
        }
    }

    public string? Language
    {
        get
        {
            lock (_syncRoot)
            {
                return _language;
            }
        }
        set
        {
            lock (_syncRoot)
            {
                _language = value;
            }
        }
    }

    public string? Tags
    {
        get
        {
            lock (_syncRoot)
            {
                return _tags;
            }
        }
        set
        {
            lock (_syncRoot)
            {
                _tags = value;
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