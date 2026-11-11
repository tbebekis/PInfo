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

    // ● public
    /// <summary>
    /// Returns the category label displayed in Favorites.
    /// </summary>
    static public string CategoryName(FavoriteKind Kind)
    {
        return Kind switch
        {
            FavoriteKind.Country => "Countries", FavoriteKind.City => "Cities", FavoriteKind.Place => "Places",
            FavoriteKind.Weather => "Weather", _ => throw new ArgumentException("Unknown favorite category.", nameof(Kind))
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
