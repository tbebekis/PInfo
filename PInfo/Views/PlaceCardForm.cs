/* PInfo, Copyright (c) 2026 Theo Bebekis, see License. */

namespace PInfo.Views;

/// <summary>
/// Displays a selected geographic place's existing data in a separate scrollable card.
/// </summary>
public class PlaceCardForm : AppCardForm
{
    // ● constructor
    /// <summary>
    /// Creates a place detail card without another database or network request.
    /// </summary>
    /// <param name="Place">The selected place.</param>
    /// <param name="Category">The category used to describe the place type.</param>
    public PlaceCardForm(PlaceModel Place, PlaceCategory Category)
    {
        ArgumentNullException.ThrowIfNull(Place);
        ArgumentNullException.ThrowIfNull(Category);
        string DisplayName = string.IsNullOrWhiteSpace(Place.AsciiName) ? Place.Name : Place.AsciiName.Trim();
        string OriginalName = string.Equals(DisplayName, Place.Name, StringComparison.Ordinal) ? string.Empty : Place.Name;
        SetHeader(DisplayName, string.Join("\n", new[] { Place.AdministrativeArea, OriginalName }
            .Where(Value => !string.IsNullOrWhiteSpace(Value))), string.Empty);
        AddBadge(Category.Name);
        AddBadge(Place.CountryCode);
        StackPanel Section = CreateSection("Place details");
        AddField(Section, "Type", Category.GetFeatureName(Place));
        AddField(Section, "Alternative names", Place.AlternateNames);
        AddField(Section, "Country code", Place.CountryCode);
        AddField(Section, "Administrative code 1", Place.Admin1Code);
        AddField(Section, "Administrative code 2", Place.Admin2Code);
        AddField(Section, "Administrative code 3", Place.Admin3Code);
        AddField(Section, "Administrative code 4", Place.Admin4Code);
        AddField(Section, "Time zone", Place.Timezone);
        AddField(Section, "Record updated", Place.ModificationDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
        AddSection(Section);
        Section = CreateSection("Location");
        AddField(Section, "Elevation", Place.Elevation.HasValue
            ? Place.Elevation.Value.ToString("N0", CultureInfo.CurrentCulture) + " m" : null);
        AddField(Section, "Terrain elevation", Place.Dem.HasValue
            ? Place.Dem.Value.ToString("N0", CultureInfo.CurrentCulture) + " m" : null);
        if (double.IsFinite(Place.Latitude) && double.IsFinite(Place.Longitude)
            && Place.Latitude >= -90 && Place.Latitude <= 90 && Place.Longitude >= -180 && Place.Longitude <= 180)
        {
            Button Weather = new() { Content = "Weather" };
            Weather.Click += (Sender, Args) => MainView.Current?.PushForm(new WeatherForm(DisplayName, Place.Latitude, Place.Longitude, Place.Timezone, Place.AdministrativeArea));
            AddHeaderAction(Weather);
            AddField(Section, "Latitude", Place.Latitude.ToString("0.#####", CultureInfo.CurrentCulture) + "°");
            AddField(Section, "Longitude", Place.Longitude.ToString("0.#####", CultureInfo.CurrentCulture) + "°");
            StackPanel Actions = new();
            Actions.Classes.Add("CardActions");
            AddLink(Actions, "Open Google Maps", "https://www.google.com/maps/search/?api=1&query="
                + Place.Latitude.ToString("R", CultureInfo.InvariantCulture) + ","
                + Place.Longitude.ToString("R", CultureInfo.InvariantCulture));
            Section.Children.Add(Actions);
        }
        AddSection(Section);
    }
}
