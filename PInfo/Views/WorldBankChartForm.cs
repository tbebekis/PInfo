/* PInfo, Copyright (c) 2026 Theo Bebekis, see License. */

namespace PInfo.Views;

/// <summary>
/// Displays an annual indicator chart with calendar-year ranges, missing-data gaps and selectable observations.
/// </summary>
public class WorldBankChartForm : AppForm
{
    // ● private
    readonly string fIndicatorCode;
    readonly IReadOnlyList<(int Year, decimal? Value)> fPoints;
    readonly StackPanel fPeriods = new() { Orientation = Avalonia.Layout.Orientation.Horizontal, Spacing = 8 };
    readonly TextBlock fRangeText = new() { TextWrapping = TextWrapping.Wrap };
    readonly TextBlock fSelectedText = new() { TextWrapping = TextWrapping.Wrap };
    readonly Border fChart = new();
    int fYears = 10;

    /// <summary>
    /// Adds a small themed axis label at a fixed canvas position.
    /// </summary>
    /// <param name="Surface">The axis or plot canvas.</param>
    /// <param name="Text">The label text.</param>
    /// <param name="X">The left coordinate.</param>
    /// <param name="Y">The top coordinate.</param>
    /// <param name="Width">The label width.</param>
    /// <param name="Alignment">The text alignment.</param>
    void AddLabel(Canvas Surface, string Text, double X, double Y, double Width, TextAlignment Alignment)
    {
        TextBlock Label = new() { Text = Text, FontSize = 12, Width = Width, TextAlignment = Alignment };
        Label.Classes.Add("SecondaryText");
        Canvas.SetLeft(Label, X);
        Canvas.SetTop(Label, Y);
        Surface.Children.Add(Label);
    }
    /// <summary>
    /// Adds a theme-aware horizontal grid line to the plot.
    /// </summary>
    /// <param name="Plot">The data canvas.</param>
    /// <param name="Width">The plot width.</param>
    /// <param name="Y">The grid-line height.</param>
    void AddGridLine(Canvas Plot, double Width, double Y)
    {
        Avalonia.Controls.Shapes.Path Line = new()
        {
            Data = new LineGeometry(new Point(20, Y), new Point(Width - 20, Y)), StrokeThickness = 1, IsHitTestVisible = false
        };
        Line.Classes.Add("WorldBankGrid");
        Plot.Children.Add(Line);
    }
    /// <summary>
    /// Draws only reported observations, leaving null or absent calendar years disconnected.
    /// </summary>
    void DisplayChart()
    {
        foreach (Button Choice in fPeriods.Children.OfType<Button>())
        {
            Choice.Classes.Remove("Selected");
            if (Choice.Tag is int Years && Years == fYears)
                Choice.Classes.Add("Selected");
        }
        List<(int Year, decimal? Value)> Available = fPoints.Where(Point => Point.Value.HasValue).ToList();
        if (Available.Count == 0)
        {
            fRangeText.Text = "No annual observations are available for this country and indicator.";
            fSelectedText.Text = string.Empty;
            TextBlock Empty = new() { Text = "No chart data", TextWrapping = TextWrapping.Wrap, Margin = new Thickness(12) };
            Empty.Classes.Add("SecondaryText");
            fChart.Child = Empty;
            return;
        }
        int EndYear = Available[Available.Count - 1].Year;
        int StartYear = fYears == 0 ? Available[0].Year : Math.Max(Available[0].Year, EndYear - fYears + 1);
        List<(int Year, decimal? Value)> Window = fPoints.Where(Point => Point.Year >= StartYear && Point.Year <= EndYear).ToList();
        List<double> Values = Window.Where(Point => Point.Value.HasValue).Select(Point => (double)Point.Value.Value).ToList();
        double Minimum = Values.Min();
        double Maximum = Values.Max();
        double Padding = Maximum == Minimum ? Math.Max(Math.Abs(Maximum) * 0.05, 1) : (Maximum - Minimum) * 0.1;
        Minimum -= Padding;
        Maximum += Padding;
        if (WorldBankService.GetSourceId(fIndicatorCode) == "3")
        {
            Minimum = 0;
            Maximum = 100;
        }
        else if (fIndicatorCode == "VC.IHR.PSRC.P5")
            Minimum = Math.Max(0, Minimum);
        double Width = Math.Max(260, (EndYear - StartYear) * 32 + 40);
        Canvas Axis = new() { Width = 64, Height = 260 };
        Canvas Plot = new() { Width = Width, Height = 260 };
        for (int Index = 0; Index <= 4; Index++)
        {
            double Y = 20 + Index * 45;
            double Tick = Maximum - (Maximum - Minimum) * Index / 4;
            AddLabel(Axis, WorldBankDisplay.Axis(Tick), 0, Y - 8, 58, TextAlignment.Right);
            AddGridLine(Plot, Width, Y);
        }
        if (Minimum < 0 && Maximum > 0)
            AddGridLine(Plot, Width, 20 + Maximum / (Maximum - Minimum) * 180);
        Dictionary<int, decimal?> ByYear = Window.ToDictionary(Point => Point.Year, Point => Point.Value);
        StreamGeometry Geometry = new();
        List<Button> PointButtons = new();
        using (StreamGeometryContext Context = Geometry.Open())
        {
            bool Connected = false;
            Point Previous = default;
            for (int Year = StartYear; Year <= EndYear; Year++)
            {
                double X = EndYear == StartYear ? Width / 2 : 20 + (Year - StartYear) * (Width - 40) / (EndYear - StartYear);
                AddLabel(Plot, Year.ToString(CultureInfo.InvariantCulture), X - 20, 218, 40, TextAlignment.Center);
                if (!ByYear.TryGetValue(Year, out decimal? Observation) || !Observation.HasValue)
                {
                    if (Connected)
                        Context.EndFigure(false);
                    Connected = false;
                    continue;
                }
                Point Current = new(X, 20 + (Maximum - (double)Observation.Value) / (Maximum - Minimum) * 180);
                if (!Connected)
                    Context.BeginFigure(Current, false);
                else
                {
                    double Middle = (Previous.X + Current.X) / 2;
                    Context.CubicBezierTo(new Point(Middle, Previous.Y), new Point(Middle, Current.Y), Current, true);
                }
                Previous = Current;
                Connected = true;
                Border Marker = new() { Width = 8, Height = 8, CornerRadius = new CornerRadius(4) };
                Marker.Classes.Add("WorldBankMarker");
                Button Action = new() { Content = Marker, Width = 28, Height = 28, MinWidth = 0, MinHeight = 0, Padding = new Thickness(0) };
                Action.Classes.Add("WorldBankPoint");
                if (Year == EndYear)
                    Action.Classes.Add("Selected");
                string Caption = Year.ToString(CultureInfo.InvariantCulture) + ": " + WorldBankDisplay.Value(fIndicatorCode, Observation, true);
                Avalonia.Automation.AutomationProperties.SetName(Action, Caption);
                ToolTip.SetTip(Action, Caption);
                Action.Click += (Sender, Args) =>
                {
                    fSelectedText.Text = Caption;
                    foreach (Button Item in PointButtons)
                        Item.Classes.Remove("Selected");
                    Action.Classes.Add("Selected");
                };
                Canvas.SetLeft(Action, Current.X - 14);
                Canvas.SetTop(Action, Current.Y - 14);
                Plot.Children.Add(Action);
                PointButtons.Add(Action);
            }
            if (Connected)
                Context.EndFigure(false);
        }
        Avalonia.Controls.Shapes.Path Curve = new() { Data = Geometry, StrokeThickness = 2, IsHitTestVisible = false };
        Curve.Classes.Add("WorldBankCurve");
        Plot.Children.Insert(5, Curve);
        ScrollViewer Scroller = new()
        {
            Content = Plot,
            HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto,
            VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled
        };
        bool Positioned = false;
        Scroller.Loaded += (Sender, Args) =>
        {
            if (Positioned)
                return;
            Positioned = true;
            Dispatcher.UIThread.Post(() =>
            {
                if (Scroller.IsAttachedToVisualTree())
                    Scroller.Offset = new Vector(Math.Max(0, Scroller.Extent.Width - Scroller.Viewport.Width), 0);
            }, DispatcherPriority.Loaded);
        };
        Grid Chart = new() { ColumnDefinitions = new ColumnDefinitions("64,*") };
        Chart.Children.Add(Axis);
        Grid.SetColumn(Scroller, 1);
        Chart.Children.Add(Scroller);
        fChart.Child = Chart;
        fRangeText.Text = StartYear.ToString(CultureInfo.InvariantCulture) + "–" + EndYear.ToString(CultureInfo.InvariantCulture)
            + " · window ends at the latest available observation";
        fSelectedText.Text = EndYear.ToString(CultureInfo.InvariantCulture) + ": "
            + WorldBankDisplay.Value(fIndicatorCode, Available[Available.Count - 1].Value, true);
    }
    /// <summary>
    /// Changes the displayed calendar-year window using the retained series, without another API request.
    /// </summary>
    void OnPeriodClick(object Sender, RoutedEventArgs Args)
    {
        if (Sender is Button Choice && Choice.Tag is int Years)
        {
            fYears = Years;
            DisplayChart();
        }
    }

    // ● constructor
    /// <summary>
    /// Creates an indicator chart from a country's already loaded annual observations.
    /// </summary>
    /// <param name="Country">The selected country.</param>
    /// <param name="IndicatorCode">The configured indicator identifier.</param>
    /// <param name="Points">The annual observations, with explicit nulls for missing data.</param>
    /// <param name="Updated">The selected source database update date.</param>
    public WorldBankChartForm(CountryModel Country, string IndicatorCode, IReadOnlyList<(int Year, decimal? Value)> Points, string Updated)
    {
        ArgumentNullException.ThrowIfNull(Country);
        ArgumentNullException.ThrowIfNull(Points);
        fIndicatorCode = IndicatorCode;
        fPoints = Points.OrderBy(Point => Point.Year).ToList().AsReadOnly();
        var Definition = WorldBankService.Indicators[IndicatorCode];
        Classes.Add("FormBackground");
        Classes.Add("CompactTitle");
        StackPanel Body = new() { Spacing = 12, Margin = new Thickness(16) };
        TextBlock Title = new() { Text = Country.Name + " — " + Definition.Name, TextWrapping = TextWrapping.Wrap };
        Title.Classes.Add("FormTitle");
        Body.Children.Add(Title);
        var Latest = fPoints.LastOrDefault(Point => Point.Value.HasValue);
        TextBlock LatestText = new()
        {
            Text = Latest.Value.HasValue ? WorldBankDisplay.Value(IndicatorCode, Latest.Value)
                + " · " + Latest.Year.ToString(CultureInfo.InvariantCulture) : "No observations available",
            TextWrapping = TextWrapping.Wrap
        };
        LatestText.Classes.Add("ListItemTitle");
        Body.Children.Add(LatestText);
        TextBlock Description = new() { Text = Definition.Description + "\nUnit: " + Definition.Unit, TextWrapping = TextWrapping.Wrap };
        Description.Classes.Add("BodyText");
        Body.Children.Add(Description);
        foreach (var Choice in new[] { (Years: 10, Caption: "10 years"), (Years: 20, Caption: "20 years"), (Years: 0, Caption: "All") })
        {
            Button Action = new() { Content = Choice.Caption, Tag = Choice.Years };
            Action.Classes.Add("PrimaryButton");
            Action.Classes.Add("WorldBankPeriod");
            Action.Click += OnPeriodClick;
            fPeriods.Children.Add(Action);
        }
        Body.Children.Add(fPeriods);
        fRangeText.Classes.Add("SecondaryText");
        Body.Children.Add(fRangeText);
        fChart.Classes.Add("WorldBankChart");
        Body.Children.Add(fChart);
        fSelectedText.Classes.Add("ListItemTitle");
        Body.Children.Add(fSelectedText);
        TextBlock Hint = new()
        {
            Text = "Tap a point for its year and value. Swipe horizontally for more years. Gaps mean missing data; zero remains a reported value. K = thousand, M = million, B = billion. Curves connect annual observations; intermediate values are not additional measurements.",
            TextWrapping = TextWrapping.Wrap
        };
        Hint.Classes.Add("SecondaryText");
        Body.Children.Add(Hint);
        TextBlock Source = new()
        {
            Text = "World Bank · " + WorldBankService.GetSourceName(WorldBankService.GetSourceId(IndicatorCode))
                + (string.IsNullOrWhiteSpace(Updated) ? string.Empty : "\nDatabase updated: " + Updated),
            TextWrapping = TextWrapping.Wrap
        };
        Source.Classes.Add("SecondaryText");
        Body.Children.Add(Source);
        Body.Children.Add(new HyperlinkButton
        {
            Content = "Indicator, sources and methodology",
            NavigateUri = new Uri(WorldBankService.GetSourceId(IndicatorCode) == "3"
                ? "https://www.worldbank.org/en/publication/worldwide-governance-indicators"
                : "https://data.worldbank.org/indicator/" + IndicatorCode + "?locations=" + Uri.EscapeDataString(Country.Alpha2Code))
        });
        Body.Children.Add(new HyperlinkButton { Content = "World Bank data terms", NavigateUri = new Uri("https://datacatalog.worldbank.org/public-licenses") });
        Body.Children.Add(new HyperlinkButton { Content = "CC BY 4.0", NavigateUri = new Uri("https://creativecommons.org/licenses/by/4.0/") });
        Content = new ScrollViewer { Content = Body, HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled };
        DisplayChart();
    }
}
