/* PInfo, Copyright (c) 2026 Theo Bebekis, see License. */

namespace PInfo.Views;

/// <summary>
/// Displays a university's catalogue information and its listed websites without another download.
/// </summary>
public class UniversityDetailForm : AppCardForm
{
    // ● constructor
    /// <summary>
    /// Creates the selected university's details from the already loaded catalogue record.
    /// </summary>
    /// <param name="Item">The university selected in the results.</param>
    public UniversityDetailForm(JsonElement Item)
    {
        string Name = Item.GetProperty("name").GetString();
        string Country = Item.GetProperty("country").GetString();
        SetHeader(Name, Country, "🎓");
        StackPanel Information = CreateSection("University information");
        AddField(Information, "Country", Country);
        AddField(Information, "Country code", Item.GetProperty("alpha_two_code").GetString());
        if (Item.TryGetProperty("state-province", out JsonElement Region))
            AddField(Information, "State / province", Region.GetString());
        AddField(Information, "Domains", JoinValues(Item.GetProperty("domains").EnumerateArray().Select(Value => Value.GetString())));
        AddSection(Information);
        StackPanel Websites = CreateSection("Websites");
        foreach (JsonElement Value in Item.GetProperty("web_pages").EnumerateArray())
        {
            string Url = Value.GetString();
            if (Uri.TryCreate(Url, UriKind.Absolute, out Uri Address)
                && (Address.Scheme == Uri.UriSchemeHttps || Address.Scheme == Uri.UriSchemeHttp)
                && !string.IsNullOrWhiteSpace(Address.Host) && string.IsNullOrEmpty(Address.UserInfo))
            {
                HyperlinkButton Link = new() { Content = Url, NavigateUri = Address };
                Link.Classes.Add("CardAction");
                Websites.Children.Add(Link);
            }
        }
        if (Websites.Children.Count == 1)
            AddField(Websites, "Website", "No valid website is listed.");
        AddSection(Websites);
        StackPanel Source = CreateSection("Source");
        AddLink(Source, "Hipo university catalogue", "https://github.com/Hipo/university-domains-list");
        AddLink(Source, "Licence: MIT", "https://github.com/Hipo/university-domains-list/blob/master/LICENSE.txt");
        AddSection(Source);
        SetSource("Source: Hipo University Domains and Names Data List. Copyright (c) 2014-2025 Hipo. Community-maintained information may be incomplete or outdated.");
    }
}
