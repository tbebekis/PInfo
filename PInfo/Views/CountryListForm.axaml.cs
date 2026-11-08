/* PInfo, Copyright (c) 2026 Theo Bebekis, see License. */

namespace PInfo.Views;

/// <summary>
/// Displays searchable country names and flag emojis using the shared Countries service.
/// </summary>
public partial class CountryListForm : AppForm
{
    // ● private
    readonly CountryService fService;
    CancellationTokenSource fLoadCancellation;
    bool fLoaded;

    /// <summary>Loads countries when the form becomes visible.</summary>
    async void OnLoaded(object Sender, RoutedEventArgs Args)
    {
        if (!fLoaded)
            await LoadCountriesAsync();
    }
    /// <summary>Cancels this view's pending load when it is removed from the visual tree.</summary>
    void OnUnloaded(object Sender, RoutedEventArgs Args)
    {
        fLoadCancellation?.Cancel();
    }
    /// <summary>Retries loading after a failed request.</summary>
    async void AnyClick(object Sender, RoutedEventArgs Args)
    {
        if (Sender == btnRetry)
            await LoadCountriesAsync();
    }
    /// <summary>Updates the country list when the search text changes.</summary>
    void OnSearchTextChanged(object Sender, TextChangedEventArgs Args)
    {
        ApplySearch();
    }
    /// <summary>Filters by name or ISO code without making another HTTP request.</summary>
    void ApplySearch()
    {
        string Search = (edtSearch.Text ?? string.Empty).Trim();
        List<CountryModel> Items = fService.Countries.Where(Item =>
            Item.Name.Contains(Search, StringComparison.OrdinalIgnoreCase)
            || Item.Alpha2Code.Contains(Search, StringComparison.OrdinalIgnoreCase)
            || Item.Alpha3Code.Contains(Search, StringComparison.OrdinalIgnoreCase)).ToList();
        CountryList.ItemsSource = Items;
        ResultCountText.Text = Items.Count == 1 ? "1 country" : $"{Items.Count} countries";
        EmptyText.IsVisible = fLoaded && Items.Count == 0;
    }
    /// <summary>Loads data and presents failures inline so the user can retry.</summary>
    async Task LoadCountriesAsync()
    {
        // A reattached form can start a new load while an earlier cancellation finishes.
        fLoadCancellation?.Cancel();
        CancellationTokenSource Cancellation = new();
        fLoadCancellation = Cancellation;
        LoadingProgress.IsVisible = true;
        ResultCountText.Text = "Loading countries...";
        NoticePanel.IsVisible = false;
        EmptyText.IsVisible = false;
        btnRetry.IsEnabled = false;
        try
        {
            ApiClientResult Result = await fService.LoadCountriesAsync(Cancellation.Token);
            if (Cancellation.IsCancellationRequested || fLoadCancellation != Cancellation)
                return;

            if (Result.ResultStatus == ApiClientResultStatus.Cancelled)
                return;
            if (Result.ResultStatus == ApiClientResultStatus.Success)
            {
                fLoaded = true;
                ApplySearch();
                // Successful loads can carry an offline fallback or persistence warning.
                if (!string.IsNullOrWhiteSpace(Result.ReasonPhrase) && Result.ReasonPhrase != "OK")
                    ShowNotice(Result.ReasonPhrase, false);
            }
            else
            {
                ResultCountText.Text = "Countries could not be loaded.";
                ShowNotice(string.IsNullOrWhiteSpace(Result.ReasonPhrase)
                    ? "Please check your connection and try again." : Result.ReasonPhrase, true);
            }
        }
        catch (Exception)
        {
            if (!Cancellation.IsCancellationRequested && fLoadCancellation == Cancellation)
            {
                ResultCountText.Text = "Countries could not be loaded.";
                ShowNotice("An unexpected error occurred while loading countries. Please try again.", true);
            }
        }
        finally
        {
            if (fLoadCancellation == Cancellation)
            {
                fLoadCancellation = null;
                LoadingProgress.IsVisible = false;
                btnRetry.IsEnabled = true;
            }
            Cancellation.Dispose();
        }
    }
    /// <summary>Displays a load error or a non-blocking cache warning.</summary>
    void ShowNotice(string Message, bool CanRetry)
    {
        NoticeText.Text = Message;
        btnRetry.IsVisible = CanRetry;
        NoticePanel.IsVisible = true;
    }

    // ● constructor
    /// <summary>Creates the country list and connects its UI events.</summary>
    public CountryListForm()
    {
        fService = AppHost.PublicServices.Select(Item => Item.Service).OfType<CountryService>().Single();
        InitializeComponent();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
        edtSearch.TextChanged += OnSearchTextChanged;
        btnRetry.Click += AnyClick;
    }
}
