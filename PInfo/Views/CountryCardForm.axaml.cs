/* PInfo, Copyright (c) 2026 Theo Bebekis, see License. */

namespace PInfo.Views;

/// <summary>
/// Displays the selected country's downloaded details without making an HTTP request.
/// </summary>
public partial class CountryCardForm : AppForm
{
    // ● private
    /// <summary>
    /// Adds a compact header badge when text is available.
    /// </summary>
    void AddBadge(string Text)
    {
        if (string.IsNullOrWhiteSpace(Text))
            return;
        TextBlock Label = new() { Text = Text };
        Label.Classes.Add("BadgeText");
        Border Badge = new() { Child = Label, Margin = new Thickness(0, 0, 8, 0) };
        Badge.Classes.Add("Badge");
        BadgePanel.Children.Add(Badge);
    }
    /// <summary>
    /// Creates a section containing a heading and vertically arranged fields.
    /// </summary>
    StackPanel CreateSection(string Title)
    {
        StackPanel Panel = new();
        Panel.Classes.Add("CardFields");
        TextBlock Heading = new() { Text = Title };
        Heading.Classes.Add("CardSectionTitle");
        Panel.Children.Add(Heading);
        return Panel;
    }
    /// <summary>
    /// Adds a labeled value, omitting fields with no available content.
    /// </summary>
    void AddField(StackPanel Section, string Label, string Value)
    {
        if (string.IsNullOrWhiteSpace(Value))
            return;
        StackPanel Field = new();
        Field.Classes.Add("CardField");
        TextBlock LabelText = new() { Text = Label };
        LabelText.Classes.Add("FieldLabel");
        TextBlock ValueText = new() { Text = Value };
        ValueText.Classes.Add("FieldValue");
        Field.Children.Add(LabelText);
        Field.Children.Add(ValueText);
        Section.Children.Add(Field);
    }
    /// <summary>
    /// Displays a section only if it contains data as well as its heading.
    /// </summary>
    void AddSection(StackPanel Section)
    {
        if (Section.Children.Count <= 1)
            return;
        Border Card = new() { Child = Section };
        Card.Classes.Add("CardSection");
        SectionPanel.Children.Add(Card);
    }
    /// <summary>
    /// Formats multiple values as separate lines, ignoring empty values.
    /// </summary>
    string JoinValues(IEnumerable<string> Values)
    {
        return Values == null ? string.Empty : string.Join(Environment.NewLine,
            Values.Where(Value => !string.IsNullOrWhiteSpace(Value)));
    }
    /// <summary>
    /// Adds an external map action only for a valid HTTPS address.
    /// </summary>
    void AddMap(StackPanel Panel, string Caption, string Url)
    {
        if (!Uri.TryCreate(Url, UriKind.Absolute, out Uri Address) || Address.Scheme != Uri.UriSchemeHttps)
            return;
        HyperlinkButton Link = new() { Content = Caption, NavigateUri = Address };
        Link.Classes.Add("CardAction");
        Panel.Children.Add(Link);
    }
    /// <summary>
    /// Builds the card from the selected model using reusable section and field styles.
    /// </summary>
    void DisplayCountry(CountryModel Country)
    {
        FlagText.Text = Country.Flag;
        NameText.Text = Country.Name;
        NativeNameText.Text = Country.NativeName;
        NativeNameText.IsVisible = !string.IsNullOrWhiteSpace(Country.NativeName);
        AddBadge(Country.Alpha2Code);
        AddBadge(Country.Alpha3Code);
        AddBadge(Country.Region);

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
        CountryService Service = AppHost.PublicServices.Select(Item => Item.Service).OfType<CountryService>().Single();
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
        AddMap(Actions, "Open Google Maps", Country.Maps?.GoogleMaps);
        AddMap(Actions, "Open OpenStreetMap", Country.Maps?.OpenStreetMaps);
        if (Actions.Children.Count > 0)
            Section.Children.Add(Actions);
        AddSection(Section);
    }

    // ● constructor
    /// <summary>
    /// Creates an empty card for the XAML designer.
    /// </summary>
    public CountryCardForm()
    {
        InitializeComponent();
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
