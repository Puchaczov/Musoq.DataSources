namespace Musoq.DataSources.Roslyn
{
    public interface INuGetPackageMetadataRetriever
    {
        (string LicenseUrl, string ProjectUrl) GetMetadata(string packageName, string packageVersion);
    }
}
