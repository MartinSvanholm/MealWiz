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
}