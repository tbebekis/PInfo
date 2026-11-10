/* PInfo, Copyright (c) 2026 Theo Bebekis, see License. */

namespace PInfo.Views;

/// <summary>
/// Formats weather measurements and MET Norway condition codes for the weather views.
/// </summary>
static class WeatherDisplay
{
    // ● public
    /// <summary>
    /// Formats a measurement without representing missing data as zero.
    /// </summary>
    /// <param name="Value">The optional measurement.</param>
    /// <param name="Format">The numeric format.</param>
    /// <param name="Unit">The optional unit suffix.</param>
    /// <returns>The formatted measurement or an unavailable marker.</returns>
    static public string Number(double? Value, string Format = "0.#", string Unit = "")
    {
        return Value.HasValue ? Value.Value.ToString(Format, CultureInfo.CurrentCulture) + Unit : "—";
    }
    /// <summary>
    /// Formats a Celsius measurement in the selected display unit, preserving unavailable values.
    /// </summary>
    /// <param name="Celsius">The unchanged source temperature.</param>
    /// <param name="Format">The display number format.</param>
    /// <param name="IncludeUnit">Includes the full unit symbol, otherwise only the degree sign.</param>
    /// <returns>The converted display text or an unavailable marker.</returns>
    static public string Temperature(double? Celsius, string Format = "0.#", bool IncludeUnit = true)
    {
        double? Value = Lib.Settings.TemperatureUnit == PInfo.TemperatureUnit.Fahrenheit
            ? Celsius * 9.0 / 5.0 + 32 : Celsius;
        return Number(Value, Format, IncludeUnit ? " " + TemperatureSymbol : "°");
    }
    /// <summary>
    /// Describes an original MET Norway symbol, including rain, sleet, snow, and thunder variants.
    /// </summary>
    /// <param name="Symbol">The original provider symbol code.</param>
    /// <returns>The English condition description.</returns>
    static public string Condition(string Symbol)
    {
        string Code = (Symbol ?? string.Empty).Split('_')[0];
        if (Code.Length == 0) return "Unavailable";
        string Basic = Code switch
        {
            "clearsky" => "Clear sky", "fair" => "Mainly clear", "partlycloudy" => "Partly cloudy",
            "cloudy" => "Overcast", "fog" => "Fog", _ => string.Empty
        };
        if (Basic.Length > 0) return Basic;
        string Amount = Code.StartsWith("heavy", StringComparison.Ordinal) ? "Heavy "
            : Code.StartsWith("light", StringComparison.Ordinal) ? "Light " : string.Empty;
        string Kind = Code.Contains("sleet", StringComparison.Ordinal) ? "sleet"
            : Code.Contains("snow", StringComparison.Ordinal) ? "snow"
            : Code.Contains("rain", StringComparison.Ordinal) ? "rain" : string.Empty;
        if (Kind.Length == 0) return "Unknown conditions";
        string Description = Amount + Kind + (Code.Contains("showers", StringComparison.Ordinal) ? " showers" : string.Empty)
            + (Code.Contains("thunder", StringComparison.Ordinal) ? " with thunder" : string.Empty);
        return char.ToUpperInvariant(Description[0]) + Description.Substring(1);
    }
    /// <summary>
    /// Selects a compact weather icon from the source symbol without needing another API for sunrise.
    /// </summary>
    /// <param name="Symbol">The original MET Norway code and optional day/night suffix.</param>
    /// <returns>The displayed condition symbol.</returns>
    static public string Icon(string Symbol)
    {
        string Code = Symbol ?? string.Empty;
        if (Code.Contains("thunder", StringComparison.Ordinal)) return "⛈";
        if (Code.Contains("snow", StringComparison.Ordinal) || Code.Contains("sleet", StringComparison.Ordinal)) return "❄";
        if (Code.Contains("rain", StringComparison.Ordinal)) return "🌧";
        return Code.Split('_')[0] switch
        {
            "clearsky" or "fair" => Code.EndsWith("_night", StringComparison.Ordinal) ? "☾" : "☀",
            "partlycloudy" => Code.EndsWith("_night", StringComparison.Ordinal) ? "☁" : "⛅",
            "cloudy" => "☁", "fog" => "≋", _ => "—"
        };
    }
    /// <summary>
    /// Formats the meteorological direction the wind comes from as a sixteen-point compass label.
    /// </summary>
    /// <param name="Degrees">The wind-from bearing in degrees.</param>
    /// <returns>The compass label or an unavailable marker.</returns>
    static public string WindDirection(double? Degrees)
    {
        if (!Degrees.HasValue)
            return "—";
        string[] Points = { "N", "NNE", "NE", "ENE", "E", "ESE", "SE", "SSE", "S", "SSW", "SW", "WSW", "W", "WNW", "NW", "NNW" };
        return Points[(int)Math.Round(Degrees.Value / 22.5, MidpointRounding.AwayFromZero) % 16];
    }

    // ● properties
    /// <summary>
    /// The symbol for the common weather temperature display unit.
    /// </summary>
    static public string TemperatureSymbol => Lib.Settings.TemperatureUnit == PInfo.TemperatureUnit.Fahrenheit ? "°F" : "°C";
}
