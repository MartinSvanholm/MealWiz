using MealWiz.Shared.Localization;
using MealWiz.Shared.Tests.TestInfrastructure;
using Xunit;

namespace MealWiz.Shared.Tests.Localization.UnitTests;

/// <summary>The display helpers follow the current culture; weeks stay hand-computed ISO weeks starting Monday.</summary>
public class DisplayDatesTests
{
    private static readonly DateTime Monday = new(2026, 11, 2);

    [Theory]
    [InlineData("en-US", new[] { "MON", "TUE", "WED", "THU", "FRI", "SAT", "SUN" })]
    [InlineData("da", new[] { "MAN", "TIR", "ONS", "TOR", "FRE", "LØR", "SØN" })]
    public void ToDayTag_IsTheUppercaseShortDayName(string culture, string[] expected)
    {
        using var _ = CultureScope.Use(culture);

        Assert.Equal(expected, Enumerable.Range(0, 7).Select(i => Monday.AddDays(i).ToDayTag()));
    }

    [Theory]
    [InlineData("en-US", "Week 45 · November")]
    [InlineData("da", "Uge 45 · november")]
    public void ToWeekLabel_GivesIsoWeekAndMonth(string culture, string expected)
    {
        using var _ = CultureScope.Use(culture);

        Assert.Equal(expected, Monday.ToWeekLabel());
    }

    [Fact]
    public void ToWeekLabel_UsesIsoWeekNumbers()
    {
        Assert.Equal("Week 53 · December", new DateTime(2026, 12, 28).ToWeekLabel());
        Assert.Equal("Week 1 · December", new DateTime(2025, 12, 29).ToWeekLabel());
    }

    [Theory]
    [InlineData("en-US", "Monday 3 November")]
    [InlineData("da", "mandag 3. november")]
    public void ToFullDate_GivesWeekdayDayAndMonth(string culture, string expected)
    {
        using var _ = CultureScope.Use(culture);

        Assert.Equal(expected, new DateTime(2025, 11, 3).ToFullDate());
    }

    [Theory]
    [InlineData("en-US", DayOfWeek.Monday, "Monday")]
    [InlineData("en-US", DayOfWeek.Sunday, "Sunday")]
    [InlineData("da", DayOfWeek.Monday, "mandag")]
    [InlineData("da", DayOfWeek.Saturday, "lørdag")]
    public void ToWeekdayName_IsTheCulturesDayName(string culture, DayOfWeek day, string expected)
    {
        using var _ = CultureScope.Use(culture);

        Assert.Equal(expected, day.ToWeekdayName());
    }

    [Fact]
    public void Helpers_IgnoreACultureWithAnotherCalendar()
    {
        // th-TH uses the Buddhist calendar (year 2569); week numbers stay ISO and Gregorian.
        using var _ = CultureScope.Use("th-TH");

        Assert.StartsWith("Week 45 · ", Monday.ToWeekLabel());
    }
}
