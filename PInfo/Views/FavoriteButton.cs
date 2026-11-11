/* PInfo, Copyright (c) 2026 Theo Bebekis, see License. */

namespace PInfo.Views;

/// <summary>
/// Displays persisted favorite membership and commits each heart toggle before updating its appearance.
/// </summary>
public class FavoriteButton : Button
{
    // ● private
    readonly FavoriteModel fFavorite;
    readonly TextBlock fHeart = new() { FontSize = 22, Text = "♡" };
    bool fBusy;
    bool fStateKnown;

    /// <summary>
    /// Updates the heart and its accessible action description.
    /// </summary>
    void DisplayState(bool Saved)
    {
        fStateKnown = true;
        fHeart.Text = Saved ? "♥" : "♡";
        Classes.Remove("Saved");
        if (Saved)
            Classes.Add("Saved");
        string Description = (Saved ? "Remove " : "Add ") + fFavorite.Name + (Saved ? " from favorites" : " to favorites");
        ToolTip.SetTip(this, Description);
        Avalonia.Automation.AutomationProperties.SetName(this, Description);
    }
    /// <summary>
    /// Refreshes membership on return without retaining subscriptions to global objects.
    /// </summary>
    async void OnFavoriteLoaded(object Sender, RoutedEventArgs Args)
    {
        if (fBusy)
            return;
        fBusy = true;
        IsEnabled = false;
        try
        {
            bool Saved = await Task.Run(() => Db.IsFavorite(fFavorite));
            if (this.IsAttachedToVisualTree())
                DisplayState(Saved);
        }
        catch (Exception Ex)
        {
            ExceptionLog.WriteException("FavoriteButton.Load", Ex, false);
            fStateKnown = false;
            fHeart.Text = "♡?";
            Avalonia.Automation.AutomationProperties.SetName(this, "Retry favorite status");
            ToolTip.SetTip(this, "Favorites could not be read. Press the heart to try again.");
        }
        finally
        {
            fBusy = false;
            IsEnabled = true;
        }
    }
    /// <summary>
    /// Adds or removes the current destination atomically, guarding rapid repeated clicks.
    /// </summary>
    async void OnFavoriteClick(object Sender, RoutedEventArgs Args)
    {
        if (fBusy)
            return;
        if (!fStateKnown)
        {
            OnFavoriteLoaded(Sender, Args);
            return;
        }
        fBusy = true;
        IsEnabled = false;
        try
        {
            bool Saved = await Task.Run(() => Db.ToggleFavorite(fFavorite));
            if (this.IsAttachedToVisualTree())
                DisplayState(Saved);
        }
        catch (Exception Ex)
        {
            ExceptionLog.WriteException("FavoriteButton.Toggle", Ex, false);
            fStateKnown = false;
            fHeart.Text = "♡?";
            if (this.IsAttachedToVisualTree())
                await MessageBoxForm.Error("The favorite could not be changed. Please try again.", true);
        }
        finally
        {
            fBusy = false;
            IsEnabled = true;
        }
    }

    // ● constructor
    /// <summary>
    /// Creates a heart for an identified destination without reading SQLite on the UI thread.
    /// </summary>
    /// <param name="Favorite">The destination identity and offline snapshot.</param>
    public FavoriteButton(FavoriteModel Favorite)
    {
        ArgumentNullException.ThrowIfNull(Favorite);
        fFavorite = Favorite;
        fHeart.Classes.Add("FavoriteHeart");
        Content = fHeart;
        Classes.Add("FavoriteButton");
        MinHeight = 32;
        MinWidth = 40;
        Padding = new Thickness(8, 2);
        DisplayState(false);
        fStateKnown = false;
        Loaded += OnFavoriteLoaded;
        Click += OnFavoriteClick;
    }
}
