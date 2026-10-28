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
        await Expect(Drawer.GetByTestId("drawer-title")).ToHaveTextAsync("Add items to grocery list");
        // Only ingredients shown on the grocery list are offered, so olive oil is left out
        await Expect(Drawer.GetByTestId("plan-ingredient-item")).ToHaveCountAsync(1);
        await Expect(Drawer.GetByTestId("plan-ingredient-item").Filter(new() { HasText = "Penne" })).ToBeVisibleAsync();

        await Drawer.GetByTestId("plan-ingredients-confirm").ClickAsync();
        await Expect(Drawer).ToHaveCountAsync(0);
        await Expect(Row("grocery-item", "Penne (500 g)")).ToBeVisibleAsync();
        await Expect(Row("grocery-item", "Olive oil")).ToHaveCountAsync(0);

        // Add an item by hand
        await Page.GetByTestId("add-grocery-item-button").ClickAsync();
        await Expect(Drawer.GetByTestId("drawer-title")).ToHaveTextAsync("Add item");
        await FillAsync(Drawer.GetByTestId("grocery-item-name-input"), "Milk");
        await FillAsync(Drawer.GetByTestId("grocery-item-amount-input"), "1 l");
        await Drawer.GetByTestId("save-grocery-item-button").ClickAsync();
        await Expect(Drawer).ToHaveCountAsync(0);
        await Expect(Row("grocery-item", "Milk (1 l)")).ToBeVisibleAsync();
        await Expect(Page.GetByTestId("grocery-item")).ToHaveCountAsync(2);

        // Check it off straight away, without a reload (MEA-42), and it stays checked after one
        var milkCheckbox = Row("grocery-item", "Milk").GetByTestId("grocery-item-checkbox");
        await Expect(milkCheckbox).Not.ToBeCheckedAsync();
        var saved = await Page.RunAndWaitForResponseAsync(
            () => milkCheckbox.ClickAsync(),
            response => response.Url.Contains("/rest/v1/grocery_items") && response.Request.Method == "PATCH");
        Assert.True(saved.Ok, $"Expected the update to succeed, got {saved.Status}.");
        await Expect(milkCheckbox).ToBeCheckedAsync();

        await Page.ReloadAsync();
        await Expect(Row("grocery-item", "Milk").GetByTestId("grocery-item-checkbox")).ToBeCheckedAsync();
        await Expect(Row("grocery-item", "Penne").GetByTestId("grocery-item-checkbox")).Not.ToBeCheckedAsync();

        // Delete it
        await Row("grocery-item", "Milk").GetByTestId("delete-grocery-item-button").ClickAsync();
        await ConfirmDrawer.GetByTestId("confirm-drawer-confirm").ClickAsync();
        await Expect(Row("grocery-item", "Milk")).ToHaveCountAsync(0);

        await Page.ReloadAsync();
        await Expect(Page.GetByTestId("grocery-item")).ToHaveCountAsync(1);
        await Expect(Row("grocery-item", "Milk")).ToHaveCountAsync(0);
    }
}
