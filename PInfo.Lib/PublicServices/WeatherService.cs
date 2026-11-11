/* PInfo, Copyright (c) 2026 Theo Bebekis, see License. */

namespace PInfo;

/// <summary>
/// Loads MET Norway forecasts with provider-aware caching, conditional requests, and offline fallback.
/// No work is performed until a forecast is explicitly requested.
/// </summary>
static public partial class WeatherService
{
    // ● private
    /// <summary>
    /// Stores the original provider response and HTTP cache metadata atomically in one location file.
    /// </summary>
    sealed class CacheEntry
    {
        // ● constructor
        /// <summary>
        /// Creates a cache envelope for JSON deserialization.
        /// </summary>
        public CacheEntry()
        {
        }

        // ● properties
        /// <summary>
        /// The original MET Norway response body.
        /// </summary>
        public string Json { get; set; } = string.Empty;
        /// <summary>
        /// The original body download time in UTC.
        /// </summary>
        public DateTime DownloadedUtc { get; set; }
        /// <summary>
        /// The most recent successful request or 304 validation time in UTC.
        /// </summary>
        public DateTime CheckedUtc { get; set; }
        /// <summary>
        /// The earliest permitted next request, respecting Expires and the three-hour conservative minimum.
        /// </summary>
        public DateTime NextRequestUtc { get; set; }
        /// <summary>
        /// The exact Last-Modified header used for conditional requests.
        /// </summary>
        public string LastModified { get; set; } = string.Empty;
    }
    static readonly ConcurrentDictionary<string, SemaphoreSlim> fLoadLocks = new(StringComparer.Ordinal);
    static readonly ConcurrentDictionary<string, CacheEntry> fCaches = new(StringComparer.Ordinal);
    static readonly ConcurrentDictionary<string, DateTime> fRetryAfter = new(StringComparer.Ordinal);
    const string UserAgent = "PInfo/1.0 (+https://github.com/tbebekis/PInfo)";

    /// <summary>
    /// Validates and truncates a coordinate to the provider's maximum four decimal places.
    /// </summary>
    /// <param name="Value">The coordinate.</param>
    /// <param name="Limit">The absolute geographic bound.</param>
    /// <returns>The provider request coordinate.</returns>
    static double Coordinate(double Value, double Limit)
    {
        if (!double.IsFinite(Value) || Value < -Limit || Value > Limit)
            throw new ArgumentOutOfRangeException(nameof(Value));
        double Result = (double)(decimal.Truncate((decimal)Value * 10000m) / 10000m);
        return Result == 0 ? 0 : Result;
    }
    /// <summary>
    /// Resolves a supplied GeoNames timezone or explicitly falls back to UTC, never the device timezone.
    /// </summary>
    /// <param name="Timezone">The location timezone.</param>
    /// <param name="Warning">An explanatory message for a missing/unusable zone.</param>
    /// <returns>The usable timezone identifier.</returns>
    static string ResolveTimezone(string Timezone, out string Warning)
    {
        Warning = string.Empty;
        if (!string.IsNullOrWhiteSpace(Timezone))
        {
            try { return TimeZoneInfo.FindSystemTimeZoneById(Timezone.Trim()).Id; }
            catch (TimeZoneNotFoundException) { }
            catch (InvalidTimeZoneException) { }
        }
        Warning = "The location timezone is unavailable. Times are shown in UTC.";
        return TimeZoneInfo.Utc.Id;
    }
    /// <summary>
    /// Returns a provider-specific cache path so old Open-Meteo files cannot be interpreted as MET data.
    /// </summary>
    /// <param name="Key">The truncated coordinate key.</param>
    /// <returns>The private cache filename.</returns>
    static string GetCachePath(string Key)
    {
        string Root = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (string.IsNullOrWhiteSpace(Root))
            throw new IOException("The application data directory is unavailable.");
        return Path.Combine(Root, "PInfo", "Data", "Weather", "met-norway", Key + ".json");
    }
    /// <summary>
    /// Reads HTTP cache metadata and validates the original response before accepting a disk cache.
    /// </summary>
    /// <param name="Key">The coordinate key.</param>
    /// <param name="Latitude">The request latitude.</param>
    /// <param name="Longitude">The request longitude.</param>
    /// <param name="Timezone">The display timezone.</param>
    /// <param name="CancellationToken">Cancels reading and parsing.</param>
    /// <returns>A valid cache entry or null.</returns>
    static async Task<CacheEntry> ReadCacheAsync(string Key, double Latitude, double Longitude, string Timezone,
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
                || Entry.NextRequestUtc < Entry.CheckedUtc
                || string.IsNullOrWhiteSpace(Entry.Json))
                return null;
            if (!string.IsNullOrEmpty(Entry.LastModified)
                && !DateTimeOffset.TryParse(Entry.LastModified, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out _))
                return null;
            await Task.Run(() => ParseForecast(Entry.Json, Latitude, Longitude, Timezone, Entry.DownloadedUtc,
                CancellationToken), CancellationToken).ConfigureAwait(false);
            return Entry;
        }
        catch (Exception Ex) when (Ex is IOException || Ex is UnauthorizedAccessException || Ex is JsonException)
        {
            // Invalid or inaccessible caches are not published as available forecasts.
            return null;
        }
    }
    /// <summary>
    /// Atomically writes the response and its HTTP metadata, retaining the previous file on failure.
    /// </summary>
    /// <param name="Key">The coordinate key.</param>
    /// <param name="Entry">The validated cache envelope.</param>
    /// <param name="CancellationToken">Cancels writing before replacement.</param>
    static async Task SaveCacheAsync(string Key, CacheEntry Entry, CancellationToken CancellationToken)
    {
        string FilePath = GetCachePath(Key);
        string TemporaryPath = FilePath + "." + Guid.NewGuid().ToString("N") + ".tmp";
        Directory.CreateDirectory(Path.GetDirectoryName(FilePath));
        try
        {
            await File.WriteAllTextAsync(TemporaryPath, JsonSerializer.Serialize(Entry), CancellationToken).ConfigureAwait(false);
            CancellationToken.ThrowIfCancellationRequested();
            File.Move(TemporaryPath, FilePath, true);
        }
        finally
        {
            try { File.Delete(TemporaryPath); }
            catch (Exception Ex) when (Ex is IOException || Ex is UnauthorizedAccessException)
            {
                ExceptionLog.WriteException("WeatherService.DeleteTemporaryCache", Ex, false);
            }
        }
    }
    /// <summary>
    /// Retrieves a single response-header value without changing its original string representation.
    /// </summary>
    /// <param name="Response">The HTTP response.</param>
    /// <param name="Name">The header name.</param>
    /// <returns>The raw value or an empty string.</returns>
    static string Header(HttpResponseMessage Response, string Name)
    {
        if (Response.Headers.TryGetValues(Name, out IEnumerable<string> Values)
            || Response.Content.Headers.TryGetValues(Name, out Values))
            return Values.FirstOrDefault() ?? string.Empty;
        return string.Empty;
    }
    /// <summary>
    /// Applies both the provider expiry and a conservative three-hour request interval for every location.
    /// </summary>
    /// <param name="Response">The provider response, including 304 responses.</param>
    /// <param name="Now">The successful request time.</param>
    /// <returns>The earliest next request in UTC.</returns>
    static DateTime NextRequest(HttpResponseMessage Response, DateTime Now)
    {
        DateTime Next = Now.AddHours(3);
        if (DateTimeOffset.TryParse(Header(Response, "Expires"), CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal, out DateTimeOffset Expires) && Expires.UtcDateTime > Next)
            Next = Expires.UtcDateTime;
        return Next;
    }

    // ● public
    /// <summary>
    /// Loads the available MET Norway forecast using truncated coordinates and the place's timezone.
    /// Uses Expires/If-Modified-Since, keeps source/download timestamps separate, and retains valid cached data on failure.
    /// </summary>
    /// <param name="Latitude">The location latitude.</param>
    /// <param name="Longitude">The location longitude.</param>
    /// <param name="Timezone">The GeoNames timezone; an unusable value produces an explicit UTC warning.</param>
    /// <param name="CancellationToken">Cancels waiting, HTTP work, parsing, or persistence.</param>
    /// <returns>The outcome and available forecast; failed initial loads and cancellation have no forecast.</returns>
    static public async Task<(ApiClientResult Result, WeatherForecast Forecast)> LoadForecastAsync(
        double Latitude, double Longitude, string Timezone, CancellationToken CancellationToken = default)
    {
        Latitude = Coordinate(Latitude, 90);
        Longitude = Coordinate(Longitude, 180);
        string Key = Latitude.ToString("0.####", CultureInfo.InvariantCulture) + "_" + Longitude.ToString("0.####", CultureInfo.InvariantCulture);
        string Zone = ResolveTimezone(Timezone, out string ZoneWarning);
        string Url = "https://api.met.no/weatherapi/locationforecast/2.0/complete?lat="
            + Latitude.ToString("0.####", CultureInfo.InvariantCulture) + "&lon=" + Longitude.ToString("0.####", CultureInfo.InvariantCulture);
        ApiClientResult Result = new(Url);
        SemaphoreSlim LoadLock = fLoadLocks.GetOrAdd(Key, Item => new SemaphoreSlim(1, 1));
        bool LockTaken = false;
        CacheEntry Cached = null;
        WeatherForecast Previous = null;
        try
        {
            await LoadLock.WaitAsync(CancellationToken).ConfigureAwait(false);
            LockTaken = true;
            if (!fCaches.TryGetValue(Key, out Cached))
            {
                Cached = await ReadCacheAsync(Key, Latitude, Longitude, Zone, CancellationToken).ConfigureAwait(false);
                if (Cached != null)
                    fCaches[Key] = Cached;
            }
            if (Cached != null)
                Previous = await Task.Run(() => ParseForecast(Cached.Json, Latitude, Longitude, Zone, Cached.DownloadedUtc,
                    CancellationToken), CancellationToken).ConfigureAwait(false);
            CancellationToken.ThrowIfCancellationRequested();
            DateTime Now = DateTime.UtcNow;
            if (Cached != null && Cached.CheckedUtc <= Now && (Cached.NextRequestUtc > Now
                || Cached.CheckedUtc.AddHours(Lib.Settings.WeatherRefreshHours) > Now))
            {
                Result.ResultStatus = ApiClientResultStatus.Success;
                Result.ReasonPhrase = ZoneWarning;
                return (Result, Previous);
            }
            if (fRetryAfter.TryGetValue(Key, out DateTime RetryAt) && RetryAt > Now)
            {
                Result.ResultStatus = Previous == null ? ApiClientResultStatus.Error : ApiClientResultStatus.Success;
                Result.ReasonPhrase = "Weather cannot be refreshed yet. Please try again later. " + ZoneWarning;
                return (Result, Previous);
            }
            using HttpRequestMessage Request = new(HttpMethod.Get, Url);
            Request.Headers.UserAgent.ParseAdd(UserAgent);
            if (!string.IsNullOrEmpty(Cached?.LastModified))
                Request.Headers.TryAddWithoutValidation("If-Modified-Since", Cached.LastModified);
            using HttpResponseMessage Response = await PublicApiClient.SendAsync(Request, CancellationToken).ConfigureAwait(false);
            Result.StatusCode = Response.StatusCode;
            Now = DateTime.UtcNow;
            if (!Response.IsSuccessStatusCode && Response.StatusCode != HttpStatusCode.NotModified)
            {
                DateTime Delay = Response.Headers.RetryAfter?.Date?.UtcDateTime
                    ?? Now.Add(Response.Headers.RetryAfter?.Delta ?? TimeSpan.FromMinutes(5));
                fRetryAfter[Key] = Delay > Now ? Delay : Now.AddMinutes(5);
                Result.ResultStatus = Previous == null ? ApiClientResultStatus.Error : ApiClientResultStatus.Success;
                Result.ReasonPhrase = Previous == null ? "Weather could not be loaded. Please try again later."
                    : "Weather could not be refreshed. Previously downloaded data is shown; check its timestamps.";
                Result.ReasonPhrase = (Result.ReasonPhrase + " " + ZoneWarning).Trim();
                return (Result, Previous);
            }
            bool Unchanged = Response.StatusCode == HttpStatusCode.NotModified;
            if (Unchanged && Cached == null)
                throw new JsonException("MET Norway returned 304 without cached data.");
            string Json = Unchanged ? Cached.Json : await Response.Content.ReadAsStringAsync(CancellationToken).ConfigureAwait(false);
            DateTime Downloaded = Unchanged ? Cached.DownloadedUtc : Now;
            WeatherForecast Forecast = await Task.Run(() => ParseForecast(Json, Latitude, Longitude, Zone, Downloaded,
                CancellationToken), CancellationToken).ConfigureAwait(false);
            string LastModified = Header(Response, "Last-Modified");
            CacheEntry Updated = new()
            {
                Json = Json, DownloadedUtc = Downloaded, CheckedUtc = Now, NextRequestUtc = NextRequest(Response, Now),
                LastModified = string.IsNullOrEmpty(LastModified) && Unchanged ? Cached.LastModified : LastModified
            };
            string Warning = ZoneWarning;
            if ((int)Response.StatusCode == 203)
            {
                ExceptionLog.WriteException("WeatherService.LoadForecastAsync", "MET Norway returned HTTP 203; check product deprecation notices.", false);
                Warning = (Warning + " The weather provider reports a product status warning.").Trim();
            }
            try { await SaveCacheAsync(Key, Updated, CancellationToken).ConfigureAwait(false); }
            catch (Exception Ex) when (Ex is IOException || Ex is UnauthorizedAccessException)
            {
                ExceptionLog.WriteException("WeatherService.SaveCacheAsync", Ex, false);
                Warning = (Warning + " Weather could not be saved for offline use.").Trim();
            }
            CancellationToken.ThrowIfCancellationRequested();
            fCaches[Key] = Updated;
            fRetryAfter.TryRemove(Key, out _);
            Result.ResultStatus = ApiClientResultStatus.Success;
            Result.ReasonPhrase = Warning;
            return (Result, Forecast);
        }
        catch (OperationCanceledException) when (CancellationToken.IsCancellationRequested)
        {
            Result.ResultStatus = ApiClientResultStatus.Cancelled;
            Result.ReasonPhrase = "The weather load was cancelled.";
            return (Result, null);
        }
        catch (Exception Ex) when (Ex is HttpRequestException || Ex is IOException || Ex is OperationCanceledException || Ex is JsonException)
        {
            ExceptionLog.WriteException("WeatherService.LoadForecastAsync", Ex, false);
            fRetryAfter[Key] = DateTime.UtcNow.AddMinutes(5);
            Result.ResultStatus = Previous == null ? ApiClientResultStatus.Error : ApiClientResultStatus.Success;
            Result.ReasonPhrase = (Previous == null ? "Weather could not be loaded. Please try again."
                : "Weather could not be refreshed. Previously downloaded data is shown; check its timestamps.") + " " + ZoneWarning;
            return (Result, Previous);
        }
        finally
        {
            if (LockTaken)
                LoadLock.Release();
        }
    }
}
