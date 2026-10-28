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
    }

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
