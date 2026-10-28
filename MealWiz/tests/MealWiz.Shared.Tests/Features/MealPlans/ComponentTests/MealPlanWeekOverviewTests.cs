using Bunit;
using FluentResults;
using MealWiz.Shared.Features.MealPlans;
using MealWiz.Shared.Features.MealPlans.GetMealPlanByDate;
using MealWiz.Shared.Features.MealPlans.Models;
using MealWiz.Shared.Helpers;
using MealWiz.Shared.Tests.TestInfrastructure;
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
    public void Next_and_previous_move_a_week_and_load_its_plan()
    {
        var cut = Render<MealPlanWeekOverview>();

        cut.Find(TestId("week-next")).Click();
        cut.WaitForAssertion(() => Assert.Equal(Iso(ThisMonday.AddDays(7)), Days(cut)[0]));
        Assert.Equal(ThisMonday.AddDays(7), MealPlan.SelectedDate.Date);

        cut.Find(TestId("week-prev")).Click();
        cut.Find(TestId("week-prev")).Click();
        cut.WaitForAssertion(() => Assert.Equal(Iso(ThisMonday.AddDays(-7)), Days(cut)[0]));

        Assert.Equal(
            [ThisMonday, ThisMonday.AddDays(7), ThisMonday, ThisMonday.AddDays(-7)],
            Sent<GetMealPlanByDate.Query>().Select(q => q.Date));
        LocalStorage.Verify(s => s.SetItemAsync(WeekStorageKey, ThisMonday.AddDays(-7), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public void Days_with_a_meal_are_marked()
    {
        var wednesday = ThisMonday.AddDays(2);
        SetupSend<GetMealPlanByDate.Query, Result<MealPlan?>>(Result.Ok<MealPlan?>(TestData.MealPlanWith(wednesday, TestData.Meal("Tacos"))));

        var cut = Render<MealPlanWeekOverview>();

        var marked = cut.FindAll(TestId("week-day-meal-indicator"));
        Assert.Single(marked);
        Assert.Equal(
            Iso(wednesday),
            marked[0].ParentElement!.QuerySelector(TestId("week-day"))!.GetAttribute("data-date"));
    }
}
