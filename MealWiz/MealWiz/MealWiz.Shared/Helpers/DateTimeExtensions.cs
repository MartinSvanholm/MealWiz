namespace MealWiz.Shared.Helpers;

public static class DateTimeExtensions
{
    public static DateTime StartOfWeek(this DateTime dateTime)
    {
        int dayOfWeek = dateTime.DayOfWeek == DayOfWeek.Sunday ? 7 : (int)dateTime.DayOfWeek;

        int diff = 1 - dayOfWeek;
        return dateTime.AddDays(diff);
    }

    public static DateTime EndOfWeek(this DateTime dateTime)
    {
        int dayOfWeek = dateTime.DayOfWeek == DayOfWeek.Sunday ? 7 : (int)dateTime.DayOfWeek;

        int diff = 7 - dayOfWeek;
        return dateTime.AddDays(diff);
    }

    /// <summary>"This week", "Next week", "Last week", "In 3 weeks" or "3 weeks ago", relative to <paramref name="today"/>.</summary>
    public static string ToRelativeWeekTitle(this DateTime weekStart, DateTime today)
    {
        var weeks = (int)Math.Round((weekStart.StartOfWeek().Date - today.StartOfWeek().Date).TotalDays / 7);
        return weeks switch
        {
            0 => "This week",
            1 => "Next week",
            -1 => "Last week",
            > 1 => $"In {weeks} weeks",
            _ => $"{-weeks} weeks ago",
        };
    }
}