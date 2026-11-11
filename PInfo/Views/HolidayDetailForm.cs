/* PInfo, Copyright (c) 2026 Theo Bebekis, see License. */

namespace PInfo.Views;

/// <summary>
/// Shows the date and source of one public-calendar entry without inventing unavailable details.
/// </summary>
public class HolidayDetailForm : AppCardForm
{
    // ● constructor
    /// <summary>
    /// Creates details from an already downloaded holiday record.
    /// </summary>
    public HolidayDetailForm(CountryModel Country, JsonElement Item)
    {
        DateOnly Date = DateOnly.ParseExact(Item.GetProperty("date").GetString(), "yyyy-MM-dd", CultureInfo.InvariantCulture);
        SetHeader(Item.GetProperty("name").GetString(), Country.Name, "📅");
        StackPanel Information = CreateSection("Public holiday");
        AddField(Information, "Date", Date.ToString("dddd, dd MMMM yyyy", CultureInfo.CurrentCulture));
        AddField(Information, "Country", Country.Name);
        AddField(Information, "Calendar", "Country-level public calendar. Subdivision calendars are not included.");
        AddField(Information, "Note", "These are precomputed calendar entries. Check official announcements for recent changes.");
        AddSection(Information);
        StackPanel Source = CreateSection("Source");
        AddLink(Source, "Vacanza holiday calendars", "https://holidays.readthedocs.io/en/latest/downloads/");
        AddLink(Source, "Licence: MIT", "https://github.com/vacanza/holidays/blob/dev/LICENSE");
        AddSection(Source);
        SetSource("Source: Vacanza holidays. Names and dates follow the selected published calendar.");
    }
}
