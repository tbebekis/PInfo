/* PInfo, Copyright (c) 2026 Theo Bebekis, see License. */

namespace PInfo.Views;

/// <summary>
/// Lists a category's saved destinations and refreshes removals on Back without resetting search or scroll.
/// </summary>
public class FavoriteListForm : AppListForm
{
    // ● private
    readonly FavoriteKind fKind;
    readonly int fPageSize;
    readonly Button fPrevious = new() { Content = "←", IsEnabled = false, MinWidth = 44, MinHeight = 44 };
    readonly Button fNext = new() { Content = "→", IsEnabled = false, MinWidth = 44, MinHeight = 44 };
    readonly TextBlock fPageText = new()
    {
        VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
        HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center
    };
    List<FavoriteModel> fItems = new();
    bool fReady;
    int fPage;
    int fVersion;
    int fScrollVersion;
    Vector fSavedOffset;
    bool fHasSavedOffset;

    /// <summary>
    /// Creates a compact saved-name row with administrative context.
    /// </summary>
    Control CreateItem(FavoriteModel Favorite)
    {
        if (Favorite == null)
            return null;
        TextBlock Name = new() { Text = Favorite.Name };
        Name.Classes.Add("ListItemTitle");
        StackPanel Text = new() { Spacing = 2 };
        Text.Children.Add(Name);
        if (!string.IsNullOrWhiteSpace(Favorite.Context))
        {
            TextBlock Context = new() { Text = Favorite.Context, FontSize = 12, TextWrapping = TextWrapping.Wrap };
            Context.Classes.Add("SecondaryText");
            Text.Children.Add(Context);
        }
        Border Card = new() { Child = Text };
        Card.Classes.Add("ListItem");
        return Card;
    }
    /// <summary>
    /// Filters the saved collection before paging without changing the existing query or requested page.
    /// </summary>
    void DisplayPage()
    {
        List<FavoriteModel> Items = fItems.Where(Item => Item.Name.Contains(SearchText, StringComparison.OrdinalIgnoreCase)
            || Item.Context.Contains(SearchText, StringComparison.OrdinalIgnoreCase)).ToList();
        fPage = Math.Clamp(fPage, 0, Math.Max(0, (Items.Count - 1) / fPageSize));
        ItemList.ItemsSource = Items.Skip(fPage * fPageSize).Take(fPageSize).ToList();
        SetResultCount(Items.Count, "favorite", "favorites");
        SetHeader("Favorites — " + FavoriteService.CategoryName(fKind),
            fKind == FavoriteKind.Weather ? "Saved locations open weather using the normal cache and refresh rules."
                : fKind == FavoriteKind.ExchangeRates || fKind == FavoriteKind.Cryptocurrencies
                    ? "Saved asset choices open reference values using the normal cache and refresh rules."
                    : fKind == FavoriteKind.Art
                        ? "Saved artworks open their details using the normal cache and refresh rules."
                        : "Saved details remain available offline. Remove an item with its heart.", "Search favorites by name or region");
        int Pages = Math.Max(1, (Items.Count + fPageSize - 1) / fPageSize);
        fPageText.Text = $"Page {fPage + 1} / {Pages}";
        fPrevious.IsEnabled = fPage > 0;
        fNext.IsEnabled = fPage + 1 < Pages;
    }
    /// <summary>
    /// Changes the local page without touching stored membership.
    /// </summary>
    void OnPageClick(object Sender, RoutedEventArgs Args)
    {
        ++fScrollVersion;
        fPage += Sender == fNext ? 1 : -1;
        DisplayPage();
        if (ItemList.ItemCount > 0)
            ItemList.ScrollIntoView(ItemList.Items[0]);
    }
    /// <summary>
    /// Refreshes after returning from details, preserving the query, page and scroll offset.
    /// </summary>
    async void OnFavoritesLoaded(object Sender, RoutedEventArgs Args)
    {
        if (!fReady)
            return;
        int Version = ++fVersion;
        ScrollViewer Scroller = ItemList.GetVisualDescendants().OfType<ScrollViewer>().FirstOrDefault();
        Vector Offset = fHasSavedOffset ? fSavedOffset : Scroller?.Offset ?? default;
        int ScrollVersion = fScrollVersion;
        try
        {
            List<FavoriteModel> Items = await Task.Run(() => Db.GetFavorites(fKind));
            if (Version != fVersion || !this.IsAttachedToVisualTree())
                return;
            fItems = Items;
            DisplayPage();
            Dispatcher.UIThread.Post(() =>
            {
                if (Version == fVersion && ScrollVersion == fScrollVersion && this.IsAttachedToVisualTree() && Scroller != null)
                    Scroller.Offset = Offset;
            }, DispatcherPriority.Loaded);
        }
        catch (Exception Ex)
        {
            if (Version == fVersion && this.IsAttachedToVisualTree())
            {
                ExceptionLog.WriteException("FavoriteListForm.Refresh", Ex, false);
                ShowNotice("Favorites could not be refreshed. Please try again.", true);
            }
        }
    }
    /// <summary>
    /// Invalidates callbacks when the user leaves the list again.
    /// </summary>
    void OnFavoritesUnloaded(object Sender, RoutedEventArgs Args)
    {
        ScrollViewer Scroller = ItemList.GetVisualDescendants().OfType<ScrollViewer>().FirstOrDefault();
        if (Scroller != null)
        {
            fSavedOffset = Scroller.Offset;
            fHasSavedOffset = true;
        }
        ++fVersion;
    }

    // ● overridables
    /// <summary>
    /// Reads stored favorites on a worker thread for the initial load or an explicit retry.
    /// </summary>
    protected override async Task<ApiClientResult> LoadListAsync(CancellationToken CancellationToken)
    {
        int Version = ++fVersion;
        List<FavoriteModel> Items = await Task.Run(() => Db.GetFavorites(fKind), CancellationToken);
        CancellationToken.ThrowIfCancellationRequested();
        if (Version == fVersion)
        {
            fItems = Items;
            fReady = true;
        }
        return new ApiClientResult(string.Empty) { ResultStatus = ApiClientResultStatus.Success };
    }
    /// <summary>
    /// Restarts paging when the user changes the filter.
    /// </summary>
    protected override void ApplySearch()
    {
        ++fScrollVersion;
        fPage = 0;
        DisplayPage();
        if (ItemList.ItemCount > 0)
            ItemList.ScrollIntoView(ItemList.Items[0]);
    }
    /// <summary>
    /// Opens the selected saved snapshot and reports corrupt or unsupported saved details inline in a message.
    /// </summary>
    protected override async void OnItemSelected(object Item)
    {
        if (Item is not FavoriteModel Favorite)
            return;
        try
        {
            FavoriteNavigation.Open(Favorite);
        }
        catch (Exception Ex)
        {
            ExceptionLog.WriteException("FavoriteListForm.Open", Ex, false);
            await MessageBoxForm.Error("This saved item could not be opened. Its stored details may be unavailable or incompatible.", true);
        }
    }

    // ● constructor
    /// <summary>
    /// Creates a saved-destination list using the shared page-size preference.
    /// </summary>
    /// <param name="Kind">The selected category.</param>
    public FavoriteListForm(FavoriteKind Kind)
    {
        if (!Enum.IsDefined(Kind))
            throw new ArgumentException("Unknown favorite category.", nameof(Kind));
        fKind = Kind;
        fPageSize = Math.Clamp(Lib.Settings.ItemsPerPage, 10, 100);
        Classes.Add("CompactTitle");
        SetHeader("Favorites — " + FavoriteService.CategoryName(Kind), string.Empty, "Search favorites by name or region");
        EmptyMessage = "No matching favorites. Use the heart in a detail or Weather view to save an item.";
        ItemList.ItemTemplate = new FuncDataTemplate<FavoriteModel>((Favorite, Scope) => CreateItem(Favorite));
        Grid Paging = new() { ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto"), Margin = new Thickness(0, 6, 0, 0) };
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
        if (ItemList.Parent is Grid ListPanel)
        {
            Control[] ExistingChildren = ListPanel.Children.ToArray();
            ListPanel.RowDefinitions = new RowDefinitions("*,Auto");
            foreach (Control Child in ExistingChildren)
                Grid.SetRow(Child, 0);
            Grid.SetRow(Paging, 1);
            ListPanel.Children.Add(Paging);
        }
        fPrevious.Click += OnPageClick;
        fNext.Click += OnPageClick;
        Loaded += OnFavoritesLoaded;
        Unloaded += OnFavoritesUnloaded;
    }

    // ● properties
    /// <summary>
    /// The saved-list loading message.
    /// </summary>
    protected override string LoadingText => "Loading favorites...";
    /// <summary>
    /// The saved-list failure message.
    /// </summary>
    protected override string LoadErrorText => "Favorites could not be loaded.";
}
