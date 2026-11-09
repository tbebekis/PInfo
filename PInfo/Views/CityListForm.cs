/* PInfo, Copyright (c) 2026 Theo Bebekis, see License. */

namespace PInfo.Views;

/// <summary>
/// Displays SQLite cities with expandable details, map links, search, and optional paging.
/// </summary>
public class CityListForm : AppListForm
{
    // ● private
    readonly bool fBigCities;
    readonly Button fPrevious = new() { Content = "Previous", IsEnabled = false };
    readonly Button fNext = new() { Content = "Next", IsEnabled = false };
    readonly TextBlock fPageText = new() { VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center };
    int fPage;
    int fQueryVersion;
    bool fReady;
    readonly string fCountryCode;
    readonly string fCountryName;
    readonly HashSet<long> fExpandedCities = new();

    /// <summary>
    /// Adds a labeled value bound to the current city so recycled rows display the correct data.
    /// Empty string values hide the entire field.
    /// </summary>
    void AddCityField(StackPanel Panel, string Caption, string PropertyName, string Format = "{0}")
    {
        StackPanel Field = new();
        Field.Classes.Add("CardField");
        TextBlock Label = new() { Text = Caption };
        Label.Classes.Add("FieldLabel");
        TextBlock Value = new();
        Value.Classes.Add("FieldValue");
        Value.Bind(TextBlock.TextProperty, new ReflectionBinding(PropertyName) { StringFormat = Format });
        Field.Bind(IsVisibleProperty, new ReflectionBinding(PropertyName)
        {
            Converter = new FuncValueConverter<object, bool>(Item => Item != null
                && (Item is not string Text || !string.IsNullOrWhiteSpace(Text)))
        });
        Field.Children.Add(Label);
        Field.Children.Add(Value);
        Panel.Children.Add(Field);
    }
    /// <summary>
    /// Builds a Google Maps search URL from valid coordinates using culture-independent formatting.
    /// </summary>
    Uri GetMapUri(CityModel City)
    {
        if (City == null || !double.IsFinite(City.Latitude) || !double.IsFinite(City.Longitude)
            || City.Latitude < -90 || City.Latitude > 90 || City.Longitude < -180 || City.Longitude > 180)
            return null;
        string Latitude = City.Latitude.ToString("R", CultureInfo.InvariantCulture);
        string Longitude = City.Longitude.ToString("R", CultureInfo.InvariantCulture);
        return new Uri("https://www.google.com/maps/search/?api=1&query=" + Latitude + "," + Longitude);
    }
    /// <summary>
    /// Creates a detailed city row. A null item during container cleanup produces no control.
    /// All city-dependent content follows DataContext changes when recycling.
    /// </summary>
    Control CreateItem(CityModel City)
    {
        if (City == null)
            return null;

        StackPanel Panel = new();
        Panel.Classes.Add("CardFields");
        TextBlock Name = new();
        Name.Classes.Add("ListItemTitle");
        Name.Bind(TextBlock.TextProperty, new ReflectionBinding(nameof(CityModel.Name)));

        TextBlock AsciiName = new();
        AsciiName.Classes.Add("CardSubtitle");
        AsciiName.Bind(TextBlock.TextProperty, new ReflectionBinding(nameof(CityModel.AsciiName)));
        AsciiName.Bind(IsVisibleProperty, new ReflectionBinding(".")
        {
            Converter = new FuncValueConverter<CityModel, bool>(Item => Item != null
                && !string.IsNullOrWhiteSpace(Item.AsciiName)
                && !string.Equals(Item.Name, Item.AsciiName, StringComparison.Ordinal))
        });
        Panel.Children.Add(AsciiName);

        AddCityField(Panel, "Population", nameof(CityModel.Population), "{0:N0}");
        AddCityField(Panel, "Country code", nameof(CityModel.CountryCode));
        AddCityField(Panel, "Administrative code", nameof(CityModel.Admin1Code));
        AddCityField(Panel, "Time zone", nameof(CityModel.Timezone));
        AddCityField(Panel, "Latitude", nameof(CityModel.Latitude), "{0:0.#####}°");
        AddCityField(Panel, "Longitude", nameof(CityModel.Longitude), "{0:0.#####}°");
        AddCityField(Panel, "Feature code", nameof(CityModel.FeatureCode));
        AddCityField(Panel, "GeoNames ID", nameof(CityModel.GeonameId), "{0:0}");

        HyperlinkButton MapLink = new() { Content = "Open Google Maps" };
        MapLink.Classes.Add("CardAction");
        MapLink.Bind(HyperlinkButton.NavigateUriProperty, new ReflectionBinding(".")
        {
            Converter = new FuncValueConverter<CityModel, Uri>(GetMapUri)
        });
        Panel.Children.Add(MapLink);

        Expander CityExpander = new() { Header = Name, Content = Panel };
        CityExpander.Classes.Add("ListItem");
        bool RestoringExpansion = false;
        CityExpander.DataContextChanged += (Sender, Args) =>
        {
            // A recycled row must restore the new city's state without changing the saved state.
            RestoringExpansion = true;
            try
            {
                CityExpander.IsExpanded = CityExpander.DataContext is CityModel CurrentCity
                    && fExpandedCities.Contains(CurrentCity.GeonameId);
            }
            finally
            {
                RestoringExpansion = false;
            }
        };
        CityExpander.PropertyChanged += (Sender, Args) =>
        {
            if (Args.Property != Expander.IsExpandedProperty || RestoringExpansion
                || CityExpander.DataContext is not CityModel CurrentCity)
                return;

            if (CityExpander.IsExpanded)
                fExpandedCities.Add(CurrentCity.GeonameId);
            else
                fExpandedCities.Remove(CurrentCity.GeonameId);
        };

        return CityExpander;
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
            var Result = await Task.Run(() => Db.GetPlaceCities(fCountryCode, fBigCities, Search, Page));
            if (Version != fQueryVersion)
                return;
            fPage = Result.Page;
            ItemList.ItemsSource = Result.Items;
            SetResultCount(Result.Total, "city", "cities");
            int PageCount = Math.Max(1, (Result.Total + 49) / 50);
            fPageText.Text = $"{fPage + 1} / {PageCount}";
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
    /// Ensures this country's GeoNames data is available in SQLite, using stored data when offline.
    /// </summary>
    /// <param name="CancellationToken">Cancels the download or import.</param>
    /// <returns>The service load result.</returns>
    protected override async Task<ApiClientResult> LoadListAsync(CancellationToken CancellationToken)
    {
        fReady = false;
        ++fQueryVersion;
        ApiClientResult Result = await GeoNamesService.LoadPlacesAsync(fCountryCode, CancellationToken);
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
    /// Creates the selected city list using the existing expandable row layout.
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
        Classes.Add("CompactTitle");
        SetHeader(fCountryName + (fBigCities ? " - Big Cities" : " - All Cities"),
            "GeoNames", "Search by city name or alternative name");
        StackPanel Pager = new()
        {
            Orientation = Avalonia.Layout.Orientation.Horizontal,
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Bottom,
            Spacing = 12,
            IsVisible = !fBigCities
        };
        Pager.Children.Add(fPrevious);
        Pager.Children.Add(fPageText);
        Pager.Children.Add(fNext);
        if (ItemList.Parent is Grid ListPanel)
        {
            if (!fBigCities)
            {
                ListPanel.RowDefinitions = new RowDefinitions("*,Auto");
                Pager.Margin = new Thickness(0, 12, 0, 0);
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
