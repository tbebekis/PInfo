/* PInfo, Copyright (c) 2026 Theo Bebekis, see License. */

namespace PInfo;

/// <summary>
/// Contains the display summary for one two-hour interval or one calculated local daily forecast.
/// Missing or incomplete measurements remain null rather than being presented as zero.
/// </summary>
public sealed class WeatherPeriod
{
    // ● properties
    /// <summary>
    /// The interval start with the location's actual timezone offset.
    /// </summary>
    public DateTimeOffset StartLocal { get; init; }
    /// <summary>
    /// The exclusive interval end with its applicable offset.
    /// </summary>
    public DateTimeOffset EndLocal { get; init; }
    /// <summary>
    /// Whether this summary represents a calendar day instead of four elapsed hours.
    /// </summary>
    public bool IsDaily { get; init; }
    /// <summary>
    /// The representative original MET Norway symbol from the available forward intervals.
    /// </summary>
    public string SymbolCode { get; init; } = string.Empty;
    /// <summary>
    /// Whether the source coverage is incomplete for this interval. No partial precipitation total is shown as a full total.
    /// </summary>
    public bool IsPartial { get; init; }
    /// <summary>
    /// The minimum available sample or fully contained interval temperature in degrees Celsius.
    /// </summary>
    public double? TemperatureMin { get; init; }
    /// <summary>
    /// The maximum available sample or fully contained interval temperature in degrees Celsius.
    /// </summary>
    public double? TemperatureMax { get; init; }
    /// <summary>
    /// The minimum apparent temperature in degrees Celsius.
    /// </summary>
    public double? FeelsLikeMin { get; init; }
    /// <summary>
    /// The maximum apparent temperature in degrees Celsius.
    /// </summary>
    public double? FeelsLikeMax { get; init; }
    /// <summary>
    /// The mean percentage across available relative humidity samples.
    /// </summary>
    public double? HumidityMean { get; init; }
    /// <summary>
    /// The maximum available interval probability, not the probability of rain over the entire period.
    /// </summary>
    public double? PrecipitationProbabilityMax { get; init; }
    /// <summary>
    /// The total precipitation water equivalent in millimeters.
    /// </summary>
    public double? Precipitation { get; init; }
    /// <summary>
    /// The highest available sampled wind speed in kilometers per hour.
    /// </summary>
    public double? WindSpeedMax { get; init; }
    /// <summary>
    /// The wind-from direction at the strongest available wind sample.
    /// </summary>
    public double? WindDirection { get; init; }
}
