using System.Globalization;

namespace MealWiz.Shared.Localization;

/// <summary>
/// Dates as the UI shows them, in the current culture. Week numbers stay hand-computed ISO weeks starting Monday
/// (<see cref="ISOWeek"/>), never the culture's calendar rules. Database date strings don't use these: they stay
/// invariant <c>yyyy-MM-dd</c>.
/// </summary>
public static class DisplayDates
{
    /// <summary>The short uppercase day: "MON" / "MAN". At most three letters, without the abbreviation's dot.</summary>
    public static string ToDayTag(this DateTime date)
    {
        var culture = CultureInfo.CurrentCulture;
        var name = culture.DateTimeFormat.GetAbbreviatedDayName(date.DayOfWeek).TrimEnd('.');
        return (name.Length > 3 ? name[..3] : name).ToUpper(culture);
    }

    /// <summary>"Week 45 · November" / "Uge 45 · november": the ISO week number and the month of the week's Monday.</summary>
    public static string ToWeekLabel(this DateTime weekStart) =>
        string.Format(CultureInfo.CurrentCulture, SharedTexts.Get("Date_WeekLabel"),
            ISOWeek.GetWeekOfYear(weekStart), MonthName(weekStart));

    /// <summary>"Monday 3 November" / "mandag 3. november".</summary>
    public static string ToFullDate(this DateTime date) =>
        date.ToString(SharedTexts.Get("Date_FullDateFormat"), GregorianCulture());

    /// <summary>"Monday" / "mandag": the weekday's full name in the current culture.</summary>
    public static string ToWeekdayName(this DayOfWeek day) => CultureInfo.CurrentCulture.DateTimeFormat.GetDayName(day);

    private static string MonthName(DateTime date) => date.ToString("MMMM", GregorianCulture());

    // Month names come from the Gregorian calendar even in a culture whose default calendar isn't (th-TH).
    private static CultureInfo GregorianCulture()
    {
        var culture = CultureInfo.CurrentCulture;
        if (culture.DateTimeFormat.Calendar is GregorianCalendar)
            return culture;

        var gregorian = (CultureInfo)culture.Clone();
        gregorian.DateTimeFormat.Calendar = new GregorianCalendar();
        return gregorian;
    }
}
