/* PInfo, Copyright (c) 2026 Theo Bebekis, see License. */

namespace PInfo;

/// <summary>
/// Provides offline world clocks using the device clock and operating-system timezone rules.
/// </summary>
public class WorldClockService : PublicService
{
    // ● private
    static readonly Lazy<Dictionary<string, string>> fTitles = new(LoadTitles);

    /// <summary>
    /// Loads bundled IANA country titles once, independently of the operating-system clock rules.
    /// </summary>
    static Dictionary<string, string> LoadTitles()
    {
        using Stream Input = typeof(WorldClockService).Assembly.GetManifestResourceStream("PInfo.world-clock-titles.json");
        if (Input == null)
            throw new InvalidOperationException("The bundled timezone country titles could not be found.");
        return JsonSerializer.Deserialize<Dictionary<string, string>>(Input)
            ?? throw new JsonException("Invalid bundled timezone country titles.");
    }

    // ● constructor
    /// <summary>
    /// Creates the World Clock service without loading timezone data or accessing the network.
    /// </summary>
    public WorldClockService() : base("World Clock")
    {
    }

    // ● public
    /// <summary>
    /// Returns a prepared area/country/location title, retaining the identifier when no country mapping exists.
    /// </summary>
    static public string GetTitle(string ZoneId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ZoneId);
        return fTitles.Value.TryGetValue(ZoneId, out string Title) ? Title : ZoneId.Replace('_', ' ').Replace("/", " / ");
    }
    /// <summary>
    /// Formats a signed UTC offset, retaining half-hour and quarter-hour zones.
    /// </summary>
    static public string FormatOffset(TimeSpan Offset)
    {
        int Minutes = (int)Offset.TotalMinutes;
        int Absolute = Math.Abs(Minutes);
        return "UTC" + (Minutes < 0 ? "-" : "+") + (Absolute / 60).ToString("00", CultureInfo.InvariantCulture)
            + ":" + (Absolute % 60).ToString("00", CultureInfo.InvariantCulture);
    }
    /// <summary>
    /// Describes the simultaneous offset difference relative to the device timezone.
    /// </summary>
    static public string DeviceDifference(TimeSpan Offset, TimeSpan DeviceOffset)
    {
        int Minutes = (int)(Offset - DeviceOffset).TotalMinutes;
        if (Minutes == 0)
            return "Same time as your device";
        int Absolute = Math.Abs(Minutes);
        return $"{Absolute / 60}h {Absolute % 60:00}m " + (Minutes > 0 ? "ahead of your device" : "behind your device");
    }
    /// <summary>
    /// Resolves an identifier without substituting a different zone if it is unavailable.
    /// </summary>
    static public bool TryGetZone(string ZoneId, out TimeZoneInfo Zone)
    {
        Zone = null;
        if (string.IsNullOrWhiteSpace(ZoneId))
            return false;
        try
        {
            Zone = TimeZoneInfo.FindSystemTimeZoneById(ZoneId);
            return true;
        }
        catch (TimeZoneNotFoundException) { return false; }
        catch (InvalidTimeZoneException) { return false; }
        catch (ArgumentException) { return false; }
    }
}
