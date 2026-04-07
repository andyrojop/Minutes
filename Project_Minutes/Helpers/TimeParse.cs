using System.Globalization;

namespace Project_Minutes.Helpers;

public static class TimeParse
{
    public static bool TryHhMm(string text, out TimeSpan time)
    {
        time = default;
        var s = text.Trim();
        if (s.Length == 0)
            return false;

        if (TimeSpan.TryParseExact(s, @"hh\:mm", CultureInfo.InvariantCulture, out time))
            return true;

        if (TimeSpan.TryParseExact(s, @"h\:mm", CultureInfo.InvariantCulture, out time))
            return true;

        var parts = s.Split(':', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 2)
            return false;

        if (!int.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var h))
            return false;
        if (!int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var m))
            return false;

        if (h is < 0 or > 23 || m is < 0 or > 59)
            return false;

        time = new TimeSpan(h, m, 0);
        return true;
    }

    public static string FormatHhMm(TimeSpan t) =>
        DateTime.Today.Add(t).ToString("HH:mm", CultureInfo.CurrentCulture);
}
