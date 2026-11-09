/* PInfo, Copyright (c) 2026 Theo Bebekis, see License. */

namespace PInfo;

/// <summary>
/// Loads country records through SQLite and caches city records in JSON files.
/// </summary>
public class CountryService : PublicService
{
    // ● private
    const string CountriesUrl = "https://countries.dev/countries?full=true";
    const int CacheLifetimeDays = 30;
    const int CityCacheLifetimeMonths = 3;
    readonly ConcurrentDictionary<string, SemaphoreSlim> fCityLoadLocks = new(StringComparer.OrdinalIgnoreCase);
    readonly ConcurrentDictionary<string, (IReadOnlyList<CityModel> Items, DateTime SavedUtc)> fCities = new(StringComparer.OrdinalIgnoreCase);
    readonly SemaphoreSlim fLoadLock = new(1, 1);
    volatile IReadOnlyList<CountryModel> fCountries = Array.Empty<CountryModel>();
    DateTime fCountriesLoadedUtc = DateTime.MinValue;

    /// <summary>
    /// Deserializes, validates, and sorts a complete country collection.
    /// </summary>
    ReadOnlyCollection<CountryModel> ReadCountries(string Json, CancellationToken CancellationToken)
    {
        List<CountryModel> Items = JsonSerializer.Deserialize<List<CountryModel>>(Json);
        if (Items == null || Items.Count == 0)
            throw new JsonException("The country response contains no countries.");

        HashSet<string> Codes = new(StringComparer.OrdinalIgnoreCase);
        foreach (CountryModel Country in Items)
        {
            CancellationToken.ThrowIfCancellationRequested();
            if (Country == null || string.IsNullOrWhiteSpace(Country.Name)
                || string.IsNullOrWhiteSpace(Country.Alpha3Code)
                || Country.Alpha3Code.Length != 3 || string.IsNullOrWhiteSpace(Country.Flag)
                || !Codes.Add(Country.Alpha3Code))
                throw new JsonException("The country response contains invalid or duplicate country identifiers, names, or flags.");
        }

        return Items.OrderBy(Item => Item.Name, StringComparer.OrdinalIgnoreCase).ToList().AsReadOnly();
    }
    /// <summary>
    /// Reads countries from SQLite. Missing data or storage failure falls back to the API, never to JSON.
    /// </summary>
    async Task<(IReadOnlyList<CountryModel> Items, DateTime UpdatedUtc)> ReadCountryStorageAsync(
        CancellationToken CancellationToken)
    {
        try
        {
            return await Task.Run(() =>
            {
                List<CountryModel> Items = Db.GetCountries(out DateTime UpdatedUtc, CancellationToken);
                return ((IReadOnlyList<CountryModel>)Items.AsReadOnly(), UpdatedUtc);
            }, CancellationToken).ConfigureAwait(false);
        }
        catch (Exception Ex) when (Ex is SqliteException || Ex is IOException || Ex is UnauthorizedAccessException)
        {
            ExceptionLog.WriteException("CountryService.ReadCountryStorageAsync", Ex, false);
            return (Array.Empty<CountryModel>(), DateTime.MinValue);
        }
    }
    /// <summary>
    /// Saves the complete country collection and refresh timestamp on a worker thread.
    /// Returns a warning on storage failure, leaving the transaction rolled back and memory data usable.
    /// </summary>
    async Task<string> SaveCountryStorageAsync(IReadOnlyList<CountryModel> Items, DateTime UpdatedUtc,
        CancellationToken CancellationToken)
    {
        try
        {
            await Task.Run(() => Db.SaveCountries(Items, UpdatedUtc, CancellationToken), CancellationToken)
                .ConfigureAwait(false);
            return string.Empty;
        }
        catch (Exception Ex) when (Ex is SqliteException || Ex is IOException || Ex is UnauthorizedAccessException)
        {
            ExceptionLog.WriteException("CountryService.SaveCountryStorageAsync", Ex, false);
            return "Countries were loaded, but could not be saved to the database for offline use.";
        }
    }

    /// <summary>
    /// Validates and normalizes an alpha-2 code before using it in a URL or cache filename.
    /// </summary>
    string NormalizeCountryCode(string CountryCode)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(CountryCode);
        string Code = CountryCode.Trim().ToUpperInvariant();
        if (Code.Length != 2 || Code.Any(Character => Character < 'A' || Character > 'Z'))
            throw new ArgumentException("A two-letter country code is required.", nameof(CountryCode));
        return Code;
    }
    /// <summary>
    /// Returns the private city cache path for a validated country code.
    /// </summary>
    string GetCitiesCachePath(string CountryCode)
    {
        string Root = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (string.IsNullOrWhiteSpace(Root))
            throw new IOException("The application data directory is unavailable.");
        return Path.Combine(Root, "PInfo", "Data", "Cities", CountryCode + ".json");
    }
    /// <summary>
    /// Validates the API payload and returns up to 100 cities ordered by descending population.
    /// An empty array is a valid result for countries with no available cities.
    /// </summary>
    ReadOnlyCollection<CityModel> ReadCities(string Json, string CountryCode, CancellationToken CancellationToken)
    {
        using JsonDocument Document = JsonDocument.Parse(Json);
        if (Document.RootElement.ValueKind != JsonValueKind.Array || Document.RootElement.GetArrayLength() > 100)
            throw new JsonException("The city response must be an array of up to 100 records.");

        // Missing numeric fields must not silently become zero-valued coordinates or identifiers.
        foreach (JsonElement Item in Document.RootElement.EnumerateArray())
        {
            CancellationToken.ThrowIfCancellationRequested();
            if (Item.ValueKind != JsonValueKind.Object
                || !Item.TryGetProperty("geonameId", out JsonElement Id) || Id.ValueKind != JsonValueKind.Number || !Id.TryGetInt64(out long IdValue) || IdValue <= 0
                || !Item.TryGetProperty("population", out JsonElement Population) || Population.ValueKind != JsonValueKind.Number || !Population.TryGetInt64(out long PopulationValue) || PopulationValue < 0
                || !Item.TryGetProperty("latitude", out JsonElement Latitude) || Latitude.ValueKind != JsonValueKind.Number || !Latitude.TryGetDouble(out double LatitudeValue)
                || !double.IsFinite(LatitudeValue) || LatitudeValue < -90 || LatitudeValue > 90
                || !Item.TryGetProperty("longitude", out JsonElement Longitude) || Longitude.ValueKind != JsonValueKind.Number || !Longitude.TryGetDouble(out double LongitudeValue)
                || !double.IsFinite(LongitudeValue) || LongitudeValue < -180 || LongitudeValue > 180)
                throw new JsonException("The city response contains invalid numeric fields.");
        }

        List<CityModel> Items = JsonSerializer.Deserialize<List<CityModel>>(Json);
        if (Items == null)
            throw new JsonException("The city response is unavailable.");
        HashSet<long> Ids = new();
        foreach (CityModel City in Items)
        {
            CancellationToken.ThrowIfCancellationRequested();
            if (City == null || string.IsNullOrWhiteSpace(City.Name)
                || !string.Equals(City.CountryCode, CountryCode, StringComparison.OrdinalIgnoreCase)
                || !Ids.Add(City.GeonameId))
                throw new JsonException("The city response contains invalid, duplicate, or unrelated cities.");
        }
        return Items.OrderByDescending(Item => Item.Population)
            .ThenBy(Item => Item.Name, StringComparer.OrdinalIgnoreCase).ToList().AsReadOnly();
    }
    /// <summary>
    /// Reads a country's city cache and its timestamp; a null collection means no valid cache exists.
    /// </summary>
    async Task<(IReadOnlyList<CityModel> Items, DateTime SavedUtc)> ReadCitiesCacheAsync(
        string CountryCode, CancellationToken CancellationToken)
    {
        try
        {
            string FilePath = GetCitiesCachePath(CountryCode);
            DateTime SavedUtc = File.GetLastWriteTimeUtc(FilePath);
            string Json = await File.ReadAllTextAsync(FilePath, CancellationToken).ConfigureAwait(false);
            return (ReadCities(Json, CountryCode, CancellationToken), SavedUtc);
        }
        catch (IOException)
        {
            // Missing or unreadable caches fall back to the API.
        }
        catch (UnauthorizedAccessException)
        {
            // Missing or unreadable caches fall back to the API.
        }
        catch (JsonException)
        {
            // Invalid caches must not be published as loaded data.
        }
        return (null, DateTime.MinValue);
    }
    /// <summary>
    /// Replaces a city cache only after the complete validated response has been written to a temporary file.
    /// </summary>
    async Task SaveCitiesCacheAsync(string CountryCode, string Json, CancellationToken CancellationToken)
    {
        string FilePath = GetCitiesCachePath(CountryCode);
        string TemporaryPath = FilePath + "." + Guid.NewGuid().ToString("N") + ".tmp";
        Directory.CreateDirectory(Path.GetDirectoryName(FilePath));
        try
        {
            await File.WriteAllTextAsync(TemporaryPath, Json, CancellationToken).ConfigureAwait(false);
            CancellationToken.ThrowIfCancellationRequested();
            File.Move(TemporaryPath, FilePath, true);
        }
        finally
        {
            try
            {
                File.Delete(TemporaryPath);
            }
            catch (IOException)
            {
                // Cleanup failures must not hide the original load outcome.
            }
            catch (UnauthorizedAccessException)
            {
                // Cleanup failures must not hide the original load outcome.
            }
        }
    }

    // ● constructor
    /// <summary>
    /// Creates the Countries service. Loading begins only when explicitly requested.
    /// </summary>
    public CountryService()
      : base("Countries")
    {
    }

    // ● public
    /// <summary>
    /// Loads countries from memory, SQLite, or the API, in that order. Serializes concurrent loads.
    /// Refreshes data older than 30 days. Failed refreshes return Success with cached countries and a warning.
    /// Cancellation returns Cancelled, including while waiting for another load.
    /// A successful download remains available in memory if persistence fails; ReasonPhrase contains a warning.
    /// </summary>
    /// <param name="CancellationToken">Cancels this caller's wait or load. Another caller may retry afterward.</param>
    /// <returns>The load outcome. On Success, Countries contains the validated records.</returns>
    public async Task<ApiClientResult> LoadCountriesAsync(CancellationToken CancellationToken = default)
    {
        ApiClientResult Result = new(CountriesUrl);
        bool LockTaken = false;
        try
        {
            await fLoadLock.WaitAsync(CancellationToken).ConfigureAwait(false);
            LockTaken = true;
            CancellationToken.ThrowIfCancellationRequested();
            if (fCountries.Count == 0)
            {
                var Stored = await ReadCountryStorageAsync(CancellationToken).ConfigureAwait(false);
                CancellationToken.ThrowIfCancellationRequested();
                if (Stored.Items.Count > 0)
                {
                    fCountries = Stored.Items;
                    fCountriesLoadedUtc = Stored.UpdatedUtc;
                }
            }

            DateTime Now = DateTime.UtcNow;
            if (fCountries.Count > 0 && fCountriesLoadedUtc <= Now
                && Now - fCountriesLoadedUtc <= TimeSpan.FromDays(CacheLifetimeDays))
            {
                Result.ResultStatus = ApiClientResultStatus.Success;
                return Result;
            }

            Result = await PublicApiClient.GetAsync(CountriesUrl, CancellationToken).ConfigureAwait(false);
            CancellationToken.ThrowIfCancellationRequested();
            if (Result.ResultStatus != ApiClientResultStatus.Success)
            {
                if (Result.ResultStatus == ApiClientResultStatus.Error && fCountries.Count > 0)
                {
                    Result.ResultStatus = ApiClientResultStatus.Success;
                    Result.ReasonPhrase = "Countries could not be refreshed. Previously downloaded data is being used.";
                }
                return Result;
            }

            ReadOnlyCollection<CountryModel> Items = ReadCountries(Result.ResponseJsonText, CancellationToken);
            DateTime DownloadedUtc = DateTime.UtcNow;
            string SaveWarning = await SaveCountryStorageAsync(Items, DownloadedUtc, CancellationToken).ConfigureAwait(false);
            CancellationToken.ThrowIfCancellationRequested();
            fCountries = Items;
            fCountriesLoadedUtc = DownloadedUtc;
            if (!string.IsNullOrEmpty(SaveWarning))
                Result.ReasonPhrase = SaveWarning;
            Result.ResultStatus = ApiClientResultStatus.Success;
        }
        catch (OperationCanceledException) when (CancellationToken.IsCancellationRequested)
        {
            Result.ResultStatus = ApiClientResultStatus.Cancelled;
            Result.ReasonPhrase = "The country load was cancelled.";
        }
        catch (JsonException)
        {
            if (fCountries.Count > 0)
            {
                Result.ResultStatus = ApiClientResultStatus.Success;
                Result.ReasonPhrase = "The refreshed country response contains invalid data. Previously downloaded data is being used.";
            }
            else
            {
                Result.ResultStatus = ApiClientResultStatus.Error;
                Result.ReasonPhrase = "The country response contains invalid data.";
            }
        }
        finally
        {
            if (LockTaken)
                fLoadLock.Release();
        }
        return Result;
    }

    /// <summary>
    /// Loads up to 100 cities for a country from memory, disk, or countries.dev.
    /// Refreshes on demand after three calendar months, retaining cached data when refresh fails.
    /// Cancellation returns Cancelled. Storage failures leave downloaded data available in memory with a warning.
    /// </summary>
    /// <param name="CountryCode">The country's ISO alpha-2 code.</param>
    /// <param name="CancellationToken">Cancels this caller's wait or load.</param>
    /// <returns>The load outcome. On Success, GetCities returns the available records, which may be empty.</returns>
    /// <exception cref="ArgumentException">The country code is not two ASCII letters.</exception>
    public async Task<ApiClientResult> LoadCitiesAsync(string CountryCode, CancellationToken CancellationToken = default)
    {
        string Code = NormalizeCountryCode(CountryCode);
        string Url = "https://countries.dev/cities?country=" + Code + "&limit=100";
        ApiClientResult Result = new(Url);
        SemaphoreSlim LoadLock = fCityLoadLocks.GetOrAdd(Code, Key => new SemaphoreSlim(1, 1));
        bool LockTaken = false;
        (IReadOnlyList<CityModel> Items, DateTime SavedUtc) Cached = (null, DateTime.MinValue);
        try
        {
            await LoadLock.WaitAsync(CancellationToken).ConfigureAwait(false);
            LockTaken = true;
            CancellationToken.ThrowIfCancellationRequested();
            if (!fCities.TryGetValue(Code, out Cached))
            {
                Cached = await ReadCitiesCacheAsync(Code, CancellationToken).ConfigureAwait(false);
                CancellationToken.ThrowIfCancellationRequested();
                if (Cached.Items != null)
                    fCities[Code] = Cached;
            }
            DateTime Now = DateTime.UtcNow;
            if (Cached.Items != null && Cached.SavedUtc <= Now && Cached.SavedUtc.AddMonths(CityCacheLifetimeMonths) > Now)
            {
                Result.ResultStatus = ApiClientResultStatus.Success;
                return Result;
            }

            Result = await PublicApiClient.GetAsync(Url, CancellationToken).ConfigureAwait(false);
            CancellationToken.ThrowIfCancellationRequested();
            if (Result.ResultStatus != ApiClientResultStatus.Success)
            {
                if (Result.ResultStatus == ApiClientResultStatus.Error && Cached.Items != null)
                {
                    Result.ResultStatus = ApiClientResultStatus.Success;
                    Result.ReasonPhrase = "Cities could not be refreshed. Previously downloaded data is being used.";
                }
                return Result;
            }

            ReadOnlyCollection<CityModel> Items = ReadCities(Result.ResponseJsonText, Code, CancellationToken);
            DateTime DownloadedUtc = DateTime.UtcNow;
            try
            {
                await SaveCitiesCacheAsync(Code, Result.ResponseJsonText, CancellationToken).ConfigureAwait(false);
            }
            catch (IOException)
            {
                Result.ReasonPhrase = "Cities were loaded, but could not be saved for offline use.";
            }
            catch (UnauthorizedAccessException)
            {
                Result.ReasonPhrase = "Cities were loaded, but could not be saved for offline use.";
            }
            CancellationToken.ThrowIfCancellationRequested();
            fCities[Code] = (Items, DownloadedUtc);
            Result.ResultStatus = ApiClientResultStatus.Success;
        }
        catch (OperationCanceledException) when (CancellationToken.IsCancellationRequested)
        {
            Result.ResultStatus = ApiClientResultStatus.Cancelled;
            Result.ReasonPhrase = "The city load was cancelled.";
        }
        catch (JsonException)
        {
            Result.ResultStatus = Cached.Items != null ? ApiClientResultStatus.Success : ApiClientResultStatus.Error;
            Result.ReasonPhrase = Cached.Items != null
                ? "The refreshed city response contains invalid data. Previously downloaded data is being used."
                : "The city response contains invalid data.";
        }
        finally
        {
            if (LockTaken)
                LoadLock.Release();
        }
        return Result;
    }
    /// <summary>
    /// Returns the loaded top cities for a country, or an empty collection if none have been loaded.
    /// Collection membership is read-only; city models remain mutable.
    /// </summary>
    /// <param name="CountryCode">The country's ISO alpha-2 code.</param>
    /// <returns>The cached cities in descending population order.</returns>
    public IReadOnlyList<CityModel> GetCities(string CountryCode)
    {
        string Code = NormalizeCountryCode(CountryCode);
        return fCities.TryGetValue(Code, out var Cached) ? Cached.Items : Array.Empty<CityModel>();
    }

    // ● properties
    /// <summary>
    /// The loaded countries in alphabetical order, or an empty collection before loading succeeds.
    /// Collection membership is read-only; country models remain mutable.
    /// </summary>
    public IReadOnlyList<CountryModel> Countries => fCountries;
}
