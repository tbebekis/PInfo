/* PInfo, Copyright (c) 2026 Theo Bebekis, see License. */

namespace PInfo.Views;

/// <summary>
/// Shows a country public calendar with local year selection, filtering and paging.
/// </summary>
public class HolidayListForm : AppListForm
{
    // ● private
    readonly CountryModel fCountry;
    readonly ComboBox fYear = new();
    string fCoverage = string.Empty;
    readonly int fPageSize;
    readonly Button fPrevious = new() { Content = "←", IsEnabled = false };
    readonly Button fNext = new() { Content = "→", IsEnabled = false };
    readonly TextBlock fPageText = new()
    {
        VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
        HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center
    };
    IReadOnlyList<JsonElement> fItems = Array.Empty<JsonElement>();
    int fPage;

    /// <summary>
    /// Creates a compact date/weekday and holiday-name row.
    /// </summary>
    Control CreateItem(JsonElement Item)
    {
        if (Item.ValueKind != JsonValueKind.Object)
            return null;
        DateOnly Date = DateOnly.ParseExact(Item.GetProperty("date").GetString(), "yyyy-MM-dd", CultureInfo.InvariantCulture);
        TextBlock Title = new() { Text = Date.ToString("ddd dd MMM yyyy", CultureInfo.CurrentCulture), TextWrapping = TextWrapping.Wrap };
        Title.Classes.Add("ListItemTitle");
        TextBlock Context = new()
        {
            Text = Item.GetProperty("name").GetString(), FontSize = 12, TextWrapping = TextWrapping.Wrap
        };
        Context.Classes.Add("SecondaryText");
        StackPanel Text = new() { Spacing = 2 };
        Text.Children.Add(Title);
        Text.Children.Add(Context);
        Border Card = new() { Child = Text };
        Card.Classes.Add("ListItem");
        return Card;
    }
    /// <summary>
    /// Filters the selected year and holiday title before local paging.
    /// </summary>
    void DisplayPage()
    {
        int Year = fYear.SelectedItem is int Selected ? Selected : DateTime.Today.Year;
        List<JsonElement> Matches = fItems.Where(Item => Item.GetProperty("date").GetString().StartsWith(Year.ToString(CultureInfo.InvariantCulture) + "-", StringComparison.Ordinal)
            && (Item.GetProperty("name").GetString().Contains(SearchText, StringComparison.CurrentCultureIgnoreCase)
                || Item.GetProperty("date").GetString().Contains(SearchText, StringComparison.Ordinal)))
            .OrderBy(Item => Item.GetProperty("date").GetString(), StringComparer.Ordinal).ToList();
        int Count = Matches.Count;
        int PageCount = Math.Max(1, (Count + fPageSize - 1) / fPageSize);
        fPage = Math.Clamp(fPage, 0, PageCount - 1);
        ItemList.ItemsSource = Matches.Skip(fPage * fPageSize).Take(fPageSize).ToList();
        SetHeader($"{fCountry.Name} — Public Holidays ({Count:N0})", "Country-level public calendar · " + Year + "\n" + fCoverage, "Search holidays");
        SetResultCount(Count, "holiday", "holidays");
        fPageText.Text = $"Page {fPage + 1} / {PageCount}";
        fPrevious.IsEnabled = fPage > 0;
        fNext.IsEnabled = fPage + 1 < PageCount;
    }
    /// <summary>
    /// Displays the requested local page and positions its first row in view.
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
    /// Loads the selected country calendar and offers only years with published records.
    /// </summary>
    protected override async Task<ApiClientResult> LoadListAsync(CancellationToken CancellationToken)
    {
        fPrevious.IsEnabled = false;
        fNext.IsEnabled = false;
        var Loaded = await PublicHolidayService.LoadCalendarAsync(fCountry.Alpha2Code, CancellationToken);
        CancellationToken.ThrowIfCancellationRequested();
        if (Loaded.Result.ResultStatus == ApiClientResultStatus.Success)
        {
            fItems = Loaded.Items;
            List<int> Years = fItems.Select(Item => DateOnly.ParseExact(Item.GetProperty("date").GetString(), "yyyy-MM-dd", CultureInfo.InvariantCulture).Year)
                .Distinct().OrderBy(Year => Year).ToList();
            fCoverage = Years.Count == 0 ? "No dated entries are available." : $"Available years: {Years[0]}–{Years[^1]}. Local holidays may be missing.";
            fYear.ItemsSource = Years;
            if (Years.Count > 0)
                fYear.SelectedItem = Years.OrderBy(Year => Math.Abs(Year - DateTime.Today.Year)).First();
        }
        return Loaded.Result;
    }
    /// <summary>
    /// Restarts local paging when the filter changes.
    /// </summary>
    protected override void ApplySearch()
    {
        fPage = 0;
        DisplayPage();
        if (ItemList.ItemCount > 0)
            ItemList.ScrollIntoView(ItemList.Items[0]);
    }
    /// <summary>
    /// Opens a separate detail card while preserving the existing list for Back.
    /// </summary>
    protected override void OnItemSelected(object Item)
    {
        if (Item is JsonElement Holiday)
            MainView.Current?.PushForm(new HolidayDetailForm(fCountry, Holiday));
    }

    // ● constructor
    /// <summary>
    /// Creates a country calendar using the common search, theme and page-size preferences.
    /// </summary>
    /// <param name="Country">The selected country.</param>
    public HolidayListForm(CountryModel Country)
    {
        ArgumentNullException.ThrowIfNull(Country);
        fCountry = Country;
        fPageSize = Math.Clamp(Lib.Settings.ItemsPerPage, 10, 100);
        Classes.Add("CompactTitle");
        SetHeader(Country.Name + " — Public Holidays", "Select a year to see the published public calendar.", "Search holidays");
        fYear.Classes.Add("ChoiceBox");
        Avalonia.Automation.AutomationProperties.SetName(fYear, "Holiday year");
        if (this.FindControl<TextBlock>("TitleText")?.Parent is StackPanel Header)
        {
            Grid YearRow = new() { ColumnDefinitions = new ColumnDefinitions("Auto,*"), ColumnSpacing = 8 };
            YearRow.Children.Add(new TextBlock { Text = "Year", VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center });
            Grid.SetColumn(fYear, 1);
            YearRow.Children.Add(fYear);
            Header.Children.Add(YearRow);
        }
        fYear.SelectionChanged += (Sender, Args) => ApplySearch();
        EmptyMessage = "No holidays match this year and search.";
        ItemList.ItemTemplate = new FuncDataTemplate<JsonElement>((Item, Scope) => CreateItem(Item));
        Grid Paging = new() { ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto") };
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
            ListPanel.RowDefinitions = new RowDefinitions("*,Auto");
            Grid.SetRow(Paging, 1);
            Paging.Margin = new Thickness(0, 6, 0, 0);
            ListPanel.Children.Add(Paging);
        }
        fPrevious.Click += OnPageClick;
        fNext.Click += OnPageClick;
    }

    // ● properties
    /// <summary>
    /// The visible resource loading message.
    /// </summary>
    protected override string LoadingText => "Loading public holidays...";
    /// <summary>
    /// The visible resource failure message.
    /// </summary>
    protected override string LoadErrorText => "Public holidays could not be loaded.";
}
