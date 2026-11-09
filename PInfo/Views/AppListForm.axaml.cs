/* PInfo, Copyright (c) 2026 Theo Bebekis, see License. */

namespace PInfo.Views;

/// <summary>
/// Provides shared list layout, loading, cancellation, search, selection, and retry behavior.
/// </summary>
public partial class AppListForm : AppForm
{
    // ● private
    CancellationTokenSource fLoadCancellation;
    bool fLoaded;

    /// <summary>
    /// Loads list data when the form becomes visible.
    /// </summary>
    async void OnLoaded(object Sender, RoutedEventArgs Args)
    {
        if (!fLoaded)
            await LoadAsync();
    }
    /// <summary>
    /// Cancels this view's pending load when it is removed from the visual tree.
    /// </summary>
    void OnUnloaded(object Sender, RoutedEventArgs Args)
    {
        fLoadCancellation?.Cancel();
    }
    /// <summary>
    /// Retries loading after a failed request.
    /// </summary>
    async void AnyClick(object Sender, RoutedEventArgs Args)
    {
        if (Sender == btnRetry)
            await LoadAsync();
    }
    /// <summary>
    /// Updates the displayed list when the search text changes.
    /// </summary>
    void OnSearchTextChanged(object Sender, TextChangedEventArgs Args)
    {
        ApplySearch();
    }
    /// <summary>
    /// Clears selection before dispatching the selected item to the derived form.
    /// </summary>
    void OnSelectionChanged(object Sender, SelectionChangedEventArgs Args)
    {
        object Item = ListControl.SelectedItem;
        if (Item == null)
            return;
        ListControl.SelectedItem = null;
        OnItemSelected(Item);
    }
    /// <summary>
    /// Loads data and presents failures inline so the user can retry.
    /// </summary>
    async Task LoadAsync()
    {
        // A reattached form can start a new load while an earlier cancellation finishes.
        fLoadCancellation?.Cancel();
        CancellationTokenSource Cancellation = new();
        fLoadCancellation = Cancellation;
        LoadingProgress.IsVisible = true;
        ResultCountText.Text = LoadingText;
        NoticePanel.IsVisible = false;
        EmptyText.IsVisible = false;
        btnRetry.IsEnabled = false;
        try
        {
            ApiClientResult Result = await LoadListAsync(Cancellation.Token);
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
                ResultCountText.Text = LoadErrorText;
                ShowNotice(string.IsNullOrWhiteSpace(Result.ReasonPhrase)
                    ? "Please check your connection and try again." : Result.ReasonPhrase, true);
            }
        }
        catch (Exception)
        {
            if (!Cancellation.IsCancellationRequested && fLoadCancellation == Cancellation)
            {
                ResultCountText.Text = LoadErrorText;
                ShowNotice("An unexpected error occurred while loading the list. Please try again.", true);
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

    // ● protected
    /// <summary>
    /// Displays a load error or a non-blocking cache warning.
    /// </summary>
    protected void ShowNotice(string Message, bool CanRetry)
    {
        NoticeText.Text = Message;
        btnRetry.IsVisible = CanRetry;
        NoticePanel.IsVisible = true;
    }
    /// <summary>
    /// Loads the list's underlying data. Derived forms override this hook.
    /// </summary>
    /// <param name="CancellationToken">Cancels the current load.</param>
    /// <returns>The load result, including any warning or error text.</returns>
    protected virtual Task<ApiClientResult> LoadListAsync(CancellationToken CancellationToken)
    {
        return Task.FromResult(new ApiClientResult(string.Empty) { ResultStatus = ApiClientResultStatus.Success });
    }
    /// <summary>
    /// Updates the visible items using the current search text.
    /// </summary>
    protected virtual void ApplySearch()
    {
    }
    /// <summary>
    /// Handles an item selected by the user.
    /// </summary>
    /// <param name="Item">The selected data item.</param>
    protected virtual void OnItemSelected(object Item)
    {
    }
    /// <summary>
    /// Sets shared header text and the search prompt.
    /// </summary>
    protected void SetHeader(string Title, string Subtitle, string SearchPlaceholder)
    {
        TitleText.Text = Title;
        SubtitleText.Text = Subtitle;
        SubtitleText.IsVisible = !string.IsNullOrWhiteSpace(Subtitle);
        edtSearch.PlaceholderText = SearchPlaceholder;
    }
    /// <summary>
    /// Updates the result count and empty-state visibility after filtering.
    /// </summary>
    protected void SetResultCount(int Count, string Singular, string Plural)
    {
        ResultCountText.Text = Count == 1 ? $"1 {Singular}" : $"{Count} {Plural}";
        EmptyText.IsVisible = fLoaded && Count == 0;
    }

    // ● constructor
    /// <summary>
    /// Creates the shared controls and connects events without calling derived hooks during construction.
    /// </summary>
    public AppListForm()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
        edtSearch.TextChanged += OnSearchTextChanged;
        btnRetry.Click += AnyClick;
        ListControl.SelectionChanged += OnSelectionChanged;
    }

    // ● properties
    /// <summary>
    /// The list control whose item template and source are supplied by the derived form.
    /// </summary>
    protected ListBox ItemList => ListControl;
    /// <summary>
    /// The trimmed current search text.
    /// </summary>
    protected string SearchText => (edtSearch.Text ?? string.Empty).Trim();
    /// <summary>
    /// The loading status text. Derived lists may override it.
    /// </summary>
    protected virtual string LoadingText => "Loading items...";
    /// <summary>
    /// The load failure status text. Derived lists may override it.
    /// </summary>
    protected virtual string LoadErrorText => "The list could not be loaded.";
    /// <summary>
    /// The explanation displayed when filtering produces no items.
    /// </summary>
    protected string EmptyMessage { get => EmptyText.Text; set => EmptyText.Text = value; }
}
