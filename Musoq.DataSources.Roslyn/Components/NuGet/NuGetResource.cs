using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using HtmlAgilityPack;

namespace Musoq.DataSources.Roslyn.Components.NuGet;

internal class NuGetResource
{
    private readonly Dictionary<string, HtmlDocument> _htmlDocuments = new();
    private readonly List<NuGetLicense> _licenses = [];
    private readonly string? _packageName;
    private readonly string? _packagePath;
    private readonly string? _packageVersion;
    private readonly object _syncRoot = new();

    private string? _authors;
    private string? _copyright;

    private string? _description;
    private string? _language;
    private string? _lookingForLicense;
    private int? _lookingForLicenseIndex;
    private string? _owners;

    private string? _projectUrl;
    private string? _releaseNotes;

    private bool? _requireLicenseAcceptance;

    private string? _summary;
    private string? _tags;

    private string? _title;

    public string? PackageName
    {
        get
        {
            lock (_syncRoot)
            {
                return _packageName;
            }
        }
        init
        {
            if (value == null) return;
            lock (_syncRoot)
            {
                _packageName ??= value;
            }
        }
    }

    public string? PackageVersion
    {
        get
        {
            lock (_syncRoot)
            {
                return _packageVersion;
            }
        }
        init
        {
            if (value == null) return;
            lock (_syncRoot)
            {
                _packageVersion ??= value;
            }
        }
    }

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

    public NuGetLicense[] Licenses
    {
        get
        {
            lock (_syncRoot)
            {
                return _licenses.ToArray();
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

    public string? LookingForLicense
    {
        get
        {
            lock (_syncRoot)
            {
                return _lookingForLicense;
            }
        }
        set
        {
            lock (_syncRoot)
            {
                _lookingForLicense = value;
            }
        }
    }

    public int? LookingForLicenseIndex
    {
        get
        {
            lock (_syncRoot)
            {
                return _lookingForLicenseIndex;
            }
        }
        set
        {
            lock (_syncRoot)
            {
                _lookingForLicenseIndex = value;
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

    public void AddLicense(NuGetLicense license)
    {
        lock (_syncRoot)
        {
            _licenses.Add(license);
        }
    }

    public void ModifyLicenseProperty(string? resolvedValue, Action<string?, NuGetLicense> modifyAction)
    {
        lock (_syncRoot)
        {
            if (_lookingForLicenseIndex == null)
                throw new InvalidOperationException("LookingForLicenseIndex is not set.");

            modifyAction(resolvedValue, _licenses[_lookingForLicenseIndex.Value]);
        }
    }

    public async Task AcceptAsync(INuGetResourceVisitor visitor, CancellationToken cancellationToken)
    {
        await Parallel.ForEachAsync([
                visitor.VisitLicensesAsync,
                visitor.VisitProjectUrlAsync,
                visitor.VisitTitleAsync,
                visitor.VisitAuthorsAsync,
                visitor.VisitOwnersAsync,
                visitor.VisitRequireLicenseAcceptanceAsync,
                visitor.VisitDescriptionAsync,
                visitor.VisitSummaryAsync,
                visitor.VisitReleaseNotesAsync,
                visitor.VisitCopyrightAsync,
                visitor.VisitLanguageAsync,
                visitor.VisitTagsAsync
            ],
            new ParallelOptions
            {
                CancellationToken = cancellationToken,
#if DEBUG
                MaxDegreeOfParallelism = 1
#endif
            },
            async (method, token) => await method(token));
    }
}