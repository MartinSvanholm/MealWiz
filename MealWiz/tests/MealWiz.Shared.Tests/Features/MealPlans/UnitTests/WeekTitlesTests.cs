using MealWiz.Shared.Features.MealPlans;
using MealWiz.Shared.Tests.TestInfrastructure;
using Xunit;

namespace MealWiz.Shared.Tests.Features.MealPlans.UnitTests;

public class WeekTitlesTests
{
    private static readonly Microsoft.Extensions.Localization.IStringLocalizer<MealPlansResource> Localizer =
        TestLocalizer.For<MealPlansResource>();

    [Theory]
    [InlineData(2026, 11, 2, "This week")]
    [InlineData(2026, 11, 9, "Next week")]
    [InlineData(2026, 10, 26, "Last week")]
    [InlineData(2026, 11, 23, "In 3 weeks")]
    [InlineData(2026, 10, 19, "2 weeks ago")]
    public void ToRelativeWeekTitle_WeeksAroundToday_NamesThemRelatively(int year, int month, int day, string expected)
    {
        var today = new DateTime(2026, 11, 4); // Wednesday

        Assert.Equal(expected, new DateTime(year, month, day).ToRelativeWeekTitle(today, Localizer));
    }

    [Fact]
    public void ToRelativeWeekTitle_TodayIsSunday_StillThisWeek()
    {
        var sunday = new DateTime(2026, 11, 8);

        Assert.Equal("This week", new DateTime(2026, 11, 2).ToRelativeWeekTitle(sunday, Localizer));
    }

    [Theory]
    [InlineData(2026, 11, 2, "Denne uge")]
    [InlineData(2026, 11, 9, "Næste uge")]
    [InlineData(2026, 10, 26, "Sidste uge")]
    [InlineData(2026, 11, 23, "Om 3 uger")]
    [InlineData(2026, 10, 19, "For 2 uger siden")]
    public void ToRelativeWeekTitle_InDanish(int year, int month, int day, string expected)
    {
        using var _ = CultureScope.Use("da");

        Assert.Equal(expected, new DateTime(year, month, day).ToRelativeWeekTitle(new DateTime(2026, 11, 4), Localizer));
    }
}
