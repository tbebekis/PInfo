/* PInfo, Copyright (c) 2026 Theo Bebekis, see License. */

namespace PInfo;

/// <summary>
/// Loads complete country records from countries.dev and caches them in memory and private storage.
/// </summary>
public class CountryService : PublicService
{
    // ● private
    const string CountriesUrl = "https://countries.dev/countries?full=true";
    const int CacheLifetimeDays = 30;
    readonly SemaphoreSlim fLoadLock = new(1, 1);
    volatile IReadOnlyList<CountryModel> fCountries = Array.Empty<CountryModel>();
    DateTime fCountriesLoadedUtc = DateTime.MinValue;

    /// <summary>
    /// Returns the persistent cache path without creating directories.
    /// </summary>
    string GetCachePath()
    {
        string Root = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (string.IsNullOrWhiteSpace(Root))
            throw new IOException("The application data directory is unavailable.");
        return Path.Combine(Root, "PInfo", "Data", "countries-full.json");
    }
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
    /// Reads a valid disk cache, falling back to the API when the cache is unavailable or invalid.
    /// </summary>
    async Task<IReadOnlyList<CountryModel>> ReadCacheAsync(CancellationToken CancellationToken)
    {
        try
        {
            string FilePath = GetCachePath();
            DateTime SavedUtc = File.GetLastWriteTimeUtc(FilePath);
            string Json = await File.ReadAllTextAsync(FilePath, CancellationToken).ConfigureAwait(false);
            IReadOnlyList<CountryModel> Items = ReadCountries(Json, CancellationToken);
            fCountriesLoadedUtc = SavedUtc;
            return Items;
        }
        catch (IOException)
        {
            return Array.Empty<CountryModel>();
        }
        catch (UnauthorizedAccessException)
        {
            return Array.Empty<CountryModel>();
        }
        catch (JsonException)
        {
            return Array.Empty<CountryModel>();
        }
    }
    /// <summary>
    /// Writes a temporary file before replacing the cache in the same directory.
    /// </summary>
    async Task SaveCacheAsync(string Json, CancellationToken CancellationToken)
    {
        string FilePath = GetCachePath();
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
                // A leftover temporary file must not hide the original outcome.
            }
            catch (UnauthorizedAccessException)
            {
                // A leftover temporary file must not hide the original outcome.
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
    /// Loads countries from memory, disk, or the API, in that order. Serializes concurrent loads.
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
                IReadOnlyList<CountryModel> CachedCountries = await ReadCacheAsync(CancellationToken).ConfigureAwait(false);
                CancellationToken.ThrowIfCancellationRequested();
                if (CachedCountries.Count > 0)
                    fCountries = CachedCountries;
            }

            if (fCountries.Count > 0 && DateTime.UtcNow - fCountriesLoadedUtc <= TimeSpan.FromDays(CacheLifetimeDays))
            {
                Result.ResultStatus = ApiClientResultStatus.Success;
                return Result;
            }

            Result = await PublicApiClient.GetAsync(CountriesUrl, CancellationToken).ConfigureAwait(false);
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
            try
            {
                await SaveCacheAsync(Result.ResponseJsonText, CancellationToken).ConfigureAwait(false);
            }
            catch (IOException)
            {
                Result.ReasonPhrase = "Countries were loaded, but could not be saved for offline use.";
            }
            catch (UnauthorizedAccessException)
            {
                Result.ReasonPhrase = "Countries were loaded, but could not be saved for offline use.";
            }
            CancellationToken.ThrowIfCancellationRequested();
            fCountries = Items;
            fCountriesLoadedUtc = DateTime.UtcNow;
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

    // ● properties
    /// <summary>
    /// The loaded countries in alphabetical order, or an empty collection before loading succeeds.
    /// Collection membership is read-only; country models remain mutable.
    /// </summary>
    public IReadOnlyList<CountryModel> Countries => fCountries;
}
