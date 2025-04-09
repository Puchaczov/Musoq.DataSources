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
        using var testContext = new TestContext("EmptyProject");
        
        var projectXml = CreateProjectXml();
        var retriever = testContext.Environment.CreateMetadataRetriever([]);

        var result = await ProjectEntity.ExtractFromProjectMetadataAsync(
            projectXml, 
            retriever, 
            CancellationToken.None);
        
        Assert.AreEqual(0, result.Count);
    }
    
    [TestMethod]
    public async Task ExtractFromProjectMetadata_SinglePackageReference_ReturnsSinglePackage()
    {
        using var testContext = new TestContext("SinglePackage");
        
        var projectXml = CreateProjectXml(("Newtonsoft.Json", "13.0.1"));
        
        testContext.Environment.SetupPackageMetadata("Newtonsoft.Json", "13.0.1");
        testContext.Environment.SetupLicenseFile(
            "https://raw.githubusercontent.com/JamesNK/Newtonsoft.Json/refs/heads/master/LICENSE.md",
            "newtonsoft.json-mit.txt");
            
        var retriever = testContext.Environment.CreateMetadataRetriever(["MIT"]);

        var result = await ProjectEntity.ExtractFromProjectMetadataAsync(
            projectXml, 
            retriever, 
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
        using var testContext = new TestContext("SinglePackage");
        
        var projectXml = CreateProjectXml(("Musoq.Schema", "8.0.1"));
        
        testContext.Environment.SetupPackageMetadata("Musoq.Schema", "8.0.1");
        testContext.Environment.SetupLicenseFile(
            "https://raw.githubusercontent.com/Puchaczov/Musoq/refs/heads/master/LICENSE",
            "musoq-mit.txt");
            
        var retriever = testContext.Environment.CreateMetadataRetriever(["MIT"]);

        var result = await ProjectEntity.ExtractFromProjectMetadataAsync(
            projectXml, 
            retriever, 
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
    
    private class TestContext : IDisposable
    {
        public string TestDir { get; }
        public string CacheDir { get; }
        
        public TestEnvironment Environment { get; }
        
        public TestContext(string testName)
        {
            TestDir = Path.Combine(
                Path.GetTempPath(),
                $"NuGetTest_{testName}_{DateTime.Now:yyyyMMddHHmmss}_{Guid.NewGuid():N}");
            
            CacheDir = Path.Combine(TestDir, "Cache");
            
            Directory.CreateDirectory(TestDir);
            Directory.CreateDirectory(CacheDir);
            
            Environment = new TestEnvironment(SourcePackagesDir, 
                SourceHtmlPagesDir, 
                SourceLicensesDir, 
                CacheDir);
        }
        
        public void Dispose()
        {
            try
            {
                if (Directory.Exists(TestDir))
                {
                    Directory.Delete(TestDir, recursive: true);
                }
            }
            catch
            {
                // ignored
            }
        }
    }
    
    #endregion
    
    private class TestEnvironment
    {
        private readonly string _packagesDir;
        private readonly string _htmlPagesDir;
        private readonly string _licensesDir;
        private readonly string _cacheDir;
        private readonly TestFileSystem _fileSystem;
        private readonly MockHttpMessageHandler _httpHandler;
        private readonly IHttpClient _httpClientWrapper;
        
        public TestEnvironment(string packagesDir, string htmlPagesDir, string licensesDir, string cacheDir)
        {
            _packagesDir = packagesDir;
            _htmlPagesDir = htmlPagesDir;
            _licensesDir = licensesDir;
            _cacheDir = cacheDir;
            
            _fileSystem = new TestFileSystem();
            _httpHandler = new MockHttpMessageHandler();
            _httpClientWrapper = new HttpClientWrapper(() => new HttpClient(_httpHandler));
            
            InitializeLocalPackages();
        }
        
        public INuGetPackageMetadataRetriever CreateMetadataRetriever(string[] licenses)
        {
            var cachePathResolver = new Mock<INuGetCachePathResolver>();
            
            cachePathResolver.Setup(r => r.ResolveAll())
                .Returns([]);

            var propertiesResolver = new Mock<INuGetPropertiesResolver>();
            propertiesResolver.Setup(r => r.GetLicensesNamesAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(licenses);
            
            var retrievalService = new NuGetRetrievalService(
                propertiesResolver.Object,
                _fileSystem,
                _httpClientWrapper);
            
            var packageVersionConcurrencyManager = new PackageVersionConcurrencyManager();
            
            var loggerMock = new Mock<ILogger>();

            return new NuGetPackageMetadataRetriever(
                cachePathResolver.Object,
                "https://api.nuget.org/v3/index.json",
                retrievalService,
                _fileSystem,
                packageVersionConcurrencyManager,
                loggerMock.Object);
        }
        
        public void SetupPackageMetadata(string packageId, string packageVersion)
        {
            var htmlFileName = $"{packageId}.{packageVersion}.html";
            var htmlFilePath = Path.Combine(_htmlPagesDir, htmlFileName);

            if (File.Exists(htmlFilePath))
            {
                _httpHandler.Register(
                    $"https://www.nuget.org/packages/{packageId}/{packageVersion}",
                    new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Content = new StringContent(
                            File.ReadAllText(htmlFilePath), 
                            System.Text.Encoding.UTF8, 
                            "text/html")
                    });
            }

            var packageFileName = $"{packageId}.{packageVersion}.nupkg".ToLowerInvariant();
            var localPackageFilePath = Path.Combine(_packagesDir, packageFileName);

            var packageContent = File.Exists(localPackageFilePath) 
                ? new ByteArrayContent(File.ReadAllBytes(localPackageFilePath)) 
                : new ByteArrayContent([]);
                
            _httpHandler.Register(
                $"https://www.nuget.org/api/v2/package/{packageId}/{packageVersion}",
                new HttpResponseMessage(HttpStatusCode.OK) { Content = packageContent });
        }
        
        public void SetupLicenseFile(string url, string licenseFileName)
        {
            var licenseFilePath = Path.Combine(_licensesDir, licenseFileName);
            
            if (File.Exists(licenseFilePath))
            {
                _httpHandler.Register(
                    url,
                    new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Content = new StringContent(File.ReadAllText(licenseFilePath))
                    });
            }
        }

        private void InitializeLocalPackages()
        {
            if (!Directory.Exists(_packagesDir))
                return;
            
            foreach (var packageFile in Directory.GetFiles(_packagesDir, "*.nupkg"))
            {
                ProcessLocalPackage(packageFile);
            }
        }
        
        private void ProcessLocalPackage(string packageFile)
        {
            var fileName = Path.GetFileName(packageFile);
            var parts = Path.GetFileNameWithoutExtension(fileName).Split('.');
            
            if (parts.Length < 2)
                return;
            
            string packageId, packageVersion;
            
            if (parts.Length == 2)
            {
                packageId = parts[0];
                packageVersion = parts[1];
            }
            else
            {
                var versionStartIndex = parts.Length - 3;
                packageId = string.Join(".", parts.Take(versionStartIndex));
                packageVersion = string.Join(".", parts.Skip(versionStartIndex));
            }
            
            var extractPath = Path.Combine(_cacheDir, packageId, packageVersion);
            
            _fileSystem.AddDirectory(extractPath);
            _fileSystem.AddFile(
                Path.Combine(extractPath, $"{packageId}.nuspec"), 
                CreateNuspecContent(packageId, packageVersion));
            
            _fileSystem.RegisterExternalFile(packageFile);
        }
        
        private static string CreateNuspecContent(string packageId, string packageVersion) => 
            $"""
            <?xml version="1.0" encoding="utf-8"?>
            <package xmlns="http://schemas.microsoft.com/packaging/2013/05/nuspec.xsd">
              <metadata>
                <id>{packageId}</id>
                <version>{packageVersion}</version>
                <authors>Test Author</authors>
                <description>Test package from local files</description>
                <title>{packageId}</title>
                <projectUrl>https://example.com/project</projectUrl>
                <license type="expression">MIT</license>
              </metadata>
            </package>
            """;
    }
    
    #region Test Infrastructure
    
    private class MockHttpMessageHandler : HttpMessageHandler
    {
        private readonly Dictionary<string, HttpResponseMessage> _responses = new(StringComparer.OrdinalIgnoreCase);
        
        public void Register(string url, HttpResponseMessage response)
        {
            _responses[url] = response;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var requestUrl = request.RequestUri?.ToString();
            
            if (requestUrl is null)
                return Task.FromResult(CreateNotFoundResponse(request));
            
            if (_responses.TryGetValue(requestUrl, out var response))
                return Task.FromResult(response);
            
            if (requestUrl.StartsWith("https://www.nuget.org/packages/"))
            {
                var dynamicResponse = TryGetDynamicHtmlResponse(requestUrl);
                if (dynamicResponse is not null)
                    return Task.FromResult(dynamicResponse);
            }
            
            return Task.FromResult(CreateNotFoundResponse(request));
        }
        
        private static HttpResponseMessage CreateNotFoundResponse(HttpRequestMessage request) =>
            new(HttpStatusCode.NotFound) { RequestMessage = request };
            
        private static HttpResponseMessage? TryGetDynamicHtmlResponse(string url)
        {
            var parts = url.Replace("https://www.nuget.org/packages/", "").Split('/');
            
            if (parts.Length < 2)
                return null;
                
            var packageId = parts[0];
            var packageVersion = parts[1];
            var htmlFilePath = Path.Combine(SourceHtmlPagesDir, $"{packageId}.{packageVersion}.html".ToLowerInvariant());
            
            if (!File.Exists(htmlFilePath))
                return null;
                
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    File.ReadAllText(htmlFilePath),
                    System.Text.Encoding.UTF8,
                    "text/html")
            };
        }
    }
    
    private class HttpClientWrapper(Func<HttpClient> createHttpClient) : IHttpClient
    {
        private readonly HttpClient _httpClient = createHttpClient();
        
        public IHttpClient NewInstance()
        {
            return new HttpClientWrapper(createHttpClient);
        }

        public IHttpClient NewInstance(Action<HttpClient> configure)
        {
            configure(_httpClient);
            
            return new HttpClientWrapper(createHttpClient);
        }

        public async Task<HttpResponseMessage?> GetAsync(string requestUri, CancellationToken cancellationToken)
        {
            return await _httpClient.GetAsync(requestUri, cancellationToken);
        }

        public async Task<HttpResponseMessage?> GetAsync(string requestUrl, Action<HttpClient> configure, CancellationToken cancellationToken)
        {
            configure(_httpClient);
            
            return await _httpClient.GetAsync(requestUrl, cancellationToken);
        }

        public async Task<TOut?> PostAsync<T, TOut>(string requestUrl, T obj, CancellationToken cancellationToken)
            where T : class
            where TOut : class
        {
            var jsonContent = new StringContent(
                System.Text.Json.JsonSerializer.Serialize(obj),
                System.Text.Encoding.UTF8,
                "application/json");
                
            var response = await _httpClient.PostAsync(requestUrl, jsonContent, cancellationToken);
            
            if (!response.IsSuccessStatusCode)
                return null;
                
            var content = await response.Content.ReadAsStringAsync(cancellationToken);
            
            return string.IsNullOrEmpty(content) 
                ? null 
                : System.Text.Json.JsonSerializer.Deserialize<TOut>(content);
        }
        
        public async Task<TOut?> PostAsync<TOut>(string requestUrl, MultipartFormDataContent multipartFormDataContent,
            CancellationToken cancellationToken) where TOut : class
        {
            var response = await _httpClient.PostAsync(requestUrl, multipartFormDataContent, cancellationToken);
            
            if (!response.IsSuccessStatusCode)
                return null;
                
            var content = await response.Content.ReadAsStringAsync(cancellationToken);
            
            return string.IsNullOrEmpty(content) 
                ? null 
                : System.Text.Json.JsonSerializer.Deserialize<TOut>(content);
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

            return Task.FromResult<Stream>(File.Exists(path)
                ? File.Open(path, FileMode.Create)
                : File.OpenWrite(path));
        }
        
        public Task ExtractZipAsync(string zipFilePath, string extractPath, CancellationToken cancellationToken)
        {
            if (!File.Exists(zipFilePath)) 
                throw new FileNotFoundException($"File not found: {zipFilePath}");
                
            _directories.Add(extractPath);
            
            using var archive = ZipFile.OpenRead(zipFilePath);
            
            var supportedExtensions = new[] { ".nuspec", ".xml", ".json", ".md" };
            
            foreach (var entry in archive.Entries
                .Where(f => supportedExtensions.Contains(Path.GetExtension(f.Name), StringComparer.OrdinalIgnoreCase)))
            {
                var entryPath = Path.Combine(extractPath, entry.FullName);
                if (Path.EndsInDirectorySeparator(entryPath))
                    continue;
                    
                var entryDir = Path.GetDirectoryName(entryPath);
                if (!string.IsNullOrEmpty(entryDir) && !Directory.Exists(entryDir))
                    Directory.CreateDirectory(entryDir);
                    
                entry.ExtractToFile(entryPath, true);
                _files[entryPath] = File.ReadAllText(entryPath);
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
}