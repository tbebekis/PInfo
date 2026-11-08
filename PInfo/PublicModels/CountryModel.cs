/* PInfo, Copyright (c) 2026 Theo Bebekis, see License. */

namespace PInfo;

/// <summary>
/// Represents a complete country record returned by countries.dev.
/// </summary>
public class CountryModel
{
    // ● properties
    /// <summary>
    /// The country name.
    /// </summary>
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;
    /// <summary>
    /// The ISO 3166-1 alpha-2 country code.
    /// </summary>
    [JsonPropertyName("alpha2Code")]
    public string Alpha2Code { get; set; } = string.Empty;
    /// <summary>
    /// The ISO 3166-1 alpha-3 country code used as the country identifier.
    /// </summary>
    [JsonPropertyName("alpha3Code")]
    public string Alpha3Code { get; set; } = string.Empty;
    /// <summary>
    /// The numeric country code, preserving leading zeroes.
    /// </summary>
    [JsonPropertyName("numericCode")]
    public string NumericCode { get; set; } = string.Empty;
    /// <summary>
    /// The International Olympic Committee code, when available.
    /// </summary>
    [JsonPropertyName("cioc")]
    public string Cioc { get; set; } = string.Empty;
    /// <summary>
    /// The native country name.
    /// </summary>
    [JsonPropertyName("nativeName")]
    public string NativeName { get; set; } = string.Empty;
    /// <summary>
    /// The capital name, when available.
    /// </summary>
    [JsonPropertyName("capital")]
    public string Capital { get; set; } = string.Empty;
    /// <summary>
    /// The geographical region.
    /// </summary>
    [JsonPropertyName("region")]
    public string Region { get; set; } = string.Empty;
    /// <summary>
    /// The geographical subregion.
    /// </summary>
    [JsonPropertyName("subregion")]
    public string Subregion { get; set; } = string.Empty;
    /// <summary>
    /// The name used for an inhabitant of the country.
    /// </summary>
    [JsonPropertyName("demonym")]
    public string Demonym { get; set; } = string.Empty;
    /// <summary>
    /// Whether the country is independent.
    /// </summary>
    [JsonPropertyName("independent")]
    public bool Independent { get; set; }
    /// <summary>
    /// The population reported by the source.
    /// </summary>
    [JsonPropertyName("population")]
    public long Population { get; set; }
    /// <summary>
    /// The area in square kilometers, or null when unavailable.
    /// </summary>
    [JsonPropertyName("area")]
    public double? Area { get; set; }
    /// <summary>
    /// The population per square kilometer, or null when unavailable.
    /// </summary>
    [JsonPropertyName("populationDensity")]
    public double? PopulationDensity { get; set; }
    /// <summary>
    /// The Gini index, or null when unavailable.
    /// </summary>
    [JsonPropertyName("gini")]
    public double? Gini { get; set; }
    /// <summary>
    /// Latitude and longitude, in that order, when available.
    /// </summary>
    [JsonPropertyName("latlng")]
    public List<double> LatLng { get; set; } = new List<double>();
    /// <summary>
    /// The alpha-3 codes of bordering countries.
    /// </summary>
    [JsonPropertyName("borders")]
    public List<string> Borders { get; set; } = new List<string>();
    /// <summary>
    /// The time zones associated with the country.
    /// </summary>
    [JsonPropertyName("timezones")]
    public List<string> Timezones { get; set; } = new List<string>();
    /// <summary>
    /// The country top-level internet domains.
    /// </summary>
    [JsonPropertyName("topLevelDomain")]
    public List<string> TopLevelDomain { get; set; } = new List<string>();
    /// <summary>
    /// The international calling codes.
    /// </summary>
    [JsonPropertyName("callingCodes")]
    public List<string> CallingCodes { get; set; } = new List<string>();
    /// <summary>
    /// Alternative spellings of the country name.
    /// </summary>
    [JsonPropertyName("altSpellings")]
    public List<string> AltSpellings { get; set; } = new List<string>();
    /// <summary>
    /// Country name translations indexed by language code.
    /// </summary>
    [JsonPropertyName("translations")]
    public Dictionary<string, string> Translations { get; set; } = new Dictionary<string, string>();
    /// <summary>
    /// The currencies used in the country.
    /// </summary>
    [JsonPropertyName("currencies")]
    public List<CurrencyDetail> Currencies { get; set; } = new List<CurrencyDetail>();
    /// <summary>
    /// The languages spoken in the country.
    /// </summary>
    [JsonPropertyName("languages")]
    public List<LanguageInfo> Languages { get; set; } = new List<LanguageInfo>();
    /// <summary>
    /// The regional organizations associated with the country.
    /// </summary>
    [JsonPropertyName("regionalBlocs")]
    public List<RegionalBlocInfo> RegionalBlocs { get; set; } = new List<RegionalBlocInfo>();
    /// <summary>
    /// The Unicode flag emoji, stored directly in the JSON.
    /// </summary>
    [JsonPropertyName("flag")]
    public string Flag { get; set; } = string.Empty;
    /// <summary>
    /// The PNG and SVG flag image URLs; image data is not embedded.
    /// </summary>
    [JsonPropertyName("flags")]
    public FlagInfo Flags { get; set; } = new FlagInfo();
    /// <summary>
    /// Links to online maps, when available.
    /// </summary>
    [JsonPropertyName("maps")]
    public MapInfo Maps { get; set; } = new MapInfo();
}

/// <summary>
/// Describes a currency used by a country.
/// </summary>
public class CurrencyDetail
{
    // ● properties
    /// <summary>
    /// The currency code.
    /// </summary>
    [JsonPropertyName("code")]
    public string Code { get; set; } = string.Empty;
    /// <summary>
    /// The currency name.
    /// </summary>
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;
    /// <summary>
    /// The currency symbol.
    /// </summary>
    [JsonPropertyName("symbol")]
    public string Symbol { get; set; } = string.Empty;
}

/// <summary>
/// Describes a language spoken in a country.
/// </summary>
public class LanguageInfo
{
    // ● properties
    /// <summary>
    /// The ISO 639-1 code, when available.
    /// </summary>
    [JsonPropertyName("iso639_1")]
    public string Iso639_1 { get; set; } = string.Empty;
    /// <summary>
    /// The ISO 639-2 code.
    /// </summary>
    [JsonPropertyName("iso639_2")]
    public string Iso639_2 { get; set; } = string.Empty;
    /// <summary>
    /// The language name.
    /// </summary>
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;
    /// <summary>
    /// The native language name, when available.
    /// </summary>
    [JsonPropertyName("nativeName")]
    public string NativeName { get; set; } = string.Empty;
}

/// <summary>
/// Describes a regional organization associated with a country.
/// </summary>
public class RegionalBlocInfo
{
    // ● properties
    /// <summary>
    /// The organization acronym.
    /// </summary>
    [JsonPropertyName("acronym")]
    public string Acronym { get; set; } = string.Empty;
    /// <summary>
    /// The organization name.
    /// </summary>
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;
    /// <summary>
    /// Alternative organization acronyms.
    /// </summary>
    [JsonPropertyName("otherAcronyms")]
    public List<string> OtherAcronyms { get; set; } = new List<string>();
    /// <summary>
    /// Alternative organization names.
    /// </summary>
    [JsonPropertyName("otherNames")]
    public List<string> OtherNames { get; set; } = new List<string>();
}

/// <summary>
/// Contains URLs for country flag images.
/// </summary>
public class FlagInfo
{
    // ● properties
    /// <summary>
    /// The PNG flag image URL.
    /// </summary>
    [JsonPropertyName("png")]
    public string Png { get; set; } = string.Empty;
    /// <summary>
    /// The SVG flag image URL.
    /// </summary>
    [JsonPropertyName("svg")]
    public string Svg { get; set; } = string.Empty;
}

/// <summary>
/// Contains links to online maps.
/// </summary>
public class MapInfo
{
    // ● properties
    /// <summary>
    /// The Google Maps URL.
    /// </summary>
    [JsonPropertyName("googleMaps")]
    public string GoogleMaps { get; set; } = string.Empty;
    /// <summary>
    /// The OpenStreetMap URL.
    /// </summary>
    [JsonPropertyName("openStreetMaps")]
    public string OpenStreetMaps { get; set; } = string.Empty;
}
