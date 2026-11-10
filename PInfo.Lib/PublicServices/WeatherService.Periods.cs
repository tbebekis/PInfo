/* PInfo, Copyright (c) 2026 Theo Bebekis, see License. */

namespace PInfo;

/// <summary>
/// Summarizes forecasts in location time without changing provider or cache timestamps.
/// </summary>
static public partial class WeatherService
{
    // ● private
    /// <summary>
    /// Chooses the most significant available source symbol for a summary.
    /// </summary>
    /// <param name="Symbol">The original MET Norway symbol.</param>
    /// <returns>The display severity rank.</returns>
    static int SymbolSeverity(string Symbol)
    {
        if (Symbol.Contains("thunder", StringComparison.Ordinal)) return 100;
        if (Symbol.Contains("heavysnow", StringComparison.Ordinal)) return 90;
        if (Symbol.Contains("heavysleet", StringComparison.Ordinal)) return 85;
        if (Symbol.Contains("heavyrain", StringComparison.Ordinal)) return 80;
        if (Symbol.Contains("snow", StringComparison.Ordinal)) return 70;
        if (Symbol.Contains("sleet", StringComparison.Ordinal)) return 65;
        if (Symbol.Contains("rain", StringComparison.Ordinal)) return 60;
        return Symbol.Split('_')[0] switch { "fog" => 40, "cloudy" => 30, "partlycloudy" => 20, "fair" => 10, "clearsky" => 0, _ => -1 };
    }
    /// <summary>
    /// Resolves a local calendar boundary without assuming the device timezone or a fixed UTC offset.
    /// </summary>
    /// <param name="Local">The unspecified local boundary.</param>
    /// <param name="Zone">The place timezone.</param>
    /// <returns>The first valid UTC instant at that boundary.</returns>
    static DateTime CalendarBoundary(DateTime Local, TimeZoneInfo Zone)
    {
        Local = DateTime.SpecifyKind(Local, DateTimeKind.Unspecified);
        while (Zone.IsInvalidTime(Local))
            Local = Local.AddMinutes(1);
        if (Zone.IsAmbiguousTime(Local))
            return Zone.GetAmbiguousTimeOffsets(Local).Select(Offset => new DateTimeOffset(Local, Offset).UtcDateTime).Min();
        return TimeZoneInfo.ConvertTimeToUtc(Local, Zone);
    }
    /// <summary>
    /// Aggregates complete forward intervals without overlap, interpolation, or splitting six-hour precipitation totals.
    /// </summary>
    /// <param name="Forecast">The source samples.</param>
    /// <param name="From">The UTC start.</param>
    /// <param name="To">The exclusive UTC end.</param>
    /// <param name="Daily">Whether this is a calendar day.</param>
    /// <param name="Zone">The place timezone.</param>
    /// <returns>The available summary and its partial-coverage marker.</returns>
    static WeatherPeriod Summarize(WeatherForecast Forecast, DateTime From, DateTime To, bool Daily, TimeZoneInfo Zone)
    {
        WeatherReading[] Samples = Forecast.Readings.Where(Item => Item.TimeUtc >= From && Item.TimeUtc < To).ToArray();
        WeatherReading[] Intervals = Forecast.Readings.Where(Item => Item.IntervalSeconds > 0
            && Item.TimeUtc >= From && Item.TimeUtc.AddSeconds(Item.IntervalSeconds) <= To).ToArray();
        DateTime Cursor = From;
        bool Covered = true;
        foreach (WeatherReading Interval in Intervals)
        {
            if (Interval.TimeUtc != Cursor)
                Covered = false;
            Cursor = Interval.TimeUtc.AddSeconds(Interval.IntervalSeconds);
        }
        Covered = Covered && Cursor == To;
        List<double> Temperatures = Samples.Where(Item => Item.Temperature.HasValue).Select(Item => Item.Temperature.Value).ToList();
        List<double> Minima = Intervals.Where(Item => Item.TemperatureMin.HasValue).Select(Item => Item.TemperatureMin.Value).ToList();
        List<double> Maxima = Intervals.Where(Item => Item.TemperatureMax.HasValue).Select(Item => Item.TemperatureMax.Value).ToList();
        Minima.AddRange(Temperatures);
        Maxima.AddRange(Temperatures);
        double[] FeelsLike = Samples.Where(Item => Item.FeelsLike.HasValue).Select(Item => Item.FeelsLike.Value).ToArray();
        double[] Humidity = Samples.Where(Item => Item.Humidity.HasValue).Select(Item => Item.Humidity.Value).ToArray();
        WeatherReading Strongest = Samples.Where(Item => Item.WindSpeed.HasValue).OrderByDescending(Item => Item.WindSpeed.Value).FirstOrDefault();
        string Symbol = Forecast.Readings.Where(Item => Item.IntervalSeconds > 0 && Item.TimeUtc < To
            && Item.TimeUtc.AddSeconds(Item.IntervalSeconds) > From && !string.IsNullOrEmpty(Item.SymbolCode))
            .OrderByDescending(Item => SymbolSeverity(Item.SymbolCode)).Select(Item => Item.SymbolCode).FirstOrDefault() ?? string.Empty;
        return new WeatherPeriod
        {
            StartLocal = TimeZoneInfo.ConvertTime(new DateTimeOffset(From), Zone),
            EndLocal = TimeZoneInfo.ConvertTime(new DateTimeOffset(To), Zone), IsDaily = Daily, SymbolCode = Symbol,
            IsPartial = !Covered || Samples.Length == 0 || Samples.Any(Item => !Item.Temperature.HasValue)
                || From < Forecast.Readings[0].TimeUtc || To > Forecast.Readings[Forecast.Readings.Count - 1].TimeUtc,
            TemperatureMin = Minima.Count == 0 ? null : Minima.Min(), TemperatureMax = Maxima.Count == 0 ? null : Maxima.Max(),
            FeelsLikeMin = FeelsLike.Length == 0 ? null : FeelsLike.Min(), FeelsLikeMax = FeelsLike.Length == 0 ? null : FeelsLike.Max(),
            HumidityMean = Humidity.Length == 0 ? null : Humidity.Average(),
            Precipitation = Covered && Intervals.All(Item => Item.Precipitation.HasValue) ? Intervals.Sum(Item => Item.Precipitation.Value) : null,
            PrecipitationProbabilityMax = Covered && Intervals.All(Item => Item.PrecipitationProbability.HasValue)
                ? Intervals.Max(Item => Item.PrecipitationProbability) : null,
            WindSpeedMax = Strongest?.WindSpeed, WindDirection = Strongest?.WindDirection
        };
    }
    /// <summary>
    /// Resolves the current local two-hour boundary, including invalid or ambiguous daylight-saving boundaries.
    /// </summary>
    /// <param name="UtcNow">The current UTC instant.</param>
    /// <param name="Zone">The location timezone.</param>
    /// <returns>The boundary as a UTC instant.</returns>
    static DateTime GetTwoHourStart(DateTime UtcNow, TimeZoneInfo Zone)
    {
        DateTime Local = TimeZoneInfo.ConvertTimeFromUtc(UtcNow, Zone);
        DateTime Start = DateTime.SpecifyKind(Local.Date.AddHours(Local.Hour / 2 * 2), DateTimeKind.Unspecified);
        // Some zones advance at midnight. Use the first valid minute within that local interval.
        while (Zone.IsInvalidTime(Start))
            Start = Start.AddMinutes(1);
        if (Zone.IsAmbiguousTime(Start))
            return Zone.GetAmbiguousTimeOffsets(Start).Select(Offset => new DateTimeOffset(Start, Offset).UtcDateTime)
                .Where(Instant => Instant <= UtcNow).Max();
        return TimeZoneInfo.ConvertTimeToUtc(Start, Zone);
    }

    // ● public
    /// <summary>
    /// Converts an explicitly UTC timestamp to the forecast location's timezone, never the device timezone.
    /// </summary>
    /// <param name="Forecast">The forecast identifying the timezone.</param>
    /// <param name="Utc">The UTC instant.</param>
    /// <returns>The local timestamp including its UTC offset.</returns>
    static public DateTimeOffset GetLocalTime(WeatherForecast Forecast, DateTime Utc)
    {
        ArgumentNullException.ThrowIfNull(Forecast);
        if (Utc.Kind != DateTimeKind.Utc)
            throw new ArgumentException("A UTC timestamp is required.", nameof(Utc));
        return TimeZoneInfo.ConvertTime(new DateTimeOffset(Utc), TimeZoneInfo.FindSystemTimeZoneById(Forecast.Timezone));
    }
    /// <summary>
    /// Creates twelve two-hour periods starting at the current local two-hour boundary.
    /// Each period spans two elapsed hours; local clock labels may shift during daylight-saving transitions.
    /// </summary>
    /// <param name="Forecast">The stored or newly downloaded forecast.</param>
    /// <param name="UtcNow">The reference time in UTC.</param>
    /// <returns>Twelve consecutive summaries spanning exactly 24 elapsed hours.</returns>
    static public IReadOnlyList<WeatherPeriod> GetTwoHourPeriods(WeatherForecast Forecast, DateTime UtcNow)
    {
        ArgumentNullException.ThrowIfNull(Forecast);
        if (UtcNow.Kind != DateTimeKind.Utc)
            throw new ArgumentException("A UTC timestamp is required.", nameof(UtcNow));
        TimeZoneInfo Zone = TimeZoneInfo.FindSystemTimeZoneById(Forecast.Timezone);
        DateTime Start = GetTwoHourStart(UtcNow, Zone);
        // A repeated clock hour can put the local boundary over two elapsed hours behind now.
        // Advance in elapsed-hour blocks so the first column always contains the reference instant.
        Start = Start.AddHours(Math.Floor((UtcNow - Start).TotalHours / 2) * 2);
        List<WeatherPeriod> Periods = new();
        for (int Index = 0; Index < 12; Index++)
        {
            DateTime From = Start.AddHours(Index * 2);
            Periods.Add(Summarize(Forecast, From, From.AddHours(2), false, Zone));
        }
        return Periods.AsReadOnly();
    }
    /// <summary>
    /// Summarizes up to nine available calendar days from tomorrow, without inventing extra forecast days.
    /// Six-hour precipitation totals crossing a local midnight are not split; incomplete totals remain unavailable.
    /// </summary>
    /// <param name="Forecast">The MET Norway source samples.</param>
    /// <param name="UtcNow">The reference UTC time.</param>
    /// <returns>The available local-day summaries, with partial-coverage markers.</returns>
    static public IReadOnlyList<WeatherPeriod> GetDailyPeriods(WeatherForecast Forecast, DateTime UtcNow)
    {
        DateTime Tomorrow = GetLocalTime(Forecast, UtcNow).Date.AddDays(1);
        TimeZoneInfo Zone = TimeZoneInfo.FindSystemTimeZoneById(Forecast.Timezone);
        DateTime Last = Forecast.Readings[Forecast.Readings.Count - 1].TimeUtc;
        List<WeatherPeriod> Periods = new();
        for (int Index = 0; Index < 9; Index++)
        {
            DateTime Date = Tomorrow.AddDays(Index);
            DateTime From = CalendarBoundary(Date, Zone);
            DateTime To = CalendarBoundary(Date.AddDays(1), Zone);
            if (From >= Last)
                break;
            Periods.Add(Summarize(Forecast, From, To, true, Zone));
        }
        return Periods.AsReadOnly();
    }
}
