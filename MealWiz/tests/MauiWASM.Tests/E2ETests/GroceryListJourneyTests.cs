using System.Text.RegularExpressions;
using MauiWASM.Tests.TestInfrastructure.E2E;
using Xunit;

namespace MauiWASM.Tests.E2ETests;

[Trait("Category", "E2E")]
[Trait("Category", "Smoke")]
public class GroceryListJourneyTests : E2ETestBase
{
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
}
