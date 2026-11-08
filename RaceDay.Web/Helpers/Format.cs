using System.Globalization;
using RaceDay.Web.Models;

namespace RaceDay.Web.Helpers;

/// <summary>Display formatting used by the views, so dates, times and prices look the same on every page.</summary>
public static class Format
{
    private static readonly CultureInfo Culture = CultureInfo.InvariantCulture;

    public static string Date(DateTime date) => date.ToString("ddd, dd MMM yyyy", Culture);

    public static string ShortDate(DateTime date) => date.ToString("dd MMM yyyy", Culture);

    public static string DateTimeText(DateTime date) => date.ToString("dd MMM yyyy, HH:mm", Culture);

    public static string Money(decimal amount) => amount == 0 ? "Free" : "R " + amount.ToString("N2", Culture);

    public static string Distance(decimal km) => km.ToString("0.##", Culture) + " km";

    /// <summary>hh:mm:ss, or a dash when there is no time (for example DNF).</summary>
    public static string Time(TimeSpan? time) =>
        time.HasValue
            ? $"{(int)time.Value.TotalHours:00}:{time.Value.Minutes:00}:{time.Value.Seconds:00}"
            : "-";

    /// <summary>1st, 2nd, 3rd, 4th ... 11th, 12th, 13th, 21st ...</summary>
    public static string Ordinal(int number)
    {
        var abs = Math.Abs(number);
        string suffix;
        if (abs % 100 is >= 11 and <= 13)
        {
            suffix = "th";
        }
        else
        {
            suffix = (abs % 10) switch { 1 => "st", 2 => "nd", 3 => "rd", _ => "th" };
        }

        return number + suffix;
    }

    /// <summary>For example "47th of 312 finishers".</summary>
    public static string Position(ResultDto result)
    {
        if (result.Status != "Finished") return result.Status == "DQ" ? "Disqualified" : "Did not finish";
        if (result.OverallPosition is not int position) return "-";
        return result.TotalFinishers > 0
            ? $"{Ordinal(position)} of {result.TotalFinishers} finishers"
            : Ordinal(position);
    }

    /// <summary>Accepts hh:mm:ss (1:02:45) or mm:ss (22:14). Returns false for anything else or a zero time.</summary>
    public static bool TryParseFinishTime(string? text, out TimeSpan time)
    {
        time = default;
        if (string.IsNullOrWhiteSpace(text)) return false;

        var parts = text.Trim().Split(':');
        int hours = 0, minutes, seconds;

        if (parts.Length == 3)
        {
            if (!int.TryParse(parts[0], NumberStyles.None, Culture, out hours)) return false;
            if (!int.TryParse(parts[1], NumberStyles.None, Culture, out minutes)) return false;
            if (!int.TryParse(parts[2], NumberStyles.None, Culture, out seconds)) return false;
        }
        else if (parts.Length == 2)
        {
            if (!int.TryParse(parts[0], NumberStyles.None, Culture, out minutes)) return false;
            if (!int.TryParse(parts[1], NumberStyles.None, Culture, out seconds)) return false;
        }
        else
        {
            return false;
        }

        if (minutes > 59 || seconds > 59 || hours > 99) return false;

        time = new TimeSpan(hours, minutes, seconds);
        return time > TimeSpan.Zero;
    }

    public static string TypeIcon(string? eventType) => eventType switch
    {
        "Run" => "bi-lightning-charge-fill",
        "Walk" => "bi-person-walking",
        "Cycle" => "bi-bicycle",
        _ => "bi-flag-fill"
    };

    public static string TypeClass(string? eventType) => "rd-type-" + (eventType ?? "other").ToLowerInvariant();

    public static string Initials(string? name)
    {
        if (string.IsNullOrWhiteSpace(name)) return "?";
        var parts = name.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return parts.Length == 1
            ? parts[0][..1].ToUpperInvariant()
            : (parts[0][..1] + parts[^1][..1]).ToUpperInvariant();
    }
}
