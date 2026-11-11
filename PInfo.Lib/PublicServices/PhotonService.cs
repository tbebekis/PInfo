/* PInfo, Copyright (c) 2026 Theo Bebekis, see License. */

namespace PInfo;

/// <summary>
/// Searches populated locations using the public Photon service for the Weather entry point.
/// </summary>
public class PhotonService : PublicService
{
    // ● private
    static readonly SemaphoreSlim fGate = new(1, 1);
    static long fNextRequest;
    static DateTimeOffset fRetryAfter;

    /// <summary>
    /// Checks the response shape before displaying or caching provider data.
    /// </summary>
    static void Validate(string Json)
    {
        using JsonDocument Document = JsonDocument.Parse(Json);
        JsonElement Root = Document.RootElement;
        if (Root.GetProperty("type").GetString() != "FeatureCollection"
            || Root.GetProperty("features").ValueKind != JsonValueKind.Array)
            throw new JsonException("Invalid Photon response.");
        foreach (JsonElement Item in Root.GetProperty("features").EnumerateArray())
        {
            JsonElement Properties = Item.GetProperty("properties");
            if (string.IsNullOrWhiteSpace(Properties.GetProperty("name").GetString()))
                throw new JsonException("A location name is required.");
            foreach (string Name in new[] { "city", "district", "county", "state", "country", "countrycode", "type" })
            {
                if (Properties.TryGetProperty(Name, out JsonElement Text)
                    && Text.ValueKind != JsonValueKind.Null && Text.ValueKind != JsonValueKind.String)
                    throw new JsonException("Invalid location description.");
            }
            JsonElement Geometry = Item.GetProperty("geometry");
            JsonElement Coordinates = Geometry.GetProperty("coordinates");
            if (Geometry.GetProperty("type").GetString() != "Point"
                || Coordinates.ValueKind != JsonValueKind.Array || Coordinates.GetArrayLength() < 2)
                throw new JsonException("Invalid location geometry.");
            double Longitude = Coordinates[0].GetDouble();
            double Latitude = Coordinates[1].GetDouble();
            if (!double.IsFinite(Longitude) || !double.IsFinite(Latitude)
                || Longitude < -180 || Longitude > 180 || Latitude < -90 || Latitude > 90)
                throw new JsonException("Invalid location coordinates.");
        }
    }
    /// <summary>
    /// Caches validated results and conservatively spaces Photon requests at least one second apart per process.
    /// </summary>
    async Task<ApiClientResult> LoadAsync(string Search, CancellationToken CancellationToken)
    {
        string Url = "https://photon.komoot.io/api/?q=" + Uri.EscapeDataString(Search.Trim())
            + "&lang=en&layer=city&limit=100";
        ApiClientResult Result = new(Url);
        string Key = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(Encoding.UTF8.GetBytes(Url)));
        string FilePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "PInfo", "Data", "Photon", Key + ".json");
        await fGate.WaitAsync(CancellationToken).ConfigureAwait(false);
        try
        {
            string Cached = null;
            try
            {
                if (File.Exists(FilePath))
                {
                    Cached = await File.ReadAllTextAsync(FilePath, CancellationToken).ConfigureAwait(false);
                    Validate(Cached);
                    DateTime Written = File.GetLastWriteTimeUtc(FilePath);
                    if (Written <= DateTime.UtcNow && Written > DateTime.UtcNow.AddDays(-Lib.Settings.PhotonRefreshDays))
                    {
                        Result.ResponseJsonText = Cached;
                        Result.ResultStatus = ApiClientResultStatus.Success;
                        return Result;
                    }
                }
            }
            catch (Exception Ex) when (Ex is IOException || Ex is UnauthorizedAccessException || Ex is JsonException
                || Ex is KeyNotFoundException || Ex is InvalidOperationException || Ex is FormatException)
            {
                Cached = null;
            }
            try
            {
                if (fRetryAfter > DateTimeOffset.UtcNow)
                    throw new HttpRequestException("Photon is temporarily limiting requests. Please try again later.");
                long Delay = fNextRequest - Environment.TickCount64;
                if (Delay > 0)
                    await Task.Delay(TimeSpan.FromMilliseconds(Delay), CancellationToken).ConfigureAwait(false);
                using CancellationTokenSource Deadline = CancellationTokenSource.CreateLinkedTokenSource(CancellationToken);
                Deadline.CancelAfter(TimeSpan.FromSeconds(30));
                using HttpRequestMessage Request = new(HttpMethod.Get, Url);
                Request.Headers.UserAgent.ParseAdd("PInfo/1.0 (+https://github.com/tbebekis/PInfo)");
                fNextRequest = Environment.TickCount64 + 1000;
                using HttpResponseMessage Response = await PublicApiClient.SendAsync(Request, Deadline.Token).ConfigureAwait(false);
                Result.StatusCode = Response.StatusCode;
                if (Response.StatusCode == HttpStatusCode.TooManyRequests || Response.StatusCode == HttpStatusCode.ServiceUnavailable)
                {
                    DateTimeOffset Until = Response.Headers.RetryAfter?.Date
                        ?? DateTimeOffset.UtcNow.Add(Response.Headers.RetryAfter?.Delta ?? TimeSpan.FromSeconds(30));
                    fRetryAfter = Until > DateTimeOffset.UtcNow ? Until : DateTimeOffset.UtcNow.AddSeconds(1);
                }
                Response.EnsureSuccessStatusCode();
                string Json = await Response.Content.ReadAsStringAsync(Deadline.Token).ConfigureAwait(false);
                Validate(Json);
                CancellationToken.ThrowIfCancellationRequested();
                Result.ResponseJsonText = Json;
                Result.ResultStatus = ApiClientResultStatus.Success;
                string TemporaryPath = FilePath + "." + Guid.NewGuid().ToString("N") + ".tmp";
                try
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(FilePath));
                    await File.WriteAllTextAsync(TemporaryPath, Json, CancellationToken).ConfigureAwait(false);
                    File.Move(TemporaryPath, FilePath, true);
                }
                catch (Exception Ex) when (Ex is IOException || Ex is UnauthorizedAccessException)
                {
                    Result.ReasonPhrase = "Location search results could not be saved for offline use.";
                }
                finally
                {
                    try { File.Delete(TemporaryPath); }
                    catch (Exception Ex) when (Ex is IOException || Ex is UnauthorizedAccessException) { }
                }
            }
            catch (OperationCanceledException) when (CancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception Ex) when (Ex is HttpRequestException || Ex is IOException || Ex is JsonException
                || Ex is OperationCanceledException || Ex is KeyNotFoundException || Ex is InvalidOperationException || Ex is FormatException)
            {
                Result.ResponseJsonText = Cached;
                Result.ResultStatus = Cached == null ? ApiClientResultStatus.Error : ApiClientResultStatus.Success;
                Result.ReasonPhrase = fRetryAfter > DateTimeOffset.UtcNow
                    ? "Photon is temporarily limiting requests. Please try again later."
                    : "Photon could not be loaded. Check your connection and try again.";
                if (Cached != null)
                    Result.ReasonPhrase += " Previously downloaded content is shown.";
            }
            return Result;
        }
        finally
        {
            fGate.Release();
        }
    }

    // ● constructor
    /// <summary>
    /// Creates the Weather search service without performing any network requests.
    /// </summary>
    public PhotonService() : base("Weather")
    {
    }

    // ● public
    /// <summary>
    /// Searches full or partial location names and returns the provider's ranked suggestions.
    /// </summary>
    /// <param name="Search">The location query, optionally including country context.</param>
    /// <param name="CancellationToken">Cancels waiting, HTTP access and cache operations.</param>
    /// <returns>Validated GeoJSON containing at most the server's permitted number of suggestions.</returns>
    public Task<ApiClientResult> SearchAsync(string Search, CancellationToken CancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(Search);
        return LoadAsync(Search, CancellationToken);
    }
}
