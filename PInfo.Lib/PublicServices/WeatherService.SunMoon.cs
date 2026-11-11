/* PInfo, Copyright (c) 2026 Theo Bebekis, see License. */

namespace PInfo;

/// <summary>
/// Extends MET Norway access with independently cached Sunrise 3.0 sun and moon event responses.
/// </summary>
static public partial class WeatherService
{
    // ● private
    /// <summary>
    /// Validates the body, requested position and independently nullable event timestamps.
    /// </summary>
    static void ValidateSunMoon(string Json, string Body, double Latitude, double Longitude)
    {
        try
        {
            using JsonDocument Document = JsonDocument.Parse(Json);
            JsonElement Root = Document.RootElement;
            JsonElement Geometry = Root.GetProperty("geometry");
            JsonElement Coordinates = Geometry.GetProperty("coordinates");
            JsonElement Properties = Root.GetProperty("properties");
            if (Root.GetProperty("type").GetString() != "Feature" || Geometry.GetProperty("type").GetString() != "Point"
                || Properties.GetProperty("body").GetString() != Body
                || Math.Abs(Coordinates[0].GetDouble() - Longitude) > 0.00001
                || Math.Abs(Coordinates[1].GetDouble() - Latitude) > 0.00001)
                throw new JsonException("MET Norway returned different celestial data than requested.");
            foreach (string Name in Body == "Sun" ? new[] { "sunrise", "sunset", "solarnoon", "solarmidnight" }
                : new[] { "moonrise", "moonset", "high_moon", "low_moon" })
            {
                JsonElement Event = Properties.GetProperty(Name);
                if (Event.ValueKind == JsonValueKind.Null)
                    continue;
                if (Event.ValueKind != JsonValueKind.Object)
                    throw new JsonException("Invalid celestial event.");
                if (Event.TryGetProperty("time", out JsonElement Time) && Time.ValueKind != JsonValueKind.Null
                    && !DateTimeOffset.TryParse(Time.GetString(), CultureInfo.InvariantCulture, DateTimeStyles.None, out _))
                    throw new JsonException("Invalid celestial event time.");
                foreach (string Number in new[] { "azimuth", "disc_centre_elevation" })
                {
                    if (Event.TryGetProperty(Number, out JsonElement Value) && Value.ValueKind != JsonValueKind.Null
                        && !double.IsFinite(Value.GetDouble()))
                        throw new JsonException("Invalid celestial angle.");
                }
            }
            if (Body == "Moon" && Properties.TryGetProperty("moonphase", out JsonElement Phase) && Phase.ValueKind != JsonValueKind.Null)
            {
                double Value = Phase.ValueKind == JsonValueKind.Object ? Phase.GetProperty("value").GetDouble() : Phase.GetDouble();
                if (!double.IsFinite(Value) || Value < 0 || Value > 360)
                    throw new JsonException("Invalid moon phase angle.");
            }
        }
        catch (Exception Ex) when (Ex is KeyNotFoundException || Ex is InvalidOperationException || Ex is FormatException || Ex is IndexOutOfRangeException)
        {
            throw new JsonException("Invalid MET Norway celestial response.", Ex);
        }
    }
    /// <summary>
    /// Reads the existing HTTP cache envelope for a celestial endpoint without treating it as forecast data.
    /// </summary>
    static async Task<CacheEntry> ReadSunMoonCacheAsync(string Key, string Body, double Latitude, double Longitude,
        CancellationToken CancellationToken)
    {
        try
        {
            string Text = await File.ReadAllTextAsync(GetCachePath(Key), CancellationToken).ConfigureAwait(false);
            CacheEntry Entry = JsonSerializer.Deserialize<CacheEntry>(Text);
            DateTime Now = DateTime.UtcNow;
            if (Entry == null || Entry.DownloadedUtc.Kind != DateTimeKind.Utc || Entry.CheckedUtc.Kind != DateTimeKind.Utc
                || Entry.NextRequestUtc.Kind != DateTimeKind.Utc || Entry.DownloadedUtc.Year < 2020
                || Entry.DownloadedUtc > Now || Entry.CheckedUtc > Now || Entry.CheckedUtc < Entry.DownloadedUtc
                || Entry.NextRequestUtc < Entry.CheckedUtc || string.IsNullOrWhiteSpace(Entry.Json))
                return null;
            if (!string.IsNullOrEmpty(Entry.LastModified)
                && !DateTimeOffset.TryParse(Entry.LastModified, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out _))
                return null;
            ValidateSunMoon(Entry.Json, Body, Latitude, Longitude);
            return Entry;
        }
        catch (Exception Ex) when (Ex is IOException || Ex is UnauthorizedAccessException || Ex is JsonException)
        {
            return null;
        }
    }

    // ● public
    /// <summary>
    /// Loads one MET Norway celestial body for an explicit date and offset, preserving HTTP cache rules and stale fallback.
    /// </summary>
    /// <param name="Body">Sun or Moon; other values are rejected.</param>
    /// <param name="Latitude">The location latitude.</param>
    /// <param name="Longitude">The location longitude.</param>
    /// <param name="Date">The selected date used by the provider's mean solar-day calculation.</param>
    /// <param name="Offset">The location offset for that date, or zero when only UTC is known.</param>
    /// <param name="CancellationToken">Cancels waiting, requests and cache writes.</param>
    /// <returns>Validated raw GeoJSON with independent event nullability and any cache warning.</returns>
    static public async Task<ApiClientResult> LoadSunMoonAsync(string Body, double Latitude, double Longitude, DateOnly Date,
        TimeSpan Offset, CancellationToken CancellationToken = default)
    {
        if (Body != "Sun" && Body != "Moon")
            throw new ArgumentException("Sun or Moon is required.", nameof(Body));
        Latitude = Coordinate(Latitude, 90);
        Longitude = Coordinate(Longitude, 180);
        if (Offset < TimeSpan.FromHours(-12) || Offset > TimeSpan.FromHours(15) || Offset.Ticks % TimeSpan.TicksPerMinute != 0)
            throw new ArgumentOutOfRangeException(nameof(Offset));
        string OffsetText = WorldClockService.FormatOffset(Offset).Substring(3);
        string Url = "https://api.met.no/weatherapi/sunrise/3.0/" + Body.ToLowerInvariant()
            + "?lat=" + Latitude.ToString("0.####", CultureInfo.InvariantCulture)
            + "&lon=" + Longitude.ToString("0.####", CultureInfo.InvariantCulture)
            + "&date=" + Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) + "&offset=" + Uri.EscapeDataString(OffsetText);
        string Key = "sunrise3-" + Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(Encoding.UTF8.GetBytes(Url)));
        ApiClientResult Result = new(Url);
        SemaphoreSlim Gate = fLoadLocks.GetOrAdd(Key, Value => new SemaphoreSlim(1, 1));
        await Gate.WaitAsync(CancellationToken).ConfigureAwait(false);
        try
        {
            CacheEntry Cached = await ReadSunMoonCacheAsync(Key, Body, Latitude, Longitude, CancellationToken).ConfigureAwait(false);
            Result.ResponseJsonText = Cached?.Json;
            DateTime Now = DateTime.UtcNow;
            if (Cached != null && (Cached.NextRequestUtc > Now || Cached.CheckedUtc.AddHours(Lib.Settings.WeatherRefreshHours) > Now))
            {
                Result.ResultStatus = ApiClientResultStatus.Success;
                return Result;
            }
            try
            {
                if (fRetryAfter.TryGetValue(Key, out DateTime RetryAt) && RetryAt > Now)
                    throw new HttpRequestException("MET Norway requests are cooling down.");
                using CancellationTokenSource Deadline = CancellationTokenSource.CreateLinkedTokenSource(CancellationToken);
                Deadline.CancelAfter(TimeSpan.FromSeconds(30));
                using HttpRequestMessage Request = new(HttpMethod.Get, Url);
                Request.Headers.UserAgent.ParseAdd(UserAgent);
                if (!string.IsNullOrWhiteSpace(Cached?.LastModified))
                    Request.Headers.TryAddWithoutValidation("If-Modified-Since", Cached.LastModified);
                using HttpResponseMessage Response = await PublicApiClient.SendAsync(Request, Deadline.Token).ConfigureAwait(false);
                Result.StatusCode = Response.StatusCode;
                Now = DateTime.UtcNow;
                bool Unchanged = Response.StatusCode == HttpStatusCode.NotModified;
                if (!Response.IsSuccessStatusCode && !Unchanged)
                {
                    DateTime Until = Response.Headers.RetryAfter?.Date?.UtcDateTime
                        ?? Now.Add(Response.Headers.RetryAfter?.Delta ?? TimeSpan.FromMinutes(5));
                    fRetryAfter[Key] = Until > Now ? Until : Now.AddMinutes(5);
                    Response.EnsureSuccessStatusCode();
                }
                if (Unchanged && Cached == null)
                    throw new JsonException("MET Norway returned 304 without saved celestial data.");
                string Json = Unchanged ? Cached.Json : await Response.Content.ReadAsStringAsync(Deadline.Token).ConfigureAwait(false);
                ValidateSunMoon(Json, Body, Latitude, Longitude);
                string Modified = Header(Response, "Last-Modified");
                CacheEntry Updated = new()
                {
                    Json = Json, DownloadedUtc = Unchanged ? Cached.DownloadedUtc : Now, CheckedUtc = Now,
                    NextRequestUtc = NextRequest(Response, Now),
                    LastModified = string.IsNullOrWhiteSpace(Modified) && Unchanged ? Cached.LastModified : Modified
                };
                CancellationToken.ThrowIfCancellationRequested();
                Result.ResponseJsonText = Json;
                Result.ResultStatus = ApiClientResultStatus.Success;
                if ((int)Response.StatusCode == 203)
                    Result.ReasonPhrase = "MET Norway reports a product status warning.";
                try { await SaveCacheAsync(Key, Updated, CancellationToken).ConfigureAwait(false); }
                catch (Exception Ex) when (Ex is IOException || Ex is UnauthorizedAccessException)
                {
                    Result.ReasonPhrase = (Result.ReasonPhrase + " Sun & Moon data could not be saved for offline use.").Trim();
                }
                fRetryAfter.TryRemove(Key, out _);
            }
            catch (OperationCanceledException) when (CancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception Ex) when (Ex is HttpRequestException || Ex is IOException || Ex is OperationCanceledException || Ex is JsonException)
            {
                Result.ResponseJsonText = Cached?.Json;
                Result.ResultStatus = Cached == null ? ApiClientResultStatus.Error : ApiClientResultStatus.Success;
                Result.ReasonPhrase = Cached == null ? Body + " data could not be loaded. Please try again later."
                    : Body + " data could not be refreshed. Previously downloaded results for this date are shown.";
                if (!fRetryAfter.TryGetValue(Key, out DateTime ExistingRetry) || ExistingRetry <= DateTime.UtcNow)
                    fRetryAfter[Key] = DateTime.UtcNow.AddMinutes(5);
            }
            return Result;
        }
        finally
        {
            Gate.Release();
        }
    }
}
