using Bunit;
using FluentResults;
using MealWiz.Shared.Features.Ingredients.GetIngredientsByMealId;
using MealWiz.Shared.Features.Ingredients.Models;
using MealWiz.Shared.Features.Ingredients.SaveIngredient;
using MealWiz.Shared.Tests.TestInfrastructure;
using Xunit;

namespace MealWiz.Shared.Tests.Features.Ingredients.ComponentTests;

public class SaveIngredientButtonTests : ComponentTestBase
{
    [Fact]
    public void Renders_a_save_button()
    {
        var cut = Render<SaveIngredientButton>(p => p.Add(b => b.Ingredient, TestData.Ingredient("Eggs")));

        Assert.Contains("Save", cut.Find(TestId("save-ingredient-button")).TextContent);
    }

    [Fact]
    public void For_a_new_meal_the_ingredient_is_kept_pending_and_not_saved()
    {
        var eggs = TestData.Ingredient("Eggs", "3");
        Meals.MealToEdit = TestData.Meal("Pancakes", id: 0);
        Drawer.OpenDrawer("Create Ingredient", typeof(object));
        var cut = Render<SaveIngredientButton>(p => p.Add(b => b.Ingredient, eggs));

        cut.Find(TestId("save-ingredient-button")).Click();

        Assert.Contains(eggs, Meals.MealToEdit.Ingredients);
        Assert.False(Drawer.IsDrawerOpen);
        Assert.Empty(SentRequests);
    }

    [Fact]
    public void For_a_saved_meal_the_ingredient_is_saved_and_the_list_reloaded()
    {
        var eggs = TestData.Ingredient("Eggs", "3");
        var reloaded = TestData.Ingredient("Eggs", "3", id: 11);
        Meals.MealToEdit = TestData.Meal("Pancakes", id: 42);
        SetupSend<SaveIngredient.Command, Result>(Result.Ok());
        SetupSend<GetIngredientsByMealId.Query, Result<List<Ingredient>>>(Result.Ok(new List<Ingredient> { reloaded }));
        Drawer.OpenDrawer("Edit Ingredient", typeof(object));
        var cut = Render<SaveIngredientButton>(p => p.Add(b => b.Ingredient, eggs));

        cut.Find(TestId("save-ingredient-button")).Click();

        cut.WaitForAssertion(() => Assert.False(Drawer.IsDrawerOpen));
        var command = Assert.Single(Sent<SaveIngredient.Command>());
        Assert.Same(eggs, command.Ingredient);
        Assert.Equal(42, command.MealId);
        Assert.Equal(42, Assert.Single(Sent<GetIngredientsByMealId.Query>()).MealId);
        Assert.Equal([reloaded], Meals.MealToEdit.Ingredients);
        Assert.Contains("Ingredient saved.", SnackbarMessages);
    }

    [Fact]
    public void A_failed_save_shows_the_error_and_keeps_the_drawer_open()
    {
        Meals.MealToEdit = TestData.Meal("Pancakes", id: 42);
        SetupSend<SaveIngredient.Command, Result>(Result.Fail("Amount is too long"));
        Drawer.OpenDrawer("Edit Ingredient", typeof(object));
        var cut = Render<SaveIngredientButton>(p => p.Add(b => b.Ingredient, TestData.Ingredient("Eggs")));

        cut.Find(TestId("save-ingredient-button")).Click();

        cut.WaitForAssertion(() => Assert.Contains("Amount is too long", SnackbarMessages));
        Assert.True(Drawer.IsDrawerOpen);
        Assert.Empty(Sent<GetIngredientsByMealId.Query>());
    }
}
