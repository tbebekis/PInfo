/* PInfo, Copyright (c) 2026 Theo Bebekis, see License. */

namespace PInfo;

/// <summary>
/// Stores user preferences in the application's private persistent storage.
/// </summary>
public class Settings
{
    // ● private
    static readonly JsonSerializerOptions fJsonOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter<AppTheme>(), new JsonStringEnumConverter<TemperatureUnit>() }
    };
    readonly SemaphoreSlim fSaveLock = new(1, 1);

    /// <summary>
    /// Returns the settings path without creating directories.
    /// </summary>
    static string GetFilePath()
    {
        string Root = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (string.IsNullOrWhiteSpace(Root))
            throw new IOException("The application data directory is unavailable.");
        return Path.Combine(Root, "PInfo", "settings.json");
    }

    // ● public
    /// <summary>
    /// Loads preferences, using Classic when the file is unavailable, invalid, or contains an unknown theme.
    /// </summary>
    /// <returns>The saved settings or default preferences.</returns>
    static public Settings Load()
    {
        try
        {
            Settings Result = JsonSerializer.Deserialize<Settings>(File.ReadAllText(GetFilePath()), fJsonOptions);
            if (Result != null && Enum.IsDefined(Result.Theme))
            {
                foreach (var Option in RefreshIntervals)
                {
                    int Value = Result.GetRefreshInterval(Option.PropertyName);
                    if (Value < Option.Minimum || Value > Option.Maximum)
                        Result.SetRefreshInterval(Option.PropertyName, Option.DefaultValue);
                }
                if (Result.ItemsPerPage < 10 || Result.ItemsPerPage > 100)
                    Result.ItemsPerPage = 100;
                if (!Enum.IsDefined(Result.TemperatureUnit))
                    Result.TemperatureUnit = PInfo.TemperatureUnit.Celsius;
                return Result;
            }
        }
        catch (IOException)
        {
            // Missing or inaccessible storage must not prevent application startup.
        }
        catch (UnauthorizedAccessException)
        {
            // Missing or inaccessible storage must not prevent application startup.
        }
        catch (JsonException)
        {
            // Invalid preferences fall back to the default theme.
        }
        return new Settings();
    }
    /// <summary>
    /// Saves a snapshot through a temporary file, preserving the previous file when writing fails.
    /// </summary>
    /// <returns>True when saved; false if storage is unavailable or a preference is invalid.</returns>
    public async Task<bool> SaveAsync()
    {
        await fSaveLock.WaitAsync().ConfigureAwait(false);
        string TemporaryPath = null;
        try
        {
            if (!Enum.IsDefined(Theme) || RefreshIntervals.Any(Option => GetRefreshInterval(Option.PropertyName) < Option.Minimum
                || GetRefreshInterval(Option.PropertyName) > Option.Maximum)
                || ItemsPerPage < 10 || ItemsPerPage > 100 || !Enum.IsDefined(TemperatureUnit))
                return false;
            string Json = JsonSerializer.Serialize(this, fJsonOptions);
            string FilePath = GetFilePath();
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath));
            TemporaryPath = FilePath + "." + Guid.NewGuid().ToString("N") + ".tmp";
            await File.WriteAllTextAsync(TemporaryPath, Json).ConfigureAwait(false);
            File.Move(TemporaryPath, FilePath, true);
            return true;
        }
        catch (IOException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
        finally
        {
            try
            {
                if (TemporaryPath != null)
                    File.Delete(TemporaryPath);
            }
            catch (IOException)
            {
                // Cleanup failure must not hide the save result.
            }
            catch (UnauthorizedAccessException)
            {
                // Cleanup failure must not hide the save result.
            }
            fSaveLock.Release();
        }
    }
    /// <summary>
    /// Reads one supported refresh preference by its stable property name.
    /// </summary>
    public int GetRefreshInterval(string PropertyName)
    {
        return PropertyName switch
        {
            nameof(CountriesRefreshDays) => CountriesRefreshDays,
            nameof(BigCitiesRefreshMonths) => BigCitiesRefreshMonths,
            nameof(PlacesRefreshMonths) => PlacesRefreshMonths,
            nameof(WeatherRefreshHours) => WeatherRefreshHours,
            nameof(RatesRefreshHours) => RatesRefreshHours,
            nameof(AssetCatalogueRefreshDays) => AssetCatalogueRefreshDays,
            nameof(WikipediaRefreshDays) => WikipediaRefreshDays,
            nameof(PhotonRefreshDays) => PhotonRefreshDays,
            nameof(BookSearchRefreshDays) => BookSearchRefreshDays,
            nameof(BookDetailsRefreshDays) => BookDetailsRefreshDays,
            nameof(UniversitiesRefreshDays) => UniversitiesRefreshDays,
            nameof(WorldBankRefreshDays) => WorldBankRefreshDays,
            nameof(HolidaysRefreshDays) => HolidaysRefreshDays,
            nameof(ArtRefreshDays) => ArtRefreshDays,
            _ => throw new ArgumentException("Unknown refresh preference.", nameof(PropertyName))
        };
    }
    /// <summary>
    /// Sets a validated whole-number refresh preference without saving or downloading data.
    /// </summary>
    public void SetRefreshInterval(string PropertyName, int Value)
    {
        var Option = RefreshIntervals.FirstOrDefault(Item => Item.PropertyName == PropertyName);
        if (Option.PropertyName == null || Value < Option.Minimum || Value > Option.Maximum)
            throw new ArgumentOutOfRangeException(nameof(Value), "Unsupported refresh preference or value.");
        switch (PropertyName)
        {
            case nameof(CountriesRefreshDays): CountriesRefreshDays = Value; break;
            case nameof(BigCitiesRefreshMonths): BigCitiesRefreshMonths = Value; break;
            case nameof(PlacesRefreshMonths): PlacesRefreshMonths = Value; break;
            case nameof(WeatherRefreshHours): WeatherRefreshHours = Value; break;
            case nameof(RatesRefreshHours): RatesRefreshHours = Value; break;
            case nameof(AssetCatalogueRefreshDays): AssetCatalogueRefreshDays = Value; break;
            case nameof(WikipediaRefreshDays): WikipediaRefreshDays = Value; break;
            case nameof(PhotonRefreshDays): PhotonRefreshDays = Value; break;
            case nameof(BookSearchRefreshDays): BookSearchRefreshDays = Value; break;
            case nameof(BookDetailsRefreshDays): BookDetailsRefreshDays = Value; break;
            case nameof(UniversitiesRefreshDays): UniversitiesRefreshDays = Value; break;
            case nameof(WorldBankRefreshDays): WorldBankRefreshDays = Value; break;
            case nameof(HolidaysRefreshDays): HolidaysRefreshDays = Value; break;
            case nameof(ArtRefreshDays): ArtRefreshDays = Value; break;
        }
    }

    // ● properties
    /// <summary>
    /// Shared refresh editor metadata; static data is not included in saved settings JSON.
    /// </summary>
    static public IReadOnlyList<(string PropertyName, string Caption, string Unit, int DefaultValue, int Minimum, int Maximum)> RefreshIntervals { get; }
        = Array.AsReadOnly(new (string PropertyName, string Caption, string Unit, int DefaultValue, int Minimum, int Maximum)[]
        {
            (nameof(CountriesRefreshDays), "Countries", "days", 30, 1, 3650),
            (nameof(BigCitiesRefreshMonths), "Big Cities", "months", 3, 1, 120),
            (nameof(PlacesRefreshMonths), "All Cities / Places", "months", 24, 1, 120),
            (nameof(WeatherRefreshHours), "Weather / Sun & Moon", "hours", 3, 3, 168),
            (nameof(RatesRefreshHours), "Exchange rates / crypto prices", "hours", 3, 1, 168),
            (nameof(AssetCatalogueRefreshDays), "Currency / crypto catalogues", "days", 7, 1, 3650),
            (nameof(WikipediaRefreshDays), "Wikipedia", "days", 7, 1, 3650),
            (nameof(PhotonRefreshDays), "Photon location searches", "days", 7, 1, 3650),
            (nameof(BookSearchRefreshDays), "Book searches", "days", 1, 1, 3650),
            (nameof(BookDetailsRefreshDays), "Book details", "days", 7, 1, 3650),
            (nameof(ArtRefreshDays), "Art - The Met", "days", 30, 1, 3650),
            (nameof(UniversitiesRefreshDays), "Universities", "days", 30, 1, 3650),
            (nameof(WorldBankRefreshDays), "World Bank", "days", 30, 1, 3650),
            (nameof(HolidaysRefreshDays), "Public holidays", "days", 30, 1, 3650),
        });
    /// <summary>
    /// The preferred application theme. Classic is used when no preference has been saved.
    /// </summary>
    public AppTheme Theme { get; set; } = AppTheme.Classic;
    /// <summary>
    /// The Places refresh interval in calendar months, from 1 to 120. Defaults to 24 months.
    /// </summary>
    public int PlacesRefreshMonths { get; set; } = 24;
    /// <summary>
    /// The shared page size for paged lists, from 10 to 100. Countries and Big Cities are excluded.
    /// </summary>
    public int ItemsPerPage { get; set; } = 100;
    /// <summary>
    /// The display unit shared by all weather views. Defaults to Celsius.
    /// </summary>
    public TemperatureUnit TemperatureUnit { get; set; } = PInfo.TemperatureUnit.Celsius;
    /// <summary>
    /// The countries refresh interval in days, from 1 to 3650. Defaults to 30.
    /// </summary>
    public int CountriesRefreshDays { get; set; } = 30;
    /// <summary>
    /// The big cities refresh interval in months, from 1 to 120. Defaults to 3.
    /// </summary>
    public int BigCitiesRefreshMonths { get; set; } = 3;
    /// <summary>
    /// The weather refresh interval in hours, from 3 to 168. Defaults to 3.
    /// </summary>
    public int WeatherRefreshHours { get; set; } = 3;
    /// <summary>
    /// The exchange rates / crypto prices refresh interval in hours, from 1 to 168. Defaults to 3.
    /// </summary>
    public int RatesRefreshHours { get; set; } = 3;
    /// <summary>
    /// The currency / crypto catalogues refresh interval in days, from 1 to 3650. Defaults to 7.
    /// </summary>
    public int AssetCatalogueRefreshDays { get; set; } = 7;
    /// <summary>
    /// The wikipedia refresh interval in days, from 1 to 3650. Defaults to 7.
    /// </summary>
    public int WikipediaRefreshDays { get; set; } = 7;
    /// <summary>
    /// The photon location searches refresh interval in days, from 1 to 3650. Defaults to 7.
    /// </summary>
    public int PhotonRefreshDays { get; set; } = 7;
    /// <summary>
    /// The book searches refresh interval in days, from 1 to 3650. Defaults to 1.
    /// </summary>
    public int BookSearchRefreshDays { get; set; } = 1;
    /// <summary>
    /// The book details refresh interval in days, from 1 to 3650. Defaults to 7.
    /// </summary>
    public int BookDetailsRefreshDays { get; set; } = 7;
    /// <summary>
    /// The universities refresh interval in days, from 1 to 3650. Defaults to 30.
    /// </summary>
    public int UniversitiesRefreshDays { get; set; } = 30;
    /// <summary>
    /// The world bank refresh interval in days, from 1 to 3650. Defaults to 30.
    /// </summary>
    public int WorldBankRefreshDays { get; set; } = 30;
    /// <summary>
    /// The public holidays refresh interval in days, from 1 to 3650. Defaults to 30.
    /// </summary>
    public int HolidaysRefreshDays { get; set; } = 30;
    /// <summary>
    /// The Met catalogue and artwork metadata refresh interval in days, from 1 to 3650. Defaults to 30.
    /// </summary>
    public int ArtRefreshDays { get; set; } = 30;
}
