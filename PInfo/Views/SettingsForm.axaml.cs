/* PInfo, Copyright (c) 2026 Theo Bebekis, see License. */

namespace PInfo.Views;

/// <summary>
/// Allows the user to select and persist the application theme.
/// </summary>
public partial class SettingsForm : AppForm
{
    // ● private
    /// <summary>
    /// Applies the selected theme immediately and saves the preference without blocking the UI.
    /// </summary>
    async void OnThemeSelectionChanged(object Sender, SelectionChangedEventArgs Args)
    {
        if (cboTheme.SelectedItem is not AppTheme Theme)
            return;

        cboTheme.IsEnabled = false;
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
            cboTheme.IsEnabled = true;
        }
    }

    // ● constructor
    /// <summary>
    /// Creates the settings form and selects the currently active theme without saving during initialization.
    /// </summary>
    public SettingsForm()
    {
        InitializeComponent();
        cboTheme.ItemsSource = Enum.GetValues<AppTheme>();
        cboTheme.SelectedItem = ThemeManager.CurrentTheme;
        cboTheme.SelectionChanged += OnThemeSelectionChanged;
    }
}
