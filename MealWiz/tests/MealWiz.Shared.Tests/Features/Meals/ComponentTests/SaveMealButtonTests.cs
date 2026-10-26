using Bunit;
using FluentResults;
using MealWiz.Shared.Features.Ingredients.SaveIngredient;
using MealWiz.Shared.Features.Meals.Models;
using MealWiz.Shared.Features.Meals.SaveMeal;
using MealWiz.Shared.Tests.TestInfrastructure;
using Xunit;

namespace MealWiz.Shared.Tests.Features.Meals.ComponentTests;

public class SaveMealButtonTests : ComponentTestBase
{
    [Fact]
    public void Renders_a_save_button()
    {
        var cut = Render<SaveMealButton>();

        Assert.NotNull(cut.Find(TestId("save-meal-button")));
    }

    [Fact]
    public void Saves_the_meal_then_its_pending_ingredients_and_returns_to_meals()
    {
        var eggs = TestData.Ingredient("Eggs", "3");
        var saved = TestData.Meal("Pancakes", id: 42);
        Meals.MealToEdit = TestData.Meal("Pancakes", id: 0, ingredients: [eggs, TestData.Ingredient("Flour", "200g", id: 9)]);
        SetupSend<SaveMeal.Command, Result<Meal>>(Result.Ok(saved));
        SetupSend<SaveIngredient.Command, Result>(Result.Ok());
        var cut = Render<SaveMealButton>();

        cut.Find(TestId("save-meal-button")).Click();

        cut.WaitForAssertion(() => Assert.Equal("meals", CurrentPath));
        Assert.Equal("Pancakes", Assert.Single(Sent<SaveMeal.Command>()).Meal.Name);
        var ingredient = Assert.Single(Sent<SaveIngredient.Command>());
        Assert.Same(eggs, ingredient.Ingredient);
        Assert.Equal(42, ingredient.MealId);
        Assert.Same(saved, Meals.MealToEdit);
        Assert.Contains("Meal saved", SnackbarMessages);
    }

    [Fact]
    public void A_failed_save_shows_the_error_and_stays_on_the_page()
    {
        Navigation.NavigateTo("/meals/edit");
        Meals.MealToEdit = TestData.Meal("Pancakes", id: 0, ingredients: [TestData.Ingredient("Eggs", "3")]);
        SetupSend<SaveMeal.Command, Result<Meal>>(Result.Fail<Meal>("Name is required"));
        var cut = Render<SaveMealButton>();

        cut.Find(TestId("save-meal-button")).Click();

        cut.WaitForAssertion(() => Assert.Contains("Name is required", SnackbarMessages));
        Assert.Empty(Sent<SaveIngredient.Command>());
        Assert.Equal("meals/edit", CurrentPath);
    }
}
