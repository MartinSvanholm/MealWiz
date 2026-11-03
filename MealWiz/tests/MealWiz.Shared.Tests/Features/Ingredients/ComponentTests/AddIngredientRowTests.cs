using Bunit;
using FluentResults;
using MealWiz.Shared.Features.Ingredients.CreateIngredient;
using MealWiz.Shared.Features.Ingredients.GetIngredientsByMealId;
using MealWiz.Shared.Features.Ingredients.Models;
using MealWiz.Shared.Features.Ingredients.SaveIngredient;
using MealWiz.Shared.Tests.TestInfrastructure;
using Xunit;

namespace MealWiz.Shared.Tests.Features.Ingredients.ComponentTests;

/// <summary>The inline "add ingredient" row on the edit meal form: name, amount and an add button.</summary>
public class AddIngredientRowTests : ComponentTestBase
{
    [Fact]
    public void Adding_to_a_new_meal_keeps_the_ingredient_pending_and_clears_the_row()
    {
        Meals.MealToEdit = TestData.Meal("Pancakes", id: 0);
        var cut = Render<AddIngredientRow>();

        cut.Find(InputOf("add-ingredient-name")).Input("Eggs");
        cut.Find(InputOf("add-ingredient-amount")).Input("3");
        cut.Find(TestId("create-ingredient-button")).Click();

        var eggs = Assert.Single(Meals.MealToEdit.Ingredients);
        Assert.Equal("Eggs", eggs.Name);
        Assert.Equal("3", eggs.Amount);
        Assert.True(eggs.ShowOnGroceryList);
        Assert.Empty(SentRequests);

        // The row was cleared: adding again with no name adds nothing.
        cut.Find(TestId("create-ingredient-button")).Click();
        Assert.Single(Meals.MealToEdit.Ingredients);
    }

    [Fact]
    public void Adding_to_a_saved_meal_saves_the_ingredient()
    {
        Meals.MealToEdit = TestData.Meal("Pancakes", id: 42);
        SetupSend<SaveIngredient.Command, Result>(Result.Ok());
        SetupSend<GetIngredientsByMealId.Query, Result<List<Ingredient>>>(Result.Ok(new List<Ingredient> { TestData.Ingredient("Eggs", id: 9) }));
        var cut = Render<AddIngredientRow>();

        cut.Find(InputOf("add-ingredient-name")).Input("Eggs");
        cut.Find(TestId("create-ingredient-button")).Click();

        cut.WaitForAssertion(() => Assert.Single(Sent<SaveIngredient.Command>()));
        Assert.Equal(42, Sent<SaveIngredient.Command>()[0].MealId);
        Assert.Equal("Eggs", Sent<SaveIngredient.Command>()[0].Ingredient.Name);
    }

    [Fact]
    public void A_failed_save_keeps_what_was_typed()
    {
        Meals.MealToEdit = TestData.Meal("Pancakes", id: 42);
        SetupSend<SaveIngredient.Command, Result>(Result.Fail("Name is too long"));
        var cut = Render<AddIngredientRow>();

        cut.Find(InputOf("add-ingredient-name")).Input("Eggs");
        cut.Find(TestId("create-ingredient-button")).Click();

        cut.WaitForAssertion(() => Assert.Contains("Name is too long", SnackbarMessages));

        // Trying again sends the same name: nothing was cleared.
        cut.Find(TestId("create-ingredient-button")).Click();
        cut.WaitForAssertion(() => Assert.Equal(2, Sent<SaveIngredient.Command>().Count));
        Assert.All(Sent<SaveIngredient.Command>(), c => Assert.Equal("Eggs", c.Ingredient.Name));
    }

    [Fact]
    public void An_empty_name_adds_nothing()
    {
        Meals.MealToEdit = TestData.Meal("Pancakes", id: 0);
        var cut = Render<AddIngredientRow>();

        cut.Find(InputOf("add-ingredient-amount")).Input("3");
        cut.Find(TestId("create-ingredient-button")).Click();

        Assert.Empty(Meals.MealToEdit.Ingredients);
    }

    [Fact]
    public void Enter_moves_from_name_to_amount_and_adds_from_the_amount()
    {
        Meals.MealToEdit = TestData.Meal("Pancakes", id: 0);
        var cut = Render<AddIngredientRow>();

        cut.Find(InputOf("add-ingredient-name")).Input("Eggs");
        PressEnter(cut.Find(InputOf("add-ingredient-name")));
        AssertFocused(cut.Find(InputOf("add-ingredient-amount")));

        PressEnter(cut.Find(InputOf("add-ingredient-amount")));

        cut.WaitForAssertion(() => Assert.Single(Meals.MealToEdit.Ingredients));
    }
}
