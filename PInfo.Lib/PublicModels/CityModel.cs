/* PInfo, Copyright (c) 2026 Theo Bebekis, see License. */

namespace PInfo;

/// <summary>
/// Represents a populated place returned by the countries.dev cities endpoint.
/// </summary>
public class CityModel
{
    // ● properties
    /// <summary>
    /// The GeoNames identifier used as the unique city identifier.
    /// </summary>
    [JsonPropertyName("geonameId")]
    public long GeonameId { get; set; }
    /// <summary>
    /// The city name.
    /// </summary>
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;
    /// <summary>
    /// The ASCII representation of the city name.
    /// </summary>
    [JsonPropertyName("asciiName")]
    public string AsciiName { get; set; } = string.Empty;
    /// <summary>
    /// The ISO 3166-1 alpha-2 code of the country containing the city.
    /// </summary>
    [JsonPropertyName("countryCode")]
    public string CountryCode { get; set; } = string.Empty;
    /// <summary>
    /// The code of the first-level administrative division within the country.
    /// </summary>
    [JsonPropertyName("admin1Code")]
    public string Admin1Code { get; set; } = string.Empty;
    /// <summary>
    /// The latitude in decimal degrees.
    /// </summary>
    [JsonPropertyName("latitude")]
    public double Latitude { get; set; }
    /// <summary>
    /// The longitude in decimal degrees.
    /// </summary>
    [JsonPropertyName("longitude")]
    public double Longitude { get; set; }
    /// <summary>
    /// The population reported by the data source.
    /// </summary>
    [JsonPropertyName("population")]
    public long Population { get; set; }
    /// <summary>
    /// The time zone identifier, such as America/New_York.
    /// </summary>
    [JsonPropertyName("timezone")]
    public string Timezone { get; set; } = string.Empty;
    /// <summary>
    /// The GeoNames feature code describing the populated place classification.
    /// </summary>
    [JsonPropertyName("featureCode")]
    public string FeatureCode { get; set; } = string.Empty;
}
