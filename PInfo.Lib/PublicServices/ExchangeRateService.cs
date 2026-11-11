/* PInfo, Copyright (c) 2026 Theo Bebekis, see License. */

namespace PInfo;

/// <summary>
/// Loads Frankfurter fiat or cryptocurrency catalogues and reference values with validated offline caching.
/// </summary>
public class ExchangeRateService : PublicService
{
    // ● private
    readonly ConcurrentDictionary<string, SemaphoreSlim> fLoadLocks = new(StringComparer.Ordinal);

    /// <summary>
    /// Normalizes a three-letter currency code before using it in a request or cache filename.
    /// </summary>
    /// <param name="Code">The currency code.</param>
    /// <returns>The uppercase code.</returns>
    static string NormalizeCode(string Code)
    {
        string Result = (Code ?? string.Empty).Trim().ToUpperInvariant();
        if (Result.Length != 3 || Result.Any(Character => Character < 'A' || Character > 'Z'))
            throw new ArgumentException("A three-letter currency code is required.", nameof(Code));
        return Result;
    }
    /// <summary>
    /// Normalizes a provider cryptocurrency identifier without assuming a three-letter ISO currency code.
    /// </summary>
    /// <param name="Code">The cryptocurrency identifier.</param>
    /// <returns>The validated uppercase alphanumeric identifier.</returns>
    static string NormalizeCryptoCode(string Code)
    {
        string Result = (Code ?? string.Empty).Trim().ToUpperInvariant();
        if (Result.Length < 2 || Result.Length > 12
            || Result.Any(Character => !(Character >= 'A' && Character <= 'Z') && !(Character >= '0' && Character <= '9')))
            throw new ArgumentException("An alphanumeric cryptocurrency identifier is required.", nameof(Code));
        return Result;
    }
    /// <summary>
    /// Reads the selected asset catalogue, excluding other provider asset types.
    /// </summary>
    /// <param name="Json">The original provider response.</param>
    /// <param name="AssetType">The explicitly selected currency or crypto asset type.</param>
    /// <returns>Validated assets sorted by code.</returns>
    static IReadOnlyList<CurrencyDetail> ReadAssets(string Json, string AssetType)
    {
        using JsonDocument Document = JsonDocument.Parse(Json);
        if (Document.RootElement.ValueKind != JsonValueKind.Array)
            throw new JsonException("The currency response must be an array.");
        List<CurrencyDetail> Items = new();
        HashSet<string> Codes = new(StringComparer.Ordinal);
        foreach (JsonElement Item in Document.RootElement.EnumerateArray())
        {
            if (Item.GetProperty("type").GetString() != AssetType)
                continue;
            string Code = AssetType == "crypto" ? NormalizeCryptoCode(Item.GetProperty("iso_code").GetString())
                : NormalizeCode(Item.GetProperty("iso_code").GetString());
            string Name = Item.GetProperty("name").GetString();
            if (string.IsNullOrWhiteSpace(Name) || !Codes.Add(Code))
                throw new JsonException("The currency response contains a missing name or duplicate code.");
            Items.Add(new CurrencyDetail
            {
                Code = Code, Name = Name.Trim(),
                Symbol = Item.TryGetProperty("symbol", out JsonElement Symbol) ? Symbol.GetString() ?? string.Empty : string.Empty
            });
        }
        if (Items.Count == 0)
            throw new JsonException("The currency response contains no currencies.");
        return Items.OrderBy(Item => Item.Code, StringComparer.Ordinal).ToList().AsReadOnly();
    }
    /// <summary>
    /// Reads dated rates only for known currencies, validating their base, value and uniqueness.
    /// </summary>
    /// <param name="Json">The original provider response.</param>
    /// <param name="BaseCode">The requested base currency.</param>
    /// <param name="Currencies">The current currency catalogue.</param>
    /// <returns>Sorted available reference rates for other currencies.</returns>
    static IReadOnlyList<(CurrencyDetail Currency, decimal Rate, DateTime Date)> ReadRates(string Json,
        string BaseCode, IReadOnlyList<CurrencyDetail> Currencies)
    {
        using JsonDocument Document = JsonDocument.Parse(Json);
        if (Document.RootElement.ValueKind != JsonValueKind.Array)
            throw new JsonException("The rates response must be an array.");
        Dictionary<string, CurrencyDetail> Catalogue = Currencies.ToDictionary(Item => Item.Code, StringComparer.Ordinal);
        List<(CurrencyDetail Currency, decimal Rate, DateTime Date)> Items = new();
        HashSet<string> Codes = new(StringComparer.Ordinal);
        foreach (JsonElement Item in Document.RootElement.EnumerateArray())
        {
            string Quote = Item.GetProperty("quote").GetString();
            if (Quote == BaseCode || Quote == null || !Catalogue.TryGetValue(Quote, out CurrencyDetail Currency))
                continue;
            JsonElement Value = Item.GetProperty("rate");
            if (Item.GetProperty("base").GetString() != BaseCode || !Codes.Add(Quote)
                || Value.ValueKind != JsonValueKind.Number || !Value.TryGetDecimal(out decimal Rate) || Rate <= 0
                || !DateTime.TryParseExact(Item.GetProperty("date").GetString(), "yyyy-MM-dd", CultureInfo.InvariantCulture,
                    DateTimeStyles.None, out DateTime Date))
                throw new JsonException("The rates response contains an invalid or duplicate currency rate.");
            Items.Add((Currency, Rate, Date));
        }
        if (Items.Count == 0)
            throw new JsonException("No reference rates are available for this base currency.");
        return Items.OrderBy(Item => Item.Currency.Code, StringComparer.Ordinal).ToList().AsReadOnly();
    }
    /// <summary>
    /// Loads and validates one resource under a per-resource lock, retaining older valid data on request failure.
    /// </summary>
    /// <typeparam name="T">The parsed resource type.</typeparam>
    /// <param name="Key">The controlled local cache key.</param>
    /// <param name="Url">The provider resource URL.</param>
    /// <param name="Lifetime">The local refresh interval.</param>
    /// <param name="Read">Parses and validates the provider response.</param>
    /// <param name="CancellationToken">Cancels waiting, downloading and saving.</param>
    /// <returns>The load outcome and validated resource.</returns>
    async Task<(ApiClientResult Result, T Items)> LoadAsync<T>(string Key, string Url, TimeSpan Lifetime,
        Func<string, T> Read, CancellationToken CancellationToken)
    {
        SemaphoreSlim Gate = fLoadLocks.GetOrAdd(Key, Value => new SemaphoreSlim(1, 1));
        await Gate.WaitAsync(CancellationToken).ConfigureAwait(false);
        try
        {
            ApiClientResult Result = new(Url);
            T Cached = default;
            bool HasCached = false;
            string FilePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "PInfo", "Data", "ExchangeRates", Key + ".json");
            try
            {
                if (File.Exists(FilePath))
                {
                    string Json = await File.ReadAllTextAsync(FilePath, CancellationToken).ConfigureAwait(false);
                    Cached = Read(Json);
                    HasCached = true;
                    DateTime Saved = File.GetLastWriteTimeUtc(FilePath);
                    DateTime Now = DateTime.UtcNow;
                    if (Saved <= Now && Now - Saved < Lifetime)
                    {
                        CancellationToken.ThrowIfCancellationRequested();
                        Result.ResultStatus = ApiClientResultStatus.Success;
                        return (Result, Cached);
                    }
                }
            }
            catch (Exception Ex) when (Ex is IOException || Ex is UnauthorizedAccessException || Ex is JsonException
                || Ex is ArgumentException || Ex is InvalidOperationException || Ex is KeyNotFoundException)
            {
                HasCached = false;
            }
            try
            {
                using HttpRequestMessage Request = new(HttpMethod.Get, Url);
                Request.Headers.UserAgent.ParseAdd("PInfo/1.0 (+https://github.com/tbebekis/PInfo)");
                using HttpResponseMessage Response = await PublicApiClient.SendAsync(Request, CancellationToken).ConfigureAwait(false);
                Result.StatusCode = Response.StatusCode;
                Response.EnsureSuccessStatusCode();
                string Json = await Response.Content.ReadAsStringAsync(CancellationToken).ConfigureAwait(false);
                T Items = Read(Json);
                CancellationToken.ThrowIfCancellationRequested();
                string TemporaryPath = FilePath + "." + Guid.NewGuid().ToString("N") + ".tmp";
                try
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(FilePath));
                    await File.WriteAllTextAsync(TemporaryPath, Json, CancellationToken).ConfigureAwait(false);
                    File.Move(TemporaryPath, FilePath, true);
                }
                catch (Exception Ex) when (Ex is IOException || Ex is UnauthorizedAccessException)
                {
                    Result.ReasonPhrase = "Data was loaded but could not be saved for offline use.";
                }
                finally
                {
                    try { File.Delete(TemporaryPath); }
                    catch (Exception Ex) when (Ex is IOException || Ex is UnauthorizedAccessException) { }
                }
                CancellationToken.ThrowIfCancellationRequested();
                Result.ResultStatus = ApiClientResultStatus.Success;
                return (Result, Items);
            }
            catch (OperationCanceledException) when (CancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception Ex) when (Ex is HttpRequestException || Ex is IOException || Ex is JsonException
                || Ex is ArgumentException || Ex is InvalidOperationException || Ex is KeyNotFoundException || Ex is OperationCanceledException)
            {
                Result.ResultStatus = HasCached ? ApiClientResultStatus.Success : ApiClientResultStatus.Error;
                Result.ReasonPhrase = HasCached ? "Refresh failed. Previously downloaded data is shown; check the rate dates."
                    : "Exchange-rate data could not be loaded. Check your connection and try again.";
                return (Result, HasCached ? Cached : default);
            }
        }
        finally
        {
            Gate.Release();
        }
    }

    // ● constructor
    /// <summary>
    /// Creates a Frankfurter currency or cryptocurrency service without requesting or reading data.
    /// </summary>
    /// <param name="Cryptocurrencies">Selects the separate cryptocurrency catalogue and reference-price service.</param>
    public ExchangeRateService(bool Cryptocurrencies = false) : base(Cryptocurrencies ? "Cryptocurrencies" : "Exchange Rates")
    {
        IsCryptocurrencyService = Cryptocurrencies;
    }

    // ● public
    /// <summary>
    /// Reads the selected currency or cryptocurrency catalogue, reusing cached data for the configured interval.
    /// </summary>
    /// <param name="CancellationToken">Cancels this load.</param>
    /// <returns>The outcome and available currencies.</returns>
    public Task<(ApiClientResult Result, IReadOnlyList<CurrencyDetail> Items)> LoadCurrenciesAsync(CancellationToken CancellationToken = default)
    {
        return LoadAsync(IsCryptocurrencyService ? "cryptocurrencies" : "currencies",
            "https://api.frankfurter.dev/v2/currencies", TimeSpan.FromDays(Lib.Settings.AssetCatalogueRefreshDays),
            Json => ReadAssets(Json, IsCryptocurrencyService ? "crypto" : "currency"), CancellationToken);
    }
    /// <summary>
    /// Reads available rates for one base currency, reusing cached data for the configured interval.
    /// </summary>
    /// <param name="BaseCode">The selected base currency code.</param>
    /// <param name="Currencies">The available base-asset catalogue; crypto prices load their own fiat quote catalogue.</param>
    /// <param name="CancellationToken">Cancels this load.</param>
    /// <returns>The outcome and dated reference rates per one base-currency unit.</returns>
    public async Task<(ApiClientResult Result, IReadOnlyList<(CurrencyDetail Currency, decimal Rate, DateTime Date)> Items)> LoadRatesAsync(
        string BaseCode, IReadOnlyList<CurrencyDetail> Currencies, CancellationToken CancellationToken = default)
    {
        string Code = IsCryptocurrencyService ? NormalizeCryptoCode(BaseCode) : NormalizeCode(BaseCode);
        ArgumentNullException.ThrowIfNull(Currencies);
        IReadOnlyList<CurrencyDetail> Quotes = Currencies;
        string Warning = string.Empty;
        if (IsCryptocurrencyService)
        {
            if (!Currencies.Any(Item => Item.Code == Code))
                throw new ArgumentException("The selected cryptocurrency is not in the available catalogue.", nameof(BaseCode));
            var Catalogue = await LoadAsync("crypto-quote-currencies", "https://api.frankfurter.dev/v2/currencies",
                TimeSpan.FromDays(Lib.Settings.AssetCatalogueRefreshDays), Json => ReadAssets(Json, "currency"), CancellationToken).ConfigureAwait(false);
            if (Catalogue.Result.ResultStatus != ApiClientResultStatus.Success)
                return (Catalogue.Result, Array.Empty<(CurrencyDetail Currency, decimal Rate, DateTime Date)>());
            Quotes = Catalogue.Items;
            Warning = Catalogue.Result.ReasonPhrase;
        }
        var Rates = await LoadAsync((IsCryptocurrencyService ? "crypto-rates-" : "rates-") + Code,
            "https://api.frankfurter.dev/v2/rates?base=" + Code, TimeSpan.FromHours(Lib.Settings.RatesRefreshHours),
            Json => ReadRates(Json, Code, Quotes), CancellationToken).ConfigureAwait(false);
        if (!string.IsNullOrWhiteSpace(Warning))
            Rates.Result.ReasonPhrase = string.Join("\n", new[] { Rates.Result.ReasonPhrase, Warning }.Where(Value => !string.IsNullOrWhiteSpace(Value)));
        return Rates;
    }

    // ● properties
    /// <summary>
    /// True when this registered service browses cryptocurrencies and their fiat reference prices.
    /// </summary>
    public bool IsCryptocurrencyService { get; }
}
