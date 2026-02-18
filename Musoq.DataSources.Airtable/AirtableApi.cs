using System.Net.Http.Headers;
using System.Text.Json;
using AirtableApiClient;
using Musoq.DataSources.Airtable.Components;
using Musoq.DataSources.Airtable.Helpers;
using AirtableBase = Musoq.DataSources.Airtable.Sources.Bases.AirtableBase;

namespace Musoq.DataSources.Airtable;

internal class AirtableApi : IAirtableApi
{
    private readonly string _apiKeyOrAccessToken;
    private readonly string? _baseId;
    private readonly string? _filterByFormula;
    private readonly int? _maxRecords;
    private readonly string? _tableIdOrTableName;

    private string? _offset;

    public AirtableApi(string apiKeyOrAccessToken, string? baseId = null, string? tableIdOrTableName = null,
        string? filterByFormula = null, int? maxRecords = null)
    {
        _apiKeyOrAccessToken = apiKeyOrAccessToken;
        _baseId = baseId;
        _tableIdOrTableName = tableIdOrTableName;
        _filterByFormula = filterByFormula;
        _maxRecords = maxRecords;
        _offset = null;
    }

    public IEnumerable<IReadOnlyList<AirtableRecord>> GetRecordsChunks(IReadOnlyCollection<string> columns)
    {
        string? errorMessage = null;

        using (var airtableBase = new AirtableApiClient.AirtableBase(_apiKeyOrAccessToken, _baseId))
        {
            do
            {
                var response = airtableBase.ListRecords(
                    _tableIdOrTableName,
                    _offset,
                    columns,
                    _filterByFormula,
                    _maxRecords).Result;

                if (response.Success)
                {
                    var records = new List<AirtableRecord>();

                    foreach (var record in response.Records)
                    {
                        var fields = record.Fields;

                        foreach (var key in fields.Keys)
                        {
                            if (fields[key] is not JsonElement objectElement) continue;

                            TypeMappingHelpers.MapFromJsonElement(fields, key, objectElement);
                        }
                    }

                    records.AddRange(response.Records);
                    _offset = response.Offset;

                    yield return records;
                    continue;
                }

                if (response.AirtableApiError != null)
                {
                    errorMessage = response.AirtableApiError.ErrorMessage;
                    if (response.AirtableApiError is AirtableInvalidRequestException)
                    {
                        errorMessage += "\nDetailed error message: ";
                        errorMessage += response.AirtableApiError.DetailedErrorMessage;
                    }

                    break;
                }

                errorMessage = "Unknown error";
                break;
            } while (_offset != null);
        }

        if (!string.IsNullOrEmpty(errorMessage))
            throw new InvalidOperationException($"Could not fetch from airtable: {errorMessage}");
    }

    public IEnumerable<AirtableField> GetColumns(IEnumerable<string> columns)
    {
        var httpClient = new HttpClient();
        httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _apiKeyOrAccessToken);

        var response = httpClient.GetAsync($"https://api.airtable.com/v0/meta/bases/{_baseId}/tables").Result;

        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException($"Could not fetch from airtable: {response.ReasonPhrase}");

        var content = response.Content.ReadAsStringAsync().Result;
        var responseObject = JsonSerializer.Deserialize<AirtableResponse>(content);

        if (responseObject == null)
            throw new InvalidOperationException($"Could not fetch from airtable: {response.ReasonPhrase}");

        var fields = responseObject.Tables
                         .SingleOrDefault(f => f.Name == _tableIdOrTableName || f.Id == _tableIdOrTableName)?.Fields ??
                     Enumerable.Empty<AirtableField>();

        return fields;
    }

    public IEnumerable<IReadOnlyList<AirtableBase>> GetBases(IEnumerable<string> columns)
    {
        var httpClient = new HttpClient();
        httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _apiKeyOrAccessToken);

        string? offset;
        do
        {
            var response = httpClient.GetAsync("https://api.airtable.com/v0/meta/bases").Result;

            if (!response.IsSuccessStatusCode)
                throw new InvalidOperationException($"Could not fetch from airtable: {response.ReasonPhrase}");

            var content = response.Content.ReadAsStringAsync().Result;
            var responseObject = JsonSerializer.Deserialize<AirtableBasesResponse>(content);

            if (responseObject == null)
                throw new InvalidOperationException($"Could not fetch from airtable: {response.ReasonPhrase}");

            yield return responseObject.Bases
                .Select(f => new AirtableBase(f.Id, f.Name, f.PermissionLevel))
                .ToList();

            offset = responseObject.Offset;
        } while (offset != null);
    }

    public IEnumerable<IReadOnlyList<AirtableTable>> GetTables(IEnumerable<string> columns)
    {
        var httpClient = new HttpClient();
        httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _apiKeyOrAccessToken);

        var response = httpClient.GetAsync($"https://api.airtable.com/v0/meta/bases/{_baseId}/tables").Result;

        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException($"Could not fetch from airtable: {response.ReasonPhrase}");

        var content = response.Content.ReadAsStringAsync().Result;

        var responseObject = JsonSerializer.Deserialize<AirtableTablesResponse>(content);

        if (responseObject == null)
            throw new InvalidOperationException($"Could not fetch from airtable: {response.ReasonPhrase}");

        yield return responseObject.Tables;
    }
}