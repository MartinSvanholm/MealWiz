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
        await Expect(Drawer.GetByTestId("drawer-title")).ToHaveTextAsync("Remove Leftovers?");
        await Expect(Drawer.GetByTestId("remove-meal-grocery-row")).ToHaveCountAsync(0);
        await Drawer.GetByTestId("remove-meal-confirm").ClickAsync();
        await Expect(Drawer).ToHaveCountAsync(0);
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
    public async Task PlanWeek_GeneratesWithLeftoversAroundALockedDay_AndSaves_AndAddsItsIngredients()
    {
        // One distinct ingredient per meal, none a substring of another, for the grocery steps after Save (MEA-83).
        foreach (var name in new[] { "Chili", "Curry", "Pasta", "Tacos" })
            await Supabase.InsertMealAsync(User, $"E2E {name}", new SeedIngredient($"{name} spice", "1 tsp"));
        var stew = await Supabase.InsertMealAsync(User, "E2E Stew", new SeedIngredient("Stew spice", "1 tsp"));

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
        Assert.All(cooked, name => Assert.Contains(name, new[] { "E2E Chili", "E2E Curry", "E2E Pasta", "E2E Tacos" }));
        await Expect(Page.GetByTestId("plan-week-fallback-note")).ToHaveCountAsync(0);

        // Adjust the preview (MEA-80): shuffle, pick and clear a day, then back and Cancel.
        var unlocked = Page.Locator("[data-testid=plan-week-day][data-locked=false]")
            .Filter(new() { HasNot = Page.GetByText("Leftovers", new() { Exact = true }) });
        await Expect(unlocked).ToHaveCountAsync(3);
        ILocator ByDate(string d) => Page.Locator($"[data-testid=plan-week-day][data-date='{d}']");

        var shuffledDate = (await unlocked.Nth(0).GetAttributeAsync("data-date"))!;
        var before = (await ByDate(shuffledDate).GetByTestId("plan-week-day-meal").InnerTextAsync()).Trim();
        await ByDate(shuffledDate).GetByTestId("plan-week-day-shuffle").ClickAsync();
        await Expect(ByDate(shuffledDate).GetByTestId("plan-week-day-meal")).Not.ToHaveTextAsync(before);

        var pickedDate = (await unlocked.Nth(1).GetAttributeAsync("data-date"))!;
        await ByDate(pickedDate).GetByTestId("plan-week-day-pick").ClickAsync();
        await Expect(Drawer.GetByTestId("drawer-title")).ToContainTextAsync("Pick a meal for");
        await Expect(Drawer.GetByTestId("meal-list-item").Filter(new() { HasText = "Leftovers" })).ToBeVisibleAsync();
        await Drawer.GetByTestId("meal-list-item").Filter(new() { HasText = "Eat Out" }).GetByTestId("meal-selection-pick").ClickAsync();
        await Expect(ByDate(pickedDate).GetByTestId("plan-week-day-meal")).ToHaveTextAsync("Eat Out");
        await Expect(ByDate(pickedDate)).ToHaveAttributeAsync("data-locked", "true");

        var clearedDate = (await unlocked.Nth(1).GetAttributeAsync("data-date"))!;
        Assert.NotEqual(pickedDate, clearedDate);
        await ByDate(clearedDate).GetByTestId("plan-week-day-clear").ClickAsync();
        await Expect(ByDate(clearedDate).GetByTestId("plan-week-day-empty")).ToBeVisibleAsync();

        await Page.GetByTestId("plan-week-back").ClickAsync();
        await Expect(ConfirmDrawer.GetByTestId("confirm-drawer-title")).ToHaveTextAsync("Discard this plan?");
        await Expect(ConfirmDrawer.GetByTestId("confirm-drawer-confirm")).ToHaveTextAsync("Discard");
        await ConfirmDrawer.GetByTestId("confirm-drawer-cancel").ClickAsync();
        await Expect(ConfirmDrawer).ToHaveCountAsync(0);
        await Expect(ByDate(pickedDate).GetByTestId("plan-week-day-meal")).ToHaveTextAsync("Eat Out");
        await Expect(ByDate(clearedDate).GetByTestId("plan-week-day-empty")).ToBeVisibleAsync();

        await Page.GetByTestId("plan-week-save").ClickAsync();

        await Expect(Page).ToHaveURLAsync(UrlFor(""));
        await Expect(Page.GetByTestId("meal-plan-week-title")).ToHaveTextAsync("Next week");
        await Expect(Page.GetByTestId("week-day-meal-indicator")).ToHaveCountAsync(6);

        // The saved week offers its ingredients (MEA-83): accept, add them, and see them once on the list.
        await Expect(Drawer.GetByTestId("drawer-title")).ToHaveTextAsync("Week saved");
        await Expect(Drawer.GetByTestId("week-ingredients-prompt-title")).ToHaveTextAsync("Add this week's ingredients to the grocery list?");
        // The header label ("Week NN · Month") is upper-cased by CSS; only its number is used.
        var week = Regex.Match(await Page.GetByTestId("screen-header-label").InnerTextAsync(), @"\d+").Value;
        await Drawer.GetByTestId("week-ingredients-prompt-accept").ClickAsync();
        await Expect(Drawer.GetByTestId("drawer-title")).ToHaveTextAsync($"Week {week} ingredients");
        await Expect(Drawer.GetByTestId("plan-ingredient-item").First).ToBeVisibleAsync();
        var names = (await Drawer.GetByTestId("plan-ingredient-name").AllInnerTextsAsync()).Select(n => n.Trim()).ToList();
        Assert.NotEmpty(names);
        Assert.Equal(names.Count, names.Distinct(StringComparer.OrdinalIgnoreCase).Count());
        await Drawer.GetByTestId("plan-ingredients-confirm").ClickAsync();
        await Expect(Drawer).ToHaveCountAsync(0);

        await Page.GetByTestId("nav-grocery-list").ClickAsync();
        await Expect(Page).ToHaveURLAsync(new Regex("grocerylist"));
        await Expect(Page.GetByTestId("grocery-item")).ToHaveCountAsync(names.Count);
        foreach (var name in names)
            await Expect(Row("grocery-item", name)).ToHaveCountAsync(1);
        await Page.GetByTestId("add-plan-ingredients-button").ClickAsync();
        await Expect(Drawer.GetByTestId("plan-ingredients-all-on-list")).ToBeVisibleAsync();
        await Drawer.GetByTestId("drawer-close").ClickAsync();
        await Expect(Drawer).ToHaveCountAsync(0);
        await Page.ReloadAsync();
        await Expect(Page.GetByTestId("grocery-item")).ToHaveCountAsync(names.Count);

        // Back on the Meal plan the week stays the one last shown.
        await Page.GetByTestId("nav-meal-plan").ClickAsync();
        await Expect(Page.GetByTestId("meal-plan-week-title")).ToHaveTextAsync("Next week");
        await Expect(Drawer).ToHaveCountAsync(0);
        await Page.Locator($"[data-testid=week-day][data-date='{pickedDate}']").ClickAsync();
        await Expect(Page.GetByTestId("meal-display-name")).ToHaveTextAsync("Eat Out");
        await Expect(Page.Locator($"[data-testid=week-day][data-date='{clearedDate}']")).Not.ToHaveClassAsync(new System.Text.RegularExpressions.Regex("has-meal"));
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
