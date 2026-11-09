/* PInfo, Copyright (c) 2026 Theo Bebekis, see License. */

namespace PInfo.Views;

/// <summary>
/// Displays up to 100 cached top cities for a country, including all available fields and map links.
/// </summary>
public class CityListForm : AppListForm
{
    // ● private
    readonly CountryService fService;
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

    // ● overridables
    /// <summary>
    /// Loads this country's top cities from the shared service and its three-month cache.
    /// </summary>
    /// <param name="CancellationToken">Cancels this load or its wait for another load.</param>
    /// <returns>The service load result.</returns>
    protected override Task<ApiClientResult> LoadListAsync(CancellationToken CancellationToken)
    {
        return fService.LoadCitiesAsync(fCountryCode, CancellationToken);
    }
    /// <summary>
    /// Filters the downloaded cities locally and displays them alphabetically by name.
    /// </summary>
    protected override void ApplySearch()
    {
        string Search = SearchText;
        IReadOnlyList<CityModel> Cities = fService.GetCities(fCountryCode);
        SetHeader(fCountryName + " - " + Cities.Count + (Cities.Count == 1 ? " City" : " Cities"),
            string.Empty, "Search by city name or administrative code");
        List<CityModel> Items = Cities.Where(Item =>
            (Item.Name ?? string.Empty).Contains(Search, StringComparison.OrdinalIgnoreCase)
            || (Item.AsciiName ?? string.Empty).Contains(Search, StringComparison.OrdinalIgnoreCase)
            || (Item.Admin1Code ?? string.Empty).Contains(Search, StringComparison.OrdinalIgnoreCase))
            .OrderBy(Item => Item.Name, StringComparer.CurrentCultureIgnoreCase).ToList();
        ItemList.ItemsSource = Items;
        SetResultCount(Items.Count, "city", "cities");
    }

    // ● constructor
    /// <summary>
    /// Creates the top-city list for the selected country using the shared base-form layout.
    /// </summary>
    /// <param name="Country">The selected country; its alpha-2 code is required.</param>
    public CityListForm(CountryModel Country)
    {
        ArgumentNullException.ThrowIfNull(Country);
        ArgumentException.ThrowIfNullOrWhiteSpace(Country.Alpha2Code);
        fCountryCode = Country.Alpha2Code.Trim().ToUpperInvariant();
        fCountryName = Country.Name;
        if (fCountryCode.Length != 2 || fCountryCode.Any(Character => Character < 'A' || Character > 'Z'))
            throw new ArgumentException("The country must have a two-letter ISO code.", nameof(Country));

        fService = Lib.Countries;
        Classes.Add("CompactTitle");
        Classes.Add("HideResultCount");
        SetHeader(fCountryName, string.Empty,
            "Search by city name or administrative code");
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
