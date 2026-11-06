using MealWiz.Shared.Helpers;
using Xunit;

namespace MealWiz.Shared.Tests.Helpers.UnitTests;

/// <summary>
/// Weeks run Monday to Sunday, and the time of day is kept. Callers (MealPlan, MealPlanStateContainer) apply
/// <c>.Date</c> themselves, and GetMealPlanByDate formats <c>Date.Date</c>.
/// </summary>
public class DateTimeExtensionsTests
{
    [Theory]
    [InlineData(19)] // Monday
    [InlineData(20)]
    [InlineData(21)]
    [InlineData(22)]
    [InlineData(23)]
    [InlineData(24)]
    [InlineData(25)] // Sunday belongs to the week that started the Monday before
    public void StartAndEndOfWeek_AreThatWeeksMondayAndSunday(int dayInOctober)
    {
        var date = new DateTime(2026, 10, dayInOctober);

        Assert.Equal(new DateTime(2026, 10, 19), date.StartOfWeek());
        Assert.Equal(new DateTime(2026, 10, 25), date.EndOfWeek());
        Assert.Equal(DayOfWeek.Monday, date.StartOfWeek().DayOfWeek);
        Assert.Equal(DayOfWeek.Sunday, date.EndOfWeek().DayOfWeek);
    }

    [Fact]
    public void StartAndEndOfWeek_KeepTheTimeOfDay()
    {
        var sundayEvening = new DateTime(2026, 10, 25, 18, 30, 15, DateTimeKind.Local);

        Assert.Equal(new DateTime(2026, 10, 19, 18, 30, 15), sundayEvening.StartOfWeek());
        Assert.Equal(new DateTime(2026, 10, 25, 18, 30, 15), sundayEvening.EndOfWeek());
        Assert.Equal(DateTimeKind.Local, sundayEvening.StartOfWeek().Kind);
    }

    [Fact]
    public void StartAndEndOfWeek_CrossMonthAndYearBoundaries()
    {
        var newYearsEve = new DateTime(2026, 12, 31); // Thursday

        Assert.Equal(new DateTime(2026, 12, 28), newYearsEve.StartOfWeek());
        Assert.Equal(new DateTime(2027, 1, 3), newYearsEve.EndOfWeek());
    }

    [Theory]
    [InlineData(2026, 11, 2, "This week")]
    [InlineData(2026, 11, 9, "Next week")]
    [InlineData(2026, 10, 26, "Last week")]
    [InlineData(2026, 11, 23, "In 3 weeks")]
    [InlineData(2026, 10, 19, "2 weeks ago")]
    public void ToRelativeWeekTitle_WeeksAroundToday_NamesThemRelatively(int year, int month, int day, string expected)
    {
        var today = new DateTime(2026, 11, 4); // Wednesday

        Assert.Equal(expected, new DateTime(year, month, day).ToRelativeWeekTitle(today));
    }

    [Fact]
    public void ToRelativeWeekTitle_TodayIsSunday_StillThisWeek()
    {
        var sunday = new DateTime(2026, 11, 8);

        Assert.Equal("This week", new DateTime(2026, 11, 2).ToRelativeWeekTitle(sunday));
    }

    [Fact]
    public void ToWeekLabel_GivesIsoWeekAndMonth()
    {
        Assert.Equal("Week 45 · November", new DateTime(2026, 11, 2).ToWeekLabel());
    }
}
