/* PInfo, Copyright (c) 2026 Theo Bebekis, see License. */

namespace PInfo;

/// <summary>
/// Creates stable favorite identities and versioned snapshots without changing imported model contracts.
/// </summary>
static public class FavoriteService
{
    // ● private
    /// <summary>
    /// Creates a saved destination from a typed snapshot.
    /// </summary>
    static FavoriteModel Create(FavoriteKind Kind, string Key, string Name, string Context, FavoriteSnapshot Snapshot)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(Key);
        ArgumentException.ThrowIfNullOrWhiteSpace(Name);
        return new FavoriteModel
        {
            Kind = Kind, TargetKey = Key, Name = Name.Trim(), Context = Context ?? string.Empty,
            DataJson = JsonSerializer.Serialize(Snapshot)
        };
    }
    /// <summary>
    /// Formats the resolved area and country context without empty or repeated parts.
    /// </summary>
    static string Context(string Area, string Country)
    {
        return string.Join(" · ", new[] { Area, Country }.Where(Value => !string.IsNullOrWhiteSpace(Value))
            .Distinct(StringComparer.OrdinalIgnoreCase));
    }
    /// <summary>
    /// Validates weather coordinates before writing or reopening a saved location.
    /// </summary>
    static void CheckLocation(double Latitude, double Longitude)
    {
        if (!double.IsFinite(Latitude) || !double.IsFinite(Longitude) || Latitude < -90 || Latitude > 90 || Longitude < -180 || Longitude > 180)
            throw new ArgumentException("A valid geographic location is required.");
    }
    /// <summary>
    /// Checks the saved asset code against the corresponding provider identifier rules.
    /// </summary>
    static bool IsAssetCode(string Code, bool Cryptocurrencies)
    {
        if (string.IsNullOrWhiteSpace(Code))
            return false;
        return Cryptocurrencies
            ? Code.Length >= 2 && Code.Length <= 12
                && Code.All(Character => Character >= 'A' && Character <= 'Z' || Character >= '0' && Character <= '9')
            : Code.Length == 3 && Code.All(Character => Character >= 'A' && Character <= 'Z');
    }

    // ● public
    /// <summary>
    /// Returns the category label displayed in Favorites.
    /// </summary>
    static public string CategoryName(FavoriteKind Kind)
    {
        return Kind switch
        {
            FavoriteKind.Country => "Countries", FavoriteKind.City => "Cities", FavoriteKind.Place => "Places",
            FavoriteKind.Weather => "Weather", FavoriteKind.ExchangeRates => "Exchange Rates",
            FavoriteKind.Cryptocurrencies => "Cryptocurrencies", FavoriteKind.WorldClock => "World Clock",
            FavoriteKind.Art => "Art - The Met", _ => throw new ArgumentException("Unknown favorite category.", nameof(Kind))
        };
    }
    /// <summary>
    /// Creates a country favorite identified by its ISO alpha-2 code.
    /// </summary>
    static public FavoriteModel Country(CountryModel Country)
    {
        ArgumentNullException.ThrowIfNull(Country);
        ArgumentException.ThrowIfNullOrWhiteSpace(Country.Alpha2Code);
        return Create(FavoriteKind.Country, Country.Alpha2Code.Trim().ToUpperInvariant(), Country.Name,
            Country.Region, new FavoriteSnapshot { Country = Country });
    }
    /// <summary>
    /// Creates a city favorite identified by its GeoNames ID.
    /// </summary>
    static public FavoriteModel City(CityModel City)
    {
        ArgumentNullException.ThrowIfNull(City);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(City.GeonameId);
        string Name = string.IsNullOrWhiteSpace(City.AsciiName) ? City.Name : City.AsciiName.Trim();
        return Create(FavoriteKind.City, City.GeonameId.ToString(CultureInfo.InvariantCulture), Name,
            Context(City.AdministrativeArea, City.CountryCode), new FavoriteSnapshot { City = City, AdministrativeArea = City.AdministrativeArea });
    }
    /// <summary>
    /// Creates a place favorite while retaining the chosen detail category.
    /// </summary>
    static public FavoriteModel Place(PlaceModel Place, PlaceCategory Category)
    {
        ArgumentNullException.ThrowIfNull(Place);
        ArgumentNullException.ThrowIfNull(Category);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(Place.GeoNameId);
        string Name = string.IsNullOrWhiteSpace(Place.AsciiName) ? Place.Name : Place.AsciiName.Trim();
        return Create(FavoriteKind.Place, Place.GeoNameId.ToString(CultureInfo.InvariantCulture), Name,
            Context(Place.AdministrativeArea, Place.CountryCode), new FavoriteSnapshot
            {
                Place = Place, CategoryName = Category.Name, AdministrativeArea = Place.AdministrativeArea
            });
    }
    /// <summary>
    /// Creates weather membership for a location, using a GeoNames identity when supplied and coordinates otherwise.
    /// </summary>
    static public FavoriteModel Weather(string Name, double Latitude, double Longitude, string Timezone,
        string AdministrativeArea, string LocationKey = "")
    {
        CheckLocation(Latitude, Longitude);
        string Key = string.IsNullOrWhiteSpace(LocationKey) ? "coordinates:"
            + (Latitude == 0 ? 0 : Latitude).ToString("R", CultureInfo.InvariantCulture) + ":"
            + (Longitude == 0 ? 0 : Longitude).ToString("R", CultureInfo.InvariantCulture) : LocationKey.Trim();
        return Create(FavoriteKind.Weather, Key, Name, string.IsNullOrWhiteSpace(AdministrativeArea) ? Timezone : AdministrativeArea, new FavoriteSnapshot
        {
            Weather = new FavoriteWeatherLocation
            {
                Name = Name, Latitude = Latitude, Longitude = Longitude, Timezone = Timezone ?? string.Empty,
                AdministrativeArea = AdministrativeArea ?? string.Empty
            }
        });
    }
    /// <summary>
    /// Saves an asset choice in its own category without storing any rate or price measurements.
    /// </summary>
    /// <param name="Currency">The selected base currency or cryptocurrency.</param>
    /// <param name="Cryptocurrencies">True for cryptocurrency reference prices.</param>
    static public FavoriteModel ExchangeRates(CurrencyDetail Currency, bool Cryptocurrencies)
    {
        ArgumentNullException.ThrowIfNull(Currency);
        string Code = (Currency.Code ?? string.Empty).Trim().ToUpperInvariant();
        if (!IsAssetCode(Code, Cryptocurrencies))
            throw new ArgumentException("A valid base asset code is required.", nameof(Currency));
        ArgumentException.ThrowIfNullOrWhiteSpace(Currency.Name);
        return Create(Cryptocurrencies ? FavoriteKind.Cryptocurrencies : FavoriteKind.ExchangeRates,
            Code, Code + " — " + Currency.Name.Trim(), Cryptocurrencies ? "Cryptocurrency reference prices" : "Currency reference rates",
            new FavoriteSnapshot { BaseCurrency = new CurrencyDetail { Code = Code, Name = Currency.Name.Trim() } });
    }
    /// <summary>
    /// Saves only the timezone identifier in the existing target key, never a clock reading.
    /// </summary>
    static public FavoriteModel WorldClock(string ZoneId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ZoneId);
        return Create(FavoriteKind.WorldClock, ZoneId, WorldClockService.GetTitle(ZoneId), "Time zone", new FavoriteSnapshot());
    }
    /// <summary>
    /// Saves a Met artwork identifier and display label without changing the snapshot contract.
    /// </summary>
    static public FavoriteModel Art(int Id, string Name)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(Id);
        return Create(FavoriteKind.Art, Id.ToString(CultureInfo.InvariantCulture), Name,
            "The Metropolitan Museum of Art", new FavoriteSnapshot());
    }
    /// <summary>
    /// Validates a stored snapshot and restores administrative display context excluded from imported models' JSON.
    /// </summary>
    static public FavoriteSnapshot ReadSnapshot(FavoriteModel Favorite)
    {
        ArgumentNullException.ThrowIfNull(Favorite);
        FavoriteSnapshot Snapshot = JsonSerializer.Deserialize<FavoriteSnapshot>(Favorite.DataJson);
        if (Snapshot == null || Snapshot.Version != 1)
            throw new JsonException("Unsupported favorite snapshot.");
        bool Valid = Favorite.Kind switch
        {
            FavoriteKind.Country => Snapshot.Country != null
                && string.Equals(Snapshot.Country.Alpha2Code?.Trim(), Favorite.TargetKey, StringComparison.OrdinalIgnoreCase),
            FavoriteKind.City => Snapshot.City != null
                && Snapshot.City.GeonameId.ToString(CultureInfo.InvariantCulture) == Favorite.TargetKey,
            FavoriteKind.Place => Snapshot.Place != null
                && Snapshot.Place.GeoNameId.ToString(CultureInfo.InvariantCulture) == Favorite.TargetKey,
            FavoriteKind.Weather => Snapshot.Weather != null && !string.IsNullOrWhiteSpace(Snapshot.Weather.Name),
            FavoriteKind.ExchangeRates or FavoriteKind.Cryptocurrencies => Snapshot.BaseCurrency != null
                && !string.IsNullOrWhiteSpace(Snapshot.BaseCurrency.Name)
                && Snapshot.BaseCurrency.Code == Favorite.TargetKey
                && IsAssetCode(Snapshot.BaseCurrency.Code, Favorite.Kind == FavoriteKind.Cryptocurrencies),
            FavoriteKind.WorldClock => !string.IsNullOrWhiteSpace(Favorite.TargetKey),
            FavoriteKind.Art => int.TryParse(Favorite.TargetKey, NumberStyles.None, CultureInfo.InvariantCulture, out int Id)
                && Id > 0 && Id.ToString(CultureInfo.InvariantCulture) == Favorite.TargetKey,
            _ => false
        };
        if (!Valid)
            throw new JsonException("The saved details do not match the favorite identity.");
        if (Snapshot.City != null)
            Snapshot.City.AdministrativeArea = Snapshot.AdministrativeArea;
        if (Snapshot.Place != null)
            Snapshot.Place.AdministrativeArea = Snapshot.AdministrativeArea;
        if (Snapshot.Weather != null)
            CheckLocation(Snapshot.Weather.Latitude, Snapshot.Weather.Longitude);
        return Snapshot;
    }
}
