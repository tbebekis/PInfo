/* PInfo, Copyright (c) 2026 Theo Bebekis, see License. */

namespace PInfo.Views;

/// <summary>
/// Displays all favorite categories, including empty ones, and refreshes their counts on return.
/// </summary>
public class FavoriteCategoriesForm : AppForm
{
    // ● private
    readonly Dictionary<FavoriteKind, Button> fButtons = new();
    readonly TextBlock fStatus = new() { TextWrapping = TextWrapping.Wrap };
    readonly Button fRetry = new() { Content = "Try again", IsVisible = false };
    int fVersion;

    /// <summary>
    /// Reads category counts off the UI thread, discarding detached or superseded refreshes.
    /// </summary>
    async Task RefreshAsync()
    {
        int Version = ++fVersion;
        fStatus.Text = "Loading favorites...";
        fRetry.IsVisible = false;
        try
        {
            Dictionary<FavoriteKind, int> Counts = await Task.Run(Db.GetFavoriteCounts);
            if (Version != fVersion || !this.IsAttachedToVisualTree())
                return;
            foreach (var Item in fButtons)
            {
                if (Item.Value.Content is TextBlock Name)
                    Name.Text = FavoriteService.CategoryName(Item.Key) + $" ({Counts[Item.Key]:N0})";
            }
            fStatus.Text = "Use the heart in a detail, Weather or reference-rate view to save an item.";
        }
        catch (Exception Ex)
        {
            if (Version == fVersion && this.IsAttachedToVisualTree())
            {
                ExceptionLog.WriteException("FavoriteCategoriesForm.Refresh", Ex, false);
                fStatus.Text = "Favorites could not be read. Please try again.";
                fRetry.IsVisible = true;
            }
        }
    }
    /// <summary>
    /// Refreshes counts when the tab becomes visible or retry is requested.
    /// </summary>
    async void OnRefreshRequested(object Sender, RoutedEventArgs Args)
    {
        await RefreshAsync();
    }
    /// <summary>
    /// Invalidates refresh callbacks when leaving this tab or navigating away.
    /// </summary>
    void OnFavoritesUnloaded(object Sender, RoutedEventArgs Args)
    {
        ++fVersion;
    }
    /// <summary>
    /// Opens the selected category's saved list through the existing navigation stack.
    /// </summary>
    void OnCategoryClick(object Sender, RoutedEventArgs Args)
    {
        if (Sender is Button Action && Action.Tag is FavoriteKind Kind)
            MainView.Current?.PushForm(Kind == FavoriteKind.WorldClock
                ? new WorldClockListForm(Favorites: true) : new FavoriteListForm(Kind));
    }

    // ● constructor
    /// <summary>
    /// Creates the category actions without synchronously accessing SQLite.
    /// </summary>
    public FavoriteCategoriesForm()
    {
        Classes.Add("FormBackground");
        StackPanel Body = new() { Spacing = 12, Margin = new Thickness(16) };
        TextBlock Title = new() { Text = "Favorites" };
        Title.Classes.Add("FormTitle");
        Body.Children.Add(Title);
        StackPanel Categories = new() { Spacing = 0 };
        foreach (FavoriteKind Kind in Enum.GetValues<FavoriteKind>())
        {
            TextBlock Name = new() { Text = FavoriteService.CategoryName(Kind) };
            Name.Classes.Add("ListItemTitle");
            Button Action = new() { Content = Name, Tag = Kind };
            Action.Classes.Add("ListItem");
            Action.Click += OnCategoryClick;
            fButtons.Add(Kind, Action);
            Categories.Children.Add(Action);
        }
        Body.Children.Add(Categories);
        fStatus.Classes.Add("SecondaryText");
        Body.Children.Add(fStatus);
        fRetry.Classes.Add("PrimaryButton");
        fRetry.Click += OnRefreshRequested;
        Body.Children.Add(fRetry);
        Content = new ScrollViewer { Content = Body, HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled };
        Loaded += OnRefreshRequested;
        Unloaded += OnFavoritesUnloaded;
    }
}
