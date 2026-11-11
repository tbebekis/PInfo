/* PInfo, Copyright (c) 2026 Theo Bebekis, see License. */

namespace PInfo.Views;

/// <summary>
/// Displays a location forecast in two vertically stacked tables with separate column detail cards.
/// </summary>
public class WeatherForm : AppForm
{
    // ● private
    readonly string fLocationName;
    readonly double fLatitude;
    readonly double fLongitude;
    readonly string fTimezone;
    readonly StackPanel fTables = new() { Spacing = 18 };
    readonly TextBlock fStatus = new() { TextWrapping = TextWrapping.Wrap };
    readonly Button fRetry = new() { Content = "Try again", IsVisible = false };
    readonly ProgressBar fProgress = new() { IsIndeterminate = true, IsVisible = false };
    CancellationTokenSource fCancellation;
    bool fLoaded;
    WeatherForecast fForecast;
    TemperatureUnit fDisplayedUnit;
    DateTime fDisplayedAtUtc;
    readonly List<ScrollViewer> fColumnScrollers = new();
    readonly ScrollViewer fScroller;
    TextBlock fSunriseText;
    TextBlock fSunsetText;
    string fSunriseValue = "Loading...";
    string fSunsetValue = "Loading...";
    bool fSunReady;
    CancellationTokenSource fSunCancellation;

    /// <summary>
    /// Creates a centered weather-table label with optional font size.
    /// </summary>
    /// <param name="Text">The label text.</param>
    /// <param name="FontSize">The font size in device-independent pixels.</param>
    /// <returns>The themed text control.</returns>
    TextBlock Label(string Text, double FontSize = 16)
    {
        TextBlock Result = new()
        {
            Text = Text, FontSize = FontSize, TextAlignment = TextAlignment.Center,
            TextWrapping = TextWrapping.Wrap, VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center
        };
        Result.Classes.Add("WeatherValue");
        return Result;
    }
    /// <summary>
    /// Connects forecast temperatures with a smooth visual curve and labels, leaving missing periods disconnected.
    /// </summary>
    /// <param name="Periods">The ordered forecast summaries.</param>
    /// <param name="Minimum">Selects daily minima instead of maxima.</param>
    /// <param name="ScaleMin">The shared Celsius scale minimum.</param>
    /// <param name="ScaleMax">The shared Celsius scale maximum.</param>
    /// <returns>A noninteractive overlay aligned with the selectable columns.</returns>
    Canvas CreateTemperatureCurve(IReadOnlyList<WeatherPeriod> Periods, bool Minimum, double ScaleMin, double ScaleMax)
    {
        bool Daily = Periods[0].IsDaily;
        string SeriesClass = Minimum ? "WeatherMinimum" : "WeatherMaximum";
        Canvas Chart = new()
        {
            Width = Periods.Count * 80, Height = Daily ? 140 : 120, Margin = new Thickness(0, 90, 0, 0),
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Top, IsHitTestVisible = false
        };
        StreamGeometry Geometry = new();
        using (StreamGeometryContext Context = Geometry.Open())
        {
            bool Connected = false;
            Point Previous = default;
            for (int Index = 0; Index < Periods.Count; Index++)
            {
                double? Temperature = Minimum ? Periods[Index].TemperatureMin : Periods[Index].TemperatureMax;
                if (!Temperature.HasValue)
                {
                    if (Connected)
                        Context.EndFigure(false);
                    Connected = false;
                    TextBlock Missing = Label("—");
                    Missing.Width = 80;
                    Canvas.SetLeft(Missing, Index * 80);
                    Canvas.SetTop(Missing, Minimum ? 104 : 4);
                    if (Daily)
                        Missing.Classes.Add(SeriesClass);
                    Chart.Children.Add(Missing);
                    continue;
                }
                Point Current = new(Index * 80 + 40, 32 + (ScaleMax - Temperature.Value) / (ScaleMax - ScaleMin) * (Daily ? 72 : 64));
                if (!Connected)
                    Context.BeginFigure(Current, false);
                else
                {
                    double Middle = (Previous.X + Current.X) / 2;
                    Context.CubicBezierTo(new Point(Middle, Previous.Y), new Point(Middle, Current.Y), Current, true);
                }
                Connected = true;
                Previous = Current;
                Border Dot = new() { Width = 6, Height = 6, CornerRadius = new CornerRadius(3) };
                Dot.Classes.Add("WeatherPoint");
                if (Daily)
                    Dot.Classes.Add(SeriesClass);
                Canvas.SetLeft(Dot, Current.X - 3);
                Canvas.SetTop(Dot, Current.Y - 3);
                Chart.Children.Add(Dot);
                TextBlock Value = Label(WeatherDisplay.Temperature(Temperature, "0", false));
                Value.Width = 80;
                Canvas.SetLeft(Value, Index * 80);
                Canvas.SetTop(Value, Minimum ? Current.Y + 8 : Current.Y - 28);
                if (Daily)
                    Value.Classes.Add(SeriesClass);
                Chart.Children.Add(Value);
            }
            if (Connected)
                Context.EndFigure(false);
        }
        Avalonia.Controls.Shapes.Path Curve = new() { Data = Geometry, StrokeThickness = 2 };
        Curve.Classes.Add("WeatherCurve");
        if (Daily)
            Curve.Classes.Add(SeriesClass);
        Chart.Children.Insert(0, Curve);
        return Chart;
    }
    /// <summary>
    /// Builds a selectable forecast column containing the compact measurements and wind arrow.
    /// </summary>
    /// <param name="Forecast">The forecast snapshot used by the detail card.</param>
    /// <param name="Period">The selected interval.</param>
    /// <returns>The clickable forecast column.</returns>
    Button CreateColumn(WeatherForecast Forecast, WeatherPeriod Period)
    {
        Grid Column = new() { RowDefinitions = new RowDefinitions(Period.IsDaily ? "48,42,140,38,62" : "48,42,120,38,62") };
        string Caption = Period.IsDaily ? Period.StartLocal.ToString("ddd\ndd", CultureInfo.CurrentCulture)
            : Period.StartLocal.ToString("dd/MM", CultureInfo.CurrentCulture) + "\n"
                + Period.StartLocal.ToString("HH", CultureInfo.InvariantCulture) + "–"
                + Period.EndLocal.ToString("HH", CultureInfo.InvariantCulture);
        if (Period.IsPartial)
            Caption += "*";
        Column.Children.Add(Label(Caption, 14));
        TextBlock Icon = Label(WeatherDisplay.Icon(Period.SymbolCode), 28);
        if (Icon.Text == "☀")
            Icon.Classes.Add("WeatherSun");
        Grid.SetRow(Icon, 1);
        Column.Children.Add(Icon);
        TextBlock Precipitation = Label(WeatherDisplay.Number(Period.Precipitation));
        Grid.SetRow(Precipitation, 3);
        Column.Children.Add(Precipitation);
        StackPanel Wind = new() { VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center };
        TextBlock Arrow = Label(Period.WindDirection.HasValue ? "↓" : "—", 24);
        Arrow.HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center;
        Arrow.RenderTransformOrigin = RelativePoint.Center;
        Arrow.RenderTransform = new RotateTransform(Period.WindDirection.GetValueOrDefault());
        Wind.Children.Add(Arrow);
        Wind.Children.Add(Label(WeatherDisplay.Number(Period.WindSpeedMax, "0")));
        Grid.SetRow(Wind, 4);
        Column.Children.Add(Wind);
        Button Action = new() { Content = Column };
        Action.Classes.Add("WeatherColumn");
        string Description = Caption.Replace('\n', ' ') + ": " + WeatherDisplay.Condition(Period.SymbolCode);
        Avalonia.Automation.AutomationProperties.SetName(Action, Description + ". Open forecast details.");
        ToolTip.SetTip(Action, Description);
        Action.Click += (Sender, Args) => MainView.Current?.PushForm(new WeatherPeriodCardForm(fLocationName, Forecast, Period));
        return Action;
    }
    /// <summary>
    /// Creates a horizontally scrolling forecast table with fixed row labels and readable equal-width columns.
    /// </summary>
    /// <param name="Title">The section title.</param>
    /// <param name="Forecast">The source forecast.</param>
    /// <param name="Periods">The ordered section columns.</param>
    /// <returns>The section containing the heading, dates, and forecast table.</returns>
    Control CreateTable(string Title, WeatherForecast Forecast, IReadOnlyList<WeatherPeriod> Periods)
    {
        StackPanel Section = new() { Spacing = 4 };
        TextBlock Heading = new() { Text = Title };
        Heading.Classes.Add("ListItemTitle");
        Section.Children.Add(Heading);
        string DateRange = Periods[0].StartLocal.ToString("dd MMM", CultureInfo.CurrentCulture) + " – "
            + (Periods[Periods.Count - 1].IsDaily ? Periods[Periods.Count - 1].StartLocal : Periods[Periods.Count - 1].EndLocal)
                .ToString("dd MMM", CultureInfo.CurrentCulture);
        TextBlock Dates = new() { Text = DateRange };
        Dates.Classes.Add("SecondaryText");
        Section.Children.Add(Dates);
        TextBlock Hint = new() { Text = "Swipe horizontally for more " + (Periods[0].IsDaily ? "days" : "hours") + " →" };
        Hint.Classes.Add("SecondaryText");
        Section.Children.Add(Hint);
        if (Periods[0].IsDaily)
        {
            StackPanel Legend = new() { Orientation = Avalonia.Layout.Orientation.Horizontal, Spacing = 16 };
            TextBlock High = Label("● Max", 14);
            High.Classes.Add("WeatherMaximum");
            TextBlock Low = Label("● Min", 14);
            Low.Classes.Add("WeatherMinimum");
            Legend.Children.Add(High);
            Legend.Children.Add(Low);
            Section.Children.Add(Legend);
        }
        Grid Table = new() { ColumnDefinitions = new ColumnDefinitions("56,*") };
        Grid Columns = new() { ColumnDefinitions = new ColumnDefinitions(string.Join(",", Periods.Select(Item => "80"))) };
        Grid Labels = new() { RowDefinitions = new RowDefinitions(Periods[0].IsDaily ? "48,42,140,38,62" : "48,42,120,38,62") };
        string[] Captions = { "Local\ntime", "", "Temp\n" + WeatherDisplay.TemperatureSymbol, "Precip.\nmm", "Wind\nkm/h" };
        for (int Index = 0; Index < Captions.Length; Index++)
        {
            TextBlock Caption = Label(Captions[Index], 14);
            Grid.SetRow(Caption, Index);
            Labels.Children.Add(Caption);
        }
        Table.Children.Add(Labels);
        double ScaleMin = Periods.Where(Item => Item.TemperatureMin.HasValue).Select(Item => Item.TemperatureMin.Value).DefaultIfEmpty(0).Min() - 1;
        double ScaleMax = Periods.Where(Item => Item.TemperatureMax.HasValue).Select(Item => Item.TemperatureMax.Value).DefaultIfEmpty(1).Max() + 1;
        for (int Index = 0; Index < Periods.Count; Index++)
        {
            Button Column = CreateColumn(Forecast, Periods[Index]);
            Grid.SetColumn(Column, Index);
            Columns.Children.Add(Column);
        }
        Grid Plot = new();
        Plot.Children.Add(Columns);
        Plot.Children.Add(CreateTemperatureCurve(Periods, false, ScaleMin, ScaleMax));
        if (Periods[0].IsDaily)
            Plot.Children.Add(CreateTemperatureCurve(Periods, true, ScaleMin, ScaleMax));
        ScrollViewer Data = new()
        {
            Content = Plot,
            HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto,
            VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled
        };
        fColumnScrollers.Add(Data);
        Grid.SetColumn(Data, 1);
        Table.Children.Add(Data);
        Border Surface = new() { Child = Table };
        Surface.Classes.Add("WeatherTable");
        Section.Children.Add(Surface);
        return Section;
    }
    /// <summary>
    /// Populates both sections from one forecast using a single reference instant in location time.
    /// </summary>
    /// <param name="Forecast">The validated forecast.</param>
    void DisplayForecast(WeatherForecast Forecast)
    {
        DateTime Now = fDisplayedAtUtc;
        fDisplayedUnit = Lib.Settings.TemperatureUnit;
        fColumnScrollers.Clear();
        IReadOnlyList<WeatherPeriod> Hours = WeatherService.GetTwoHourPeriods(Forecast, Now);
        IReadOnlyList<WeatherPeriod> Days = WeatherService.GetDailyPeriods(Forecast, Now);
        fTables.Children.Clear();
        WeatherReading Reading = Forecast.Readings.LastOrDefault(Item => Item.TimeUtc <= Now) ?? Forecast.Readings[0];
        TextBlock Current = new()
        {
            Text = WeatherDisplay.Temperature(Reading.Temperature) + " · "
                + WeatherDisplay.Condition(Reading.SymbolCode),
            TextWrapping = TextWrapping.Wrap
        };
        Current.Classes.Add("ListItemTitle");
        StackPanel Summary = new() { Spacing = 2 };
        Grid CurrentRow = new() { ColumnDefinitions = new ColumnDefinitions("*,Auto"), ColumnSpacing = 8 };
        CurrentRow.Children.Add(Current);
        Button SunMoon = new()
        {
            Content = "Sun & Moon", FontSize = 12, MinHeight = 28, Padding = new Thickness(8, 2),
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center
        };
        SunMoon.Classes.Add("PrimaryButton");
        SunMoon.Click += (Sender, Args) => MainView.Current?.PushForm(new SunMoonForm(fLocationName, fLatitude, fLongitude,
            fTimezone, string.Empty));
        Grid.SetColumn(SunMoon, 1);
        CurrentRow.Children.Add(SunMoon);
        Summary.Children.Add(CurrentRow);
        TextBlock Date = new()
        {
            Text = WeatherService.GetLocalTime(Forecast, Reading.TimeUtc).ToString("dd MMM HH:mm", CultureInfo.CurrentCulture)
                + " · " + Forecast.Timezone, TextWrapping = TextWrapping.Wrap
        };
        Date.Classes.Add("SecondaryText");
        Summary.Children.Add(Date);
        fSunriseText = new TextBlock { Text = "Sunrise: " + fSunriseValue, TextWrapping = TextWrapping.Wrap };
        fSunsetText = new TextBlock { Text = "Sunset: " + fSunsetValue, TextWrapping = TextWrapping.Wrap };
        fSunriseText.Classes.Add("SecondaryText");
        fSunsetText.Classes.Add("SecondaryText");
        Summary.Children.Add(fSunriseText);
        Summary.Children.Add(fSunsetText);
        fTables.Children.Add(Summary);
        fTables.Children.Add(CreateTable("Next 24 hours", Forecast, Hours));
        if (Days.Count > 0)
            fTables.Children.Add(CreateTable("Daily forecast", Forecast, Days));
        else
            fTables.Children.Add(Label("No future daily forecast is available in these stored data."));
        TextBlock Note = new()
        {
            Text = "Tap a column for details. * marks incomplete coverage. The first block includes its elapsed portion. Temperatures and winds summarize available forecast samples. Precipitation totals require complete coverage; — means unavailable data. Later days are less certain.",
            TextWrapping = TextWrapping.Wrap
        };
        Note.Classes.Add("SecondaryText");
        fTables.Children.Add(Note);
        HyperlinkButton Source = new() { Content = "Data from MET Norway", NavigateUri = new Uri("https://api.met.no/") };
        fTables.Children.Add(Source);
        HyperlinkButton Licence = new() { Content = "CC BY 4.0 · summaries calculated by PInfo", NavigateUri = new Uri("https://creativecommons.org/licenses/by/4.0/") };
        Licence.Content = new TextBlock
        {
            Text = "CC BY 4.0 · summaries calculated by PInfo", TextWrapping = TextWrapping.Wrap
        };
        Licence.HorizontalContentAlignment = Avalonia.Layout.HorizontalAlignment.Left;
        fTables.Children.Add(Licence);
    }
    /// <summary>
    /// Formats a solar event time with a short marker only when it falls outside the displayed forecast date.
    /// </summary>
    string SummarySunTime(JsonElement Properties, string Name, TimeZoneInfo Zone, DateOnly Date)
    {
        if (!Properties.TryGetProperty(Name, out JsonElement Event) || Event.ValueKind != JsonValueKind.Object
            || !Event.TryGetProperty("time", out JsonElement Text) || Text.ValueKind != JsonValueKind.String
            || !DateTimeOffset.TryParse(Text.GetString(), CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTimeOffset Time))
            return "No event reported";
        DateTimeOffset Local = TimeZoneInfo.ConvertTime(Time, Zone);
        int Difference = DateOnly.FromDateTime(Local.DateTime).DayNumber - Date.DayNumber;
        string Suffix = Difference == 0 ? string.Empty : Difference == 1 ? " (next day)"
            : Difference == -1 ? " (previous day)" : $" ({Difference:+0;-0} days)";
        return Local.ToString("HH:mm", CultureInfo.CurrentCulture) + Suffix;
    }
    /// <summary>
    /// Loads compact sunrise/set data after the forecast is visible without rebuilding the table or losing scroll position.
    /// </summary>
    async Task LoadSunSummaryAsync()
    {
        if (fSunReady || fSunCancellation != null || fForecast == null || !this.IsAttachedToVisualTree())
            return;
        WeatherForecast Forecast = fForecast;
        WeatherReading Reading = Forecast.Readings.LastOrDefault(Item => Item.TimeUtc <= fDisplayedAtUtc) ?? Forecast.Readings[0];
        DateOnly Date = DateOnly.FromDateTime(WeatherService.GetLocalTime(Forecast, Reading.TimeUtc).DateTime);
        WorldClockService.TryGetZone(Forecast.Timezone, out TimeZoneInfo Zone);
        Zone ??= TimeZoneInfo.Utc;
        TimeSpan Offset = Zone.GetUtcOffset(Date.ToDateTime(new TimeOnly(12, 0), DateTimeKind.Unspecified));
        using CancellationTokenSource Cancellation = new();
        fSunCancellation = Cancellation;
        try
        {
            ApiClientResult Result = await WeatherService.LoadSunMoonAsync("Sun", fLatitude, fLongitude, Date, Offset, Cancellation.Token);
            if (Cancellation.IsCancellationRequested || fSunCancellation != Cancellation || Forecast != fForecast || !this.IsAttachedToVisualTree())
                return;
            if (Result.ResultStatus == ApiClientResultStatus.Success)
            {
                using JsonDocument Document = JsonDocument.Parse(Result.ResponseJsonText);
                JsonElement Properties = Document.RootElement.GetProperty("properties");
                string Saved = string.IsNullOrWhiteSpace(Result.ReasonPhrase) ? string.Empty : " (saved data; see Sun & Moon)";
                fSunriseValue = SummarySunTime(Properties, "sunrise", Zone, Date) + Saved;
                fSunsetValue = SummarySunTime(Properties, "sunset", Zone, Date) + Saved;
                fSunReady = true;
            }
            else
            {
                fSunriseValue = "Unavailable — see Sun & Moon";
                fSunsetValue = "Unavailable — see Sun & Moon";
            }
            fSunriseText.Text = "Sunrise: " + fSunriseValue;
            fSunsetText.Text = "Sunset: " + fSunsetValue;
        }
        catch (OperationCanceledException) when (Cancellation.IsCancellationRequested)
        {
            // Returning to this forecast can retry an interrupted summary load.
        }
        catch (Exception Ex)
        {
            if (!Cancellation.IsCancellationRequested && fSunCancellation == Cancellation && this.IsAttachedToVisualTree())
            {
                ExceptionLog.WriteException("WeatherForm.SunSummary", Ex, false);
                fSunriseValue = fSunsetValue = "Unavailable — see Sun & Moon";
                fSunriseText.Text = "Sunrise: " + fSunriseValue;
                fSunsetText.Text = "Sunset: " + fSunsetValue;
            }
        }
        finally
        {
            if (fSunCancellation == Cancellation)
                fSunCancellation = null;
        }
    }
    /// <summary>
    /// Loads on entry or retry while safely cancelling work when this form is left.
    /// </summary>
    async Task LoadAsync()
    {
        fCancellation?.Cancel();
        using CancellationTokenSource Cancellation = new();
        fCancellation = Cancellation;
        fProgress.IsVisible = true;
        fRetry.IsVisible = false;
        fStatus.Text = "Loading weather...";
        fStatus.IsVisible = true;
        try
        {
            var Loaded = await WeatherService.LoadForecastAsync(fLatitude, fLongitude, fTimezone, Cancellation.Token);
            if (Cancellation.IsCancellationRequested || fCancellation != Cancellation)
                return;
            if (Loaded.Result.ResultStatus == ApiClientResultStatus.Success && Loaded.Forecast != null)
            {
                fForecast = Loaded.Forecast;
                fDisplayedAtUtc = DateTime.UtcNow;
                fSunReady = false;
                fSunriseValue = "Loading...";
                fSunsetValue = "Loading...";
                DisplayForecast(fForecast);
                fLoaded = true;
                fStatus.Text = Loaded.Result.ReasonPhrase ?? string.Empty;
                if (fStatus.Text == "OK")
                    fStatus.Text = string.Empty;
            }
            else if (Loaded.Result.ResultStatus != ApiClientResultStatus.Cancelled)
            {
                fStatus.Text = string.IsNullOrWhiteSpace(Loaded.Result.ReasonPhrase)
                    ? "Weather could not be loaded. Please check your connection." : Loaded.Result.ReasonPhrase;
                fRetry.IsVisible = true;
            }
        }
        catch (Exception Ex)
        {
            if (Cancellation.IsCancellationRequested || fCancellation != Cancellation)
                return;
            ExceptionLog.WriteException("WeatherForm.LoadAsync", Ex, false);
            fStatus.Text = "Weather could not be displayed. Please try again.";
            fRetry.IsVisible = true;
        }
        finally
        {
            if (fCancellation == Cancellation)
            {
                fCancellation = null;
                fProgress.IsVisible = false;
                fStatus.IsVisible = !string.IsNullOrWhiteSpace(fStatus.Text);
            }
        }
        if (fLoaded && !Cancellation.IsCancellationRequested && this.IsAttachedToVisualTree())
            await LoadSunSummaryAsync();
    }
    /// <summary>
    /// Rebuilds display temperatures from the current snapshot while retaining vertical and horizontal positions.
    /// </summary>
    void RefreshTemperatureUnit()
    {
        Vector Vertical = fScroller.Offset;
        Vector[] Horizontal = fColumnScrollers.Select(Item => Item.Offset).ToArray();
        DisplayForecast(fForecast);
        Dispatcher.UIThread.Post(() =>
        {
            if (!this.IsAttachedToVisualTree())
                return;
            fScroller.Offset = Vertical;
            for (int Index = 0; Index < Horizontal.Length && Index < fColumnScrollers.Count; Index++)
                fColumnScrollers[Index].Offset = Horizontal[Index];
        }, DispatcherPriority.Loaded);
    }
    /// <summary>
    /// Loads once, preserving the current forecast and scroll position when returning from a detail card.
    /// </summary>
    async void OnWeatherLoaded(object Sender, RoutedEventArgs Args)
    {
        if (!fLoaded)
            await LoadAsync();
        else
        {
            if (fDisplayedUnit != Lib.Settings.TemperatureUnit)
                RefreshTemperatureUnit();
            await LoadSunSummaryAsync();
        }
    }
    /// <summary>
    /// Cancels an unfinished request on navigation without discarding a completed forecast view.
    /// </summary>
    void OnWeatherUnloaded(object Sender, RoutedEventArgs Args)
    {
        fCancellation?.Cancel();
        CancellationTokenSource SunCancellation = fSunCancellation;
        fSunCancellation = null;
        SunCancellation?.Cancel();
    }
    /// <summary>
    /// Retries a failed load from the inline notice.
    /// </summary>
    async void OnRetryClick(object Sender, RoutedEventArgs Args)
    {
        await LoadAsync();
    }

    // ● constructor
    /// <summary>
    /// Creates the location weather view; data is requested only when the form becomes visible.
    /// </summary>
    /// <param name="LocationName">The selected city or place name.</param>
    /// <param name="Latitude">The requested latitude.</param>
    /// <param name="Longitude">The requested longitude.</param>
    /// <param name="Timezone">The selected location timezone from GeoNames.</param>
    /// <param name="AdministrativeArea">The resolved administrative context shown below the title.</param>
    /// <param name="LocationKey">The stable GeoNames weather-location identity, or empty to use coordinates.</param>
    public WeatherForm(string LocationName, double Latitude, double Longitude, string Timezone, string AdministrativeArea = "", string LocationKey = "")
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(LocationName);
        fLocationName = LocationName;
        fLatitude = Latitude;
        fLongitude = Longitude;
        fTimezone = Timezone;
        Classes.Add("FormBackground");
        Classes.Add("CompactTitle");
        Grid Layout = new() { RowDefinitions = new RowDefinitions("Auto,Auto,*"), Margin = new Thickness(12) };
        TextBlock Title = new() { Text = LocationName + " - Weather", Margin = new Thickness(4, 0, 4, 10), TextWrapping = TextWrapping.Wrap };
        Title.Classes.Add("FormTitle");
        StackPanel Header = new() { Spacing = 2, Margin = new Thickness(0, 0, 0, 8) };
        Grid Heading = new() { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
        Heading.Children.Add(Title);
        FavoriteButton Favorite = new(FavoriteService.Weather(LocationName, Latitude, Longitude, Timezone, AdministrativeArea, LocationKey));
        Favorite.VerticalAlignment = Avalonia.Layout.VerticalAlignment.Top;
        Grid.SetColumn(Favorite, 1);
        Heading.Children.Add(Favorite);
        Header.Children.Add(Heading);
        if (!string.IsNullOrWhiteSpace(AdministrativeArea))
        {
            TextBlock Area = new() { Text = AdministrativeArea, FontSize = 12, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(4, 0) };
            Area.Classes.Add("SecondaryText");
            Header.Children.Add(Area);
        }
        Layout.Children.Add(Header);
        StackPanel Status = new() { Spacing = 6, Margin = new Thickness(0, 0, 0, 8) };
        fStatus.Classes.Add("NoticeText");
        fRetry.Classes.Add("PrimaryButton");
        fProgress.Classes.Add("LoadingBar");
        Status.Children.Add(fStatus);
        Status.Children.Add(fProgress);
        Status.Children.Add(fRetry);
        Grid.SetRow(Status, 1);
        Layout.Children.Add(Status);
        fScroller = new ScrollViewer { Content = fTables, HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled };
        Grid.SetRow(fScroller, 2);
        Layout.Children.Add(fScroller);
        Content = Layout;
        Loaded += OnWeatherLoaded;
        Unloaded += OnWeatherUnloaded;
        fRetry.Click += OnRetryClick;
    }
}
