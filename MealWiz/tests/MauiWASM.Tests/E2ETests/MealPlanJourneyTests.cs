using System.Text.RegularExpressions;
using MauiWASM.Tests.TestInfrastructure.E2E;
using Microsoft.Playwright;
using Xunit;

namespace MauiWASM.Tests.E2ETests;

/// <summary>
/// Days are picked by position in the week strip the page renders, never by a date computed in the test, so the
/// test doesn't depend on the machine's timezone or on running across midnight.
/// </summary>
[Trait("Category", "E2E")]
public class MealPlanJourneyTests : E2ETestBase
{
    private static readonly Regex HasMeal = new(@"\bhas-meal\b");
    private static readonly Regex SelectedDay = new(@"\bselected-day\b");

    [Fact]
    public async Task MealPlan_SpansWeeks_IncludesSystemMeals_AndPersists()
    {
        const string chili = "E2E Chili";
        await Supabase.InsertMealAsync(User, chili, new SeedIngredient("Kidney beans", "400 g"));

        await LoginAsync();

        // This week: a meal of the user's own, then the two system meals every user has
        await AssignAsync(day: 0, chili);
        await AssignAsync(day: 1, "Leftovers");
        await AssignAsync(day: 2, "Eat Out");
        await Expect(Page.GetByTestId("week-day-meal-indicator")).ToHaveCountAsync(3);

        // Swap: the pick list replaces the card, without the day's own meal, and a pick replaces the meal
        await Day(0).ClickAsync();
        await Expect(Page.GetByTestId("meal-display-name")).ToHaveTextAsync(chili);
        await Page.GetByTestId("swap-meal-button").ClickAsync();
        await Expect(Page.GetByTestId("meal-selection-heading")).ToContainTextAsync("Swap the meal for");
        await Expect(Row("meal-list-item", "Leftovers")).ToBeVisibleAsync(); // the list has loaded
        await Expect(Row("meal-list-item", chili)).ToHaveCountAsync(0);
        await Row("meal-list-item", "Leftovers").ClickAsync();
        await Expect(Page.GetByTestId("meal-display-name")).ToHaveTextAsync("Leftovers");

        // Next week
        var thisWeek = await Page.GetByTestId("meal-plan-week-title").InnerTextAsync();
        await Page.GetByTestId("week-next").ClickAsync();
        await Expect(Page.GetByTestId("meal-plan-week-title")).Not.ToHaveTextAsync(thisWeek);
        await Expect(Page.GetByTestId("week-day-meal-indicator")).ToHaveCountAsync(0);
        await AssignAsync(day: 4, chili);

        // Saved, not just held in memory. The page reopens on the week last shown.
        await Page.ReloadAsync();
        await Expect(Day(4)).ToHaveClassAsync(HasMeal);
        await Expect(Page.GetByTestId("week-day-meal-indicator")).ToHaveCountAsync(1);

        await Page.GetByTestId("week-prev").ClickAsync();
        await Expect(Page.GetByTestId("meal-plan-week-title")).ToHaveTextAsync(thisWeek);
        await Expect(Page.GetByTestId("week-day-meal-indicator")).ToHaveCountAsync(3);

        await Day(1).ClickAsync();
        await Expect(Day(1)).ToHaveClassAsync(SelectedDay);
        await Expect(Page.GetByTestId("meal-display-name")).ToHaveTextAsync("Leftovers");

        // Remove one
        await Page.GetByTestId("remove-meal-button").ClickAsync();
        await Expect(Page.GetByTestId("meal-selection-heading")).ToBeVisibleAsync();
        await Expect(Day(1)).Not.ToHaveClassAsync(HasMeal);

        await Page.ReloadAsync();
        await Expect(Day(0)).ToHaveClassAsync(HasMeal);
        await Expect(Day(1)).Not.ToHaveClassAsync(HasMeal);
        await Expect(Day(2)).ToHaveClassAsync(HasMeal);
        await Expect(Page.GetByTestId("week-day-meal-indicator")).ToHaveCountAsync(2);

        // The swap was saved too
        await Day(0).ClickAsync();
        await Expect(Day(0)).ToHaveClassAsync(SelectedDay);
        await Expect(Page.GetByTestId("meal-display-name")).ToHaveTextAsync("Leftovers");
    }

    [Fact]
    public async Task PlanWeek_GeneratesWithLeftoversAroundALockedDay_AndSaves()
    {
        var chili = await Supabase.InsertMealAsync(User, "E2E Chili");
        await Supabase.InsertMealAsync(User, "E2E Curry");
        await Supabase.InsertMealAsync(User, "E2E Pasta");
        var stew = await Supabase.InsertMealAsync(User, "E2E Stew");
        _ = chili;

        await LoginAsync();
        await Page.GetByTestId("week-next").ClickAsync();
        await Expect(Page.GetByTestId("meal-plan-week-title")).ToHaveTextAsync("Next week");
        await Page.GetByTestId("plan-week-button").ClickAsync();
        await Expect(Page.GetByTestId("plan-week-title")).ToHaveTextAsync("Next week");

        // Plan the Wednesday behind the page's back, then reload so it shows up locked.
        var date = (await PlanDay(2).GetAttributeAsync("data-date"))!;
        await Supabase.InsertPlannedMealAsync(User, stew, date);
        await Page.ReloadAsync();
        await Expect(PlanDay(2)).ToHaveAttributeAsync("data-locked", "true");
        await Expect(PlanDay(2)).ToContainTextAsync("E2E Stew");

        await LeftoversSwitch.CheckAsync();
        await Page.GetByTestId("plan-week-generate").ClickAsync();

        foreach (var i in new[] { 1, 3, 5 })
            await Expect(PlanDay(i)).ToContainTextAsync("Leftovers");
        await Expect(PlanDay(2)).ToContainTextAsync("E2E Stew");
        var cooked = new List<string>();
        foreach (var i in new[] { 0, 4, 6 })
        {
            await Expect(PlanDay(i).GetByTestId("plan-week-day-meal")).Not.ToHaveTextAsync("Empty");
            cooked.Add((await PlanDay(i).GetByTestId("plan-week-day-meal").InnerTextAsync()).Trim());
        }

        Assert.Equal(3, cooked.Distinct().Count());
        Assert.All(cooked, name => Assert.Contains(name, new[] { "E2E Chili", "E2E Curry", "E2E Pasta" }));
        await Expect(Page.GetByTestId("plan-week-fallback-note")).ToHaveCountAsync(0);

        await Page.GetByTestId("plan-week-save").ClickAsync();

        await Expect(Page).ToHaveURLAsync(UrlFor(""));
        await Expect(Page.GetByTestId("meal-plan-week-title")).ToHaveTextAsync("Next week");
        await Expect(Page.GetByTestId("week-day-meal-indicator")).ToHaveCountAsync(7);
        await Day(1).ClickAsync();
        await Expect(Page.GetByTestId("meal-display-name")).ToHaveTextAsync("Leftovers");
        await Day(2).ClickAsync();
        await Expect(Page.GetByTestId("meal-display-name")).ToHaveTextAsync("E2E Stew");

        // The settings are remembered on this device.
        await Page.GetByTestId("plan-week-button").ClickAsync();
        await Expect(LeftoversSwitch).ToBeCheckedAsync();
    }

    [Fact]
    public async Task PlanWeek_SkipsMealsUsedInTheNeighbouringWeeks()
    {
        for (var i = 1; i <= 6; i++)
            await Supabase.InsertMealAsync(User, $"E2E Fresh {i}");
        var old = await Supabase.InsertMealAsync(User, "E2E Old");
        var lastWeek = await Supabase.InsertMealAsync(User, "E2E Last week");
        var nextWeek = await Supabase.InsertMealAsync(User, "E2E Next week");

        await LoginAsync();
        await Page.GetByTestId("week-next").ClickAsync();
        await Expect(Page.GetByTestId("meal-plan-week-title")).ToHaveTextAsync("Next week");
        await Page.GetByTestId("plan-week-button").ClickAsync();
        await Expect(Page.GetByTestId("plan-week-title")).ToHaveTextAsync("Next week");

        // Dates one week before and one and two weeks after the target week, read from the page.
        await Page.GetByTestId("plan-week-prev").ClickAsync();
        await Expect(Page.GetByTestId("plan-week-title")).ToHaveTextAsync("This week");
        var before = (await PlanDay(0).GetAttributeAsync("data-date"))!;
        for (var i = 0; i < 2; i++)
            await Page.GetByTestId("plan-week-next").ClickAsync();
        await Expect(Page.GetByTestId("plan-week-title")).ToHaveTextAsync("In 2 weeks");
        var after1 = (await PlanDay(0).GetAttributeAsync("data-date"))!;
        await Page.GetByTestId("plan-week-next").ClickAsync();
        await Expect(Page.GetByTestId("plan-week-title")).ToHaveTextAsync("In 3 weeks");
        var after2 = (await PlanDay(0).GetAttributeAsync("data-date"))!;

        await Supabase.InsertPlannedMealAsync(User, lastWeek, before);
        await Supabase.InsertPlannedMealAsync(User, nextWeek, after1);
        await Supabase.InsertPlannedMealAsync(User, old, after2);

        await Page.ReloadAsync();
        await Page.GetByTestId("plan-week-weeks-back-decrease").ClickAsync();
        await Page.GetByTestId("plan-week-weeks-back-decrease").ClickAsync();
        await Expect(Page.GetByTestId("plan-week-weeks-back-value")).ToHaveTextAsync("1 week");

        await Page.GetByTestId("plan-week-generate").ClickAsync();
        await Expect(Page.GetByTestId("plan-week-day-meal").Filter(new() { HasText = "Empty" })).ToHaveCountAsync(0);

        var names = await Page.GetByTestId("plan-week-day-meal").AllInnerTextsAsync();
        Assert.Equal(
            ["E2E Fresh 1", "E2E Fresh 2", "E2E Fresh 3", "E2E Fresh 4", "E2E Fresh 5", "E2E Fresh 6", "E2E Old"],
            names.Select(n => n.Trim()).Order().ToArray());
        await Expect(Page.GetByTestId("plan-week-fallback-note")).ToHaveCountAsync(0);
    }

    private ILocator PlanDay(int index) => Page.GetByTestId("plan-week-day").Nth(index);

    private ILocator LeftoversSwitch =>
        Page.Locator("input[data-testid=plan-week-leftovers-switch], [data-testid=plan-week-leftovers-switch] input");

    private ILocator Day(int index) => Page.GetByTestId("week-day").Nth(index);

    private async Task AssignAsync(int day, string meal)
    {
        await Day(day).ClickAsync();
        // The heading may still be showing for the previously selected day, so wait for the selection itself.
        await Expect(Day(day)).ToHaveClassAsync(SelectedDay);
        await Expect(Page.GetByTestId("meal-selection-heading")).ToBeVisibleAsync();

        await Row("meal-list-item", meal).ClickAsync();

        await Expect(Page.GetByTestId("meal-display-name")).ToHaveTextAsync(meal);
        await Expect(Day(day)).ToHaveClassAsync(HasMeal);
    }
}
