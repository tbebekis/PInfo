/* PInfo, Copyright (c) 2026 Theo Bebekis, see License. */

namespace PInfo;

/// <summary>
/// Represents a location from a GeoNames country dump, matching the Place table.
/// </summary>
public class PlaceModel
{
    // ● properties
    /// <summary>
    /// The resolved administrative area and region for display; not stored or serialized.
    /// </summary>
    [JsonIgnore]
    public string AdministrativeArea { get; set; } = string.Empty;
    /// <summary>
    /// The unique GeoNames record identifier.
    /// </summary>
    public long GeoNameId { get; set; }
    /// <summary>
    /// The ISO alpha-2 country code linked to Country.Alpha2Code.
    /// </summary>
    public string CountryCode { get; set; } = string.Empty;
    /// <summary>
    /// The primary name supplied by GeoNames.
    /// </summary>
    public string Name { get; set; } = string.Empty;
    /// <summary>
    /// The ASCII transliteration of the primary name.
    /// </summary>
    public string AsciiName { get; set; } = string.Empty;
    /// <summary>
    /// The comma-separated alternative names; language identifiers are not supplied.
    /// </summary>
    public string AlternateNames { get; set; } = string.Empty;
    /// <summary>
    /// The latitude in decimal degrees.
    /// </summary>
    public double Latitude { get; set; }
    /// <summary>
    /// The longitude in decimal degrees.
    /// </summary>
    public double Longitude { get; set; }
    /// <summary>
    /// The GeoNames feature classification letter.
    /// </summary>
    public string FeatureClass { get; set; } = string.Empty;
    /// <summary>
    /// The GeoNames feature code within its class.
    /// </summary>
    public string FeatureCode { get; set; } = string.Empty;
    /// <summary>
    /// The comma-separated additional country codes from the source cc2 field.
    /// </summary>
    public string CountryCodes { get; set; } = string.Empty;
    /// <summary>
    /// The first-level administrative code.
    /// </summary>
    public string Admin1Code { get; set; } = string.Empty;
    /// <summary>
    /// The second-level administrative code.
    /// </summary>
    public string Admin2Code { get; set; } = string.Empty;
    /// <summary>
    /// The third-level administrative code.
    /// </summary>
    public string Admin3Code { get; set; } = string.Empty;
    /// <summary>
    /// The fourth-level administrative code.
    /// </summary>
    public string Admin4Code { get; set; } = string.Empty;
    /// <summary>
    /// The reported population; zero may mean no population was supplied.
    /// </summary>
    public long Population { get; set; }
    /// <summary>
    /// The elevation in meters, or null when absent.
    /// </summary>
    public int? Elevation { get; set; }
    /// <summary>
    /// The digital elevation model value in meters, or null when absent.
    /// </summary>
    public int? Dem { get; set; }
    /// <summary>
    /// The timezone identifier associated with this place.
    /// </summary>
    public string Timezone { get; set; } = string.Empty;
    /// <summary>
    /// The source modification date, without a time or timezone.
    /// </summary>
    public DateTime ModificationDate { get; set; }
}
