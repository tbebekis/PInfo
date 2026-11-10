/* PInfo, Copyright (c) 2026 Theo Bebekis, see License. */

namespace PInfo.Views;

/// <summary>
/// Allows the user to select and persist the application theme, Places refresh interval, and shared page size.
/// </summary>
public partial class SettingsForm : AppForm
{
    // ● private
    /// <summary>
    /// Prevents simultaneous preference edits while a settings snapshot is being saved.
    /// </summary>
    /// <param name="Enabled">Whether the preference controls accept changes.</param>
    void SetEditingEnabled(bool Enabled)
    {
        cboTheme.IsEnabled = Enabled;
        btnSavePlacesRefresh.IsEnabled = Enabled;
        edtPlacesRefreshMonths.IsEnabled = Enabled;
        btnSaveItemsPerPage.IsEnabled = Enabled;
        edtItemsPerPage.IsEnabled = Enabled;
    }
    /// <summary>
    /// Applies the selected theme immediately and saves the preference without blocking the UI.
    /// </summary>
    async void OnThemeSelectionChanged(object Sender, SelectionChangedEventArgs Args)
    {
        if (cboTheme.SelectedItem is not AppTheme Theme)
            return;

        SetEditingEnabled(false);
        SaveNotice.IsVisible = false;
        SaveStatusText.Text = "Saving preference...";
        try
        {
            ThemeManager.Apply(Theme);
            AppHost.Settings.Theme = Theme;
            bool Saved = await AppHost.Settings.SaveAsync();
            SaveStatusText.Text = Saved ? "Preference saved" : "Active for this session";
            SaveNotice.IsVisible = !Saved;
        }
        catch (Exception)
        {
            SaveStatusText.Text = "The theme preference could not be updated.";
            SaveNotice.IsVisible = true;
        }
        finally
        {
            SetEditingEnabled(true);
        }
    }
    /// <summary>
    /// Validates and saves the Places refresh interval without downloading or deleting existing data.
    /// </summary>
    async void OnSavePlacesRefreshClick(object Sender, RoutedEventArgs Args)
    {
        decimal Months = edtPlacesRefreshMonths.Value.GetValueOrDefault();
        if (Months < 1 || Months > 120 || Months != decimal.Truncate(Months))
        {
            PlacesSaveStatusText.Text = "Enter a whole number of months from 1 to 120.";
            return;
        }
        SetEditingEnabled(false);
        SaveNotice.IsVisible = false;
        PlacesSaveStatusText.Text = "Saving preference...";
        try
        {
            Lib.Settings.PlacesRefreshMonths = (int)Months;
            bool Saved = await Lib.Settings.SaveAsync();
            PlacesSaveStatusText.Text = Saved ? "Preference saved" : "Active for this session";
            SaveNotice.IsVisible = !Saved;
        }
        catch (Exception Ex)
        {
            ExceptionLog.WriteException("SettingsForm.OnSavePlacesRefreshClick", Ex, false);
            PlacesSaveStatusText.Text = "The refresh interval could not be saved.";
            SaveNotice.IsVisible = true;
        }
        finally
        {
            SetEditingEnabled(true);
        }
    }
    /// <summary>
    /// Saves the common page size used when opening a paged list.
    /// </summary>
    async void OnSaveItemsPerPageClick(object Sender, RoutedEventArgs Args)
    {
        decimal Count = edtItemsPerPage.Value.GetValueOrDefault();
        if (Count < 10 || Count > 100 || Count != decimal.Truncate(Count))
        {
            PageSizeSaveStatusText.Text = "Enter a whole number from 10 to 100.";
            return;
        }
        SetEditingEnabled(false);
        SaveNotice.IsVisible = false;
        PageSizeSaveStatusText.Text = "Saving preference...";
        try
        {
            Lib.Settings.ItemsPerPage = (int)Count;
            bool Saved = await Lib.Settings.SaveAsync();
            PageSizeSaveStatusText.Text = Saved ? "Preference saved" : "Active for this session";
            SaveNotice.IsVisible = !Saved;
        }
        catch (Exception Ex)
        {
            ExceptionLog.WriteException("SettingsForm.OnSaveItemsPerPageClick", Ex, false);
            PageSizeSaveStatusText.Text = "The page size could not be saved.";
            SaveNotice.IsVisible = true;
        }
        finally
        {
            SetEditingEnabled(true);
        }
    }

    // ● constructor
    /// <summary>
    /// Creates the settings form and displays current preferences without saving during initialization.
    /// </summary>
    public SettingsForm()
    {
        InitializeComponent();
        cboTheme.ItemsSource = Enum.GetValues<AppTheme>();
        cboTheme.SelectedItem = ThemeManager.CurrentTheme;
        cboTheme.SelectionChanged += OnThemeSelectionChanged;
        edtPlacesRefreshMonths.Value = Lib.Settings.PlacesRefreshMonths;
        btnSavePlacesRefresh.Click += OnSavePlacesRefreshClick;
        edtItemsPerPage.Value = Lib.Settings.ItemsPerPage;
        btnSaveItemsPerPage.Click += OnSaveItemsPerPageClick;
    }
}
