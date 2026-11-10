/* PInfo, Copyright (c) 2026 Theo Bebekis, see License. */

namespace PInfo;

/// <summary>
/// Validates MET Norway Locationforecast JSON without inventing unavailable measurements or time samples.
/// </summary>
static public partial class WeatherService
{
    // ● private
    /// <summary>
    /// Reads a required JSON property with a controlled validation failure.
    /// </summary>
    /// <param name="Item">The containing object.</param>
    /// <param name="Name">The property name.</param>
    /// <returns>The required value.</returns>
    static JsonElement ReadProperty(JsonElement Item, string Name)
    {
        if (Item.ValueKind != JsonValueKind.Object || !Item.TryGetProperty(Name, out JsonElement Value))
            throw new JsonException("Missing weather field: " + Name);
        return Value;
    }
    /// <summary>
    /// Reads a required UTC source timestamp.
    /// </summary>
    /// <param name="Value">The timestamp string.</param>
    /// <returns>The UTC instant.</returns>
    static DateTime ReadTime(JsonElement Value)
    {
        if (Value.ValueKind != JsonValueKind.String || !DateTime.TryParseExact(Value.GetString(),
            "yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out DateTime Result))
            throw new JsonException("Invalid MET Norway timestamp.");
        return Result;
    }
    /// <summary>
    /// Reads an optional measurement while validating its declared unit and numeric range.
    /// </summary>
    /// <param name="Details">The source details object.</param>
    /// <param name="Units">The response unit declarations.</param>
    /// <param name="Name">The measurement key.</param>
    /// <param name="Unit">The expected source unit.</param>
    /// <param name="Minimum">The accepted minimum.</param>
    /// <param name="Maximum">The accepted maximum.</param>
    /// <returns>The value, or null if absent or explicitly unavailable.</returns>
    static double? ReadMeasurement(JsonElement Details, JsonElement Units, string Name, string Unit,
        double Minimum = double.NegativeInfinity, double Maximum = double.PositiveInfinity)
    {
        if (Details.ValueKind != JsonValueKind.Object || !Details.TryGetProperty(Name, out JsonElement Value)
            || Value.ValueKind == JsonValueKind.Null)
            return null;
        JsonElement DeclaredUnit = ReadProperty(Units, Name);
        if (DeclaredUnit.ValueKind != JsonValueKind.String || DeclaredUnit.GetString() != Unit)
            throw new JsonException("Unexpected MET Norway units: " + Name);
        if (Value.ValueKind != JsonValueKind.Number || !Value.TryGetDouble(out double Number)
            || !double.IsFinite(Number) || Number < Minimum || Number > Maximum)
            throw new JsonException("Invalid MET Norway measurement: " + Name);
        return Number;
    }
    /// <summary>
    /// Parses the actual one-hour/six-hour source samples and their forward precipitation intervals.
    /// </summary>
    /// <param name="Json">The raw response.</param>
    /// <param name="Latitude">The requested truncated latitude.</param>
    /// <param name="Longitude">The requested truncated longitude.</param>
    /// <param name="Timezone">The validated location timezone, or UTC.</param>
    /// <param name="DownloadedUtc">The original body download timestamp.</param>
    /// <param name="CancellationToken">Cancels parsing between samples.</param>
    /// <returns>The validated forecast snapshot.</returns>
    static WeatherForecast ParseForecast(string Json, double Latitude, double Longitude, string Timezone,
        DateTime DownloadedUtc, CancellationToken CancellationToken)
    {
        using JsonDocument Document = JsonDocument.Parse(Json);
        JsonElement Root = Document.RootElement;
        JsonElement Properties = ReadProperty(Root, "properties");
        JsonElement Meta = ReadProperty(Properties, "meta");
        JsonElement Units = ReadProperty(Meta, "units");
        if (Units.ValueKind != JsonValueKind.Object)
            throw new JsonException("Missing MET Norway units.");
        JsonElement Timeseries = ReadProperty(Properties, "timeseries");
        if (Timeseries.ValueKind != JsonValueKind.Array || Timeseries.GetArrayLength() < 2 || Timeseries.GetArrayLength() > 512)
            throw new JsonException("Invalid MET Norway forecast coverage.");
        List<WeatherReading> Readings = new();
        foreach (JsonElement Item in Timeseries.EnumerateArray())
        {
            CancellationToken.ThrowIfCancellationRequested();
            DateTime Time = ReadTime(ReadProperty(Item, "time"));
            if (Readings.Count > 0)
            {
                WeatherReading Previous = Readings[Readings.Count - 1];
                if (Time <= Previous.TimeUtc || Previous.TimeUtc.AddSeconds(Previous.IntervalSeconds) > Time)
                    throw new JsonException("Forecast timestamps are not increasing or source intervals overlap.");
            }
            JsonElement Data = ReadProperty(Item, "data");
            JsonElement Instant = ReadProperty(ReadProperty(Data, "instant"), "details");
            JsonElement Interval = default;
            int Seconds = 0;
            if (Data.TryGetProperty("next_1_hours", out Interval))
                Seconds = 3600;
            else if (Data.TryGetProperty("next_6_hours", out Interval))
                Seconds = 21600;
            JsonElement Details = default;
            string Symbol = string.Empty;
            if (Seconds > 0)
            {
                Details = ReadProperty(Interval, "details");
                JsonElement Code = ReadProperty(ReadProperty(Interval, "summary"), "symbol_code");
                if (Code.ValueKind != JsonValueKind.String)
                    throw new JsonException("Invalid MET Norway symbol.");
                Symbol = Code.GetString();
            }
            double? Minimum = ReadMeasurement(Details, Units, "air_temperature_min", "celsius");
            double? Maximum = ReadMeasurement(Details, Units, "air_temperature_max", "celsius");
            if (Minimum > Maximum)
                throw new JsonException("Invalid MET Norway temperature range.");
            Readings.Add(new WeatherReading
            {
                TimeUtc = Time, IntervalSeconds = Seconds, SymbolCode = Symbol,
                Temperature = ReadMeasurement(Instant, Units, "air_temperature", "celsius"),
                TemperatureMin = Minimum, TemperatureMax = Maximum,
                FeelsLike = ReadMeasurement(Instant, Units, "apparent_air_temperature", "celsius"),
                Humidity = ReadMeasurement(Instant, Units, "relative_humidity", "%", 0, 100),
                Precipitation = ReadMeasurement(Details, Units, "precipitation_amount", "mm", 0),
                PrecipitationProbability = ReadMeasurement(Details, Units, "probability_of_precipitation", "%", 0, 100),
                WindSpeed = ReadMeasurement(Instant, Units, "wind_speed", "m/s", 0) * 3.6,
                WindDirection = ReadMeasurement(Instant, Units, "wind_from_direction", "degrees", 0, 360)
            });
        }
        if (!Readings.Any(Item => Item.Temperature.HasValue))
            throw new JsonException("The forecast contains no temperature data.");
        JsonElement Coordinates = ReadProperty(ReadProperty(Root, "geometry"), "coordinates");
        double? Elevation = null;
        if (Coordinates.ValueKind != JsonValueKind.Array || Coordinates.GetArrayLength() < 2
            || Coordinates[0].ValueKind != JsonValueKind.Number || Coordinates[1].ValueKind != JsonValueKind.Number
            || !Coordinates[0].TryGetDouble(out double ResponseLongitude) || !double.IsFinite(ResponseLongitude)
            || !Coordinates[1].TryGetDouble(out double ResponseLatitude) || !double.IsFinite(ResponseLatitude)
            || Math.Abs(ResponseLongitude - Longitude) > 0.001 || Math.Abs(ResponseLatitude - Latitude) > 0.001)
            throw new JsonException("The weather response is for a different location.");
        if (Coordinates.GetArrayLength() > 2 && Coordinates[2].ValueKind != JsonValueKind.Null)
        {
            if (Coordinates[2].ValueKind != JsonValueKind.Number || !Coordinates[2].TryGetDouble(out double Height) || !double.IsFinite(Height))
                throw new JsonException("Invalid forecast elevation.");
            Elevation = Height;
        }
        return new WeatherForecast
        {
            Latitude = Latitude, Longitude = Longitude, Timezone = Timezone, Elevation = Elevation,
            UpdatedUtc = DownloadedUtc, ModelUpdatedUtc = ReadTime(ReadProperty(Meta, "updated_at")),
            Readings = Readings.AsReadOnly()
        };
    }
}
