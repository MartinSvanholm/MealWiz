using Bunit;
using FluentResults;
using MealWiz.Shared.Features.Ingredients.GetIngredientsByMealId;
using MealWiz.Shared.Features.Ingredients.Models;
using MealWiz.Shared.Features.Ingredients.SaveIngredient;
using MealWiz.Shared.Features.Meals.Models;
using MealWiz.Shared.Features.Meals.SaveMeal;
using MealWiz.Shared.Tests.TestInfrastructure;
using Moq;
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

    // MEA-33: the form used to swap in the saved meal (no ingredients) first, so a failed ingredient dropped the
    // rest without saving them.
    [Fact]
    public void A_failed_ingredient_save_keeps_the_unsaved_ingredients_on_the_saved_meal()
    {
        Navigation.NavigateTo("/meals/edit");
        var eggs = TestData.Ingredient("Eggs", "3");
        var flour = TestData.Ingredient("Flour", "200g");
        var milk = TestData.Ingredient("Milk", "1l");
        var savedEggs = TestData.Ingredient("Eggs", "3", id: 11);
        Meals.MealToEdit = TestData.Meal("Pancakes", id: 0, ingredients: [eggs, flour, milk]);
        SetupSend<SaveMeal.Command, Result<Meal>>(Result.Ok(TestData.Meal("Pancakes", id: 42)));
        Mediator.SetupSequence(m => m.Send(It.IsAny<SaveIngredient.Command>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Ok())
            .ReturnsAsync(Result.Fail("Amount is too long"));
        SetupSend<GetIngredientsByMealId.Query, Result<List<Ingredient>>>(Result.Ok(new List<Ingredient> { savedEggs }));
        var cut = Render<SaveMealButton>();

        cut.Find(TestId("save-meal-button")).Click();

        cut.WaitForAssertion(() => Assert.Contains("Amount is too long", SnackbarMessages));
        Assert.Equal(2, Sent<SaveIngredient.Command>().Count);
        Assert.Equal(42, Meals.MealToEdit.Id);
        Assert.Equal([savedEggs, flour, milk], Meals.MealToEdit.Ingredients);
        Assert.Equal("meals/edit", CurrentPath);
        Assert.DoesNotContain("Meal saved", SnackbarMessages);
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
