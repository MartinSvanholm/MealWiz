using MauiWASM.Tests.TestInfrastructure.E2E;
using Microsoft.Playwright;
using Xunit;

namespace MauiWASM.Tests.E2ETests;

[Trait("Category", "E2E")]
[Trait("Category", "Smoke")]
public class MealJourneyTests : E2ETestBase
{
    [Fact]
    public async Task Meal_CanBeCreatedWithIngredients_Edited_AndDeleted()
    {
        const string name = "E2E Lasagne";
        const string renamed = "E2E Moussaka";

        await LoginAsync();
        await Page.GetByTestId("nav-meals").ClickAsync();
        await Expect(Page).ToHaveURLAsync(UrlFor("meals"));

        // Create
        await Page.GetByTestId("add-meal-menu").ClickAsync();
        await Page.GetByTestId("add-meal-create-manually").ClickAsync();
        await Expect(Page).ToHaveURLAsync(UrlFor("meals/edit"));

        await FillAsync(Page.GetByTestId("meal-name-input"), name);
        await FillAsync(RecipeInput(), "Layer it.\nBake it.");
        await AddIngredientAsync("Onion", "2");
        await AddIngredientAsync("Minced beef", "500 g");

        await Page.GetByTestId("save-meal-button").ClickAsync();
        await Expect(Page).ToHaveURLAsync(UrlFor("meals"));
        await Expect(Row("meal-list-item", name)).ToBeVisibleAsync();

        // Edit: rename the meal and change an ingredient's amount
        await Row("meal-list-item", name).ClickAsync();
        await Expect(Page).ToHaveURLAsync(UrlFor("meals/edit"));
        await Expect(Page.GetByTestId("meal-name-input")).ToHaveValueAsync(name);
        await Expect(Page.GetByTestId("ingredient-card")).ToHaveCountAsync(2);

        await FillAsync(Page.GetByTestId("meal-name-input"), renamed);

        await Row("ingredient-card", "Onion").ClickAsync();
        await Expect(Drawer.GetByTestId("drawer-title")).ToHaveTextAsync("Edit Ingredient");
        await FillAsync(Drawer.GetByTestId("ingredient-amount-input"), "3");
        await Drawer.GetByTestId("save-ingredient-button").ClickAsync();
        await Expect(Drawer).ToHaveCountAsync(0);
        await Expect(Row("ingredient-card", "Onion")).ToContainTextAsync("3");

        await Page.GetByTestId("save-meal-button").ClickAsync();
        await Expect(Page).ToHaveURLAsync(UrlFor("meals"));

        // Both changes were saved, not just kept in memory
        await Page.ReloadAsync();
        await Expect(Row("meal-list-item", renamed)).ToBeVisibleAsync();
        await Expect(Row("meal-list-item", name)).ToHaveCountAsync(0);

        await Row("meal-list-item", renamed).ClickAsync();
        await Expect(Page.GetByTestId("meal-name-input")).ToHaveValueAsync(renamed);
        await Expect(Row("ingredient-card", "Onion")).ToContainTextAsync("3");
        await Expect(Row("ingredient-card", "Minced beef")).ToContainTextAsync("500 g");
        await Page.GetByTestId("edit-meal-back").ClickAsync();

        // Delete
        await Row("meal-list-item", renamed).GetByTestId("delete-meal-button").ClickAsync();
        await ConfirmDrawer.GetByTestId("confirm-drawer-confirm").ClickAsync();
        await Expect(Row("meal-list-item", renamed)).ToHaveCountAsync(0);

        await Page.ReloadAsync();
        await Expect(Row("meal-list-item", "Leftovers")).ToBeVisibleAsync();
        await Expect(Row("meal-list-item", renamed)).ToHaveCountAsync(0);
    }

    [Fact]
    public async Task Meal_OnShortPhone_ShowsLastIngredientAndDrawerSaveButton()
    {
        const string name = "E2E Paella";
        var ingredients = Enumerable.Range(1, 12).Select(i => new SeedIngredient($"Ingredient {i:00}", $"{i} g")).ToArray();
        await Supabase.InsertMealAsync(User, name, ingredients);

        await LoginAsync();
        // iPhone SE: short enough that a 45% drawer can't fit its whole form.
        await Page.SetViewportSizeAsync(375, 667);
        await Page.GetByTestId("nav-meals").ClickAsync();
        await Row("meal-list-item", name).ClickAsync();
        await Expect(Page.GetByTestId("ingredient-card")).ToHaveCountAsync(ingredients.Length);

        // MEA-9: the last ingredient scrolls fully clear of the bottom nav bar.
        var last = Row("ingredient-card", "Ingredient 12");
        await last.ScrollIntoViewIfNeededAsync();
        var lastBox = await last.BoundingBoxAsync();
        var navBox = await Page.GetByTestId("bottom-nav").BoundingBoxAsync();
        Assert.True(lastBox!.Y + lastBox.Height <= navBox!.Y,
            $"Last ingredient ends at {lastBox.Y + lastBox.Height}px, under the nav bar at {navBox.Y}px");

        // MEA-6: a drawer whose form is taller than the drawer scrolls, so its save button can be reached.
        await last.ClickAsync();
        await Expect(Drawer.GetByTestId("drawer-title")).ToHaveTextAsync("Edit Ingredient");
        await Expect(Drawer.GetByTestId("ingredient-name-input")).ToBeInViewportAsync(new() { Ratio = 1 });
        await Drawer.GetByTestId("ingredient-name-input").HoverAsync();
        await Page.Mouse.WheelAsync(0, 500);
        await Expect(Drawer.GetByTestId("save-ingredient-button")).ToBeInViewportAsync(new() { Ratio = 1 });
    }

    private async Task AddIngredientAsync(string name, string amount)
    {
        await Page.GetByTestId("create-ingredient-button").ClickAsync();
        await Expect(Drawer.GetByTestId("drawer-title")).ToHaveTextAsync("Create Ingredient");

        await FillAsync(Drawer.GetByTestId("ingredient-name-input"), name);
        await FillAsync(Drawer.GetByTestId("ingredient-amount-input"), amount);
        await Drawer.GetByTestId("save-ingredient-button").ClickAsync();
        await Expect(Drawer).ToHaveCountAsync(0);

        await Expect(Row("ingredient-card", name)).ToContainTextAsync(amount);
    }

    // MudTextFieldExtended may put the test id on its wrapper rather than the textarea.
    private ILocator RecipeInput() =>
        Page.Locator("textarea[data-testid=meal-recipe-input], [data-testid=meal-recipe-input] textarea").First;
}
