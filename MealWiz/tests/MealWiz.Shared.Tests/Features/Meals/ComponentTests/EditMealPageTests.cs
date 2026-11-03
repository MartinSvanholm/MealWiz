using Bunit;
using MealWiz.Shared.Features.Meals.EditMeal;
using MealWiz.Shared.Tests.TestInfrastructure;
using Xunit;

namespace MealWiz.Shared.Tests.Features.Meals.ComponentTests;

/// <summary>The edit meal form, including adding and removing ingredient rows through the add row.</summary>
public class EditMealPageTests : ComponentTestBase
{
    [Fact]
    public void Renders_the_meal_being_edited_with_its_ingredients()
    {
        Meals.MealToEdit = TestData.Meal("Pancakes", ingredients: [TestData.Ingredient("Flour", "200g", id: 1), TestData.Ingredient("Milk", "3 dl", id: 2)]);

        var cut = Render<EditMealPage>();

        Assert.Equal("Pancakes", cut.Find(InputOf("meal-name-input")).GetAttribute("value"));
        var cards = cut.FindAll(TestId("ingredient-card")).Select(c => c.TextContent).ToList();
        Assert.Equal(2, cards.Count);
        Assert.Contains("Flour", cards[0]);
        Assert.Contains("200g", cards[0]);
        Assert.Contains("Milk", cards[1]);
    }

    [Fact]
    public void Typing_a_name_updates_the_meal_being_edited()
    {
        Meals.MealToEdit = TestData.Meal("Pancakes", id: 0);
        var cut = Render<EditMealPage>();

        cut.Find(InputOf("meal-name-input")).Change("Crêpes");

        Assert.Equal("Crêpes", Meals.MealToEdit.Name);
    }

    [Fact]
    public void Enter_in_the_name_moves_to_the_recipe()
    {
        Meals.MealToEdit = TestData.Meal("Pancakes", id: 0);
        var cut = Render<EditMealPage>();

        PressEnter(cut.Find(InputOf("meal-name-input")));

        AssertFocused(cut.Find(InputOf("meal-recipe-input")));
    }

    [Fact]
    public void Back_returns_to_the_meals_page()
    {
        var cut = Render<EditMealPage>();

        cut.Find(TestId("edit-meal-back")).Click();

        Assert.Equal("meals", CurrentPath);
    }

    [Fact]
    public void Adding_an_ingredient_to_a_new_meal_adds_a_pending_row_without_saving()
    {
        Meals.MealToEdit = TestData.Meal("Pancakes", id: 0);
        RenderDrawers();
        var cut = Render<EditMealPage>();

        cut.Find(InputOf("add-ingredient-name")).Input("Eggs");
        cut.Find(InputOf("add-ingredient-amount")).Input("3");
        cut.Find(TestId("create-ingredient-button")).Click();
        // The add button opens the drawer with what was typed; saving happens there.
        Assert.Empty(cut.FindAll(TestId("ingredient-card")));
        DrawerHost.Find(TestId("save-ingredient-button")).Click();

        var card = cut.WaitForElement(TestId("ingredient-card"));
        Assert.Contains("Eggs", card.TextContent);
        Assert.Contains("3", card.TextContent);
        Assert.Equal("1 item", cut.Find(TestId("edit-meal-ingredient-count")).TextContent.Trim());
        Assert.Empty(SentRequests);
    }

    [Fact]
    public void Deleting_a_pending_ingredient_removes_its_row_without_a_request()
    {
        Meals.MealToEdit = TestData.Meal("Pancakes", id: 0, ingredients: [TestData.Ingredient("Eggs", "3")]);
        var cut = Render<EditMealPage>();

        cut.Find(TestId("delete-ingredient-button")).Click();

        Assert.Empty(cut.FindAll(TestId("ingredient-card")));
        Assert.Empty(Meals.MealToEdit.Ingredients);
        Assert.Empty(SentRequests);
    }

    [Fact]
    public void A_saved_meal_can_be_deleted_from_the_page()
    {
        Meals.MealToEdit = TestData.Meal("Pancakes", id: 5);

        var cut = Render<EditMealPage>();

        Assert.NotNull(cut.Find(TestId("delete-meal-button")));
    }

    [Fact]
    public void A_new_meal_has_no_delete_button()
    {
        Meals.MealToEdit = TestData.Meal("Pancakes", id: 0);

        var cut = Render<EditMealPage>();

        Assert.Empty(cut.FindAll(TestId("delete-meal-button")));
    }

    [Fact]
    public async Task Leaving_the_page_closes_the_drawer()
    {
        Meals.MealToEdit = TestData.Meal("Pancakes", id: 0, ingredients: [TestData.Ingredient("Eggs", "3")]);
        var cut = Render<EditMealPage>();
        cut.Find(TestId("ingredient-card")).Click();
        Assert.True(Drawer.IsDrawerOpen);

        await DisposeComponentsAsync();

        Assert.False(Drawer.IsDrawerOpen);
    }
}
