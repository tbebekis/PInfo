/* PInfo, Copyright (c) 2026 Theo Bebekis, see License. */

namespace PInfo.Views;

/// <summary>
/// Displays one live timezone comparison using the shared list-card appearance.
/// </summary>
class WorldClockRow : Border
{
    // ● private
    readonly TimeZoneInfo fZone;
    readonly TextBlock fTime = new() { FontSize = 20, VerticalAlignment = Avalonia.Layout.VerticalAlignment.Top };
    readonly TextBlock fContext = new() { FontSize = 12, TextWrapping = TextWrapping.Wrap };
    readonly TextBlock fDifference = new() { FontSize = 12, TextWrapping = TextWrapping.Wrap };

    // ● constructor
    /// <summary>
    /// Creates a row for a timezone identifier, including a recoverable unavailable state.
    /// </summary>
    public WorldClockRow(string ZoneId, string DisplayTitle)
    {
        WorldClockService.TryGetZone(ZoneId, out TimeZoneInfo Zone);
        fZone = Zone;
        Classes.Add("ListItem");
        TextBlock Title = new() { Text = DisplayTitle, TextWrapping = TextWrapping.Wrap };
        Title.Classes.Add("ListItemTitle");
        Grid Heading = new() { ColumnDefinitions = new ColumnDefinitions("*,Auto"), ColumnSpacing = 8 };
        Heading.Children.Add(Title);
        Grid.SetColumn(fTime, 1);
        Heading.Children.Add(fTime);
        fContext.Classes.Add("SecondaryText");
        fDifference.Classes.Add("SecondaryText");
        StackPanel Body = new() { Spacing = 2 };
        Body.Children.Add(Heading);
        Body.Children.Add(fContext);
        Body.Children.Add(fDifference);
        Child = Body;
        Refresh(DateTimeOffset.UtcNow);
    }

    // ● public
    /// <summary>
    /// Updates this row using the same UTC instant as the other visible clocks.
    /// </summary>
    public void Refresh(DateTimeOffset Now)
    {
        if (fZone == null)
        {
            fTime.Text = "—";
            fContext.Text = "This timezone is unavailable on this device.";
            fDifference.Text = "Open details to remove the saved clock.";
            return;
        }
        DateTimeOffset Local = TimeZoneInfo.ConvertTime(Now, fZone);
        fTime.Text = Local.ToString("HH:mm:ss", CultureInfo.CurrentCulture);
        fContext.Text = Local.ToString("ddd dd MMM yyyy", CultureInfo.CurrentCulture) + " · "
            + WorldClockService.FormatOffset(Local.Offset) + " · "
            + (fZone.IsDaylightSavingTime(Now) ? "Daylight saving time" : "Standard time");
        fDifference.Text = WorldClockService.DeviceDifference(Local.Offset, TimeZoneInfo.Local.GetUtcOffset(Now));
    }
}
