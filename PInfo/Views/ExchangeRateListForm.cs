/* PInfo, Copyright (c) 2026 Theo Bebekis, see License. */

namespace PInfo.Views;

/// <summary>
/// Lists searchable fiat or crypto assets and their dated reference values in available fiat currencies.
/// </summary>
public class ExchangeRateListForm : AppListForm
{
    // ● private
    static readonly string[] fPriorityCurrencies = { "USD", "EUR", "GBP", "CAD", "AUD", "CHF", "JPY", "CNY", "RUB" };
    static readonly string[] fPriorityCryptocurrencies = { "BTC", "ETH", "USDT", "USDC", "SOL", "XRP" };
    readonly Dictionary<string, IReadOnlyList<string>> fCurrencyAreas = new(StringComparer.OrdinalIgnoreCase);
    readonly ExchangeRateService fService;
    readonly CurrencyDetail fBaseCurrency;
    readonly int fPageSize;
    readonly Button fPrevious = new() { Content = "←", IsEnabled = false, MinWidth = 44, MinHeight = 44 };
    readonly Button fNext = new() { Content = "→", IsEnabled = false, MinWidth = 44, MinHeight = 44 };
    readonly TextBlock fPageText = new()
    {
        VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
        HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center
    };
    IReadOnlyList<CurrencyDetail> fCurrencies;
    IReadOnlyList<(CurrencyDetail Currency, decimal Rate, DateTime Date)> fRates = Array.Empty<(CurrencyDetail Currency, decimal Rate, DateTime Date)>();
    int fPage;

    /// <summary>
    /// Returns the agreed display priority, leaving all other currencies in the alphabetical group.
    /// </summary>
    /// <param name="Currency">The currency to order.</param>
    /// <returns>The display rank, unrelated to economic size or exchange-rate value.</returns>
    int CurrencyPriority(CurrencyDetail Currency)
    {
        string[] Priorities = fService.IsCryptocurrencyService && fBaseCurrency == null ? fPriorityCryptocurrencies : fPriorityCurrencies;
        int Index = Array.IndexOf(Priorities, Currency.Code);
        return Index < 0 ? Priorities.Length : Index;
    }
    /// <summary>
    /// Identifies the principal issuer to keep it visible ahead of other currency users.
    /// </summary>
    /// <param name="Code">The currency code.</param>
    /// <returns>The principal country code for the preferred currencies, or an empty string.</returns>
    static string PrincipalCountry(string Code)
    {
        return Code switch
        {
            "USD" => "US", "GBP" => "GB", "CAD" => "CA", "AUD" => "AU", "CHF" => "CH",
            "JPY" => "JP", "CNY" => "CN", "RUB" => "RU", _ => string.Empty
        };
    }
    /// <summary>
    /// Resolves currency users from the existing country service without making label failure fatal to rates.
    /// </summary>
    /// <param name="CancellationToken">Cancels the country load and mapping.</param>
    /// <returns>An optional warning when geographic labels are unavailable or use older country data.</returns>
    async Task<string> LoadCurrencyAreasAsync(CancellationToken CancellationToken)
    {
        if (fCurrencyAreas.Count > 0 || (fService.IsCryptocurrencyService && fBaseCurrency == null))
            return string.Empty;
        try
        {
            this.FindControl<TextBlock>("ResultCountText").Text = "Loading country/region labels...";
            ApiClientResult Result = await Lib.Countries.LoadCountriesAsync(CancellationToken);
            CancellationToken.ThrowIfCancellationRequested();
            if (Result.ResultStatus != ApiClientResultStatus.Success)
                return "Country/region labels could not be loaded. Currency rates remain available.";
            Dictionary<string, IReadOnlyList<string>> Areas = new(StringComparer.OrdinalIgnoreCase);
            IEnumerable<CurrencyDetail> Currencies = fService.IsCryptocurrencyService
                ? fRates.Select(Item => Item.Currency) : fCurrencies;
            foreach (CurrencyDetail Currency in Currencies)
            {
                CancellationToken.ThrowIfCancellationRequested();
                string Principal = PrincipalCountry(Currency.Code);
                List<string> Names = Lib.Countries.Countries
                    .Where(Country => Country.Currencies != null && Country.Currencies.Any(Item => Item != null
                        && string.Equals(Item.Code, Currency.Code, StringComparison.OrdinalIgnoreCase)))
                    .OrderBy(Country => string.Equals(Country.Alpha2Code, Principal, StringComparison.OrdinalIgnoreCase) ? 0 : 1)
                    .ThenBy(Country => Country.Name, StringComparer.OrdinalIgnoreCase)
                    .Select(Country => Country.Name).Where(Name => !string.IsNullOrWhiteSpace(Name))
                    .Distinct(StringComparer.OrdinalIgnoreCase).ToList();
                if (Names.Count > 0)
                    Areas[Currency.Code] = Names.AsReadOnly();
            }
            CancellationToken.ThrowIfCancellationRequested();
            foreach (var Area in Areas)
                fCurrencyAreas[Area.Key] = Area.Value;
            return string.IsNullOrWhiteSpace(Result.ReasonPhrase) || Result.ReasonPhrase == "OK" ? string.Empty
                : "Country/region labels: " + Result.ReasonPhrase;
        }
        catch (OperationCanceledException) when (CancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception Ex)
        {
            ExceptionLog.WriteException("ExchangeRateListForm.LoadCurrencyAreasAsync", Ex, false);
            return "Country/region labels could not be loaded. Currency rates remain available.";
        }
    }
    /// <summary>
    /// Describes the available currency users without implying that a shared currency belongs to just one country.
    /// </summary>
    /// <param name="Currency">The currency to describe.</param>
    /// <returns>A region, up to three named users and an additional-user count, or an unavailable marker.</returns>
    string CurrencyArea(CurrencyDetail Currency)
    {
        if (fService.IsCryptocurrencyService && fBaseCurrency == null)
            return "Cryptocurrency · daily reference prices";
        if (Currency.Code == "EUR")
            return "Euro area";
        if (!fCurrencyAreas.TryGetValue(Currency.Code, out IReadOnlyList<string> Names))
            return "Country/region unavailable";
        return string.Join(", ", Names.Take(3)) + (Names.Count > 3 ? $" + {Names.Count - 3} other countries/territories" : string.Empty);
    }
    /// <summary>
    /// Creates a compact row for choosing a base currency.
    /// </summary>
    /// <param name="Currency">The available currency.</param>
    /// <returns>The currency name and code in a themed row.</returns>
    Control CreateCurrencyItem(CurrencyDetail Currency)
    {
        if (Currency == null)
            return null;
        TextBlock Name = new() { Text = Currency.Code + " — " + Currency.Name, TextWrapping = TextWrapping.Wrap };
        Name.Classes.Add("ListItemTitle");
        TextBlock Area = new() { Text = CurrencyArea(Currency), FontSize = 12, TextWrapping = TextWrapping.Wrap };
        Area.Classes.Add("SecondaryText");
        StackPanel Text = new() { Spacing = 2 };
        Text.Children.Add(Name);
        Text.Children.Add(Area);
        Border Card = new() { Child = Text };
        Card.Classes.Add("ListItem");
        return Card;
    }
    /// <summary>
    /// Creates a readable reference-rate row with its target name and observation date.
    /// </summary>
    /// <param name="Currency">The target currency.</param>
    /// <param name="Rate">Target units per one base-currency unit.</param>
    /// <param name="Date">The provider's date for this particular pair.</param>
    /// <returns>The rate and its dated context.</returns>
    Control CreateRateItem(CurrencyDetail Currency, decimal Rate, DateTime Date)
    {
        TextBlock Value = new()
        {
            Text = Currency.Code + " = " + Rate.ToString("0.##########", CultureInfo.CurrentCulture),
            TextWrapping = TextWrapping.Wrap
        };
        Value.Classes.Add("ListItemTitle");
        TextBlock Description = new()
        {
            Text = Currency.Name + " · " + CurrencyArea(Currency) + "\n" + Date.ToString("dd MMM yyyy", CultureInfo.CurrentCulture),
            FontSize = 12, TextWrapping = TextWrapping.Wrap
        };
        Description.Classes.Add("SecondaryText");
        StackPanel Text = new() { Spacing = 2 };
        Text.Children.Add(Value);
        Text.Children.Add(Description);
        Border Card = new() { Child = Text };
        Card.Classes.Add("ListItem");
        return Card;
    }
    /// <summary>
    /// Matches a currency code or name without another provider request.
    /// </summary>
    /// <param name="Currency">The currency to match.</param>
    /// <returns>True when the currency matches the current search text.</returns>
    bool Matches(CurrencyDetail Currency)
    {
        return Currency.Code.Contains(SearchText, StringComparison.OrdinalIgnoreCase)
            || Currency.Name.Contains(SearchText, StringComparison.OrdinalIgnoreCase)
            || (!(fService.IsCryptocurrencyService && fBaseCurrency == null)
                && (CurrencyArea(Currency).Contains(SearchText, StringComparison.OrdinalIgnoreCase)
                    || (fCurrencyAreas.TryGetValue(Currency.Code, out IReadOnlyList<string> Areas)
                        && Areas.Any(Area => Area.Contains(SearchText, StringComparison.OrdinalIgnoreCase)))));
    }
    /// <summary>
    /// Filters before paging and updates the page controls, count and reference-rate context.
    /// </summary>
    void DisplayPage()
    {
        int Count;
        string Title;
        string Subtitle;
        if (fBaseCurrency == null)
        {
            List<CurrencyDetail> Items = fCurrencies.Where(Matches).OrderBy(CurrencyPriority)
                .ThenBy(Item => Item.Code, StringComparer.Ordinal).ToList();
            Count = Items.Count;
            fPage = Math.Clamp(fPage, 0, Math.Max(0, (Count - 1) / fPageSize));
            ItemList.ItemsSource = Items.Skip(fPage * fPageSize).Take(fPageSize).ToList();
            Title = fService.Name;
            Subtitle = fService.IsCryptocurrencyService ? "Select a cryptocurrency to see its daily reference prices."
                : "Select a base currency to see its reference rates.";
            SetResultCount(Count, fService.IsCryptocurrencyService ? "cryptocurrency" : "currency",
                fService.IsCryptocurrencyService ? "cryptocurrencies" : "currencies");
        }
        else
        {
            List<(CurrencyDetail Currency, decimal Rate, DateTime Date)> Items = fRates.Where(Item => Matches(Item.Currency))
                .OrderBy(Item => CurrencyPriority(Item.Currency)).ThenBy(Item => Item.Currency.Code, StringComparer.Ordinal).ToList();
            Count = Items.Count;
            fPage = Math.Clamp(fPage, 0, Math.Max(0, (Count - 1) / fPageSize));
            ItemList.ItemsSource = Items.Skip(fPage * fPageSize).Take(fPageSize).ToList();
            Title = fBaseCurrency.Code + (fService.IsCryptocurrencyService ? " — Reference Prices" : " — Exchange Rates");
            Subtitle = fBaseCurrency.Name + (fService.IsCryptocurrencyService ? "\nDaily reference prices per 1 " : "\nReference rates per 1 ")
                + fBaseCurrency.Code + "; not live market quotes.";
            if (fRates.Count > 0)
            {
                DateTime First = fRates.Min(Item => Item.Date);
                DateTime Last = fRates.Max(Item => Item.Date);
                Subtitle += (fService.IsCryptocurrencyService ? "\nPrice dates: " : "\nRate dates: ") + First.ToString("dd MMM yyyy", CultureInfo.CurrentCulture)
                    + (First == Last ? string.Empty : " – " + Last.ToString("dd MMM yyyy", CultureInfo.CurrentCulture));
            }
            SetResultCount(Count, fService.IsCryptocurrencyService ? "price" : "rate",
                fService.IsCryptocurrencyService ? "prices" : "rates");
        }
        SetHeader(Title + $" ({Count:N0})", Subtitle,
            fService.IsCryptocurrencyService && fBaseCurrency == null ? "Search by cryptocurrency code or name" : "Search by currency code, name or region");
        int PageCount = Math.Max(1, (Count + fPageSize - 1) / fPageSize);
        fPageText.Text = $"Page {fPage + 1} / {PageCount}";
        fPrevious.IsEnabled = fPage > 0;
        fNext.IsEnabled = fPage + 1 < PageCount;
    }
    /// <summary>
    /// Opens a local page of already loaded data and returns to its first row.
    /// </summary>
    void OnPageClick(object Sender, RoutedEventArgs Args)
    {
        fPage += Sender == fNext ? 1 : -1;
        DisplayPage();
        if (ItemList.ItemCount > 0)
            ItemList.ScrollIntoView(ItemList.Items[0]);
    }

    // ● overridables
    /// <summary>
    /// Loads either the catalogue or the selected base currency's rates using validated cached fallback.
    /// </summary>
    /// <param name="CancellationToken">Cancels the pending resource load.</param>
    /// <returns>The outcome and any offline or persistence warning.</returns>
    protected override async Task<ApiClientResult> LoadListAsync(CancellationToken CancellationToken)
    {
        fPrevious.IsEnabled = false;
        fNext.IsEnabled = false;
        if (fBaseCurrency == null)
        {
            var Result = await fService.LoadCurrenciesAsync(CancellationToken);
            if (!CancellationToken.IsCancellationRequested && Result.Result.ResultStatus == ApiClientResultStatus.Success)
            {
                fCurrencies = Result.Items;
                string Warning = await LoadCurrencyAreasAsync(CancellationToken);
                if (!string.IsNullOrWhiteSpace(Warning))
                    Result.Result.ReasonPhrase = string.Join("\n", new[] { Result.Result.ReasonPhrase, Warning }.Where(Value => !string.IsNullOrWhiteSpace(Value)));
            }
            return Result.Result;
        }
        else
        {
            string CatalogueWarning = string.Empty;
            if (fCurrencies.Count == 0)
            {
                var Catalogue = await fService.LoadCurrenciesAsync(CancellationToken);
                CancellationToken.ThrowIfCancellationRequested();
                if (Catalogue.Result.ResultStatus != ApiClientResultStatus.Success)
                    return Catalogue.Result;
                if (!Catalogue.Items.Any(Item => Item.Code == fBaseCurrency.Code))
                    return new ApiClientResult(Catalogue.Result.ActionUrl)
                    {
                        ResultStatus = ApiClientResultStatus.Error,
                        ReasonPhrase = "This saved asset is no longer in the available catalogue. You can remove it with its heart."
                    };
                fCurrencies = Catalogue.Items;
                CatalogueWarning = Catalogue.Result.ReasonPhrase;
            }
            var Result = await fService.LoadRatesAsync(fBaseCurrency.Code, fCurrencies, CancellationToken);
            if (!string.IsNullOrWhiteSpace(CatalogueWarning))
                Result.Result.ReasonPhrase = string.Join("\n", new[] { CatalogueWarning, Result.Result.ReasonPhrase }
                    .Where(Value => !string.IsNullOrWhiteSpace(Value)));
            if (!CancellationToken.IsCancellationRequested && Result.Result.ResultStatus == ApiClientResultStatus.Success)
            {
                fRates = Result.Items;
                string Warning = await LoadCurrencyAreasAsync(CancellationToken);
                if (!string.IsNullOrWhiteSpace(Warning))
                    Result.Result.ReasonPhrase = string.Join("\n", new[] { Result.Result.ReasonPhrase, Warning }.Where(Value => !string.IsNullOrWhiteSpace(Value)));
            }
            return Result.Result;
        }
    }
    /// <summary>
    /// Applies the text filter to the complete collection before restarting local paging.
    /// </summary>
    protected override void ApplySearch()
    {
        fPage = 0;
        DisplayPage();
        if (ItemList.ItemCount > 0)
            ItemList.ScrollIntoView(ItemList.Items[0]);
    }
    /// <summary>
    /// Opens the selected currency's reference-rate list, preserving this catalogue and its search for Back.
    /// </summary>
    /// <param name="Item">The selected currency.</param>
    protected override void OnItemSelected(object Item)
    {
        if (fBaseCurrency == null && Item is CurrencyDetail Currency)
        {
            ExchangeRateListForm Form = new(fService, Currency, fCurrencies);
            foreach (var Area in fCurrencyAreas)
                Form.fCurrencyAreas[Area.Key] = Area.Value;
            MainView.Current?.PushForm(Form);
        }
    }

    // ● constructor
    /// <summary>
    /// Creates the catalogue list, or a rate list for a currency chosen from that catalogue.
    /// </summary>
    /// <param name="Service">The registered Exchange Rates service.</param>
    /// <param name="BaseCurrency">The chosen base currency, or null for the catalogue.</param>
    /// <param name="Currencies">The available catalogue when opening rates.</param>
    public ExchangeRateListForm(ExchangeRateService Service, CurrencyDetail BaseCurrency = null, IReadOnlyList<CurrencyDetail> Currencies = null)
    {
        ArgumentNullException.ThrowIfNull(Service);
        if (BaseCurrency != null)
            ArgumentNullException.ThrowIfNull(Currencies);
        fService = Service;
        fBaseCurrency = BaseCurrency;
        fCurrencies = Currencies ?? Array.Empty<CurrencyDetail>();
        fPageSize = Math.Clamp(Lib.Settings.ItemsPerPage, 10, 100);
        Classes.Add("CompactTitle");
        SetHeader(BaseCurrency == null ? Service.Name : BaseCurrency.Code + (Service.IsCryptocurrencyService ? " — Reference Prices" : " — Exchange Rates"),
            BaseCurrency == null ? (Service.IsCryptocurrencyService ? "Select a cryptocurrency" : "Select a base currency") : BaseCurrency.Name,
            Service.IsCryptocurrencyService && BaseCurrency == null ? "Search by cryptocurrency code or name" : "Search by currency code, name or region");
        EmptyMessage = BaseCurrency == null ? (Service.IsCryptocurrencyService ? "No cryptocurrencies match your search." : "No currencies match your search.")
            : "No available rates match your search.";
        if (BaseCurrency == null)
            ItemList.ItemTemplate = new FuncDataTemplate<CurrencyDetail>((Item, Scope) => CreateCurrencyItem(Item));
        else
            ItemList.ItemTemplate = new FuncDataTemplate<(CurrencyDetail Currency, decimal Rate, DateTime Date)>(
                (Item, Scope) => CreateRateItem(Item.Currency, Item.Rate, Item.Date));
        if (BaseCurrency != null && this.FindControl<TextBlock>("TitleText") is TextBlock Title
            && Title.Parent is StackPanel Header)
        {
            int Index = Header.Children.IndexOf(Title);
            Header.Children.RemoveAt(Index);
            Grid Heading = new() { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
            Heading.Children.Add(Title);
            Title.TextWrapping = TextWrapping.Wrap;
            FavoriteButton Heart = new(FavoriteService.ExchangeRates(BaseCurrency, Service.IsCryptocurrencyService))
            {
                HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right,
                VerticalAlignment = Avalonia.Layout.VerticalAlignment.Top,
                Margin = new Thickness(8, 0, 0, 0)
            };
            Grid.SetColumn(Heart, 1);
            Heading.Children.Add(Heart);
            Header.Children.Insert(Index, Heading);
        }
        Grid Paging = new() { ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto") };
        fPrevious.Classes.Add("PrimaryButton");
        fPrevious.Classes.Add("PageNavigation");
        fNext.Classes.Add("PrimaryButton");
        fNext.Classes.Add("PageNavigation");
        Avalonia.Automation.AutomationProperties.SetName(fPrevious, "Previous page");
        Avalonia.Automation.AutomationProperties.SetName(fNext, "Next page");
        Grid.SetColumn(fPageText, 1);
        Grid.SetColumn(fNext, 2);
        Paging.Children.Add(fPrevious);
        Paging.Children.Add(fPageText);
        Paging.Children.Add(fNext);
        StackPanel Footer = new() { Spacing = 4, Margin = new Thickness(0, 6, 0, 0) };
        Footer.Children.Add(Paging);
        HyperlinkButton Source = new() { Content = Service.IsCryptocurrencyService ? "Reference prices from Frankfurter" : "Rates from Frankfurter", NavigateUri = new Uri("https://frankfurter.dev/") };
        Footer.Children.Add(Source);
        if (ItemList.Parent is Grid ListPanel)
        {
            Control[] ExistingChildren = ListPanel.Children.ToArray();
            ListPanel.RowDefinitions = new RowDefinitions("*,Auto");
            foreach (Control Child in ExistingChildren)
                Grid.SetRow(Child, 0);
            Grid.SetRow(Footer, 1);
            ListPanel.Children.Add(Footer);
        }
        fPrevious.Click += OnPageClick;
        fNext.Click += OnPageClick;
    }

    // ● properties
    /// <summary>
    /// The visible resource loading message.
    /// </summary>
    protected override string LoadingText => fBaseCurrency == null ? (fService.IsCryptocurrencyService ? "Loading cryptocurrencies..." : "Loading currencies...")
        : "Loading reference rates...";
    /// <summary>
    /// The visible load failure message.
    /// </summary>
    protected override string LoadErrorText => fService.IsCryptocurrencyService ? "Cryptocurrency data could not be loaded."
        : "Exchange-rate data could not be loaded.";
}
