/* PInfo, Copyright (c) 2026 Theo Bebekis, see License. */

namespace PInfo.Views;

/// <summary>
/// Displays a live timezone clock, UTC and device comparisons, daylight-saving information and a favorite heart.
/// </summary>
public class WorldClockForm : AppCardForm
{
    // ● private
    readonly string fZoneId;
    readonly DispatcherTimer fTimer = new() { Interval = TimeSpan.FromSeconds(1) };
    readonly TextBlock fTime = new() { FontSize = 36, TextWrapping = TextWrapping.Wrap };
    readonly TextBlock fDate = new() { TextWrapping = TextWrapping.Wrap };
    readonly TextBlock fOffset;
    readonly TextBlock fDaylight;
    readonly TextBlock fStandardOffset;
    readonly TextBlock fAdjustment;
    readonly TextBlock fDifference;
    readonly TextBlock fDevice;
    readonly TextBlock fUtc;
    TimeZoneInfo fZone;

    /// <summary>
    /// Adds a labeled value which can be refreshed without rebuilding the card or its scroll position.
    /// </summary>
    TextBlock AddLiveField(StackPanel Section, string Caption)
    {
        StackPanel Field = new();
        Field.Classes.Add("CardField");
        TextBlock Label = new() { Text = Caption };
        Label.Classes.Add("FieldLabel");
        TextBlock Value = new() { TextWrapping = TextWrapping.Wrap };
        Value.Classes.Add("FieldValue");
        Field.Children.Add(Label);
        Field.Children.Add(Value);
        Section.Children.Add(Field);
        return Value;
    }
    /// <summary>
    /// Updates all comparisons from one instant, including date changes and DST transitions.
    /// </summary>
    void Refresh()
    {
        if (fZone == null)
        {
            fTime.Text = "Timezone unavailable";
            fDate.Text = "You can remove this saved clock with its heart.";
            return;
        }
        DateTimeOffset Now = DateTimeOffset.UtcNow;
        DateTimeOffset Local = TimeZoneInfo.ConvertTime(Now, fZone);
        TimeZoneInfo DeviceZone = TimeZoneInfo.Local;
        DateTimeOffset DeviceTime = TimeZoneInfo.ConvertTime(Now, DeviceZone);
        fTime.Text = Local.ToString("HH:mm:ss", CultureInfo.CurrentCulture);
        fDate.Text = Local.ToString("dddd, dd MMMM yyyy", CultureInfo.CurrentCulture);
        fOffset.Text = WorldClockService.FormatOffset(Local.Offset);
        fDaylight.Text = fZone.IsDaylightSavingTime(Now) ? "Daylight saving time is active"
            : fZone.SupportsDaylightSavingTime ? "Standard time is active" : "No daylight saving time rules";
        fStandardOffset.Text = WorldClockService.FormatOffset(fZone.BaseUtcOffset);
        fAdjustment.Text = ((int)(Local.Offset - fZone.BaseUtcOffset).TotalMinutes).ToString("+0;-0;0", CultureInfo.CurrentCulture) + " minutes";
        fDifference.Text = WorldClockService.DeviceDifference(Local.Offset, DeviceTime.Offset);
        fDevice.Text = DeviceTime.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.CurrentCulture) + " · " + DeviceZone.Id;
        fUtc.Text = Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.CurrentCulture) + " UTC";
    }
    /// <summary>
    /// Starts live updates when visible and resolves the stored zone again when returning.
    /// </summary>
    void OnClockLoaded(object Sender, RoutedEventArgs Args)
    {
        WorldClockService.TryGetZone(fZoneId, out fZone);
        Refresh();
        fTimer.Start();
    }
    /// <summary>
    /// Stops the timer while this detail form is outside the navigation view.
    /// </summary>
    void OnClockUnloaded(object Sender, RoutedEventArgs Args)
    {
        fTimer.Stop();
    }
    /// <summary>
    /// Updates the visible clock on each timer tick.
    /// </summary>
    void OnClockTick(object Sender, EventArgs Args)
    {
        Refresh();
    }

    // ● constructor
    /// <summary>
    /// Creates timezone details; only its identifier is persisted when saved as a favorite.
    /// </summary>
    public WorldClockForm(string ZoneId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ZoneId);
        fZoneId = ZoneId;
        WorldClockService.TryGetZone(ZoneId, out fZone);
        SetHeader(WorldClockService.GetTitle(ZoneId), fZone?.DisplayName ?? ZoneId, "🕒");
        AddTitleAction(new FavoriteButton(FavoriteService.WorldClock(ZoneId)));
        StackPanel Clock = CreateSection("Current time");
        fDate.Classes.Add("BodyText");
        Clock.Children.Add(fTime);
        Clock.Children.Add(fDate);
        AddSection(Clock);
        StackPanel Information = CreateSection("Timezone information");
        AddField(Information, "Timezone identifier", ZoneId);
        fOffset = AddLiveField(Information, "Current UTC offset");
        fDaylight = AddLiveField(Information, "Daylight saving time");
        fStandardOffset = AddLiveField(Information, "Standard UTC offset");
        fAdjustment = AddLiveField(Information, "Current adjustment from standard offset");
        AddSection(Information);
        StackPanel Comparison = CreateSection("Compare clocks");
        fDifference = AddLiveField(Comparison, "Compared with your device");
        fDevice = AddLiveField(Comparison, "Your device time");
        fUtc = AddLiveField(Comparison, "UTC time");
        AddSection(Comparison);
        SetSource("Uses your device clock and operating-system timezone rules. Works offline; accuracy depends on the device clock and updated timezone data.");
        fTimer.Tick += OnClockTick;
        Loaded += OnClockLoaded;
        Unloaded += OnClockUnloaded;
        Refresh();
    }
}
