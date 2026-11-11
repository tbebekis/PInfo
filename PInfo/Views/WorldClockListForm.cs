/* PInfo, Copyright (c) 2026 Theo Bebekis, see License. */

namespace PInfo.Views;

/// <summary>
/// Lists searchable system timezones or all saved clocks, with synchronized visible clocks and preserved Back state.
/// </summary>
public class WorldClockListForm : AppListForm
{
    // ● private
    readonly bool fFavorites;
    readonly DispatcherTimer fTimer = new() { Interval = TimeSpan.FromSeconds(1) };
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
    /// Creates a clock row using the shared card style.
    /// </summary>
    Control CreateItem(FavoriteModel Favorite)
    {
        if (Favorite == null)
            return null;
        return new WorldClockRow(Favorite.TargetKey, Favorite.Name);
    }
    /// <summary>
    /// Reads saved clocks or the complete system timezone catalogue off the UI thread.
    /// </summary>
    List<FavoriteModel> ReadItems()
    {
        List<FavoriteModel> Items;
        if (fFavorites)
            Items = Db.GetFavorites(FavoriteKind.WorldClock);
        else
            Items = TimeZoneInfo.GetSystemTimeZones().Append(TimeZoneInfo.Utc).GroupBy(Zone => Zone.Id, StringComparer.Ordinal)
                .Select(Group => Group.First()).Select(Zone => new FavoriteModel
                {
                    Kind = FavoriteKind.WorldClock, TargetKey = Zone.Id, Context = Zone.DisplayName
                }).ToList();
        foreach (FavoriteModel Item in Items)
            Item.Name = WorldClockService.GetTitle(Item.TargetKey);
        return Items.OrderBy(Item => Item.Name, StringComparer.CurrentCultureIgnoreCase).ToList();
    }
    /// <summary>
    /// Updates realized clock rows from one instant without replacing the list or moving its scroll position.
    /// </summary>
    void OnClockTick(object Sender, EventArgs Args)
    {
        DateTimeOffset Now = DateTimeOffset.UtcNow;
        foreach (WorldClockRow Row in ItemList.GetVisualDescendants().OfType<WorldClockRow>())
            Row.Refresh(Now);
    }
    /// <summary>
    /// Filters the saved collection before paging without changing the existing query or requested page.
    /// </summary>
    void DisplayPage()
    {
        List<FavoriteModel> Items = fItems.Where(Item => Item.Name.Contains(SearchText, StringComparison.CurrentCultureIgnoreCase)).ToList();
        fPage = fFavorites ? 0 : Math.Clamp(fPage, 0, Math.Max(0, (Items.Count - 1) / fPageSize));
        ItemList.ItemsSource = fFavorites ? Items : Items.Skip(fPage * fPageSize).Take(fPageSize).ToList();
        SetResultCount(Items.Count, "clock", "clocks");
        SetHeader((fFavorites ? "Favorites — World Clock" : "World Clock") + $" ({Items.Count:N0})",
            fFavorites ? "Your saved clocks, updated together. Select one for details."
                : "Select a timezone to see details and save it with its heart.", "Search by region, country or city (e.g. Greece)");
        int Pages = fFavorites ? 1 : Math.Max(1, (Items.Count + fPageSize - 1) / fPageSize);
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
        fTimer.Start();
        OnClockTick(this, EventArgs.Empty);
        if (!fReady || !fFavorites)
            return;
        int Version = ++fVersion;
        ScrollViewer Scroller = ItemList.GetVisualDescendants().OfType<ScrollViewer>().FirstOrDefault();
        Vector Offset = fHasSavedOffset ? fSavedOffset : Scroller?.Offset ?? default;
        int ScrollVersion = fScrollVersion;
        try
        {
            List<FavoriteModel> Items = await Task.Run(ReadItems);
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
                ExceptionLog.WriteException("WorldClockListForm.Refresh", Ex, false);
                ShowNotice("Favorites could not be refreshed. Please try again.", true);
            }
        }
    }
    /// <summary>
    /// Invalidates callbacks when the user leaves the list again.
    /// </summary>
    void OnFavoritesUnloaded(object Sender, RoutedEventArgs Args)
    {
        fTimer.Stop();
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
        List<FavoriteModel> Items = await Task.Run(ReadItems, CancellationToken);
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
            if (fFavorites)
                FavoriteNavigation.Open(Favorite);
            else
                MainView.Current?.PushForm(new WorldClockForm(Favorite.TargetKey));
        }
        catch (Exception Ex)
        {
            ExceptionLog.WriteException("WorldClockListForm.Open", Ex, false);
            await MessageBoxForm.Error("This clock could not be opened. Please try again.", true);
        }
    }

    // ● constructor
    /// <summary>
    /// Creates a paged timezone catalogue or one scrollable comparison list of all favorite clocks.
    /// </summary>
    /// <param name="Favorites">True to show every saved clock together without paging.</param>
    public WorldClockListForm(bool Favorites = false)
    {
        fFavorites = Favorites;
        fPageSize = Math.Clamp(Lib.Settings.ItemsPerPage, 10, 100);
        Classes.Add("CompactTitle");
        SetHeader(Favorites ? "Favorites — World Clock" : "World Clock", "Uses your device clock. Works offline.",
            "Search by region, country or city (e.g. Greece)");
        EmptyMessage = Favorites ? "No matching saved clocks. Open World Clock from Services and use the heart to save a timezone."
            : "No timezones match your search.";
        ItemList.ItemTemplate = new FuncDataTemplate<FavoriteModel>((Favorite, Scope) => CreateItem(Favorite));
        Grid Paging = new() { ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto"), Margin = new Thickness(0, 6, 0, 0), IsVisible = !Favorites };
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
        fTimer.Tick += OnClockTick;
        Loaded += OnFavoritesLoaded;
        Unloaded += OnFavoritesUnloaded;
    }

    // ● properties
    /// <summary>
    /// The saved-list loading message.
    /// </summary>
    protected override string LoadingText => "Loading clocks...";
    /// <summary>
    /// The saved-list failure message.
    /// </summary>
    protected override string LoadErrorText => "Clocks could not be loaded.";
}
