/* PInfo, Copyright (c) 2026 Theo Bebekis, see License. */

namespace PInfo.Views;

/// <summary>
/// Lists development and governance indicators together, with independent source failures and retained chart navigation.
/// </summary>
public class WorldBankListForm : AppListForm
{
    // ● private
    readonly CountryModel fCountry;
    readonly int fPageSize;
    readonly Button fPrevious = new() { Content = "←", IsEnabled = false, MinWidth = 44, MinHeight = 44 };
    readonly Button fNext = new() { Content = "→", IsEnabled = false, MinWidth = 44, MinHeight = 44 };
    readonly TextBlock fPageText = new()
    {
        VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
        HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center
    };
    IReadOnlyDictionary<string, IReadOnlyList<(int Year, decimal? Value)>> fSeries = new Dictionary<string, IReadOnlyList<(int Year, decimal? Value)>>();
    readonly Dictionary<string, string> fUpdates = new(StringComparer.Ordinal);
    string fWarnings = string.Empty;
    int fPage;

    /// <summary>
    /// Creates a row distinguishing unavailable sources from valid series with no observations.
    /// </summary>
    /// <param name="Code">The indicator code, or null during cleanup.</param>
    /// <returns>The indicator row, or null.</returns>
    Control CreateItem(string Code)
    {
        if (string.IsNullOrWhiteSpace(Code))
            return null;
        var Definition = WorldBankService.Indicators[Code];
        TextBlock Name = new() { Text = Definition.Name, TextWrapping = TextWrapping.Wrap };
        Name.Classes.Add("ListItemTitle");
        string Text = "Source unavailable · press Try again";
        if (fSeries.TryGetValue(Code, out IReadOnlyList<(int Year, decimal? Value)> Points))
        {
            var Latest = Points.LastOrDefault(Point => Point.Value.HasValue);
            Text = Latest.Value.HasValue ? WorldBankDisplay.Value(Code, Latest.Value) + " · "
                + Latest.Year.ToString(CultureInfo.InvariantCulture) : "No observations available";
        }
        TextBlock LatestText = new()
        {
            Text = (WorldBankService.GetSourceId(Code) == "3" ? "WGI · " : "WDI · ") + Text,
            FontSize = 12, TextWrapping = TextWrapping.Wrap
        };
        LatestText.Classes.Add("SecondaryText");
        StackPanel Labels = new() { Spacing = 2 };
        Labels.Children.Add(Name);
        Labels.Children.Add(LatestText);
        Border Card = new() { Child = Labels };
        Card.Classes.Add("ListItem");
        return Card;
    }
    /// <summary>
    /// Displays the locally filtered page and source-specific database dates.
    /// </summary>
    void DisplayPage()
    {
        if (fSeries.Count == 0)
        {
            ItemList.ItemsSource = null;
            return;
        }
        List<string> Codes = WorldBankService.Indicators.Where(Item => Item.Value.Name.Contains(SearchText, StringComparison.OrdinalIgnoreCase)
            || Item.Value.Description.Contains(SearchText, StringComparison.OrdinalIgnoreCase)
            || Item.Key.Contains(SearchText, StringComparison.OrdinalIgnoreCase)).Select(Item => Item.Key).ToList();
        fPage = Math.Clamp(fPage, 0, Math.Max(0, (Codes.Count - 1) / fPageSize));
        ItemList.ItemsSource = Codes.Skip(fPage * fPageSize).Take(fPageSize).ToList();
        SetResultCount(Codes.Count, "indicator", "indicators");
        string Updates = string.Join(" · ", fUpdates.Where(Item => !string.IsNullOrWhiteSpace(Item.Value))
            .Select(Item => (Item.Key == "3" ? "WGI: " : "WDI: ") + Item.Value));
        SetHeader(fCountry.Name + " — World Bank", "Latest available annual observations"
            + (Updates.Length == 0 ? string.Empty : "\nDatabase updates — " + Updates), "Search indicators");
        int PageCount = Math.Max(1, (Codes.Count + fPageSize - 1) / fPageSize);
        fPageText.Text = $"Page {fPage + 1} / {PageCount}";
        fPrevious.IsEnabled = fPage > 0;
        fNext.IsEnabled = fPage + 1 < PageCount;
        if (!string.IsNullOrWhiteSpace(fWarnings))
            ShowNotice(fWarnings, fSeries.Count < WorldBankService.Indicators.Count);
    }
    /// <summary>
    /// Changes the local page without another source request.
    /// </summary>
    void OnPageClick(object Sender, RoutedEventArgs Args)
    {
        fPage += Sender == fNext ? 1 : -1;
        DisplayPage();
        if (ItemList.ItemCount > 0)
            ItemList.ScrollIntoView(ItemList.Items[0]);
    }

    // ● overridables
    /// <summary>
    /// Loads the two datasets independently, retaining usable series when only one source succeeds.
    /// </summary>
    /// <param name="CancellationToken">Cancels both source requests.</param>
    /// <returns>A successful or partial outcome, or an error when neither dataset is available.</returns>
    protected override async Task<ApiClientResult> LoadListAsync(CancellationToken CancellationToken)
    {
        fPrevious.IsEnabled = false;
        fNext.IsEnabled = false;
        string[] Sources = { "2", "3" };
        ApiClientResult[] Results = await Task.WhenAll(Sources.Select(Source =>
            WorldBankService.LoadCountryAsync(fCountry.Alpha3Code, CancellationToken, Source)));
        CancellationToken.ThrowIfCancellationRequested();
        Dictionary<string, IReadOnlyList<(int Year, decimal? Value)>> Series = new(StringComparer.Ordinal);
        Dictionary<string, string> Updates = new(StringComparer.Ordinal);
        List<string> Warnings = new();
        for (int Index = 0; Index < Results.Length; Index++)
        {
            ApiClientResult Result = Results[Index];
            string Source = Sources[Index];
            if (Result.ResultStatus == ApiClientResultStatus.Success)
            {
                var Data = WorldBankService.ReadSeries(Result.ResponseJsonText, fCountry.Alpha3Code, out string Updated, Source);
                foreach (var Item in Data)
                    Series.Add(Item.Key, Item.Value);
                Updates[Source] = Updated;
            }
            if (!string.IsNullOrWhiteSpace(Result.ReasonPhrase))
                Warnings.Add(WorldBankService.GetSourceName(Source) + ": " + Result.ReasonPhrase);
        }
        CancellationToken.ThrowIfCancellationRequested();
        if (Series.Count > 0)
        {
            fSeries = Series;
            fUpdates.Clear();
            foreach (var Item in Updates)
                fUpdates.Add(Item.Key, Item.Value);
        }
        fWarnings = string.Join("\n", Warnings);
        return new ApiClientResult(string.Empty)
        {
            ResultStatus = Series.Count > 0 ? ApiClientResultStatus.Success : ApiClientResultStatus.Error,
            ReasonPhrase = Series.Count > 0 ? string.Empty : fWarnings
        };
    }
    /// <summary>
    /// Restarts paging after filtering the indicator names and descriptions locally.
    /// </summary>
    protected override void ApplySearch()
    {
        fPage = 0;
        DisplayPage();
        if (ItemList.ItemCount > 0)
            ItemList.ScrollIntoView(ItemList.Items[0]);
    }
    /// <summary>
    /// Opens the selected series with its own source's update date, preserving this list for Back.
    /// </summary>
    /// <param name="Item">The selected indicator code.</param>
    protected override void OnItemSelected(object Item)
    {
        if (Item is string Code && fSeries.TryGetValue(Code, out IReadOnlyList<(int Year, decimal? Value)> Points))
        {
            fUpdates.TryGetValue(WorldBankService.GetSourceId(Code), out string Updated);
            MainView.Current?.PushForm(new WorldBankChartForm(fCountry, Code, Points, Updated));
        }
    }

    // ● constructor
    /// <summary>
    /// Creates a country's combined indicator list without requesting data until the view is shown.
    /// </summary>
    /// <param name="Country">The country with its ISO alpha-3 identifier.</param>
    public WorldBankListForm(CountryModel Country)
    {
        ArgumentNullException.ThrowIfNull(Country);
        fCountry = Country;
        fPageSize = Math.Clamp(Lib.Settings.ItemsPerPage, 10, 100);
        Classes.Add("CompactTitle");
        SetHeader(Country.Name + " — World Bank", "Development, governance and homicide indicators", "Search indicators");
        EmptyMessage = "No indicators match your search.";
        ItemList.ItemTemplate = new FuncDataTemplate<string>((Code, Scope) => CreateItem(Code));
        Grid Paging = new() { ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto"), Margin = new Thickness(0, 6, 0, 0) };
        fPrevious.Classes.Add("PrimaryButton");
        fPrevious.Classes.Add("PageNavigation");
        fNext.Classes.Add("PrimaryButton");
        fNext.Classes.Add("PageNavigation");
        Avalonia.Automation.AutomationProperties.SetName(fPrevious, "Previous page");
        Avalonia.Automation.AutomationProperties.SetName(fNext, "Next page");
        Grid.SetColumn(fPageText, 1);
        Grid.SetColumn(fNext, 2);
        Paging.Children.Add(fPrevious);
        Paging.Children.Add(fPageText);
        Paging.Children.Add(fNext);
        if (ItemList.Parent is Grid ListPanel)
        {
            Control[] ExistingChildren = ListPanel.Children.ToArray();
            ListPanel.RowDefinitions = new RowDefinitions("*,Auto");
            foreach (Control Child in ExistingChildren)
                Grid.SetRow(Child, 0);
            Grid.SetRow(Paging, 1);
            ListPanel.Children.Add(Paging);
        }
        fPrevious.Click += OnPageClick;
        fNext.Click += OnPageClick;
    }

    // ● properties
    /// <summary>
    /// The source loading message.
    /// </summary>
    protected override string LoadingText => "Loading development and governance indicators...";
    /// <summary>
    /// The source failure message.
    /// </summary>
    protected override string LoadErrorText => "World Bank indicators could not be loaded.";
}
