using Bunit;
using MealWiz.Shared.Features.Ingredients;
using MealWiz.Shared.Features.Meals.EditMeal;
using MealWiz.Shared.Tests.TestInfrastructure;
using Xunit;

namespace MealWiz.Shared.Tests.Features.Meals.ComponentTests;

/// <summary>The edit meal form, including adding and removing ingredient rows through the ingredient drawer.</summary>
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

        cut.Find(TestId("create-ingredient-card")).Click();
        Assert.Equal(typeof(IngredientDrawer), Drawer.StateParameters.Content);
        DrawerHost.Find(InputOf("ingredient-name-input")).Change("Eggs");
        DrawerHost.Find(InputOf("ingredient-amount-input")).Input("3");
        DrawerHost.Find(TestId("save-ingredient-button")).Click();

        Assert.False(Drawer.IsDrawerOpen);
        var card = Assert.Single(cut.FindAll(TestId("ingredient-card")));
        Assert.Contains("Eggs", card.TextContent);
        Assert.Contains("3", card.TextContent);
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
    public async Task Leaving_the_page_closes_the_drawer()
    {
        var cut = Render<EditMealPage>();
        cut.Find(TestId("create-ingredient-card")).Click();
        Assert.True(Drawer.IsDrawerOpen);

        await DisposeComponentsAsync();

        Assert.False(Drawer.IsDrawerOpen);
    }
}
