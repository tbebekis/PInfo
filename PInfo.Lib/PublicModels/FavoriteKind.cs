/* PInfo, Copyright (c) 2026 Theo Bebekis, see License. */

namespace PInfo;

/// <summary>
/// Stable persisted categories of saved destinations.
/// </summary>
public enum FavoriteKind
{
    /// <summary>
    /// A country detail card.
    /// </summary>
    Country = 0,
    /// <summary>
    /// A populated place detail card.
    /// </summary>
    City = 1,
    /// <summary>
    /// A geographic place detail card.
    /// </summary>
    Place = 2,
    /// <summary>
    /// Weather for a saved location, rather than a frozen forecast.
    /// </summary>
    Weather = 3,
    /// <summary>
    /// Reference rates for a saved fiat base currency.
    /// </summary>
    ExchangeRates = 4,
    /// <summary>
    /// Fiat reference prices for a saved cryptocurrency.
    /// </summary>
    Cryptocurrencies = 5
}
