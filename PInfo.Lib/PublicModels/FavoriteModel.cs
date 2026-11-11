/* PInfo, Copyright (c) 2026 Theo Bebekis, see License. */

namespace PInfo;

/// <summary>
/// A uniquely identified saved destination and its offline detail snapshot.
/// </summary>
public class FavoriteModel
{
    // ● properties
    /// <summary>
    /// The destination category.
    /// </summary>
    public FavoriteKind Kind { get; set; }
    /// <summary>
    /// The stable country, GeoNames, weather-location, asset-code or timezone identifier within this category.
    /// </summary>
    public string TargetKey { get; set; } = string.Empty;
    /// <summary>
    /// The saved display name.
    /// </summary>
    public string Name { get; set; } = string.Empty;
    /// <summary>
    /// Administrative or country context used to distinguish equal names.
    /// </summary>
    public string Context { get; set; } = string.Empty;
    /// <summary>
    /// The versioned JSON detail snapshot.
    /// </summary>
    public string DataJson { get; set; } = string.Empty;
    /// <summary>
    /// The UTC time this favorite was added.
    /// </summary>
    public DateTime AddedUtc { get; set; }
}
/// <summary>
/// Versioned snapshots keep saved details available independently of imported rows.
/// </summary>
public class FavoriteSnapshot
{
    // ● properties
    /// <summary>
    /// The snapshot format version.
    /// </summary>
    public int Version { get; set; } = 1;
    /// <summary>
    /// The country snapshot for a country destination.
    /// </summary>
    public CountryModel Country { get; set; }
    /// <summary>
    /// The city snapshot for a city destination.
    /// </summary>
    public CityModel City { get; set; }
    /// <summary>
    /// The geographic snapshot for a place destination.
    /// </summary>
    public PlaceModel Place { get; set; }
    /// <summary>
    /// The original place category used by its detail card.
    /// </summary>
    public string CategoryName { get; set; } = string.Empty;
    /// <summary>
    /// Resolved administrative names excluded from the existing city/place serialization contracts.
    /// </summary>
    public string AdministrativeArea { get; set; } = string.Empty;
    /// <summary>
    /// The saved weather location; no forecast measurements are stored here.
    /// </summary>
    public FavoriteWeatherLocation Weather { get; set; }
    /// <summary>
    /// The selected base asset identity and display name; reference prices are never saved here.
    /// </summary>
    public CurrencyDetail BaseCurrency { get; set; }
}
/// <summary>
/// A location whose forecast is obtained through the normal weather cache and refresh rules.
/// </summary>
public class FavoriteWeatherLocation
{
    // ● properties
    /// <summary>
    /// The saved location name.
    /// </summary>
    public string Name { get; set; } = string.Empty;
    /// <summary>
    /// The original latitude.
    /// </summary>
    public double Latitude { get; set; }
    /// <summary>
    /// The original longitude.
    /// </summary>
    public double Longitude { get; set; }
    /// <summary>
    /// The GeoNames timezone identifier, when available.
    /// </summary>
    public string Timezone { get; set; } = string.Empty;
    /// <summary>
    /// Resolved administrative context.
    /// </summary>
    public string AdministrativeArea { get; set; } = string.Empty;
}
