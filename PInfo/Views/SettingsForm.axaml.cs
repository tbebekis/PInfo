/* PInfo, Copyright (c) 2026 Theo Bebekis, see License. */

namespace PInfo.Views;

/// <summary>
/// Allows the user to select and persist the application theme, data preferences, shared page size, and weather temperature unit.
/// </summary>
public partial class SettingsForm : AppForm
{
    // ● private
    readonly Dictionary<string, NumericUpDown> fRefreshEditors = new();

    /// <summary>
    /// Prevents simultaneous preference edits while a settings snapshot is being saved.
    /// </summary>
    /// <param name="Enabled">Whether the preference controls accept changes.</param>
    void SetEditingEnabled(bool Enabled)
    {
        cboTheme.IsEnabled = Enabled;
        btnSaveRefresh.IsEnabled = Enabled;
        btnDefaultRefresh.IsEnabled = Enabled;
        foreach (NumericUpDown Editor in fRefreshEditors.Values)
            Editor.IsEnabled = Enabled;
        btnSaveItemsPerPage.IsEnabled = Enabled;
        edtItemsPerPage.IsEnabled = Enabled;
        cboTemperatureUnit.IsEnabled = Enabled;
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
    /// Builds all refresh controls from the same bounds and defaults used by settings validation.
    /// </summary>
    void CreateRefreshEditors()
    {
        foreach (var Option in PInfo.Settings.RefreshIntervals)
        {
            TextBlock Label = new() { Text = Option.Caption + " (" + Option.Unit + ")" };
            Label.Classes.Add("ListItemTitle");
            NumericUpDown Editor = new()
            {
                Minimum = Option.Minimum, Maximum = Option.Maximum, Increment = 1, FormatString = "0",
                Value = Lib.Settings.GetRefreshInterval(Option.PropertyName), AllowSpin = true
            };
            Avalonia.Automation.AutomationProperties.SetName(Editor, Option.Caption + " refresh interval in " + Option.Unit);
            StackPanel Body = new() { Spacing = 4 };
            Body.Children.Add(Label);
            Body.Children.Add(Editor);
            Body.Children.Add(new TextBlock { Text = $"Default: {Option.DefaultValue} {Option.Unit}", Classes = { "SecondaryText" } });
            Border Card = new() { Child = Body };
            Card.Classes.Add("ListItem");
            RefreshItemsPanel.Children.Add(Card);
            fRefreshEditors.Add(Option.PropertyName, Editor);
        }
    }
    /// <summary>
    /// Saves all refresh intervals together; failed persistence leaves the choices active for this session.
    /// </summary>
    async void OnSaveRefreshClick(object Sender, RoutedEventArgs Args)
    {
        Dictionary<string, int> Values = new();
        foreach (var Option in PInfo.Settings.RefreshIntervals)
        {
            decimal Value = fRefreshEditors[Option.PropertyName].Value.GetValueOrDefault();
            if (Value < Option.Minimum || Value > Option.Maximum || Value != decimal.Truncate(Value))
            {
                RefreshSaveStatusText.Text = $"{Option.Caption}: enter a whole number from {Option.Minimum} to {Option.Maximum}.";
                return;
            }
            Values.Add(Option.PropertyName, (int)Value);
        }
        SetEditingEnabled(false);
        SaveNotice.IsVisible = false;
        RefreshSaveStatusText.Text = "Saving intervals...";
        try
        {
            foreach (var Value in Values)
                Lib.Settings.SetRefreshInterval(Value.Key, Value.Value);
            bool Saved = await Lib.Settings.SaveAsync();
            RefreshSaveStatusText.Text = Saved ? "Intervals saved" : "Active for this session";
            SaveNotice.IsVisible = !Saved;
        }
        catch (Exception Ex)
        {
            ExceptionLog.WriteException("SettingsForm.SaveRefresh", Ex, false);
            RefreshSaveStatusText.Text = "The refresh intervals could not be saved.";
            SaveNotice.IsVisible = true;
        }
        finally
        {
            SetEditingEnabled(true);
        }
    }
    /// <summary>
    /// Restores only the refresh editor values; the user presses Save to persist them.
    /// </summary>
    void OnDefaultRefreshClick(object Sender, RoutedEventArgs Args)
    {
        foreach (var Option in PInfo.Settings.RefreshIntervals)
            fRefreshEditors[Option.PropertyName].Value = Option.DefaultValue;
        RefreshSaveStatusText.Text = "Default intervals selected. Press Save to apply them.";
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
    /// <summary>
    /// Applies and saves the shared temperature unit without changing cached weather measurements.
    /// </summary>
    async void OnTemperatureUnitChanged(object Sender, SelectionChangedEventArgs Args)
    {
        if (cboTemperatureUnit.SelectedIndex < 0 || cboTemperatureUnit.SelectedIndex > 1)
            return;
        SetEditingEnabled(false);
        SaveNotice.IsVisible = false;
        TemperatureSaveStatusText.Text = "Saving preference...";
        try
        {
            Lib.Settings.TemperatureUnit = cboTemperatureUnit.SelectedIndex == 1
                ? TemperatureUnit.Fahrenheit : TemperatureUnit.Celsius;
            bool Saved = await Lib.Settings.SaveAsync();
            TemperatureSaveStatusText.Text = Saved ? "Preference saved" : "Active for this session";
            SaveNotice.IsVisible = !Saved;
        }
        catch (Exception Ex)
        {
            ExceptionLog.WriteException("SettingsForm.OnTemperatureUnitChanged", Ex, false);
            TemperatureSaveStatusText.Text = "The temperature unit could not be saved.";
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
        CreateRefreshEditors();
        btnSaveRefresh.Click += OnSaveRefreshClick;
        btnDefaultRefresh.Click += OnDefaultRefreshClick;
        edtItemsPerPage.Value = Lib.Settings.ItemsPerPage;
        btnSaveItemsPerPage.Click += OnSaveItemsPerPageClick;
        cboTemperatureUnit.ItemsSource = new[] { "Celsius (°C)", "Fahrenheit (°F)" };
        cboTemperatureUnit.SelectedIndex = Lib.Settings.TemperatureUnit == TemperatureUnit.Fahrenheit ? 1 : 0;
        cboTemperatureUnit.SelectionChanged += OnTemperatureUnitChanged;
    }
}
