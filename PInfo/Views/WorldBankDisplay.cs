/* PInfo, Copyright (c) 2026 Theo Bebekis, see License. */

namespace PInfo.Views;

/// <summary>
/// Formats annual World Bank values without confusing displayed units with chart-axis abbreviations.
/// </summary>
static class WorldBankDisplay
{
    // ● public
    /// <summary>
    /// Formats a source observation with units, optionally retaining the decimal precision returned by the API.
    /// </summary>
    /// <param name="Code">The indicator code.</param>
    /// <param name="Observation">The available source value, or null.</param>
    /// <param name="Detailed">Shows the source decimal precision rather than a compact overview.</param>
    /// <returns>The formatted observation or an unavailable marker.</returns>
    static public string Value(string Code, decimal? Observation, bool Detailed = false)
    {
        if (!Observation.HasValue)
            return "—";
        string Format = Detailed ? "G29" : Code == "SP.POP.TOTL" ? "N0" : "N2";
        string Unit = Code switch
        {
            "SP.POP.TOTL" => " people", "NY.GDP.PCAP.CD" => " US$",
            "SP.DYN.LE00.IN" => " years", "VC.IHR.PSRC.P5" => " per 100,000 people",
            "GOV_WGI_VA.SC" or "GOV_WGI_CC.SC" or "GOV_WGI_RL.SC" or "GOV_WGI_PV.SC" => " points / 100",
            _ => " %"
        };
        return Observation.Value.ToString(Format, CultureInfo.CurrentCulture) + Unit;
    }
    /// <summary>
    /// Formats a chart-axis tick in the original unit with readable thousand, million and billion suffixes.
    /// </summary>
    /// <param name="Number">The chart scale value.</param>
    /// <returns>The abbreviated tick text.</returns>
    static public string Axis(double Number)
    {
        double Absolute = Math.Abs(Number);
        if (Absolute >= 1_000_000_000)
            return (Number / 1_000_000_000).ToString("0.##", CultureInfo.CurrentCulture) + "B";
        if (Absolute >= 1_000_000)
            return (Number / 1_000_000).ToString("0.##", CultureInfo.CurrentCulture) + "M";
        if (Absolute >= 1_000)
            return (Number / 1_000).ToString("0.##", CultureInfo.CurrentCulture) + "K";
        return Number.ToString("0.##", CultureInfo.CurrentCulture);
    }
}
