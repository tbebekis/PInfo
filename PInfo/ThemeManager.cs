/* PInfo, Copyright (c) 2026 Theo Bebekis, see License. */

namespace PInfo;

/// <summary>
/// Applies application palettes and the corresponding Fluent accent without recreating views.
/// User preferences are persisted separately by Settings.
/// </summary>
static public class ThemeManager
{
    // ● private
    static AppTheme fCurrentTheme = AppTheme.Classic;

    // ● public
    /// <summary>
    /// Applies a named palette on the UI thread after application XAML has been loaded.
    /// </summary>
    /// <param name="Theme">The palette to apply.</param>
    /// <exception cref="ArgumentOutOfRangeException">The theme is not supported.</exception>
    /// <exception cref="InvalidOperationException">The application has not been created.</exception>
    static public void Apply(AppTheme Theme)
    {
        Dispatcher.UIThread.VerifyAccess();
        if (!Enum.IsDefined(Theme))
            throw new ArgumentOutOfRangeException(nameof(Theme));

        Application App = Application.Current;
        if (App == null)
            throw new InvalidOperationException("The application has not been created.");

        ResourceDictionary Palette = (ResourceDictionary)App.Resources[Theme + "Palette"];
        FluentTheme Fluent = App.Styles.OfType<FluentTheme>().Single();
        foreach (KeyValuePair<object, object> Resource in Palette)
        {
            if (Resource.Key is string Key && Key != "AccentColor")
                App.Resources[Key] = Resource.Value;
        }
        ThemeVariant Variant = Theme == AppTheme.Dark ? ThemeVariant.Dark : ThemeVariant.Light;
        Fluent.Palettes[Variant].Accent = (Color)Palette["AccentColor"];
        App.RequestedThemeVariant = Variant;
        fCurrentTheme = Theme;
    }

    // ● properties
    /// <summary>
    /// The last applied palette; startup applies the saved preference or Classic.
    /// </summary>
    static public AppTheme CurrentTheme => fCurrentTheme;
}
