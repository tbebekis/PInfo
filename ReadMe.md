# PInfo

PInfo is a free, open-source Android application that brings useful information from public services into one place. Explore countries and cities, check the weather and world clocks, discover books and art, and follow currency reference rates through a consistent, compact interface.

Created by **Theo Bebekis**. Built with **C#**, **.NET 10** and **Avalonia UI**.

## Download and install

Download the signed **PInfo-1.0.0.apk** from the [latest GitHub release](https://github.com/tbebekis/PInfo/releases/latest). Open the APK on your Android device and, if prompted, allow installation from the browser or file manager you used to download it.

- Requires Android 6.0 (API 23) or newer.
- The current APK supports 64-bit ARM64 and x86_64 devices.
- A Google Play account is not required.
- Release assets include a SHA-256 checksum for the APK.

## What you can explore

- **Countries:** country profiles, flags, capitals, population and other useful facts. Open a country's major cities, all cities or geographic places, and access Wikipedia, public holidays and World Bank indicators.
- **Weather:** search for a location and view the next 24 hours in two-hour periods, followed by the available daily forecast. Temperature curves, precipitation and wind information make it easier to compare periods. Sun & Moon provides date selection and astronomical event times for the location.
- **World Clock:** explore timezones, current local times, UTC offsets and daylight saving information. Saved clocks can be viewed together for comparison.
- **Books:** search Open Library by title, author or ISBN, then open a book's work description and catalogue details.
- **Universities:** search university names and countries, inspect their details and open their websites.
- **Art - The Met:** browse museum collections without entering a search, or look for an artist, artwork or subject. Results include available Open Access image previews and separate artwork detail cards.
- **Exchange Rates:** select a base currency and browse reference rates against other supported currencies, with currency names, countries or regions, and observation dates.
- **Cryptocurrencies:** browse supported cryptocurrency reference prices. These and currency exchange rates are reference data, rather than live trading quotes.

Country exploration also includes six geographic place categories: mountains, water, coasts and islands, administrative areas, transport, and points of interest. World Bank indicators include historical charts for economic, social and governance measures where observations are available.

## Favorites and preferences

Use the heart on supported detail views to save countries, cities, places, weather locations, world clocks, currencies, cryptocurrencies and Met artworks. The Favorites tab groups saved items by category for quick access.

Settings include application themes, Celsius or Fahrenheit temperatures, a shared page size for paginated lists, and refresh intervals for downloaded data. Separate detail forms let you return to an existing list while keeping its search, filters, page and scroll position.

## Downloaded data and offline use

PInfo retains downloaded country and place data, and caches supported service responses for later use. Availability offline depends on what has already been downloaded and on the service involved; fetching new information requires an Internet connection. World Clock uses the device clock and system timezone rules and works offline.

Refresh intervals are configurable in Settings. Where supported, saved data remains available if a refresh fails, with a notice explaining that older content is being shown. Forecasts, catalogue records and statistical observations reflect their providers' coverage and update schedules.

## Screenshots

The following screenshots show the Android interface, including country exploration, forecasts, historical indicators, art and reference rates.

| Services and information | More views |
| --- | --- |
| **Services**<br><img src="Images/PInfo%20-%20Services.jpg" alt="PInfo: services" width="260"> | **Favorites**<br><img src="Images/PInfo%20-%20Favorites.jpg" alt="PInfo: favorites" width="260"> |
| **Country details**<br><img src="Images/PInfo%20-%20Greece.jpg" alt="PInfo: country details" width="260"> | **Geographic places**<br><img src="Images/PInfo%20-%20Greece%20-%20Places.jpg" alt="PInfo: geographic places" width="260"> |
| **Next 24 hours**<br><img src="Images/PInfo%20-%20Weather-1.jpg" alt="PInfo: next 24 hours" width="260"> | **Daily forecast**<br><img src="Images/PInfo%20-%20Weather-2.jpg" alt="PInfo: daily forecast" width="260"> |
| **Country indicators**<br><img src="Images/PInfo%20-%20Greece%20-%20WorldBank.jpg" alt="PInfo: country indicators" width="260"> | **Historical indicator chart**<br><img src="Images/PInfo%20-%20Greece%20-%20Internet.jpg" alt="PInfo: historical indicator chart" width="260"> |
| **Art - The Met**<br><img src="Images/PInfo%20-%20TheMet.jpg" alt="PInfo: art - the met" width="260"> | **Currency reference rates**<br><img src="Images/PInfo%20-%20ExchangeRates.jpg" alt="PInfo: currency reference rates" width="260"> |

## Data sources

PInfo brings together information from countries.dev, GeoNames, MET Norway, Photon and OpenStreetMap, Wikipedia, the World Bank, Open Library, Hipo's university catalogue, The Metropolitan Museum of Art, Frankfurter and Vacanza's holidays project.

The application's **About** screen provides source acknowledgements, licensing information and links. These providers are independent of PInfo, and their data may have different coverage, refresh schedules and usage conditions.

## License

PInfo is licensed under the [MIT License](LICENSE). Copyright (c) 2026 Theo Bebekis.

Third-party libraries, data and services remain subject to their respective licenses and terms.
