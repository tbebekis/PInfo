/* PInfo, Copyright (c) 2026 Theo Bebekis, see License. */

namespace PInfo.Views;

/// <summary>
/// Shows a selected forecast column's details without making another weather request.
/// </summary>
public class WeatherPeriodCardForm : AppCardForm
{
    // ● private
    readonly Dictionary<TextBlock, double?> fTemperatures = new();

    /// <summary>
    /// Adds a temperature field and retains its Celsius value for display-unit changes on return.
    /// </summary>
    /// <param name="Section">The containing section.</param>
    /// <param name="Caption">The field label.</param>
    /// <param name="Celsius">The original source value.</param>
    void AddTemperatureField(StackPanel Section, string Caption, double? Celsius)
    {
        AddField(Section, Caption, WeatherDisplay.Temperature(Celsius));
        StackPanel Field = (StackPanel)Section.Children[Section.Children.Count - 1];
        TextBlock Value = (TextBlock)Field.Children[Field.Children.Count - 1];
        fTemperatures.Add(Value, Celsius);
    }
    /// <summary>
    /// Updates temperature text when returning from Settings without rebuilding the card or its scroll state.
    /// </summary>
    void OnTemperatureLoaded(object Sender, RoutedEventArgs Args)
    {
        foreach (var Field in fTemperatures)
            Field.Key.Text = WeatherDisplay.Temperature(Field.Value);
    }

    // ● constructor
    /// <summary>
    /// Creates a daily or two-hour weather detail card from the displayed forecast snapshot.
    /// </summary>
    /// <param name="LocationName">The city or place label.</param>
    /// <param name="Forecast">The source forecast with its update timestamp.</param>
    /// <param name="Period">The selected forecast interval.</param>
    public WeatherPeriodCardForm(string LocationName, WeatherForecast Forecast, WeatherPeriod Period)
    {
        ArgumentNullException.ThrowIfNull(Forecast);
        ArgumentNullException.ThrowIfNull(Period);
        string Caption = Period.IsDaily ? Period.StartLocal.ToString("dddd, dd MMM yyyy", CultureInfo.CurrentCulture)
            : Period.StartLocal.ToString("dd MMM HH:mm zzz", CultureInfo.CurrentCulture) + " – "
                + Period.EndLocal.ToString("dd MMM HH:mm zzz", CultureInfo.CurrentCulture);
        SetHeader(LocationName, Caption, WeatherDisplay.Icon(Period.SymbolCode));
        if (WeatherDisplay.Icon(Period.SymbolCode) == "☀")
            this.FindControl<TextBlock>("HeaderIconText")?.Classes.Add("WeatherSun");
        AddBadge(WeatherDisplay.Condition(Period.SymbolCode));
        StackPanel Section = CreateSection("Temperature");
        AddTemperatureField(Section, "Highest available forecast", Period.TemperatureMax);
        AddTemperatureField(Section, "Lowest available forecast", Period.TemperatureMin);
        AddTemperatureField(Section, "Feels like maximum", Period.FeelsLikeMax);
        AddTemperatureField(Section, "Feels like minimum", Period.FeelsLikeMin);
        AddField(Section, "Mean sampled humidity", WeatherDisplay.Number(Period.HumidityMean, "0", " %"));
        AddSection(Section);
        Section = CreateSection("Precipitation");
        AddField(Section, "Maximum interval chance", WeatherDisplay.Number(Period.PrecipitationProbabilityMax, "0", " %"));
        AddField(Section, "Total water equivalent", WeatherDisplay.Number(Period.Precipitation, Unit: " mm"));
        AddField(Section, "Coverage", "Totals require complete source intervals. Separate rain and snowfall amounts are not supplied by this forecast.");
        AddSection(Section);
        Section = CreateSection("Wind");
        AddField(Section, "Highest sampled speed", WeatherDisplay.Number(Period.WindSpeedMax, Unit: " km/h"));
        AddField(Section, "Direction at highest sampled speed (from)",
            WeatherDisplay.WindDirection(Period.WindDirection) + " · " + WeatherDisplay.Number(Period.WindDirection, "0", "°"));
        AddSection(Section);
        Section = CreateSection("Forecast information");
        AddField(Section, "Location timezone", Forecast.Timezone);
        AddField(Section, "Downloaded", WeatherService.GetLocalTime(Forecast, Forecast.UpdatedUtc)
            .ToString("dd MMM yyyy HH:mm zzz", CultureInfo.CurrentCulture));
        AddField(Section, "Model updated", WeatherService.GetLocalTime(Forecast, Forecast.ModelUpdatedUtc)
            .ToString("dd MMM yyyy HH:mm zzz", CultureInfo.CurrentCulture));
        AddField(Section, "Coverage", Period.IsPartial ? "Partial coverage. Values summarize the available samples; incomplete precipitation totals are unavailable."
            : "The source intervals cover this period. Values summarize the available forecast samples.");
        AddField(Section, "Period", Period.IsDaily ? "Local calendar day calculated from MET Norway forecast samples. Later days are less certain."
            : "Two elapsed hours. The first displayed interval includes the elapsed part of its current two-hour block.");
        AddField(Section, "Missing values", "An em dash means data is unavailable or incomplete.");
        AddSection(Section);
        Section = CreateSection("Source");
        StackPanel Links = new();
        Links.Classes.Add("CardActions");
        AddLink(Links, "Data from MET Norway", "https://api.met.no/");
        AddLink(Links, "Licence: CC BY 4.0", "https://creativecommons.org/licenses/by/4.0/");
        Section.Children.Add(Links);
        AddField(Section, "Summary", "Daily and two-hour summaries calculated by PInfo from MET Norway source intervals. Missing values are not interpolated.");
        AddSection(Section);
        Loaded += OnTemperatureLoaded;
    }
}
