using Bunit;
using FluentResults;
using MealWiz.Shared.Features.MealPlans;
using MealWiz.Shared.Features.MealPlans.GetMealPlanByDate;
using MealWiz.Shared.Features.MealPlans.Models;
using MealWiz.Shared.Helpers;
using MealWiz.Shared.Tests.TestInfrastructure;
using AngleSharp.Dom;
using Moq;
using Xunit;

namespace MealWiz.Shared.Tests.Features.MealPlans.ComponentTests;

/// <summary>Day and week selection in the week strip at the top of the meal plan page.</summary>
public class MealPlanWeekOverviewTests : ComponentTestBase
{
    private const string WeekStorageKey = "mealplan_selected_week";

    private static readonly DateTime ThisMonday = DateTime.Today.StartOfWeek().Date;

    private List<string> Days(IRenderedComponent<MealPlanWeekOverview> cut) =>
        cut.FindAll(TestId("week-day")).Select(d => d.GetAttribute("data-date")!).ToList();

    private static string Iso(DateTime date) => date.ToString("yyyy-MM-dd");

    [Fact]
    public void Renders_the_seven_days_of_the_current_week()
    {
        var cut = Render<MealPlanWeekOverview>();

        Assert.Equal(Enumerable.Range(0, 7).Select(i => Iso(ThisMonday.AddDays(i))), Days(cut));
        Assert.Equal(ThisMonday, Assert.Single(Sent<GetMealPlanByDate.Query>()).Date);
    }

    [Fact]
    public void Restores_the_week_saved_in_local_storage()
    {
        var savedMonday = new DateTime(2026, 1, 5);
        LocalStorage.Setup(s => s.GetItemAsync<DateTime?>(WeekStorageKey, It.IsAny<CancellationToken>())).ReturnsAsync(savedMonday);

        var cut = Render<MealPlanWeekOverview>();

        Assert.Equal(Iso(savedMonday), Days(cut)[0]);
        Assert.Equal(savedMonday, MealPlan.SelectedWeekStart);
    }

    [Fact]
    public void Clicking_a_day_selects_it()
    {
        var cut = Render<MealPlanWeekOverview>();

        cut.Find($"{TestId("week-day")}[data-date=\"{Iso(ThisMonday.AddDays(2))}\"]").Click();

        Assert.Equal(ThisMonday.AddDays(2), MealPlan.SelectedDate.Date);
    }

    [Fact]
    public void Days_with_a_meal_are_marked()
    {
        var wednesday = ThisMonday.AddDays(2);
        SetupSend<GetMealPlanByDate.Query, Result<MealPlan?>>(Result.Ok<MealPlan?>(TestData.MealPlanWith(wednesday, TestData.Meal("Tacos"))));

        var cut = Render<MealPlanWeekOverview>();

        var marked = cut.FindAll(TestId("week-day-meal-indicator"));
        Assert.Single(marked);
        var day = marked[0].Closest(TestId("week-day"))!;
        Assert.Equal(Iso(wednesday), day.GetAttribute("data-date"));
        Assert.Contains("has-meal", day.ClassList);
        Assert.DoesNotContain("empty-day", day.ClassList);
    }

    [Fact]
    public void Days_without_a_meal_are_empty_tiles()
    {
        var wednesday = ThisMonday.AddDays(2);
        SetupSend<GetMealPlanByDate.Query, Result<MealPlan?>>(Result.Ok<MealPlan?>(TestData.MealPlanWith(wednesday, TestData.Meal("Tacos"))));

        var cut = Render<MealPlanWeekOverview>();

        var empty = cut.FindAll(TestId("week-day")).Where(d => d.ClassList.Contains("empty-day")).ToList();
        Assert.Equal(6, empty.Count);
        Assert.All(empty, d => Assert.DoesNotContain("has-meal", d.ClassList));
        Assert.All(empty, d => Assert.NotNull(d.QuerySelector(TestId("week-day-empty"))));
    }

    [Fact]
    public void Only_the_selected_day_is_marked_selected()
    {
        var cut = Render<MealPlanWeekOverview>();

        cut.Find($"{TestId("week-day")}[data-date=\"{Iso(ThisMonday.AddDays(4))}\"]").Click();

        var selected = Assert.Single(cut.FindAll(TestId("week-day")), d => d.ClassList.Contains("selected-day"));
        Assert.Equal(Iso(ThisMonday.AddDays(4)), selected.GetAttribute("data-date"));
        Assert.Equal("true", selected.GetAttribute("aria-pressed"));
    }

    [Fact]
    public void Each_day_shows_its_weekday_and_date()
    {
        var cut = Render<MealPlanWeekOverview>();

        var monday = cut.FindAll(TestId("week-day"))[0];
        Assert.Equal("MON", monday.QuerySelector(TestId("week-day-name"))!.TextContent.Trim());
        Assert.Equal(ThisMonday.Day.ToString(), monday.QuerySelector(TestId("week-day-date"))!.TextContent.Trim());
    }
}
