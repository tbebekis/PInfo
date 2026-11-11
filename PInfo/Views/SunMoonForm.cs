/* PInfo, Copyright (c) 2026 Theo Bebekis, see License. */

namespace PInfo.Views;

/// <summary>
/// Displays independently available MET Norway sun and moon events for a selected location and date.
/// </summary>
public class SunMoonForm : AppCardForm
{
    // ● private
    readonly double fLatitude;
    readonly double fLongitude;
    readonly TimeZoneInfo fZone;
    readonly string fZoneNotice;
    readonly CalendarDatePicker fDate = new() { SelectedDateFormat = CalendarDatePickerFormat.Short };
    readonly Button fShow = new() { Content = "Show" };
    readonly ProgressBar fProgress = new() { IsIndeterminate = true, IsVisible = false };
    readonly TextBlock fStatus = new() { TextWrapping = TextWrapping.Wrap };
    readonly StackPanel fSun;
    readonly StackPanel fMoon;
    CancellationTokenSource fCancellation;
    bool fLoaded;

    /// <summary>
    /// Formats an independently nullable event, retaining relative calendar-day markers and the correct offset at that instant.
    /// </summary>
    string EventTime(JsonElement Properties, string Name)
    {
        if (!TryEvent(Properties, Name, out DateTimeOffset Time))
            return "No event reported for this solar-day interval";
        DateTimeOffset Local = TimeZoneInfo.ConvertTime(Time, fZone);
        int Difference = DateOnly.FromDateTime(Local.DateTime).DayNumber - DateOnly.FromDateTime(fDate.SelectedDate.Value).DayNumber;
        string Suffix = Difference == 0 ? string.Empty : Difference == 1 ? " (next day)"
            : Difference == -1 ? " (previous day)" : $" ({Difference:+0;-0} days)";
        return Local.ToString("HH:mm", CultureInfo.CurrentCulture) + " · " + WorldClockService.FormatOffset(Local.Offset) + Suffix;
    }
    /// <summary>
    /// Reads one valid event timestamp without deriving it from another event.
    /// </summary>
    static bool TryEvent(JsonElement Properties, string Name, out DateTimeOffset Time)
    {
        Time = default;
        return Properties.TryGetProperty(Name, out JsonElement Event) && Event.ValueKind == JsonValueKind.Object
            && Event.TryGetProperty("time", out JsonElement Text) && Text.ValueKind == JsonValueKind.String
            && DateTimeOffset.TryParse(Text.GetString(), CultureInfo.InvariantCulture, DateTimeStyles.None, out Time);
    }
    /// <summary>
    /// Adds available horizon angles from the source without substituting an unavailable value.
    /// </summary>
    void AddAngle(StackPanel Section, JsonElement Properties, string EventName, string PropertyName, string Caption)
    {
        if (Properties.TryGetProperty(EventName, out JsonElement Event) && Event.ValueKind == JsonValueKind.Object
            && Event.TryGetProperty(PropertyName, out JsonElement Value) && Value.ValueKind == JsonValueKind.Number)
            AddField(Section, Caption, Value.GetDouble().ToString("0.##", CultureInfo.CurrentCulture) + "°");
    }
    /// <summary>
    /// Displays the documented moon phase quadrants and exact provider angle.
    /// </summary>
    static string PhaseName(double Angle)
    {
        if (Angle == 0 || Angle == 360) return "New moon";
        if (Angle == 90) return "First quarter";
        if (Angle == 180) return "Full moon";
        if (Angle == 270) return "Last quarter";
        if (Angle < 90) return "Waxing crescent";
        if (Angle < 180) return "Waxing gibbous";
        if (Angle < 270) return "Waning gibbous";
        return "Waning crescent";
    }
    /// <summary>
    /// Clears only a body's field controls, preserving the shared section heading.
    /// </summary>
    void ClearFields(StackPanel Section)
    {
        while (Section.Children.Count > 1)
            Section.Children.RemoveAt(Section.Children.Count - 1);
    }
    /// <summary>
    /// Shows one body's result or recoverable error independently of the other body.
    /// </summary>
    void DisplayBody(StackPanel Section, ApiClientResult Result, string Body)
    {
        ClearFields(Section);
        if (Result.ResultStatus != ApiClientResultStatus.Success)
        {
            AddField(Section, "Unavailable", Result.ReasonPhrase);
            return;
        }
        using JsonDocument Document = JsonDocument.Parse(Result.ResponseJsonText);
        JsonElement Properties = Document.RootElement.GetProperty("properties");
        if (Body == "Sun")
        {
            AddField(Section, "Sunrise", EventTime(Properties, "sunrise"));
            AddField(Section, "Sunset", EventTime(Properties, "sunset"));
            AddField(Section, "Solar noon", EventTime(Properties, "solarnoon"));
            AddField(Section, "Solar midnight", EventTime(Properties, "solarmidnight"));
            if (TryEvent(Properties, "sunrise", out DateTimeOffset Rise) && TryEvent(Properties, "sunset", out DateTimeOffset Set)
                && Set >= Rise && Set - Rise < TimeSpan.FromDays(2))
            {
                TimeSpan Length = Set - Rise;
                AddField(Section, "Time between reported sunrise and sunset", $"{(int)Length.TotalHours}h {Length.Minutes:00}m");
            }
            AddAngle(Section, Properties, "sunrise", "azimuth", "Sunrise azimuth (from north)");
            AddAngle(Section, Properties, "sunset", "azimuth", "Sunset azimuth (from north)");
            AddAngle(Section, Properties, "solarnoon", "disc_centre_elevation", "Solar noon altitude");
            if (Properties.TryGetProperty("solarnoon", out JsonElement Noon) && Noon.ValueKind == JsonValueKind.Object
                && Noon.TryGetProperty("visible", out JsonElement Visible) && (Visible.ValueKind == JsonValueKind.True || Visible.ValueKind == JsonValueKind.False))
                AddField(Section, "Sun above the horizon at solar noon", Visible.GetBoolean() ? "Yes" : "No");
        }
        else
        {
            AddField(Section, "Moonrise", EventTime(Properties, "moonrise"));
            AddField(Section, "Moonset", EventTime(Properties, "moonset"));
            AddField(Section, "Upper transit", EventTime(Properties, "high_moon"));
            AddField(Section, "Lower transit", EventTime(Properties, "low_moon"));
            if (Properties.TryGetProperty("moonphase", out JsonElement Phase) && Phase.ValueKind != JsonValueKind.Null)
            {
                double Angle = Phase.ValueKind == JsonValueKind.Object ? Phase.GetProperty("value").GetDouble() : Phase.GetDouble();
                AddField(Section, "Moon phase", PhaseName(Angle));
                AddField(Section, "Phase angle (0° new, 180° full)", Angle.ToString("0.##", CultureInfo.CurrentCulture) + "°");
            }
            AddAngle(Section, Properties, "moonrise", "azimuth", "Moonrise azimuth (from north)");
            AddAngle(Section, Properties, "moonset", "azimuth", "Moonset azimuth (from north)");
            AddAngle(Section, Properties, "high_moon", "disc_centre_elevation", "Upper transit altitude");
        }
        AddField(Section, "Notice", Result.ReasonPhrase);
    }
    /// <summary>
    /// Loads both bodies for an explicit date, guarding cancellation and rapid navigation.
    /// </summary>
    async Task LoadAsync()
    {
        if (fCancellation != null || !this.IsAttachedToVisualTree() || !fDate.SelectedDate.HasValue)
            return;
        DateOnly Date = DateOnly.FromDateTime(fDate.SelectedDate.Value);
        DateTime Noon = Date.ToDateTime(new TimeOnly(12, 0), DateTimeKind.Unspecified);
        TimeSpan Offset = fZone.GetUtcOffset(Noon);
        using CancellationTokenSource Cancellation = new();
        fCancellation = Cancellation;
        fProgress.IsVisible = true;
        fShow.IsEnabled = false;
        fDate.IsEnabled = false;
        fStatus.Text = "Loading Sun & Moon. Please wait...";
        try
        {
            Task<ApiClientResult> Sun = WeatherService.LoadSunMoonAsync("Sun", fLatitude, fLongitude, Date, Offset, Cancellation.Token);
            Task<ApiClientResult> Moon = WeatherService.LoadSunMoonAsync("Moon", fLatitude, fLongitude, Date, Offset, Cancellation.Token);
            await Task.WhenAll(Sun, Moon);
            if (Cancellation.IsCancellationRequested || !this.IsAttachedToVisualTree())
                return;
            DisplayBody(fSun, Sun.Result, "Sun");
            DisplayBody(fMoon, Moon.Result, "Moon");
            fLoaded = Sun.Result.ResultStatus == ApiClientResultStatus.Success || Moon.Result.ResultStatus == ApiClientResultStatus.Success;
            fStatus.Text = fLoaded ? fZoneNotice + " MET Norway reports mean solar-day intervals; events may fall on an adjacent calendar date."
                : "Sun & Moon could not be loaded. Press Show to try again. " + fZoneNotice;
        }
        catch (OperationCanceledException) when (Cancellation.IsCancellationRequested)
        {
            // Leaving this form cancels both requests.
        }
        catch (Exception Ex)
        {
            if (!Cancellation.IsCancellationRequested && this.IsAttachedToVisualTree())
            {
                ExceptionLog.WriteException("SunMoonForm.Load", Ex, false);
                fStatus.Text = "Sun & Moon could not be read. Press Show to try again.";
            }
        }
        finally
        {
            if (fCancellation == Cancellation)
            {
                fCancellation = null;
                fProgress.IsVisible = false;
                fShow.IsEnabled = true;
                fDate.IsEnabled = true;
            }
        }
    }
    /// <summary>
    /// Loads the initial date when visible without reloading completed content on Back.
    /// </summary>
    async void OnSunMoonLoaded(object Sender, RoutedEventArgs Args)
    {
        if (!fLoaded)
            await LoadAsync();
    }
    /// <summary>
    /// Requests data only when Show is pressed after selecting a date.
    /// </summary>
    async void OnShowClick(object Sender, RoutedEventArgs Args)
    {
        await LoadAsync();
    }
    /// <summary>
    /// Invalidates outstanding callbacks before cancelling them.
    /// </summary>
    void OnSunMoonUnloaded(object Sender, RoutedEventArgs Args)
    {
        CancellationTokenSource Cancellation = fCancellation;
        fCancellation = null;
        Cancellation?.Cancel();
    }

    // ● constructor
    /// <summary>
    /// Creates date-selectable celestial details for the currently selected weather location.
    /// </summary>
    public SunMoonForm(string Name, double Latitude, double Longitude, string Timezone, string Context)
    {
        fLatitude = Latitude;
        fLongitude = Longitude;
        bool Known = WorldClockService.TryGetZone(Timezone, out TimeZoneInfo Zone);
        fZone = Known ? Zone : TimeZoneInfo.Utc;
        fZoneNotice = Known ? "Times shown in " + fZone.Id + "."
            : "The location timezone is unknown. Times use UTC; the requested solar-day date may differ from the local calendar date.";
        SetHeader(Name + " — Sun & Moon", Context, "☀︎");
        this.FindControl<TextBlock>("HeaderIconText").Classes.Add("WeatherSun");
        StackPanel Controls = CreateSection("Date");
        DateTimeOffset Today = TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, fZone);
        fDate.SelectedDate = Today.Date;
        Avalonia.Automation.AutomationProperties.SetName(fDate, "Sun and moon date");
        WrapPanel Selection = new();
        fShow.Classes.Add("PrimaryButton");
        fShow.Margin = new Thickness(8, 0, 0, 0);
        Selection.Children.Add(fDate);
        Selection.Children.Add(fShow);
        Controls.Children.Add(Selection);
        Controls.Children.Add(new TextBlock { Text = "Choose a date, then press Show.", TextWrapping = TextWrapping.Wrap, Classes = { "SecondaryText" } });
        fStatus.Classes.Add("SecondaryText");
        Controls.Children.Add(fStatus);
        Controls.Children.Add(fProgress);
        AddSection(Controls);
        fSun = CreateSection("Sun");
        AddField(fSun, "Events", "Select a date and press Show.");
        AddSection(fSun);
        fMoon = CreateSection("Moon");
        AddField(fMoon, "Events", "Select a date and press Show.");
        AddSection(fMoon);
        StackPanel Source = CreateSection("Source");
        AddLink(Source, "Data from MET Norway — Sunrise 3.0", "https://api.met.no/weatherapi/sunrise/3.0/documentation");
        AddLink(Source, "Licence: CC BY 4.0", "https://creativecommons.org/licenses/by/4.0/");
        AddSection(Source);
        SetSource("Based on data from MET Norway. Refresh follows the Weather interval in Settings and provider cache rules. Unavailable events are labelled explicitly.");
        fDate.SelectedDateChanged += (Sender, Args) =>
        {
            if (fCancellation == null)
            {
                fLoaded = false;
                ClearFields(fSun);
                ClearFields(fMoon);
                AddField(fSun, "Events", "Press Show to load this selection.");
                AddField(fMoon, "Events", "Press Show to load this selection.");
                fStatus.Text = "Press Show to load the selected date.";
            }
        };
        fShow.Click += OnShowClick;
        Loaded += OnSunMoonLoaded;
        Unloaded += OnSunMoonUnloaded;
    }
}
