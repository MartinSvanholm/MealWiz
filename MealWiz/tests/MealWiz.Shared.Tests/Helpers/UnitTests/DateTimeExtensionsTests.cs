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
}
