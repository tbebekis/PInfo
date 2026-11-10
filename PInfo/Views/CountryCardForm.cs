/* PInfo, Copyright (c) 2026 Theo Bebekis, see License. */

namespace PInfo.Views;

/// <summary>
/// Displays the selected country's downloaded details without making an HTTP request.
/// </summary>
public class CountryCardForm : AppCardForm
{
    // ● private
    /// <summary>
    /// Opens the selected country's big-city or all-city list using the shared navigation stack.
    /// </summary>
    void AnyClick(object Sender, RoutedEventArgs Args)
    {
        if (Sender is Button ActionButton && ActionButton.Tag is CountryModel Country
            && (ActionButton.Name == "btnCities" || ActionButton.Name == "btnAllCities"))
            MainView.Current?.PushForm(new CityListForm(Country, ActionButton.Name == "btnCities"));
    }
    /// <summary>
    /// Builds the card from the selected model using reusable section and field styles.
    /// </summary>
    void DisplayCountry(CountryModel Country)
    {
        SetHeader(Country.Name, Country.NativeName, Country.Flag);
        SetSource("Country data from countries.dev");
        AddBadge(Country.Alpha2Code);
        AddBadge(Country.Alpha3Code);
        AddBadge(Country.Region);

        Button btnCities = new() { Name = "btnCities", Content = "Big Cities", Tag = Country };
        btnCities.Click += AnyClick;
        AddHeaderAction(btnCities);
        Button btnAllCities = new() { Name = "btnAllCities", Content = "All Cities", Tag = Country };
        btnAllCities.Click += AnyClick;
        AddHeaderAction(btnAllCities);

        Button btnPlaces = new() { Content = "Places" };
        btnPlaces.Click += (Sender, Args) => MainView.Current?.PushForm(new PlaceListForm(Country));
        AddHeaderAction(btnPlaces);

        Button Wikipedia = new() { Content = "📖 Wikipedia" };
        Wikipedia.Click += (Sender, Args) => MainView.Current?.PushForm(new WikipediaForm(Country.Name, Country.Region));
        AddHeaderAction(Wikipedia);

        Button WorldBank = new() { Content = "World Bank" };
        WorldBank.Click += (Sender, Args) => MainView.Current?.PushForm(new WorldBankListForm(Country));
        AddHeaderAction(WorldBank);

        StackPanel Section = CreateSection("Overview");
        AddField(Section, "Capital", Country.Capital);
        AddField(Section, "Region", Country.Region);
        AddField(Section, "Subregion", Country.Subregion);
        AddField(Section, "Population", Country.Population.ToString("N0", CultureInfo.CurrentCulture));
        AddField(Section, "Area", Country.Area.HasValue ? Country.Area.Value.ToString("N2", CultureInfo.CurrentCulture) + " km²" : null);
        AddField(Section, "Population density", Country.PopulationDensity.HasValue ? Country.PopulationDensity.Value.ToString("N2", CultureInfo.CurrentCulture) + " people/km²" : null);
        AddSection(Section);

        Section = CreateSection("Languages & currencies");
        AddField(Section, "Languages", JoinValues(Country.Languages?.Where(Item => Item != null).Select(Item =>
            string.IsNullOrWhiteSpace(Item.NativeName) || Item.Name == Item.NativeName ? Item.Name : $"{Item.Name} ({Item.NativeName})")));
        AddField(Section, "Currencies", JoinValues(Country.Currencies?.Where(Item => Item != null)
            .Select(Item => $"{Item.Name} · {Item.Code} {Item.Symbol}".Trim())));
        AddSection(Section);

        Section = CreateSection("Codes & time");
        AddField(Section, "ISO alpha-2", Country.Alpha2Code);
        AddField(Section, "ISO alpha-3", Country.Alpha3Code);
        AddField(Section, "Numeric code", Country.NumericCode);
        AddField(Section, "IOC code", Country.Cioc);
        AddField(Section, "Calling codes", JoinValues(Country.CallingCodes?.Where(Value => !string.IsNullOrWhiteSpace(Value))
            .Select(Value => "+" + Value.TrimStart('+'))));
        AddField(Section, "Internet domains", JoinValues(Country.TopLevelDomain));
        AddField(Section, "Time zones", JoinValues(Country.Timezones));
        AddSection(Section);

        Section = CreateSection("Geography");
        if (Country.LatLng != null && Country.LatLng.Count >= 2)
        {
            AddField(Section, "Latitude", Country.LatLng[0].ToString("0.####", CultureInfo.CurrentCulture) + "°");
            AddField(Section, "Longitude", Country.LatLng[1].ToString("0.####", CultureInfo.CurrentCulture) + "°");
        }
        CountryService Service = Lib.Countries;
        AddField(Section, "Bordering countries", JoinValues(Country.Borders?.Select(Code =>
            Service.Countries.FirstOrDefault(Item => string.Equals(Item.Alpha3Code, Code, StringComparison.OrdinalIgnoreCase))?.Name ?? Code)));
        AddSection(Section);

        Section = CreateSection("More information");
        AddField(Section, "Demonym", Country.Demonym);
        AddField(Section, "Independent", Country.Independent ? "Yes" : "No");
        AddField(Section, "Gini index", Country.Gini?.ToString("0.##", CultureInfo.CurrentCulture));
        AddField(Section, "Regional organizations", JoinValues(Country.RegionalBlocs?.Where(Item => Item != null)
            .Select(Item => string.IsNullOrWhiteSpace(Item.Acronym) ? Item.Name : $"{Item.Name} ({Item.Acronym})")));
        AddField(Section, "Alternative names", JoinValues(Country.AltSpellings));
        AddSection(Section);

        Section = CreateSection("Translations");
        if (Country.Translations != null)
        {
            foreach (KeyValuePair<string, string> Translation in Country.Translations.OrderBy(Item => Item.Key, StringComparer.OrdinalIgnoreCase))
                AddField(Section, Translation.Key.ToUpperInvariant(), Translation.Value);
        }
        AddSection(Section);

        Section = CreateSection("Maps");
        StackPanel Actions = new();
        Actions.Classes.Add("CardActions");
        AddLink(Actions, "Open Google Maps", Country.Maps?.GoogleMaps);
        AddLink(Actions, "Open OpenStreetMap", Country.Maps?.OpenStreetMaps);
        if (Actions.Children.Count > 0)
            Section.Children.Add(Actions);
        AddSection(Section);
    }

    // ● constructor
    /// <summary>
    /// Creates an empty country card using the shared base layout.
    /// </summary>
    public CountryCardForm()
    {
    }
    /// <summary>
    /// Creates a detail card using the existing downloaded country instance.
    /// </summary>
    /// <param name="Country">The country selected in the list.</param>
    public CountryCardForm(CountryModel Country) : this()
    {
        ArgumentNullException.ThrowIfNull(Country);
        DisplayCountry(Country);
    }
}
