/* PInfo, Copyright (c) 2026 Theo Bebekis, see License. */

namespace PInfo;

/// <summary>
/// Contains a validated MET Norway forecast with the original location timezone and UTC timestamps.
/// </summary>
public sealed class WeatherForecast
{
    // ● properties
    /// <summary>
    /// The latitude sent to MET Norway, truncated to four decimal places.
    /// </summary>
    public double Latitude { get; init; }
    /// <summary>
    /// The longitude sent to MET Norway, truncated to four decimal places.
    /// </summary>
    public double Longitude { get; init; }
    /// <summary>
    /// The ground elevation returned by MET Norway, in meters.
    /// </summary>
    public double? Elevation { get; init; }
    /// <summary>
    /// The location timezone from GeoNames, or UTC when no usable timezone is available.
    /// </summary>
    public string Timezone { get; init; } = string.Empty;
    /// <summary>
    /// The UTC time the response body was downloaded; revalidation does not change this timestamp.
    /// </summary>
    public DateTime UpdatedUtc { get; init; }
    /// <summary>
    /// The source model update time, distinct from the download time.
    /// </summary>
    public DateTime ModelUpdatedUtc { get; init; }
    /// <summary>
    /// The forecast samples at their actual source resolution, normally one or six hours apart.
    /// </summary>
    public IReadOnlyList<WeatherReading> Readings { get; init; } = Array.Empty<WeatherReading>();
}

/// <summary>
/// Contains one MET Norway instant and its shortest available forward forecast interval.
/// Longer-range samples are not expanded into invented hourly measurements.
/// </summary>
public sealed class WeatherReading
{
    // ● properties
    /// <summary>
    /// The UTC instant of the sample and start of its forward precipitation interval.
    /// </summary>
    public DateTime TimeUtc { get; init; }
    /// <summary>
    /// The forward interval duration in seconds, or zero when the final sample has no interval data.
    /// </summary>
    public int IntervalSeconds { get; init; }
    /// <summary>
    /// The instantaneous air temperature in degrees Celsius.
    /// </summary>
    public double? Temperature { get; init; }
    /// <summary>
    /// The provider minimum temperature for the forward interval, when supplied.
    /// </summary>
    public double? TemperatureMin { get; init; }
    /// <summary>
    /// The provider maximum temperature for the forward interval, when supplied.
    /// </summary>
    public double? TemperatureMax { get; init; }
    /// <summary>
    /// The instantaneous apparent temperature in degrees Celsius, when supplied.
    /// </summary>
    public double? FeelsLike { get; init; }
    /// <summary>
    /// The instantaneous relative humidity percentage.
    /// </summary>
    public double? Humidity { get; init; }
    /// <summary>
    /// The original MET Norway symbol code, including day/night suffix when present.
    /// </summary>
    public string SymbolCode { get; init; } = string.Empty;
    /// <summary>
    /// The forward interval's precipitation probability percentage, when supplied.
    /// </summary>
    public double? PrecipitationProbability { get; init; }
    /// <summary>
    /// The total precipitation water equivalent in millimeters over the forward interval.
    /// </summary>
    public double? Precipitation { get; init; }
    /// <summary>
    /// The instantaneous wind speed in kilometers per hour, converted from provider meters per second.
    /// </summary>
    public double? WindSpeed { get; init; }
    /// <summary>
    /// The meteorological direction the wind comes from, in degrees.
    /// </summary>
    public double? WindDirection { get; init; }
}
