/* PInfo, Copyright (c) 2026 Theo Bebekis, see License. */

namespace PInfo.Views;

/// <summary>
/// Displays the selected city's existing data without another database or network request.
/// </summary>
public class CityCardForm : AppCardForm
{
    // ● constructor
    /// <summary>
    /// Creates a city detail card from an item selected in Big Cities or All Cities.
    /// </summary>
    /// <param name="City">The selected city.</param>
    public CityCardForm(CityModel City)
    {
        ArgumentNullException.ThrowIfNull(City);
        string DisplayName = string.IsNullOrWhiteSpace(City.AsciiName) ? City.Name : City.AsciiName.Trim();
        string OriginalName = string.Equals(DisplayName, City.Name, StringComparison.Ordinal) ? string.Empty : City.Name;
        SetHeader(DisplayName, string.Join("\n", new[] { City.AdministrativeArea, OriginalName }
            .Where(Value => !string.IsNullOrWhiteSpace(Value))), string.Empty);
        AddBadge(City.CountryCode);
        StackPanel Section = CreateSection("City details");
        AddField(Section, "Population", City.Population.ToString("N0", CultureInfo.CurrentCulture));
        AddField(Section, "Country code", City.CountryCode);
        AddField(Section, "Administrative code", City.Admin1Code);
        AddField(Section, "Time zone", City.Timezone);
        AddField(Section, "Feature code", City.FeatureCode);
        AddField(Section, "GeoNames ID", City.GeonameId.ToString(CultureInfo.InvariantCulture));
        AddSection(Section);
        if (double.IsFinite(City.Latitude) && double.IsFinite(City.Longitude)
            && City.Latitude >= -90 && City.Latitude <= 90 && City.Longitude >= -180 && City.Longitude <= 180)
        {
            Button Weather = new() { Content = "Weather" };
            Weather.Click += (Sender, Args) => MainView.Current?.PushForm(new WeatherForm(DisplayName, City.Latitude, City.Longitude, City.Timezone, City.AdministrativeArea));
            AddHeaderAction(Weather);
            Section = CreateSection("Location");
            AddField(Section, "Latitude", City.Latitude.ToString("0.#####", CultureInfo.CurrentCulture) + "°");
            AddField(Section, "Longitude", City.Longitude.ToString("0.#####", CultureInfo.CurrentCulture) + "°");
            StackPanel Actions = new();
            Actions.Classes.Add("CardActions");
            AddLink(Actions, "Open Google Maps", "https://www.google.com/maps/search/?api=1&query="
                + City.Latitude.ToString("R", CultureInfo.InvariantCulture) + ","
                + City.Longitude.ToString("R", CultureInfo.InvariantCulture));
            Section.Children.Add(Actions);
            AddSection(Section);
        }
    }
}
