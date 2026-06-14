using System.Globalization;

namespace GW2RaidsGui.Config;

/// <summary>
/// One day's parsed RAID_HOURS_&lt;DAY&gt; value. A day is either disabled
/// (blank), the Friday-only special value "RESET", or a START-END window.
///
/// When a stored value cannot be parsed we keep the original text in
/// <see cref="OriginalRaw"/> and set <see cref="ParseOk"/> = false, so the UI
/// can surface it without silently discarding the user's data.
/// </summary>
public sealed class RaidHoursValue
{
    public bool Enabled { get; set; }
    public bool IsReset { get; set; }
    public TimeSpan Start { get; set; }
    public TimeSpan End { get; set; }

    /// <summary>Original text as read from config; preserved for round-trips when ParseOk is false.</summary>
    public string OriginalRaw { get; set; } = string.Empty;

    /// <summary>False when OriginalRaw was non-blank but could not be parsed.</summary>
    public bool ParseOk { get; set; } = true;

    // Mirrors the formats accepted by Scripts/Resolve-RaidWindow.ps1 so the GUI
    // and pipeline agree on what is valid.
    // Single-character custom specifiers must be written as "%H"; a bare "H"
    // would be read as a (invalid) standard format specifier and throw.
    private static readonly string[] TimeFormats =
    {
        "h:mmtt", "hh:mmtt", "h:mm tt", "hh:mm tt",
        "htt", "hhtt", "h tt", "hh tt",
        "H:mm", "HH:mm", "%H", "HH",
    };

    public static RaidHoursValue Parse(string? raw, bool allowReset)
    {
        var value = new RaidHoursValue { OriginalRaw = raw ?? string.Empty };

        if (string.IsNullOrWhiteSpace(raw))
        {
            value.Enabled = false;
            return value;
        }

        var text = raw.Trim();

        if (allowReset && text.Equals("RESET", StringComparison.OrdinalIgnoreCase))
        {
            value.Enabled = true;
            value.IsReset = true;
            return value;
        }

        var parts = text.Split('-', 2);
        if (parts.Length == 2
            && TryParseTime(parts[0], out var start)
            && TryParseTime(parts[1], out var end))
        {
            value.Enabled = true;
            value.Start = start;
            value.End = end;
            return value;
        }

        // Non-blank but unparseable: keep it, flag it.
        value.Enabled = true;
        value.ParseOk = false;
        return value;
    }

    /// <summary>Produces the canonical config string for this value.</summary>
    public string ToConfigString()
    {
        if (!Enabled) return string.Empty;
        if (!ParseOk) return OriginalRaw; // round-trip text we couldn't parse
        if (IsReset) return "RESET";
        return $"{FormatTime(Start)}-{FormatTime(End)}";
    }

    public static bool TryParseTime(string? text, out TimeSpan time)
    {
        time = TimeSpan.Zero;
        if (string.IsNullOrWhiteSpace(text)) return false;

        var t = text.Trim().ToUpperInvariant();
        try
        {
            if (DateTime.TryParseExact(t, TimeFormats, CultureInfo.InvariantCulture,
                    DateTimeStyles.None, out var dt))
            {
                time = dt.TimeOfDay;
                return true;
            }
        }
        catch (FormatException)
        {
            // An unusable format specifier should never crash config parsing.
        }
        return false;
    }

    public static string FormatTime(TimeSpan time)
    {
        // 12-hour with AM/PM, no leading zero on the hour: "7:30PM".
        var dt = DateTime.Today.Add(time);
        return dt.ToString("h:mmtt", CultureInfo.InvariantCulture);
    }
}
