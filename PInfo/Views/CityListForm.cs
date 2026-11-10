/* PInfo, Copyright (c) 2026 Theo Bebekis, see License. */

namespace PInfo.Views;

/// <summary>
/// Displays selectable SQLite cities with detail-card navigation, search, and optional paging.
/// </summary>
public class CityListForm : AppListForm
{
    // ● private
    readonly bool fBigCities;
    readonly int fPageSize;
    readonly Button fPrevious = new() { Content = "←", IsEnabled = false, MinWidth = 44, MinHeight = 44 };
    readonly Button fNext = new() { Content = "→", IsEnabled = false, MinWidth = 44, MinHeight = 44 };
    readonly TextBlock fPageText = new() { VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
        HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center };
    int fPage;
    int fQueryVersion;
    bool fReady;
    readonly string fCountryCode;
    readonly string fCountryName;

    /// <summary>
    /// Creates a compact selectable row whose name follows its data context during recycling.
    /// </summary>
    /// <param name="City">The initial item, or null during container cleanup.</param>
    /// <returns>The bound list row, or null during cleanup.</returns>
    Control CreateItem(CityModel City)
    {
        if (City == null)
            return null;
        TextBlock Name = new();
        Name.Classes.Add("ListItemTitle");
        Name.Bind(TextBlock.TextProperty, new ReflectionBinding(".")
        {
            Converter = new FuncValueConverter<CityModel, string>(Item => Item == null ? string.Empty
                : string.IsNullOrWhiteSpace(Item.AsciiName) ? Item.Name : Item.AsciiName.Trim())
        });
        TextBlock Area = new() { FontSize = 12, TextWrapping = TextWrapping.Wrap };
        Area.Classes.Add("SecondaryText");
        Area.Bind(TextBlock.TextProperty, new ReflectionBinding("AdministrativeArea"));
        Area.Bind(TextBlock.IsVisibleProperty, new ReflectionBinding("AdministrativeArea")
        {
            Converter = new FuncValueConverter<string, bool>(Value => !string.IsNullOrWhiteSpace(Value))
        });
        StackPanel Content = new() { Spacing = 2 };
        Content.Children.Add(Name);
        Content.Children.Add(Area);
        Border Card = new() { Child = Content };
        Card.Classes.Add("ListItem");
        return Card;
    }
    /// <summary>
    /// Reads only the requested page off the UI thread and ignores superseded search results.
    /// </summary>
    async Task DisplayPageAsync(bool Debounce)
    {
        int Version = ++fQueryVersion;
        string Search = SearchText;
        int Page = fPage;
        fPrevious.IsEnabled = false;
        fNext.IsEnabled = false;
        try
        {
            if (Debounce)
                await Task.Delay(250);
            if (Version != fQueryVersion)
                return;
            var Result = await Task.Run(() => Db.GetPlaceCities(fCountryCode, fBigCities, Search, Page, PageSize: fPageSize));
            if (Version != fQueryVersion)
                return;
            fPage = Result.Page;
            ItemList.ItemsSource = Result.Items;
            SetResultCount(Result.Total, "city", "cities");
            int PageCount = Math.Max(1, (Result.Total + fPageSize - 1) / fPageSize);
            fPageText.Text = $"Page {fPage + 1} / {PageCount}";
            fPrevious.IsEnabled = !fBigCities && fPage > 0;
            fNext.IsEnabled = !fBigCities && fPage + 1 < PageCount;
            if (Result.Items.Count > 0)
                ItemList.ScrollIntoView(Result.Items[0]);
        }
        catch (Exception Ex)
        {
            if (Version != fQueryVersion)
                return;
            ExceptionLog.WriteException("CityListForm.DisplayPageAsync", Ex, false);
            ShowNotice("Cities could not be read. Please try again.", true);
        }
    }
    /// <summary>
    /// Moves to the previous or next page without changing the current search.
    /// </summary>
    async void OnPageClick(object Sender, RoutedEventArgs Args)
    {
        fPage += Sender == fNext ? 1 : -1;
        await DisplayPageAsync(false);
    }

    // ● overridables
    /// <summary>
    /// Opens the selected item's existing data in a detail card while retaining this list for Back.
    /// </summary>
    /// <param name="Item">The selected list item.</param>
    protected override void OnItemSelected(object Item)
    {
        if (Item is CityModel City && MainView.Current != null)
            MainView.Current.PushForm(new CityCardForm(City));
    }
    /// <summary>
    /// Ensures this country's GeoNames data is available in SQLite, using stored data when offline.
    /// </summary>
    /// <param name="CancellationToken">Cancels the download or import.</param>
    /// <returns>The service load result.</returns>
    protected override async Task<ApiClientResult> LoadListAsync(CancellationToken CancellationToken)
    {
        fReady = false;
        ++fQueryVersion;
        ApiClientResult Result = await GeoNamesService.LoadPlacesAsync(fCountryCode, CancellationToken, CreateLoadProgress());
        fReady = Result.ResultStatus == ApiClientResultStatus.Success && !CancellationToken.IsCancellationRequested;
        return Result;
    }
    /// <summary>
    /// Restarts the current SQLite search at the first page.
    /// </summary>
    protected override async void ApplySearch()
    {
        if (!fReady)
            return;
        fPage = 0;
        await DisplayPageAsync(true);
    }

    // ● constructor
    /// <summary>
    /// Creates the selected city list using compact selectable rows.
    /// </summary>
    /// <param name="Country">The selected country; its alpha-2 code is required.</param>
    /// <param name="BigCities">Shows up to 100 large cities; otherwise enables pages of all populated places.</param>
    public CityListForm(CountryModel Country, bool BigCities = true)
    {
        ArgumentNullException.ThrowIfNull(Country);
        ArgumentException.ThrowIfNullOrWhiteSpace(Country.Alpha2Code);
        fCountryCode = Country.Alpha2Code.Trim().ToUpperInvariant();
        fCountryName = Country.Name;
        if (fCountryCode.Length != 2 || fCountryCode.Any(Character => Character < 'A' || Character > 'Z'))
            throw new ArgumentException("The country must have a two-letter ISO code.", nameof(Country));

        fBigCities = BigCities;
        fPageSize = BigCities ? 100 : Math.Clamp(Lib.Settings.ItemsPerPage, 10, 100);
        Classes.Add("CompactTitle");
        SetHeader(fCountryName + (fBigCities ? " - Big Cities" : " - All Cities"),
            string.Empty, "Search by city name or alternative name");
        Grid Pager = new()
        {
            ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto"),
            Margin = new Thickness(0, 6, 0, 0),
            IsVisible = !fBigCities
        };
        fPrevious.Classes.Add("PrimaryButton");
        fPrevious.Classes.Add("PageNavigation");
        fNext.Classes.Add("PrimaryButton");
        fNext.Classes.Add("PageNavigation");
        Avalonia.Automation.AutomationProperties.SetName(fPrevious, "Previous page");
        Avalonia.Automation.AutomationProperties.SetName(fNext, "Next page");
        Grid.SetColumn(fPageText, 1);
        Grid.SetColumn(fNext, 2);
        Pager.Children.Add(fPrevious);
        Pager.Children.Add(fPageText);
        Pager.Children.Add(fNext);
        if (ItemList.Parent is Grid ListPanel)
        {
            if (!fBigCities)
            {
                ListPanel.RowDefinitions = new RowDefinitions("*,Auto");
                Grid.SetRow(Pager, 1);
                ListPanel.Children.Add(Pager);
            }
        }
        fPrevious.Click += OnPageClick;
        fNext.Click += OnPageClick;
        EmptyMessage = "No cities are available or match your search.";
        ItemList.ItemTemplate = new FuncDataTemplate<CityModel>(
            (City, Scope) => CreateItem(City), supportsRecycling: true);
    }

    // ● properties
    /// <summary>
    /// The status text displayed while loading cities.
    /// </summary>
    protected override string LoadingText => "Loading cities...";
    /// <summary>
    /// The status text displayed when loading cities fails.
    /// </summary>
    protected override string LoadErrorText => "Cities could not be loaded.";
}
