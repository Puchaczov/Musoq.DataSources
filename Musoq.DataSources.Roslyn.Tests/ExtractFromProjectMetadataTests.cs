using System.IO.Compression;
using System.Net;
using System.Xml.Linq;
using Microsoft.Extensions.Logging;
using Moq;
using Musoq.DataSources.Roslyn.Components;
using Musoq.DataSources.Roslyn.Components.NuGet;
using Musoq.DataSources.Roslyn.Components.NuGet.Helpers;
using Musoq.DataSources.Roslyn.Entities;

namespace Musoq.DataSources.Roslyn.Tests;

[TestClass]
public class ExtractFromProjectMetadataTests
{
    private static string SourcePackagesDir => Path.Combine(AppContext.BaseDirectory, "Files", "NugetPackages");
    private static string SourceHtmlPagesDir => Path.Combine(AppContext.BaseDirectory, "Files", "HtmlPages");
    private static string SourceLicensesDir => Path.Combine(AppContext.BaseDirectory, "Files", "Licenses");
    
    [TestMethod]
    public async Task ExtractFromProjectMetadata_EmptyProject_ReturnsEmptyList()
    {
        using var scenario = new TestScenario();
        
        scenario.WhenRequestingLicensesNames()
            .ThenReturnLicensesNames([]);

        var result = await ProjectEntity.ExtractFromProjectMetadataAsync(
            scenario.GetProjectXml(), 
            scenario.GetNugetPackageMetadataRetriever(),
            false,
            CancellationToken.None);
        
        Assert.AreEqual(0, result.Count);
    }
    
    [TestMethod]
    public async Task ExtractFromProjectMetadata_NewtonsoftJson_ReturnsSinglePackage()
    {
        using var scenario = new TestScenario(("Newtonsoft.Json", "13.0.1"));

        scenario
            .WhenLookingForPhysicalNuget()
            .ThenReturnFromFilePath("./Files/NugetPackages/Newtonsoft.Json.13.0.1.nupkg");
        
        scenario
            .WhenRequesting("https://raw.githubusercontent.com/JamesNK/Newtonsoft.Json/refs/heads/master/LICENSE.md")
            .ThenReturnRequestContentFromFile("./Files/Licenses/newtonsoft.json-mit.txt");
        
        scenario
            .WhenRequesting("https://www.nuget.org/packages/Newtonsoft.Json/13.0.1")
            .ThenReturnRequestContentFromFile("./Files/HtmlPages/Newtonsoft.Json.13.0.1.html");
        
        scenario
            .WhenRequestingLicensesNames()
            .ThenReturnLicensesNames(["MIT"]);

        var result = await ProjectEntity.ExtractFromProjectMetadataAsync(
            scenario.GetProjectXml(), 
            scenario.GetNugetPackageMetadataRetriever(),
            false,
            CancellationToken.None);
        
        Assert.AreEqual(1, result.Count);
        var package = result[0];
        Assert.AreEqual("Newtonsoft.Json", package.Id);
        Assert.AreEqual("13.0.1", package.Version);
        Assert.AreEqual("Json.NET", package.Title);
        Assert.AreEqual("James Newton-King", package.Authors);
        Assert.AreEqual("Json.NET is a popular high-performance JSON framework for .NET", package.Description);
        Assert.AreEqual("https://www.newtonsoft.com/json", package.ProjectUrl);
        Assert.AreEqual("MIT", package.License);
    }

    [TestMethod]
    public async Task ExtractFromProjectMetadata_MusoqSchemaCase_ShouldPass()
    {
        using var scenario = new TestScenario(("Musoq.Schema", "8.0.1"));

        scenario
            .WhenLookingForPhysicalNuget()
            .ThenReturnFromFilePath("./Files/NugetPackages/musoq.schema.8.0.1.nupkg");
        
        scenario
            .WhenRequesting("https://raw.githubusercontent.com/Puchaczov/Musoq/refs/heads/master/LICENSE")
            .ThenReturnRequestContentFromFile("./Files/Licenses/musoq-mit.txt");
        
        scenario
            .WhenRequesting("https://www.nuget.org/packages/Musoq.Schema/8.0.1")
            .ThenReturnRequestContentFromFile("./Files/HtmlPages/musoq.schema.8.0.1.html");
        
        scenario
            .WhenRequestingLicensesNames()
            .ThenReturnLicensesNames(["MIT"]);
        

        var result = await ProjectEntity.ExtractFromProjectMetadataAsync(
            scenario.GetProjectXml(), 
            scenario.GetNugetPackageMetadataRetriever(),
            false,
            CancellationToken.None);
        
        Assert.AreEqual(1, result.Count);
        var package = result[0];
        Assert.AreEqual("Musoq.Schema", package.Id);
        Assert.AreEqual("8.0.1", package.Version);
        Assert.IsNull(package.Title);
        Assert.AreEqual("Jakub Puchała", package.Authors);
        Assert.AreEqual("Package Description", package.Description);
        Assert.AreEqual("https://github.com/Puchaczov/Musoq", package.ProjectUrl);
        Assert.AreEqual("MIT", package.License);
        Assert.AreEqual("https://github.com/Puchaczov/Musoq/blob/master/LICENSE", package.LicenseUrl);
    }
    
    #region Helper Methods
    
    private static XDocument CreateProjectXml(params (string id, string version)[] packages)
    {
        if (packages.Length == 0)
        {
            return XDocument.Parse(@"
                <Project Sdk=""Microsoft.NET.Sdk"">
                  <PropertyGroup>
                    <TargetFramework>net6.0</TargetFramework>
                  </PropertyGroup>
                </Project>");
        }
        
        var packageReferences = string.Join(Environment.NewLine, 
            packages.Select(p => $@"<PackageReference Include=""{p.id}"" Version=""{p.version}"" />"));
            
        return XDocument.Parse($@"
            <Project Sdk=""Microsoft.NET.Sdk"">
              <PropertyGroup>
                <TargetFramework>net6.0</TargetFramework>
              </PropertyGroup>
              <ItemGroup>
                {packageReferences}
              </ItemGroup>
            </Project>");
    }

    #endregion

    #region Test Infrastructure
    
    private class HttpClientWrapper : IHttpClient
    {
        private readonly Dictionary<string, string> _licenseUrls = new(StringComparer.OrdinalIgnoreCase);
        
        public void RegisterLicenseUrl(string url, string content)
        {
            _licenseUrls[url] = content;
        }
        
        public IHttpClient NewInstance()
        {
            return this;
        }

        public IHttpClient NewInstance(Action<HttpClient> configure)
        {
            return this;
        }

        public Task<HttpResponseMessage?> GetAsync(string requestUri, CancellationToken cancellationToken)
        {
            if (_licenseUrls.TryGetValue(requestUri, out var content))
            {
                return Task.FromResult<HttpResponseMessage?>(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(content)
                });
            }
            
            return Task.FromResult<HttpResponseMessage?>(new HttpResponseMessage(HttpStatusCode.NotFound));
        }

        public Task<HttpResponseMessage?> GetAsync(string requestUrl, Action<HttpClient> configure, CancellationToken cancellationToken)
        {
            return GetAsync(requestUrl, cancellationToken);
        }

        public Task<TOut?> PostAsync<T, TOut>(string requestUrl, T obj, CancellationToken cancellationToken)
            where T : class
            where TOut : class
        {
            return Task.FromResult<TOut?>(null);
        }
        
        public Task<TOut?> PostAsync<TOut>(string requestUrl, MultipartFormDataContent multipartFormDataContent,
            CancellationToken cancellationToken) where TOut : class
        {
            return Task.FromResult<TOut?>(null);
        }
    }
    
    private class TestFileSystem : IFileSystem
    {
        private readonly Dictionary<string, string> _files = new(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> _directories = new(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> _externalFiles = new(StringComparer.OrdinalIgnoreCase);
        
        public Task<string> ReadAllTextAsync(string path, CancellationToken cancellationToken)
        {
            if (_files.TryGetValue(path, out var content))
                return Task.FromResult(content);
            
            if (_externalFiles.Contains(path) || File.Exists(path))
                return File.ReadAllTextAsync(path, cancellationToken);
                
            throw new FileNotFoundException($"File not found: {path}");
        }

        public string ReadAllText(string path, CancellationToken cancellationToken)
        {
            if (_files.TryGetValue(path, out var content))
                return content;
            
            if (_externalFiles.Contains(path) || File.Exists(path))
                return File.ReadAllText(path);
                
            throw new FileNotFoundException($"File not found: {path}");
        }

        public Stream OpenRead(string path)
        {
            if (_files.TryGetValue(path, out var content))
                return new MemoryStream(System.Text.Encoding.UTF8.GetBytes(content));
            
            if (_externalFiles.Contains(path) || File.Exists(path))
                return File.OpenRead(path);
                
            throw new FileNotFoundException($"File not found: {path}");
        }

        public Task WriteAllTextAsync(string path, string content, CancellationToken cancellationToken)
        {
            _files[path] = content;

            return Task.CompletedTask;
        }

        public void WriteAllText(string path, string content, CancellationToken cancellationToken)
        {
            _files[path] = content;
        }

        public Task<Stream> CreateFileAsync(string path)
        {
            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                Directory.CreateDirectory(directory);

            return Task.FromResult<Stream>(File.Create(path));
        }
        
        public Task ExtractZipAsync(string zipFilePath, string extractPath, CancellationToken cancellationToken)
        {
            if (!File.Exists(zipFilePath)) 
                throw new FileNotFoundException($"File not found: {zipFilePath}");
                
            _directories.Add(extractPath);
            
            Directory.CreateDirectory(extractPath);
            using var archive = ZipFile.OpenRead(zipFilePath);
            
            var supportedExtensions = new[] { ".nuspec", ".xml", ".json", ".md" };
            
            var nuspecEntry = archive.Entries.FirstOrDefault(e => 
                e.Name.EndsWith(".nuspec", StringComparison.OrdinalIgnoreCase));
                
            if (nuspecEntry != null)
            {
                var entryPath = Path.Combine(extractPath, nuspecEntry.Name);
                if (!Path.EndsInDirectorySeparator(entryPath))
                {
                    var entryDir = Path.GetDirectoryName(entryPath);
                    if (!string.IsNullOrEmpty(entryDir))
                        Directory.CreateDirectory(entryDir);
                        
                    nuspecEntry.ExtractToFile(entryPath, true);
                    _files[entryPath] = File.ReadAllText(entryPath);
                    
                    var packageId = Path.GetFileNameWithoutExtension(zipFilePath).Split('.')[0];
                    var packageNuspecPath = Path.Combine(extractPath, $"{packageId}.nuspec");
                    if (!string.Equals(entryPath, packageNuspecPath, StringComparison.OrdinalIgnoreCase))
                    {
                        File.Copy(entryPath, packageNuspecPath, true);
                        _files[packageNuspecPath] = _files[entryPath];
                    }
                }
            }
            
            foreach (var entry in archive.Entries)
            {
                if (entry.Name.EndsWith(".nuspec", StringComparison.OrdinalIgnoreCase))
                    continue;
                    
                if (!supportedExtensions.Contains(Path.GetExtension(entry.Name), StringComparer.OrdinalIgnoreCase))
                    continue;
                    
                var entryPath = Path.Combine(extractPath, entry.FullName);
                if (Path.EndsInDirectorySeparator(entryPath))
                    continue;
                    
                var entryDir = Path.GetDirectoryName(entryPath);
                if (!string.IsNullOrEmpty(entryDir))
                    Directory.CreateDirectory(entryDir);
                
                try
                {
                    entry.ExtractToFile(entryPath, true);
                    _files[entryPath] = File.ReadAllText(entryPath);
                }
                catch
                {
                    // ignored
                }
            }
            
            return Task.CompletedTask;
        }

        public IAsyncEnumerable<string> GetFilesAsync(string path, bool recursive, CancellationToken cancellationToken)
        {
            if (_directories.Contains(path))
            {
                var files = _files.Keys
                    .Where(f => f.StartsWith(path, StringComparison.OrdinalIgnoreCase))
                    .ToArray();
                
                return files.ToAsyncEnumerable();
            }

            if (Directory.Exists(path))
            {
                var files = Directory.GetFiles(path, "*", recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly);
                return files.ToAsyncEnumerable();
            }

            throw new DirectoryNotFoundException($"Directory not found: {path}");
        }

        public IEnumerable<string> GetFiles(string path, bool recursive, CancellationToken cancellationToken)
        {
            if (_directories.Contains(path))
            {
                var files = _files.Keys
                    .Where(f => f.StartsWith(path, StringComparison.OrdinalIgnoreCase))
                    .ToArray();
                
                return files;
            }

            if (Directory.Exists(path))
            {
                var files = Directory.GetFiles(path, "*", recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly);
                return files;
            }

            throw new DirectoryNotFoundException($"Directory not found: {path}");
        }

        public bool IsFileExists(string path) => 
            _files.ContainsKey(path) || _externalFiles.Contains(path) || File.Exists(path);
            
        public bool IsDirectoryExists(string path) => 
            _directories.Contains(path) || Directory.Exists(path);
            
        public void AddFile(string path, string content) => 
            _files[path] = content;
            
        public void AddDirectory(string path) => 
            _directories.Add(path);
            
        public void RegisterExternalFile(string path) => 
            _externalFiles.Add(path);
    }
    
    #endregion

    #region TestScenario Implementation
    
    private class TestScenario : IDisposable
    {
        private readonly (string id, string version)[] _packages;
        private readonly string _testDir;
        private readonly string _cacheDir;
        private readonly TestFileSystem _fileSystem;
        private readonly HttpClientWrapper _httpClient;
        private string[] _licenseNames = [];
        private readonly Mock<INuGetCachePathResolver> _cachePathResolver;
        private readonly Mock<INuGetPropertiesResolver> _propertiesResolver;
        private string? _lastRequestUrl;
        
        public TestScenario(params (string id, string version)[] packages)
        {
            _packages = packages;
            
            _testDir = Path.Combine(
                Path.GetTempPath(),
                $"NuGetScenario_{DateTime.UtcNow:yyyyMMddHHmmss}_{Guid.NewGuid():N}");
            
            _cacheDir = Path.Combine(_testDir, "Cache");
            
            Directory.CreateDirectory(_testDir);
            Directory.CreateDirectory(_cacheDir);
            
            _fileSystem = new TestFileSystem();
            _httpClient = new HttpClientWrapper();
            
            _cachePathResolver = new Mock<INuGetCachePathResolver>();
            _cachePathResolver.Setup(r => r.ResolveAll())
                .Returns([_cacheDir]);
                
            _propertiesResolver = new Mock<INuGetPropertiesResolver>();
        }
        
        public TestScenario WhenLookingForPhysicalNuget()
        {
            return this;
        }
        
        public TestScenario ThenReturnFromFilePath(string filePath)
        {
            foreach (var package in _packages)
            {
                var extractPath = Path.Combine(_cacheDir, package.id, package.version);
                _fileSystem.AddDirectory(extractPath);
                
                var nuspecPath = Path.Combine(extractPath, $"{package.id}.nuspec");
                
                if (File.Exists(filePath))
                {
                    _fileSystem.RegisterExternalFile(filePath);
                    
                    if (filePath.EndsWith(".nupkg", StringComparison.OrdinalIgnoreCase))
                    {
                        using var archive = ZipFile.OpenRead(filePath);
                        var nuspecEntry = archive.Entries.FirstOrDefault(e => 
                            e.Name.EndsWith(".nuspec", StringComparison.OrdinalIgnoreCase));
                            
                        if (nuspecEntry != null)
                        {
                            using var stream = nuspecEntry.Open();
                            using var reader = new StreamReader(stream);
                            var content = reader.ReadToEnd();
                            _fileSystem.WriteAllText(nuspecPath, content, CancellationToken.None);
                        }
                    }
                }
            }
            return this;
        }
        
        public TestScenario WhenRequesting(string url)
        {
            _lastRequestUrl = url;
            return this;
        }
        
        public TestScenario ThenReturnRequestContentFromFile(string filePath)
        {
            if (_lastRequestUrl == null)
                throw new InvalidOperationException("WhenRequesting must be called before ThenReturnRequestContentFromFile");

            if (!File.Exists(filePath)) return this;
            
            var content = File.ReadAllText(filePath);
            _httpClient.RegisterLicenseUrl(_lastRequestUrl, content);
            return this;
        }
        
        public TestScenario WhenRequestingLicensesNames()
        {
            return this;
        }
        
        public TestScenario ThenReturnLicensesNames(string[] licenseNames)
        {
            _licenseNames = licenseNames;
            
            _propertiesResolver.Setup(r => r.GetLicensesNamesAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(_licenseNames);
                
            return this;
        }
        
        public XDocument GetProjectXml()
        {
            return CreateProjectXml(_packages);
        }
        
        public INuGetPackageMetadataRetriever GetNugetPackageMetadataRetriever()
        {
            var retrievalService = new NuGetRetrievalService(
                _propertiesResolver.Object,
                _fileSystem,
                _httpClient);
            
            var packageVersionConcurrencyManager = new PackageVersionConcurrencyManager();
            var bannedPropertiesValues = new Dictionary<string, HashSet<string>>();
            
            var loggerMock = new Mock<ILogger>();

            return new NuGetPackageMetadataRetriever(
                _cachePathResolver.Object,
                null,
                retrievalService,
                _fileSystem,
                packageVersionConcurrencyManager,
                bannedPropertiesValues,
                loggerMock.Object);
        }

        public void Dispose()
        {
            try
            {
                if (Directory.Exists(_testDir))
                {
                    Directory.Delete(_testDir, recursive: true);
                }
            }
            catch
            {
                // ignored
            }
        }
    }
    
    #endregion
}