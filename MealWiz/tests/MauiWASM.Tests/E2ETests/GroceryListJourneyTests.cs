using System.Text.RegularExpressions;
using MauiWASM.Tests.TestInfrastructure.E2E;
using Microsoft.Playwright;
using Xunit;

namespace MauiWASM.Tests.E2ETests;

[Trait("Category", "E2E")]
[Trait("Category", "Smoke")]
public class GroceryListJourneyTests : E2ETestBase
{
    private static readonly Regex HasMeal = new(@"\bhas-meal\b");
    private static readonly Regex SelectedDay = new(@"\bselected-day\b");

    [Fact]
    public async Task GroceryList_IsFilledFromThePlan_AndManualItemsCanBeAddedCheckedAndDeleted()
    {
        const string meal = "E2E Pesto pasta";
        await Supabase.InsertMealAsync(User, meal,
            new SeedIngredient("Penne", "500 g", ShowOnGroceryList: true),
            new SeedIngredient("Olive oil", "2 tbsp", ShowOnGroceryList: false));

        await LoginAsync();

        // Put the meal in this week's plan
        await Page.GetByTestId("week-day").First.ClickAsync();
        await Expect(Page.GetByTestId("week-day").First).ToHaveClassAsync(new Regex(@"\bselected-day\b"));
        await Row("meal-list-item", meal).ClickAsync();
        await Expect(Page.GetByTestId("meal-display-name")).ToHaveTextAsync(meal);

        await Page.GetByTestId("nav-grocery-list").ClickAsync();
        await Expect(Page).ToHaveURLAsync(UrlFor("grocerylist"));

        // Add the plan's ingredients
        await Page.GetByTestId("add-plan-ingredients-button").ClickAsync();
        await Expect(Drawer.GetByTestId("drawer-title")).ToHaveTextAsync("From meal plan");
        // Only ingredients shown on the grocery list are offered, so olive oil is left out
        await Expect(Drawer.GetByTestId("plan-ingredient-item")).ToHaveCountAsync(1);
        await Expect(Drawer.GetByTestId("plan-ingredient-item").Filter(new() { HasText = "Penne" })).ToBeVisibleAsync();
        await Expect(Drawer.GetByTestId("plan-ingredients-confirm")).ToHaveTextAsync("Add 1 to grocery list");

        await Drawer.GetByTestId("plan-ingredients-confirm").ClickAsync();
        await Expect(Drawer).ToHaveCountAsync(0);
        await Expect(Row("grocery-item", "Penne").GetByTestId("grocery-item-amount")).ToHaveTextAsync("500 g");
        await Expect(Row("grocery-item", "Olive oil")).ToHaveCountAsync(0);

        // Add an item by hand: the add field opens the drawer with the name filled in
        await FillAsync(Page.GetByTestId("add-grocery-item-input"), "Milk");
        await Page.GetByTestId("add-grocery-item-button").ClickAsync();
        await Expect(Drawer.GetByTestId("drawer-title")).ToHaveTextAsync("Add item");
        await Expect(Drawer.GetByTestId("grocery-item-name-input")).ToHaveValueAsync("Milk");
        await FillAsync(Drawer.GetByTestId("grocery-item-amount-input"), "1 l");
        await Drawer.GetByTestId("save-grocery-item-button").ClickAsync();
        await Expect(Drawer).ToHaveCountAsync(0);
        await Expect(Row("grocery-item", "Milk").GetByTestId("grocery-item-amount")).ToHaveTextAsync("1 l");
        await Expect(Page.GetByTestId("grocery-item")).ToHaveCountAsync(2);
        await Expect(Page.GetByTestId("grocery-progress-count")).ToHaveTextAsync("0 of 2 items");

        // Check it off straight away, without a reload (MEA-42): it moves to the basket and stays there after one
        var milkCheckbox = Row("grocery-item", "Milk").GetByTestId("grocery-item-checkbox");
        await Expect(milkCheckbox).Not.ToBeCheckedAsync();
        var saved = await Page.RunAndWaitForResponseAsync(
            () => milkCheckbox.ClickAsync(),
            response => response.Url.Contains("/rest/v1/grocery_items") && response.Request.Method == "PATCH");
        Assert.True(saved.Ok, $"Expected the update to succeed, got {saved.Status}.");
        await Expect(milkCheckbox).ToBeCheckedAsync();
        await Expect(Page.GetByTestId("grocery-basket").GetByTestId("grocery-item")).ToHaveTextAsync("Milk");
        await Expect(Page.GetByTestId("grocery-progress-count")).ToHaveTextAsync("1 of 2 items");

        await Page.ReloadAsync();
        await Expect(Row("grocery-item", "Milk").GetByTestId("grocery-item-checkbox")).ToBeCheckedAsync();
        await Expect(Row("grocery-item", "Penne").GetByTestId("grocery-item-checkbox")).Not.ToBeCheckedAsync();

        // Put it back on the list, then delete it from the edit drawer (tap the name, red delete in the header)
        await Row("grocery-item", "Milk").GetByTestId("grocery-item-checkbox").ClickAsync();
        await Expect(Page.GetByTestId("grocery-to-buy").GetByTestId("grocery-item")).ToHaveCountAsync(2);
        await Row("grocery-item", "Milk").GetByTestId("grocery-item-name").ClickAsync();
        await Expect(Drawer.GetByTestId("drawer-title")).ToHaveTextAsync("Edit item");
        await Drawer.GetByTestId("drawer-header-action").GetByTestId("delete-grocery-item-button").ClickAsync();
        await Expect(ConfirmDrawer.GetByTestId("confirm-drawer-title")).ToHaveTextAsync("Delete Milk?");
        await ConfirmDrawer.GetByTestId("confirm-drawer-confirm").ClickAsync();
        await Expect(Row("grocery-item", "Milk")).ToHaveCountAsync(0);

        await Page.ReloadAsync();
        await Expect(Page.GetByTestId("grocery-item")).ToHaveCountAsync(1);
        await Expect(Row("grocery-item", "Milk")).ToHaveCountAsync(0);

        // Add another item and delete it by swiping its row left with the mouse (MEA-67)
        await FillAsync(Page.GetByTestId("add-grocery-item-input"), "Eggs");
        await Page.GetByTestId("add-grocery-item-button").ClickAsync();
        await Drawer.GetByTestId("save-grocery-item-button").ClickAsync();
        await Expect(Drawer).ToHaveCountAsync(0);
        await Expect(Page.GetByTestId("grocery-item")).ToHaveCountAsync(2);

        var eggs = await Row("grocery-item", "Eggs").GetByTestId("grocery-item-swipe").BoundingBoxAsync();
        Assert.NotNull(eggs);
        var y = eggs.Y + eggs.Height / 2;
        await Page.Mouse.MoveAsync(eggs.X + eggs.Width / 2, y);
        await Page.Mouse.DownAsync();
        await Page.Mouse.MoveAsync(eggs.X + eggs.Width / 2 - 150, y, new() { Steps = 10 });
        await Page.Mouse.UpAsync();
        // Letting go doesn't also tap the name, so only the confirm drawer opens
        await Expect(ConfirmDrawer.GetByTestId("confirm-drawer-title")).ToHaveTextAsync("Delete Eggs?");
        await Expect(Drawer).ToHaveCountAsync(0);
        await ConfirmDrawer.GetByTestId("confirm-drawer-confirm").ClickAsync();
        await Expect(Row("grocery-item", "Eggs")).ToHaveCountAsync(0);

        await Page.ReloadAsync();
        await Expect(Page.GetByTestId("grocery-item")).ToHaveCountAsync(1);
        await Expect(Row("grocery-item", "Eggs")).ToHaveCountAsync(0);

        // Check off the last item, then clear the basket (MEA-73): it's deleted, also after a reload
        await Row("grocery-item", "Penne").GetByTestId("grocery-item-checkbox").ClickAsync();
        await Expect(Page.GetByTestId("grocery-basket").GetByTestId("grocery-item")).ToHaveTextAsync("Penne");
        await Page.GetByTestId("clear-basket-button").ClickAsync();
        await Expect(ConfirmDrawer.GetByTestId("confirm-drawer-title")).ToHaveTextAsync("Clear 1 item?");
        await ConfirmDrawer.GetByTestId("confirm-drawer-confirm").ClickAsync();
        await Expect(Page.GetByTestId("grocery-basket")).ToHaveCountAsync(0);
        await Expect(Page.GetByTestId("grocery-list-empty")).ToBeVisibleAsync();

        await Page.ReloadAsync();
        await Expect(Page.GetByTestId("grocery-list-empty")).ToBeVisibleAsync();
        await Expect(Page.GetByTestId("grocery-item")).ToHaveCountAsync(0);
    }

    [Fact]
    public async Task GroceryList_FromMealPlanAfterASwap_OffersOnlyTheNewMealsIngredients_AndMergesSameNames()
    {
        const string soup = "E2E Tomato soup";
        const string penne = "E2E Penne bake";
        const string salad = "E2E Tomato salad";
        await Supabase.InsertMealAsync(User, soup,
            new SeedIngredient("Tomatoes", "400 g", ShowOnGroceryList: true),
            new SeedIngredient("Cream", "2 dl", ShowOnGroceryList: true),
            new SeedIngredient("Salt", "1 tsp", ShowOnGroceryList: false));
        await Supabase.InsertMealAsync(User, penne, new SeedIngredient("Penne", "500 g", ShowOnGroceryList: true));
        await Supabase.InsertMealAsync(User, salad,
            new SeedIngredient("Tomatoes", "200g", ShowOnGroceryList: true),
            new SeedIngredient("Feta", "150 g", ShowOnGroceryList: true));

        await LoginAsync();

        // Plan the soup on the first day and the penne bake on the second
        await Page.GetByTestId("week-day").Nth(0).ClickAsync();
        await Expect(Page.GetByTestId("week-day").Nth(0)).ToHaveClassAsync(new Regex(@"\bselected-day\b"));
        await Row("meal-list-item", soup).ClickAsync();
        await Expect(Page.GetByTestId("meal-display-name")).ToHaveTextAsync(soup);
        await Page.GetByTestId("week-day").Nth(1).ClickAsync();
        await Expect(Page.GetByTestId("week-day").Nth(1)).ToHaveClassAsync(new Regex(@"\bselected-day\b"));
        await Row("meal-list-item", penne).ClickAsync();
        await Expect(Page.GetByTestId("meal-display-name")).ToHaveTextAsync(penne);

        // Everything shown on the list is offered the first time
        await Page.GetByTestId("nav-grocery-list").ClickAsync();
        await Page.GetByTestId("add-plan-ingredients-button").ClickAsync();
        await Expect(Drawer.GetByTestId("drawer-title")).ToHaveTextAsync("From meal plan");
        await Expect(Drawer.GetByTestId("plan-ingredient-item")).ToHaveCountAsync(3);
        await Expect(Drawer.GetByTestId("plan-ingredients-confirm")).ToHaveTextAsync("Add 3 to grocery list");
        await Drawer.GetByTestId("plan-ingredients-confirm").ClickAsync();
        await Expect(Drawer).ToHaveCountAsync(0);
        await Expect(Row("grocery-item", "Tomatoes").GetByTestId("grocery-item-amount")).ToHaveTextAsync("400 g");
        await Expect(Page.GetByTestId("grocery-item")).ToHaveCountAsync(3);

        // Swap the second day's meal for the salad
        await Page.GetByTestId("nav-meal-plan").ClickAsync();
        await Page.GetByTestId("week-day").Nth(1).ClickAsync();
        await Expect(Page.GetByTestId("meal-display-name")).ToHaveTextAsync(penne);
        await Page.GetByTestId("swap-meal-button").ClickAsync();
        await Row("meal-list-item", salad).ClickAsync();
        await Expect(Page.GetByTestId("meal-display-name")).ToHaveTextAsync(salad);

        // Only the salad's ingredients are new; tomatoes merge into the item already on the list
        await Page.GetByTestId("nav-grocery-list").ClickAsync();
        await Page.GetByTestId("add-plan-ingredients-button").ClickAsync();
        await Expect(Drawer.GetByTestId("plan-ingredient-item")).ToHaveCountAsync(2);
        var tomatoes = Drawer.GetByTestId("plan-ingredient-item").Filter(new() { HasText = "Tomatoes" });
        await Expect(tomatoes.GetByTestId("plan-ingredient-existing")).ToHaveTextAsync("Plan 200g · In list 400 g");
        await Expect(tomatoes.GetByTestId("plan-ingredient-amount-input")).ToHaveValueAsync("600 g");
        var feta = Drawer.GetByTestId("plan-ingredient-item").Filter(new() { HasText = "Feta" });
        await Expect(feta.GetByTestId("plan-ingredient-amount-input")).ToHaveValueAsync("150 g");
        await Expect(Drawer.GetByTestId("plan-ingredients-confirm")).ToHaveTextAsync("Add 2 to grocery list");
        await Drawer.GetByTestId("plan-ingredients-confirm").ClickAsync();
        await Expect(Drawer).ToHaveCountAsync(0);

        await Expect(Row("grocery-item", "Tomatoes").GetByTestId("grocery-item-amount")).ToHaveTextAsync("600 g");
        await Expect(Row("grocery-item", "Feta").GetByTestId("grocery-item-amount")).ToHaveTextAsync("150 g");
        // Penne stays: removing items of meals that left the plan is another story
        await Expect(Page.GetByTestId("grocery-item")).ToHaveCountAsync(4);

        // Now everything is linked
        await Page.GetByTestId("add-plan-ingredients-button").ClickAsync();
        await Expect(Drawer.GetByTestId("plan-ingredients-all-on-list")).ToHaveTextAsync("Everything is already on your list");
        await Expect(Drawer.GetByTestId("plan-ingredient-item")).ToHaveCountAsync(0);
        await Expect(Drawer.GetByTestId("plan-ingredients-confirm")).ToHaveCountAsync(0);
        await Drawer.GetByTestId("drawer-close").ClickAsync();

        await Page.ReloadAsync();
        await Expect(Page.GetByTestId("grocery-item")).ToHaveCountAsync(4);
        await Expect(Row("grocery-item", "Tomatoes").GetByTestId("grocery-item-amount")).ToHaveTextAsync("600 g");
    }

    [Fact]
    public async Task GroceryList_RemovingAPlannedMeal_RemovesItsOwnItems_AndKeepsSharedOnes()
    {
        const string pasta = "E2E Garlic pasta";
        const string soup = "E2E Tomato soup";
        await Supabase.InsertMealAsync(User, pasta,
            new SeedIngredient("Spaghetti", "400 g", ShowOnGroceryList: true),
            new SeedIngredient("Garlic", "2 cloves", ShowOnGroceryList: true));
        await Supabase.InsertMealAsync(User, soup,
            new SeedIngredient("Tomatoes", "6", ShowOnGroceryList: true),
            new SeedIngredient("Garlic", "1 clove", ShowOnGroceryList: true));

        await LoginAsync();
        await PlanOnDayAsync(0, pasta);

        await Page.GetByTestId("nav-grocery-list").ClickAsync();
        await Page.GetByTestId("add-plan-ingredients-button").ClickAsync();
        await Drawer.GetByTestId("plan-ingredients-confirm").ClickAsync();
        await Expect(Drawer).ToHaveCountAsync(0);
        await Expect(Row("grocery-item", "Spaghetti")).ToHaveCountAsync(1);
        await Expect(Row("grocery-item", "Garlic")).ToHaveCountAsync(1);

        await Page.GetByTestId("nav-meal-plan").ClickAsync();
        await PlanOnDayAsync(1, soup);

        await Page.GetByTestId("nav-grocery-list").ClickAsync();
        await Page.GetByTestId("add-plan-ingredients-button").ClickAsync();
        await Expect(Drawer.GetByTestId("plan-ingredient-item")).ToHaveCountAsync(2);
        var garlicRow = Drawer.GetByTestId("plan-ingredient-item").Filter(new() { HasText = "Garlic" });
        await FillAsync(garlicRow.GetByTestId("plan-ingredient-amount-input"), "3 cloves");
        await Drawer.GetByTestId("plan-ingredients-confirm").ClickAsync();
        await Expect(Drawer).ToHaveCountAsync(0);
        await Expect(Row("grocery-item", "Garlic").GetByTestId("grocery-item-amount")).ToHaveTextAsync("3 cloves");
        await Expect(Page.GetByTestId("grocery-item")).ToHaveCountAsync(3);

        // Remove the pasta: only the spaghetti goes with it
        await Page.GetByTestId("nav-meal-plan").ClickAsync();
        await Day(0).ClickAsync();
        await Expect(Day(0)).ToHaveClassAsync(SelectedDay);
        await Page.GetByTestId("remove-meal-button").ClickAsync();
        await Expect(Drawer.GetByTestId("drawer-title")).ToHaveTextAsync("Remove E2E Garlic pasta?");
        await Expect(Drawer.GetByTestId("remove-meal-grocery-label")).ToHaveTextAsync("Also remove 1 item from the grocery list");
        await Expect(Drawer.Locator("input[data-testid=remove-meal-grocery-switch], [data-testid=remove-meal-grocery-switch] input")).ToBeCheckedAsync();
        await Drawer.GetByTestId("remove-meal-confirm").ClickAsync();
        await Expect(Drawer).ToHaveCountAsync(0);
        await Expect(Day(0)).Not.ToHaveClassAsync(HasMeal);

        await Page.GetByTestId("nav-grocery-list").ClickAsync();
        await Expect(Row("grocery-item", "Spaghetti")).ToHaveCountAsync(0);
        await Expect(Row("grocery-item", "Garlic").GetByTestId("grocery-item-amount")).ToHaveTextAsync("3 cloves");
        await Expect(Row("grocery-item", "Tomatoes")).ToBeVisibleAsync();
        await Expect(Page.GetByTestId("grocery-item")).ToHaveCountAsync(2);

        await Page.ReloadAsync();
        await Expect(Row("grocery-item", "Spaghetti")).ToHaveCountAsync(0);
        await Expect(Row("grocery-item", "Garlic").GetByTestId("grocery-item-amount")).ToHaveTextAsync("3 cloves");
        await Expect(Page.GetByTestId("grocery-item")).ToHaveCountAsync(2);
    }

    private ILocator Day(int index) => Page.GetByTestId("week-day").Nth(index);

    private async Task PlanOnDayAsync(int day, string meal)
    {
        await Day(day).ClickAsync();
        await Expect(Day(day)).ToHaveClassAsync(SelectedDay);
        await Row("meal-list-item", meal).ClickAsync();
        await Expect(Page.GetByTestId("meal-display-name")).ToHaveTextAsync(meal);
        await Expect(Day(day)).ToHaveClassAsync(HasMeal);
    }
}
