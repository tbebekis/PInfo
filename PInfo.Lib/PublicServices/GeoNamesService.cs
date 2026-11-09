/* PInfo, Copyright (c) 2026 Theo Bebekis, see License. */

namespace PInfo;

/// <summary>
/// Downloads and imports GeoNames country dumps on demand, refreshing after three calendar months.
/// Data is stored in SQLite; this service has no UI dependencies and keeps no full dump in memory.
/// </summary>
static public class GeoNamesService
{
    // ● private
    static readonly ConcurrentDictionary<string, SemaphoreSlim> fLoadLocks = new(StringComparer.Ordinal);

    /// <summary>
    /// Validates a country code before using it in an archive name, URL, or database scope.
    /// </summary>
    static string NormalizeCountryCode(string CountryCode)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(CountryCode);
        string Code = CountryCode.Trim().ToUpperInvariant();
        if (Code.Length != 2 || Code.Any(Character => Character < 'A' || Character > 'Z'))
            throw new ArgumentException("A two-letter country code is required.", nameof(CountryCode));
        return Code;
    }
    /// <summary>
    /// Parses an optional integer, preserving an empty value as null.
    /// </summary>
    static bool TryReadOptionalInteger(string Value, out int? Result)
    {
        Result = null;
        if (Value.Length == 0)
            return true;
        if (!int.TryParse(Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int Number))
            return false;
        Result = Number;
        return true;
    }
    /// <summary>
    /// Parses one record without throwing for invalid data and returns its rejection reason.
    /// </summary>
    static bool TryParsePlace(string Line, string CountryCode, out PlaceModel Place, out string Reason)
    {
        Place = null;
        Reason = string.Empty;
        string[] Fields = Line.Split('\t');
        if (Fields.Length != 19)
        {
            Reason = "Expected nineteen tab-separated fields.";
            return false;
        }
        if (!long.TryParse(Fields[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out long GeoNameId)
            || GeoNameId <= 0)
            Reason = "Invalid place identifier.";
        else if (string.IsNullOrWhiteSpace(Fields[1]))
            Reason = "Missing place name.";
        else if (Fields[8] != CountryCode)
            Reason = "The place belongs to a different country.";
        else if (Fields[6].Length != 1 || !"AHLPRSTUV".Contains(Fields[6]))
            Reason = "Invalid feature class.";
        if (Reason.Length != 0)
            return false;
        if (!double.TryParse(Fields[4], NumberStyles.Float | NumberStyles.AllowThousands,
                CultureInfo.InvariantCulture, out double Latitude)
            || !double.IsFinite(Latitude) || Latitude < -90 || Latitude > 90
            || !double.TryParse(Fields[5], NumberStyles.Float | NumberStyles.AllowThousands,
                CultureInfo.InvariantCulture, out double Longitude)
            || !double.IsFinite(Longitude) || Longitude < -180 || Longitude > 180)
        {
            Reason = "Invalid coordinates.";
            return false;
        }
        long Population = 0;
        if (Fields[14].Length != 0
            && (!long.TryParse(Fields[14], NumberStyles.Integer, CultureInfo.InvariantCulture, out Population)
                || Population < 0))
        {
            Reason = "Invalid population.";
            return false;
        }
        if (!TryReadOptionalInteger(Fields[15], out int? Elevation)
            || !TryReadOptionalInteger(Fields[16], out int? Dem))
        {
            Reason = "Invalid elevation or DEM.";
            return false;
        }
        if (!DateTime.TryParseExact(Fields[18], "yyyy-MM-dd", CultureInfo.InvariantCulture,
            DateTimeStyles.None, out DateTime ModificationDate))
        {
            Reason = "Invalid modification date.";
            return false;
        }
        Place = new()
        {
            GeoNameId = GeoNameId,
            Name = Fields[1], AsciiName = Fields[2], AlternateNames = Fields[3],
            Latitude = Latitude, Longitude = Longitude,
            FeatureClass = Fields[6], FeatureCode = Fields[7], CountryCode = Fields[8], CountryCodes = Fields[9],
            Admin1Code = Fields[10], Admin2Code = Fields[11], Admin3Code = Fields[12], Admin4Code = Fields[13],
            Population = Population, Elevation = Elevation, Dem = Dem, Timezone = Fields[17],
            ModificationDate = ModificationDate
        };
        return true;
    }
    /// <summary>
    /// Reads only the expected country text entry directly from the ZIP, without extracting files.
    /// Logs and skips invalid records before insertion; archive and reading failures propagate.
    /// </summary>
    static IEnumerable<PlaceModel> ReadPlaces(string ZipPath, string CountryCode, CancellationToken CancellationToken)
    {
        using ZipArchive Archive = ZipFile.OpenRead(ZipPath);
        string EntryName = CountryCode + ".txt";
        ZipArchiveEntry[] Entries = Archive.Entries.Where(Item => Item.FullName == EntryName).ToArray();
        if (Entries.Length != 1)
            throw new InvalidDataException("The ZIP must contain exactly one " + EntryName + " entry.");
        using Stream Input = Entries[0].Open();
        using StreamReader Reader = new(Input, new UTF8Encoding(false, true));
        long LineNumber = 0;
        string Line;
        while ((Line = Reader.ReadLine()) != null)
        {
            CancellationToken.ThrowIfCancellationRequested();
            LineNumber++;
            if (Line.Length == 0)
                continue;
            if (!TryParsePlace(Line, CountryCode, out PlaceModel Place, out string Reason))
            {
                ExceptionLog.WriteException("GeoNamesService.ReadPlaces",
                    $"Skipped GeoNames record for {CountryCode} at line {LineNumber}: {Reason}", false);
                continue;
            }
            yield return Place;
        }
    }

    // ● public
    /// <summary>
    /// Imports a local GeoNames ZIP atomically. Runs parsing and SQLite work on a background worker.
    /// The caller retains ownership of the ZIP. The country must already exist in SQLite.
    /// </summary>
    /// <param name="CountryCode">The ISO alpha-2 country code.</param>
    /// <param name="ZipPath">The country ZIP file.</param>
    /// <param name="UpdatedUtc">The UTC download timestamp.</param>
    /// <param name="CancellationToken">Cancels the import and rolls back changes.</param>
    /// <returns>The imported record count. Failures propagate to the caller.</returns>
    static public async Task<int> ImportPlacesAsync(string CountryCode, string ZipPath, DateTime UpdatedUtc,
        CancellationToken CancellationToken = default)
    {
        string Code = NormalizeCountryCode(CountryCode);
        ArgumentException.ThrowIfNullOrWhiteSpace(ZipPath);
        SemaphoreSlim LoadLock = fLoadLocks.GetOrAdd(Code, Key => new SemaphoreSlim(1, 1));
        await LoadLock.WaitAsync(CancellationToken).ConfigureAwait(false);
        try
        {
            return await Task.Run(() => Db.SavePlaces(Code, ReadPlaces(ZipPath, Code, CancellationToken),
                UpdatedUtc, CancellationToken), CancellationToken).ConfigureAwait(false);
        }
        finally
        {
            LoadLock.Release();
        }
    }
    /// <summary>
    /// Ensures a country has a GeoNames import no older than three calendar months.
    /// Download or import failures retain previous data and return a warning when it is available.
    /// Caller cancellation returns Cancelled. Temporary downloads are removed after every attempt.
    /// </summary>
    /// <param name="CountryCode">The ISO alpha-2 code of a country already stored in SQLite.</param>
    /// <param name="CancellationToken">Cancels waiting, download, or import.</param>
    /// <returns>The import outcome. Success may include a stale-data warning.</returns>
    static public async Task<ApiClientResult> LoadPlacesAsync(string CountryCode, CancellationToken CancellationToken = default)
    {
        string Code = NormalizeCountryCode(CountryCode);
        string Url = "https://download.geonames.org/export/dump/" + Code + ".zip";
        ApiClientResult Result = new(Url);
        SemaphoreSlim LoadLock = fLoadLocks.GetOrAdd(Code, Key => new SemaphoreSlim(1, 1));
        bool LockTaken = false;
        DateTime UpdatedUtc = DateTime.MinValue;
        string ZipPath = null;
        try
        {
            await LoadLock.WaitAsync(CancellationToken).ConfigureAwait(false);
            LockTaken = true;
            UpdatedUtc = await Task.Run(() => Db.GetPlacesUpdatedUtc(Code, CancellationToken), CancellationToken).ConfigureAwait(false);
            DateTime Now = DateTime.UtcNow;
            if (UpdatedUtc != DateTime.MinValue && UpdatedUtc <= Now && UpdatedUtc.AddMonths(3) > Now)
            {
                Result.ResultStatus = ApiClientResultStatus.Success;
                return Result;
            }
            ZipPath = Path.Combine(Path.GetTempPath(), "pinfo-geonames-" + Code + "-" + Guid.NewGuid().ToString("N") + ".zip");
            Result = await PublicApiClient.DownloadFileAsync(Url, ZipPath, CancellationToken).ConfigureAwait(false);
            CancellationToken.ThrowIfCancellationRequested();
            if (Result.ResultStatus == ApiClientResultStatus.Success)
            {
                DateTime DownloadedUtc = DateTime.UtcNow;
                // This lock is already held; avoid re-entering ImportPlacesAsync for the same country.
                await Task.Run(() => Db.SavePlaces(Code, ReadPlaces(ZipPath, Code, CancellationToken),
                    DownloadedUtc, CancellationToken), CancellationToken).ConfigureAwait(false);
                Result.ReasonPhrase = string.Empty;
            }
            else if (Result.ResultStatus == ApiClientResultStatus.Error && UpdatedUtc != DateTime.MinValue)
            {
                Result.ResultStatus = ApiClientResultStatus.Success;
                Result.ReasonPhrase = "Places could not be refreshed. Previously downloaded data is being used.";
            }
        }
        catch (OperationCanceledException) when (CancellationToken.IsCancellationRequested)
        {
            Result.ResultStatus = ApiClientResultStatus.Cancelled;
            Result.ReasonPhrase = "The place load was cancelled.";
        }
        catch (Exception Ex) when (Ex is IOException || Ex is InvalidDataException || Ex is UnauthorizedAccessException || Ex is SqliteException
            || Ex is DecoderFallbackException)
        {
            ExceptionLog.WriteException("GeoNamesService.LoadPlacesAsync", Ex, false);
            Result.ResultStatus = UpdatedUtc != DateTime.MinValue ? ApiClientResultStatus.Success : ApiClientResultStatus.Error;
            Result.ReasonPhrase = UpdatedUtc != DateTime.MinValue
                ? "Places could not be refreshed. Previously downloaded data is being used."
                : "Places could not be downloaded or saved. Please try again.";
        }
        finally
        {
            if (ZipPath != null)
            {
                try { File.Delete(ZipPath); }
                catch (Exception Ex) when (Ex is IOException || Ex is UnauthorizedAccessException)
                {
                    ExceptionLog.WriteException("GeoNamesService.DeleteDownload", Ex, false);
                }
            }
            if (LockTaken)
                LoadLock.Release();
        }
        return Result;
    }
}
