/* PInfo, Copyright (c) 2026 Theo Bebekis, see License. */

namespace PInfo;

/// <summary>
/// Provides shared application services and functionality.
/// </summary>
static public class AppHost
{
    // ● private
    static readonly ReadOnlyCollection<PublicServiceItem> fPublicServices = RegisterServices();

    /// <summary>
    /// Registers each service together with its form factory and checks unique service names.
    /// </summary>
    static ReadOnlyCollection<PublicServiceItem> RegisterServices()
    {
        List<PublicServiceItem> Items = new();
        Items.Add(new PublicServiceItem(Lib.Countries, () => new CountryListForm()));
        PhotonService Weather = new();
        Items.Add(new PublicServiceItem(Weather, () => new WeatherSearchForm(Weather)));
        WorldClockService WorldClock = new();
        Items.Add(new PublicServiceItem(WorldClock, () => new WorldClockListForm()));
        ExchangeRateService ExchangeRates = new();
        Items.Add(new PublicServiceItem(ExchangeRates, () => new ExchangeRateListForm(ExchangeRates)));
        ExchangeRateService Cryptocurrencies = new(Cryptocurrencies: true);
        Items.Add(new PublicServiceItem(Cryptocurrencies, () => new ExchangeRateListForm(Cryptocurrencies)));
        BookService Books = new();
        Items.Add(new PublicServiceItem(Books, () => new BookListForm(Books)));
        UniversityService Universities = new();
        Items.Add(new PublicServiceItem(Universities, () => new UniversityListForm(Universities)));

        HashSet<string> Names = new(StringComparer.OrdinalIgnoreCase);
        foreach (PublicServiceItem Item in Items)
        {
            if (!Names.Add(Item.Service.Name))
                throw new InvalidOperationException($"Duplicate public service name: {Item.Service.Name}");
        }
        return Items.AsReadOnly();
    }

    // ● properties
    /// <summary>
    /// The shared preferences, loaded once from private storage.
    /// </summary>
    static public Settings Settings => Lib.Settings;

    /// <summary>
    /// The registered services and their form factories, exposed as a read-only collection.
    /// </summary>
    static public IReadOnlyList<PublicServiceItem> PublicServices => fPublicServices;
}
