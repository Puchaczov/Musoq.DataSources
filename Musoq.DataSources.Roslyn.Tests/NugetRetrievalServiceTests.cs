using System.Runtime.CompilerServices;
using Musoq.DataSources.Roslyn.Components;
using Musoq.DataSources.Roslyn.Components.NuGet;
using System.Text;
using System.Text.Json;
using System.Xml;

namespace Musoq.DataSources.Roslyn.Tests
{
    [TestClass]
    public class NugetRetrievalServiceTests
    {
        private const string SamplePackageName = "SamplePackage";
        private const string SamplePackageVersion = "1.0.0";
        private const string SamplePackagePath = "d:\\temp\\packages\\SamplePackage";
        private const string SampleNuspecPath = "d:\\temp\\packages\\SamplePackage\\SamplePackage.nuspec";
        
        private static readonly string SampleNuspecContent = @"<?xml version=""1.0"" encoding=""utf-8""?>
<package xmlns=""http://schemas.microsoft.com/packaging/2013/05/nuspec.xsd"">
  <metadata>
    <id>SamplePackage</id>
    <version>1.0.0</version>
    <title>Sample Package</title>
    <authors>John Doe</authors>
    <owners>John Doe</owners>
    <requireLicenseAcceptance>false</requireLicenseAcceptance>
    <license type=""expression"">MIT</license>
    <licenseUrl>https://licenses.nuget.org/MIT</licenseUrl>
    <projectUrl>https://github.com/sample/samplepackage</projectUrl>
    <description>Sample package for testing</description>
    <summary>A summary of the package</summary>
    <releaseNotes>Initial release</releaseNotes>
    <copyright>Copyright 2023</copyright>
    <tags>sample test</tags>
    <language>en-US</language>
  </metadata>
</package>";

        private FakeAiBasedPropertiesResolver _aiResolver;
        private FakeFileSystem _fileSystem;
        private FakeHttpClient _httpClient;
        private NuGetRetrievalService _service;
        private NuGetResource _commonResources;

        [TestInitialize]
        public void Setup()
        {
            _aiResolver = new FakeAiBasedPropertiesResolver();
            _fileSystem = new FakeFileSystem();
            _httpClient = new FakeHttpClient();
            
            _service = new NuGetRetrievalService(_aiResolver, _fileSystem, _httpClient);
            
            _commonResources = new NuGetResource
            {
                PackageName = SamplePackageName,
                PackageVersion = SamplePackageVersion,
                PackagePath = SamplePackagePath
            };
        }

        #region GetMetadataFromPathAsync Tests

        [TestMethod]
        public async Task GetMetadataFromPathAsync_WhenPackagePathIsNull_ReturnsNull()
        {
            // Arrange
            var resources = new NuGetResource
            {
                PackageName = SamplePackageName,
                PackageVersion = SamplePackageVersion,
                PackagePath = null
            };
            
            // Act
            var result = await _service.GetMetadataFromPathAsync(resources, "ProjectUrl", CancellationToken.None);
            
            // Assert
            Assert.IsNull(result);
        }

        [TestMethod]
        public async Task GetMetadataFromPathAsync_WhenNuspecFileDoesNotExist_ReturnsNull()
        {
            // Arrange - file system is empty by default
            
            // Act
            var result = await _service.GetMetadataFromPathAsync(_commonResources, "ProjectUrl", CancellationToken.None);
            
            // Assert
            Assert.IsNull(result);
        }

        [TestMethod]
        public async Task GetMetadataFromPathAsync_WhenNuspecFileExistsAndPropertyFound_ReturnsPropertyValue()
        {
            // Arrange
            _fileSystem.AddFile(SampleNuspecPath, SampleNuspecContent);
            
            // Act
            var result = await _service.GetMetadataFromPathAsync(_commonResources, "ProjectUrl", CancellationToken.None);
            
            // Assert
            Assert.AreEqual("https://github.com/sample/samplepackage", result);
        }

        [TestMethod]
        public async Task GetMetadataFromPathAsync_WhenPropertyNotFound_ReturnsNull()
        {
            // Arrange
            _fileSystem.AddFile(SampleNuspecPath, SampleNuspecContent);
            
            // Act
            var result = await _service.GetMetadataFromPathAsync(_commonResources, "NonExistentProperty", CancellationToken.None);
            
            // Assert
            Assert.IsNull(result);
        }

        [TestMethod]
        public async Task GetMetadataFromPathAsync_WhenXmlIsInvalid_ReturnsErrorMessage()
        {
            // Arrange
            _fileSystem.AddFile(SampleNuspecPath, "This is not valid XML");
            
            // Act
            var result = await _service.GetMetadataFromPathAsync(_commonResources, "ProjectUrl", CancellationToken.None);
            
            // Assert
            Assert.IsNull(result);
        }

        [TestMethod]
        public async Task GetMetadataFromPathAsync_WhenPackageNameIsEmpty_ReturnsNull()
        {
            // Arrange
            var resources = new NuGetResource
            {
                PackageName = string.Empty,
                PackageVersion = SamplePackageVersion,
                PackagePath = SamplePackagePath
            };
            
            // Act
            var result = await _service.GetMetadataFromPathAsync(resources, "ProjectUrl", CancellationToken.None);

            // Assert
            Assert.IsNull(result);
        }

        [TestMethod]
        public async Task GetMetadataFromPathAsync_WhenPackageVersionIsEmpty_ReturnsNull()
        {
            // Arrange
            var resources = new NuGetResource
            {
                PackageName = SamplePackageName,
                PackageVersion = string.Empty,
                PackagePath = SamplePackagePath
            };
            
            // Act
            var result = await _service.GetMetadataFromPathAsync(resources, "ProjectUrl", CancellationToken.None);

            // Assert
            Assert.IsNull(result);
        }

        #endregion

        #region GetMetadataFromNugetOrgAsync Tests

        [TestMethod]
        public async Task GetMetadataFromNugetOrgAsync_WhenPropertyNotInStrategies_ReturnsNull()
        {
            // Arrange
            const string baseUrl = "https://www.nuget.org";
            
            // Act
            var result = await _service.GetMetadataFromNugetOrgAsync(baseUrl, _commonResources, "NonExistentProperty", CancellationToken.None);
            
            // Assert
            Assert.IsNull(result);
        }

        [TestMethod]
        public async Task GetMetadataFromNugetOrgAsync_WhenPropertyExists_ReturnsExpectedValue()
        {
            // Arrange
            const string baseUrl = "https://www.nuget.org";
            const string expectedLicense = "MIT";
            
            // Setup HTTP client to return HTML that contains license info
            var path = $"/packages/{SamplePackageName}/{SamplePackageVersion}";
            var licensePath = $"/packages/{SamplePackageName}/{SamplePackageVersion}/license";
            var url = $"{baseUrl}{path}";
            var licenseUrl = $"{baseUrl}{licensePath}";
            
            _httpClient.AddResponse(url, $"<html><body><a href=\"{licensePath}\" data-track=\"outbound-license-url\">License</a></body></html>");
            _httpClient.AddResponse(licenseUrl, $"<html><body><pre class=\"license-file-contents custom-license-container\">{expectedLicense}</pre></body></html>");
            
            // Setup AI resolver to identify the license
            _aiResolver.SetupLicenseResolution(expectedLicense);
            
            // Act
            var result = await _service.GetMetadataFromNugetOrgAsync(baseUrl, _commonResources, "LicensesNames", CancellationToken.None);
            
            // Assert
            Assert.AreEqual($"[\"{expectedLicense}\"]", result);
        }

        #endregion

        #region GetMetadataFromCustomApiAsync Tests

        [TestMethod]
        public async Task GetMetadataFromCustomApiAsync_WhenApiReturnsData_ReturnsExpectedValue()
        {
            // Arrange
            const string apiEndpoint = "https://api.example.com/nuget";
            const string propertyName = "ProjectUrl";
            const string expectedResponse = "https://github.com/sample/samplepackage";
            
            var requestUrl = $"{apiEndpoint}?packageName={SamplePackageName}&packageVersion={SamplePackageVersion}&propertyName={propertyName}";
            _httpClient.AddResponse(requestUrl, expectedResponse);
            
            // Act
            var result = await _service.GetMetadataFromCustomApiAsync(apiEndpoint, _commonResources, propertyName, CancellationToken.None);
            
            // Assert
            Assert.AreEqual(expectedResponse, result);
        }

        [TestMethod]
        public async Task GetMetadataFromCustomApiAsync_WhenApiReturnsError_ReturnsNull()
        {
            // Arrange
            const string apiEndpoint = "https://api.example.com/nuget";
            const string propertyName = "ProjectUrl";
            
            const string requestUrl = $"{apiEndpoint}?packageName={SamplePackageName}&packageVersion={SamplePackageVersion}&propertyName={propertyName}";
            _httpClient.AddErrorResponse(requestUrl, 500, "Internal Server Error");
            
            // Act & Assert
            await Assert.ThrowsAsync<HttpRequestException>(async () =>
                await _service.GetMetadataFromCustomApiAsync(apiEndpoint, _commonResources, propertyName,
                    CancellationToken.None));
        }

        [TestMethod]
        public async Task GetMetadataFromCustomApiAsync_WhenApiReturnsNull_ReturnsNull()
        {
            // Arrange
            const string apiEndpoint = "https://api.example.com/nuget";
            const string propertyName = "ProjectUrl";
            
            // Act
            var result = await _service.GetMetadataFromCustomApiAsync(apiEndpoint, _commonResources, propertyName, CancellationToken.None);
            
            // Assert
            Assert.IsNull(result);
        }

        [TestMethod]
        public async Task GetMetadataFromCustomApiAsync_When404ErrorOccurs_ThrowsHttpRequestException()
        {
            // Arrange
            var requestUrl = $"https://api.example.com/nuget?packageName={SamplePackageName}&packageVersion={SamplePackageVersion}&propertyName=ProjectUrl";
            _httpClient.AddErrorResponse(requestUrl, 404, "Not Found");

            // Act & Assert
            await Assert.ThrowsAsync<HttpRequestException>(async () =>
                await _service.GetMetadataFromCustomApiAsync("https://api.example.com/nuget", _commonResources,
                    "ProjectUrl", CancellationToken.None));
        }

        #endregion

        #region DownloadPackageAsync Tests

        [TestMethod]
        public async Task DownloadPackageAsync_WhenSuccessful_ReturnsPackagePath()
        {
            // Arrange
            var downloadUrl = $"https://www.nuget.org/api/v2/package/{SamplePackageName}/{SamplePackageVersion}";
            _httpClient.AddResponse(downloadUrl, "package content");
            
            // Act
            var result = await _service.DownloadPackageAsync(SamplePackageName, SamplePackageVersion, SamplePackagePath, CancellationToken.None);
            
            // Assert
            Assert.AreEqual(SamplePackagePath, result);
            Assert.IsTrue(_fileSystem.ExtractZipWasCalled);
        }

        [TestMethod]
        public async Task DownloadPackageAsync_WhenHttpClientReturnsNull_ReturnsNull()
        {
            // Arrange - no response added to _httpClient
            
            // Act
            var result = await _service.DownloadPackageAsync(SamplePackageName, SamplePackageVersion, SamplePackagePath, CancellationToken.None);
            
            // Assert
            Assert.IsNull(result);
        }

        #endregion
    }

    #region Mock Implementations

    internal class FakeAiBasedPropertiesResolver : INuGetPropertiesResolver
    {
        private string _licenseResponse = string.Empty;

        public void SetupLicenseResolution(string licenseResponse)
        {
            _licenseResponse = licenseResponse;
        }

        public Task<string[]> GetLicensesNamesAsync(string licenseContent, CancellationToken cancellationToken)
        {
            // Convert single license to array to match the interface
            return Task.FromResult(new[] { _licenseResponse });
        }
    }

    internal class FakeFileSystem : IFileSystem
    {
        private readonly Dictionary<string, string> _files = new();
        private readonly HashSet<string> _directories = new();
        public bool ExtractZipWasCalled { get; private set; }

        public void AddFile(string path, string content)
        {
            _files[path] = content;
            // Add directory path for this file
            var directoryPath = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directoryPath))
            {
                _directories.Add(directoryPath);
            }
        }

        public bool IsFileExists(string path)
        {
            return _files.ContainsKey(path);
        }

        public bool IsDirectoryExists(string path)
        {
            return _directories.Contains(path);
        }

        public Task<string> ReadAllTextAsync(string path, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            
            if (_files.TryGetValue(path, out var content))
                return Task.FromResult(content);
                
            throw new FileNotFoundException($"File not found: {path}");
        }

        public string ReadAllText(string path, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            
            if (_files.TryGetValue(path, out var content))
                return content;
                
            throw new FileNotFoundException($"File not found: {path}");
        }

        public Stream OpenRead(string path)
        {
            if (_files.TryGetValue(path, out var content))
                return new MemoryStream(Encoding.UTF8.GetBytes(content));
                
            throw new FileNotFoundException($"File not found: {path}");
        }

        public Task WriteAllTextAsync(string path, string content, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            
            _files[path] = content;
            
            var directoryPath = Path.GetDirectoryName(path);
            
            if (!string.IsNullOrEmpty(directoryPath))
            {
                _directories.Add(directoryPath);
            }
            
            return Task.CompletedTask;
        }

        public void WriteAllText(string path, string content, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            
            _files[path] = content;
            
            var directoryPath = Path.GetDirectoryName(path);
            
            if (!string.IsNullOrEmpty(directoryPath))
            {
                _directories.Add(directoryPath);
            }
        }

        public Task<Stream> CreateFileAsync(string path)
        {
            return Task.FromResult<Stream>(new MemoryStream());
        }

        public Task ExtractZipAsync(string zipPath, string extractPath, CancellationToken cancellationToken)
        {
            ExtractZipWasCalled = true;
            return Task.CompletedTask;
        }

        public async IAsyncEnumerable<string> GetFilesAsync(string path, bool recursive, [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            
            await Task.Yield();

            var directory = path;
            var subDirectories = _directories.Where(d => d.StartsWith(directory)).ToList();
            
            foreach (var file in subDirectories.SelectMany(subDirectory => _files.Keys.Where(f => f.StartsWith(subDirectory) && subDirectory != directory)))
            {
                yield return file;
            }
            
            foreach (var file in _files.Keys.Where(f => f.StartsWith(directory) && !subDirectories.Any(f.StartsWith)))
            {
                yield return file;
            }
        }

        public IEnumerable<string> GetFiles(string path, bool recursive, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            
            var directory = path;
            var subDirectories = _directories.Where(d => d.StartsWith(directory)).ToList();
            
            foreach (var file in subDirectories.SelectMany(subDirectory => _files.Keys.Where(f => f.StartsWith(subDirectory) && subDirectory != directory)))
            {
                yield return file;
            }
            
            foreach (var file in _files.Keys.Where(f => f.StartsWith(directory) && !subDirectories.Any(f.StartsWith)))
            {
                yield return file;
            }
        }

        // Helper method for tests, not part of the interface
        internal XmlDocument Load(string path)
        {
            if (!_files.TryGetValue(path, out var content))
                throw new FileNotFoundException($"File not found: {path}");

            var doc = new XmlDocument();
            doc.LoadXml(content);
            return doc;
        }
    }

    internal class FakeHttpClient : IHttpClient
    {
        private readonly Dictionary<string, FakeHttpResponse> _responses = new();

        public void AddResponse(string url, string content, int statusCode = 200)
        {
            _responses[url] = new FakeHttpResponse { Content = content, StatusCode = statusCode, ShouldThrow = false };
        }

        public void AddErrorResponse(string url, int statusCode, string errorMessage)
        {
            _responses[url] = new FakeHttpResponse { Content = errorMessage, StatusCode = statusCode, ShouldThrow = true };
        }

        public IHttpClient NewInstance()
        {
            return new FakeHttpClient();
        }

        public IHttpClient NewInstance(Action<HttpClient> configure)
        {
            return new FakeHttpClient();
        }

        public Task<HttpResponseMessage?> GetAsync(string requestUri, CancellationToken cancellationToken)
        {
            if (!_responses.TryGetValue(requestUri, out var response))
                return Task.FromResult<HttpResponseMessage?>(null);

            if (response is { ShouldThrow: true, Exception: not null })
                throw response.Exception;

            return Task.FromResult<HttpResponseMessage?>(new HttpResponseMessage
            {
                Content = new StringContent(response.Content, Encoding.UTF8, "text/html"),
                StatusCode = (System.Net.HttpStatusCode)response.StatusCode
            });
        }

        public Task<HttpResponseMessage?> GetAsync(string requestUrl, Action<HttpClient> configure, CancellationToken cancellationToken)
        {
            return GetAsync(requestUrl, cancellationToken);
        }

        public Task<TOut?> PostAsync<T, TOut>(string requestUri, T obj, CancellationToken cancellationToken) 
            where T : class
            where TOut : class
        {
            if (!_responses.TryGetValue(requestUri, out var response))
                return Task.FromResult(default(TOut));
            
            if (response is { ShouldThrow: true, Exception: not null })
                throw response.Exception;
            
            return Task.FromResult(JsonSerializer.Deserialize<TOut>(response.Content));
        }

        public Task<TOut?> PostAsync<TOut>(string requestUrl, MultipartFormDataContent multipartFormDataContent,
            CancellationToken cancellationToken) where TOut : class
        {
            if (!_responses.TryGetValue(requestUrl, out var response))
                return Task.FromResult(default(TOut));
            
            if (response is { ShouldThrow: true, Exception: not null })
                throw response.Exception;
            
            return Task.FromResult(JsonSerializer.Deserialize<TOut>(response.Content));
        }

        public Task<TOut?> PostAsync<TOut>(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (!_responses.TryGetValue(request.RequestUri?.ToString() ?? throw new InvalidOperationException("Request URI is null"), out var response))
                return Task.FromResult(default(TOut));
            
            if (response is { ShouldThrow: true, Exception: not null })
                throw response.Exception;
            
            return Task.FromResult(JsonSerializer.Deserialize<TOut>(response.Content));
        }

        private class FakeHttpResponse
        {
            public string Content { get; init; } = string.Empty;
            public int StatusCode { get; init; } = 200;
            public bool ShouldThrow { get; init; }
            public Exception? Exception { get; init; }
        }
    }

    #endregion
}