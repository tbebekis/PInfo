/* PInfo, Copyright (c) 2026 Theo Bebekis, see License. */

namespace PInfo.Views;

/// <summary>
/// Opens the typed saved destination without requiring a country/place import; live services retain their own cache and refresh rules.
/// </summary>
static class FavoriteNavigation
{
    // ● public
    /// <summary>
    /// Restores saved details or opens current weather for the saved location.
    /// </summary>
    /// <param name="Favorite">The selected favorite.</param>
    static public void Open(FavoriteModel Favorite)
    {
        FavoriteSnapshot Snapshot = FavoriteService.ReadSnapshot(Favorite);
        AppForm Form;
        switch (Favorite.Kind)
        {
            case FavoriteKind.Country:
                Form = new CountryCardForm(Snapshot.Country);
                break;
            case FavoriteKind.City:
                Form = new CityCardForm(Snapshot.City);
                break;
            case FavoriteKind.Place:
                PlaceCategory Category = PlaceCategory.All.FirstOrDefault(Item => Item.Name == Snapshot.CategoryName)
                    ?? PlaceCategory.All.FirstOrDefault(Item => Item.Filters["All"].Contains(Snapshot.Place.FeatureClass + "." + Snapshot.Place.FeatureCode))
                    ?? PlaceCategory.All[0];
                Form = new PlaceCardForm(Snapshot.Place, Category);
                break;
            case FavoriteKind.Weather:
                FavoriteWeatherLocation Location = Snapshot.Weather;
                Form = new WeatherForm(Location.Name, Location.Latitude, Location.Longitude, Location.Timezone,
                    Location.AdministrativeArea, Favorite.TargetKey);
                break;
            case FavoriteKind.ExchangeRates:
            case FavoriteKind.Cryptocurrencies:
                ExchangeRateService Service = new(Cryptocurrencies: Favorite.Kind == FavoriteKind.Cryptocurrencies);
                Form = new ExchangeRateListForm(Service, Snapshot.BaseCurrency, Array.Empty<CurrencyDetail>());
                break;
            case FavoriteKind.Art:
                Form = new MetDetailForm(new MetService(), int.Parse(Favorite.TargetKey, CultureInfo.InvariantCulture), Favorite.Name);
                break;
            case FavoriteKind.WorldClock:
                Form = new WorldClockForm(Favorite.TargetKey);
                break;
            default:
                throw new InvalidOperationException("Unsupported favorite destination.");
        }
        MainView.Current?.PushForm(Form);
    }
}
