using Bunit;
using FluentResults;
using MealWiz.Shared.Features.MealPlans.GetMealPlanByDate;
using MealWiz.Shared.Features.MealPlans.Models;
using MealWiz.Shared.Features.MealPlans.PlanWeek;
using MealWiz.Shared.Features.MealPlans.SaveGeneratedWeek;
using MealWiz.Shared.Features.Meals.GetAllMeals;
using MealWiz.Shared.Features.Meals.Models;
using MealWiz.Shared.Tests.TestInfrastructure;
using Moq;
using Xunit;

namespace MealWiz.Shared.Tests.Features.MealPlans.ComponentTests;

/// <summary>The clock is Wednesday 2026-11-04 (<see cref="ComponentTestBase.Time"/>), so this week starts Monday 2026-11-02.</summary>
public class PlanWeekPageTests : ComponentTestBase
{
    private static readonly DateTime ThisMonday = new(2026, 11, 2);
    private static readonly DateTime NextMonday = ThisMonday.AddDays(7);

    private readonly List<Meal> _meals = [.. Enumerable.Range(1, 10).Select(i => TestData.Meal($"Meal {i}", i))];
    private readonly Meal _leftovers = TestData.Meal("Leftovers", 900, MealType.Leftover);

    public PlanWeekPageTests()
    {
        SetupMeals([.. _meals, _leftovers]);
    }

    private void SetupMeals(List<Meal> meals) =>
        SetupSend<GetAllMeals.Query, Result<List<Meal>>>(Result.Ok(meals));

    private IRenderedComponent<PlanWeekPage> RenderPage(string? week)
    {
        Auth.SetAuthorized("martin@example.com");
        Navigation.NavigateTo(week is null ? "/plan-week" : $"/plan-week?week={week}");
        return Render<PlanWeekPage>();
    }

    private static string Iso(DateTime d) => d.ToString("yyyy-MM-dd");

    private static string Text(IRenderedComponent<PlanWeekPage> cut, string id) => cut.Find(TestId(id)).TextContent.Trim();

    [Fact]
    public void Opens_on_the_requested_week_titled_relative_to_today()
    {
        var cut = RenderPage(Iso(NextMonday));

        Assert.Equal("Next week", Text(cut, "plan-week-title"));
        Assert.Contains("Week 46 · November", cut.Markup);
    }

    [Fact]
    public void A_past_week_opens_on_this_week_with_previous_disabled()
    {
        var cut = RenderPage("2026-10-12");

        Assert.Equal("This week", Text(cut, "plan-week-title"));
        Assert.True(cut.Find(TestId("plan-week-prev")).HasAttribute("disabled"));
    }

    [Fact]
    public void No_week_given_opens_this_week()
    {
        var cut = RenderPage(null);

        Assert.Equal("This week", Text(cut, "plan-week-title"));
    }

    [Fact]
    public void Next_and_previous_step_weeks_and_load_their_plans()
    {
        var cut = RenderPage(Iso(ThisMonday));

        cut.Find(TestId("plan-week-next")).Click();
        cut.WaitForAssertion(() => Assert.Equal("Next week", Text(cut, "plan-week-title")));
        cut.Find(TestId("plan-week-next")).Click();
        cut.WaitForAssertion(() => Assert.Equal("In 2 weeks", Text(cut, "plan-week-title")));
        cut.Find(TestId("plan-week-prev")).Click();
        cut.WaitForAssertion(() => Assert.Equal("Next week", Text(cut, "plan-week-title")));

        Assert.Equal(NextMonday, Sent<GetMealPlanByDate.Query>().Last().Date);
        Assert.False(cut.Find(TestId("plan-week-prev")).HasAttribute("disabled"));
    }

    [Fact]
    public void Planned_days_show_locked_with_their_meal_and_empty_days_say_Empty()
    {
        SetupSend<GetMealPlanByDate.Query, Result<MealPlan?>>(
            Result.Ok<MealPlan?>(TestData.MealPlanWith(NextMonday.AddDays(2), TestData.Meal("Tacos", id: 50))));
        var cut = RenderPage(Iso(NextMonday));

        var days = cut.FindAll(TestId("plan-week-day"));
        Assert.Equal(7, days.Count);
        Assert.Equal("true", days[2].GetAttribute("data-locked"));
        Assert.Equal(Iso(NextMonday.AddDays(2)), days[2].GetAttribute("data-date"));
        Assert.Contains("Tacos", days[2].TextContent);
        Assert.Equal("false", days[0].GetAttribute("data-locked"));
        Assert.Contains("Empty", days[0].TextContent);
        Assert.Empty(days[0].QuerySelectorAll(TestId("plan-week-day-lock")));
    }

    [Fact]
    public void Weeks_back_stepper_changes_the_value_and_stops_at_0_and_8()
    {
        var cut = RenderPage(Iso(ThisMonday));
        Assert.Equal("3 weeks", Text(cut, "plan-week-weeks-back-value"));

        cut.Find(TestId("plan-week-weeks-back-decrease")).Click();
        cut.Find(TestId("plan-week-weeks-back-decrease")).Click();
        cut.WaitForAssertion(() => Assert.Equal("1 week", Text(cut, "plan-week-weeks-back-value")));
        cut.Find(TestId("plan-week-weeks-back-decrease")).Click();
        cut.WaitForAssertion(() => Assert.Equal("0 weeks", Text(cut, "plan-week-weeks-back-value")));
        Assert.True(cut.Find(TestId("plan-week-weeks-back-decrease")).HasAttribute("disabled"));

        for (var i = 0; i < 8; i++)
        {
            cut.Find(TestId("plan-week-weeks-back-increase")).Click();
        }

        cut.WaitForAssertion(() => Assert.Equal("8 weeks", Text(cut, "plan-week-weeks-back-value")));
        Assert.True(cut.Find(TestId("plan-week-weeks-back-increase")).HasAttribute("disabled"));
        LocalStorage.Verify(s => s.SetItemAsync("planweek_settings", new PlanWeekSettings(8), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public void Leftovers_switch_is_stored()
    {
        var cut = RenderPage(Iso(ThisMonday));

        cut.Find(InputOf("plan-week-leftovers-switch")).Change(true);

        cut.WaitForAssertion(() => LocalStorage.Verify(
            s => s.SetItemAsync("planweek_settings", new PlanWeekSettings(3, true), It.IsAny<CancellationToken>()), Times.Once));
    }

    [Fact]
    public void Generate_fills_the_empty_days_and_keeps_locked_ones()
    {
        SetupSend<GetMealPlanByDate.Query, Result<MealPlan?>>(
            Result.Ok<MealPlan?>(TestData.MealPlanWith(NextMonday.AddDays(2), _meals[3])));
        var cut = RenderPage(Iso(NextMonday));

        cut.Find(TestId("plan-week-generate")).Click();

        cut.WaitForAssertion(() => Assert.DoesNotContain("Empty", cut.Find(TestId("plan-week-days")).TextContent));
        var days = cut.FindAll(TestId("plan-week-day"));
        Assert.Contains("Meal 4", days[2].TextContent);
        Assert.Equal("true", days[2].GetAttribute("data-locked"));
        var names = cut.FindAll(TestId("plan-week-day-meal")).Select(e => e.TextContent.Trim()).ToList();
        Assert.Equal(7, names.Distinct().Count());
        Assert.Empty(cut.FindAll(TestId("plan-week-fallback-note")));
    }

    [Fact]
    public void Generate_with_leftovers_alternates()
    {
        LocalStorage
            .Setup(s => s.GetItemAsync<PlanWeekSettings>("planweek_settings", It.IsAny<CancellationToken>()))
            .Returns(ValueTask.FromResult<PlanWeekSettings?>(new PlanWeekSettings(3, true)));
        var cut = RenderPage(Iso(NextMonday));

        cut.Find(TestId("plan-week-generate")).Click();

        cut.WaitForAssertion(() => Assert.DoesNotContain("Empty", cut.Find(TestId("plan-week-days")).TextContent));
        var names = cut.FindAll(TestId("plan-week-day-meal")).Select(e => e.TextContent.Trim()).ToList();
        Assert.Equal(["Leftovers"], names.Where((_, i) => i % 2 == 1).Distinct());
        Assert.DoesNotContain("Leftovers", names.Where((_, i) => i % 2 == 0));
    }

    [Fact]
    public void Unlocking_a_planned_day_lets_generate_replace_it()
    {
        SetupSend<GetMealPlanByDate.Query, Result<MealPlan?>>(
            Result.Ok<MealPlan?>(TestData.MealPlanWith(NextMonday.AddDays(2), _meals[3])));
        SetupSend<MealWiz.Shared.Features.MealPlans.GetMealPlannedDates.GetMealPlannedDates.Query, Result<List<PlannedMealDate>>>(
            Result.Ok(new List<PlannedMealDate> { new(_meals[3].Id, NextMonday.AddDays(2)) }));
        var cut = RenderPage(Iso(NextMonday));

        cut.FindAll(TestId("plan-week-day"))[2].QuerySelector(TestId("plan-week-day-lock"))!.Click();
        Assert.Equal("false", cut.FindAll(TestId("plan-week-day"))[2].GetAttribute("data-locked"));
        cut.Find(TestId("plan-week-generate")).Click();

        cut.WaitForAssertion(() => Assert.DoesNotContain("Empty", cut.Find(TestId("plan-week-days")).TextContent));
        Assert.DoesNotContain("Meal 4", cut.FindAll(TestId("plan-week-day"))[2].TextContent);
    }

    [Fact]
    public void The_fallback_note_shows_when_too_few_fresh_meals()
    {
        SetupMeals([.. _meals.Take(3)]);
        var cut = RenderPage(Iso(NextMonday));

        cut.Find(TestId("plan-week-generate")).Click();

        cut.WaitForAssertion(() => Assert.Contains("Too few meals", Text(cut, "plan-week-fallback-note")));
    }

    [Fact]
    public void No_regular_meals_shows_the_empty_pool_message_and_its_link_goes_to_meals()
    {
        SetupMeals([_leftovers]);
        var cut = RenderPage(Iso(ThisMonday));

        Assert.Contains("No meals to plan with", Text(cut, "plan-week-empty-pool"));
        Assert.Empty(cut.FindAll(TestId("plan-week-generate")));

        cut.Find(TestId("plan-week-go-to-meals")).Click();

        Assert.Equal("meals", CurrentPath);
    }

    [Fact]
    public void Save_is_disabled_until_something_is_generated()
    {
        var cut = RenderPage(Iso(NextMonday));
        Assert.True(cut.Find(TestId("plan-week-save")).HasAttribute("disabled"));

        cut.Find(TestId("plan-week-generate")).Click();

        cut.WaitForAssertion(() => Assert.False(cut.Find(TestId("plan-week-save")).HasAttribute("disabled")));
    }

    [Fact]
    public void Save_writes_the_week_and_lands_on_the_meal_plan_on_that_week()
    {
        SetupSend<SaveGeneratedWeek.Command, Result>(Result.Ok().WithSuccess("Week saved"));
        var cut = RenderPage(Iso(NextMonday));
        cut.Find(TestId("plan-week-generate")).Click();
        cut.WaitForAssertion(() => Assert.False(cut.Find(TestId("plan-week-save")).HasAttribute("disabled")));

        cut.Find(TestId("plan-week-save")).Click();

        cut.WaitForAssertion(() => Assert.Equal("", CurrentPath));
        var command = Assert.Single(Sent<SaveGeneratedWeek.Command>());
        Assert.Equal(NextMonday, command.WeekStart);
        Assert.Equal(7, command.Days.Count);
        Assert.Equal(NextMonday, MealPlan.SelectedWeekStart);
    }

    [Fact]
    public void Back_goes_to_the_meal_plan()
    {
        var cut = RenderPage(Iso(ThisMonday));

        cut.Find(TestId("plan-week-back")).Click();

        Assert.Equal("", CurrentPath);
    }
}
