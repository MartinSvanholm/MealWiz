using Bunit;
using FluentResults;
using MealWiz.Shared.Features.Ingredients.DeleteIngredient;
using MealWiz.Shared.Features.Ingredients.GetIngredientsByMealId;
using MealWiz.Shared.Features.Ingredients.Models;
using MealWiz.Shared.Tests.TestInfrastructure;
using Xunit;

namespace MealWiz.Shared.Tests.Features.Ingredients.ComponentTests;

public class DeleteIngredientButtonTests : ComponentTestBase
{
    [Fact]
    public void A_pending_ingredient_is_removed_locally_without_confirmation()
    {
        var eggs = TestData.Ingredient("Eggs");
        Meals.MealToEdit = TestData.Meal("Pancakes", id: 0, ingredients: [eggs]);
        var cut = Render<DeleteIngredientButton>(p => p.Add(b => b.Ingredient, eggs));

        cut.Find(TestId("delete-ingredient-button")).Click();

        Assert.Empty(Meals.MealToEdit.Ingredients);
        Assert.False(Drawer.IsConfirmDrawerOpen);
        Assert.Empty(SentRequests);
    }

    [Fact]
    public void A_saved_ingredient_is_deleted_after_confirmation()
    {
        var eggs = TestData.Ingredient("Eggs", id: 11);
        Meals.MealToEdit = TestData.Meal("Pancakes", id: 42, ingredients: [eggs]);
        SetupSend<DeleteIngredient.Command, Result>(Result.Ok());
        SetupSend<GetIngredientsByMealId.Query, Result<List<Ingredient>>>(Result.Ok(new List<Ingredient>()));
        RenderDrawers();
        var cut = Render<DeleteIngredientButton>(p => p.Add(b => b.Ingredient, eggs));

        cut.Find(TestId("delete-ingredient-button")).Click();
        Assert.True(Drawer.IsConfirmDrawerOpen);
        Assert.Empty(Sent<DeleteIngredient.Command>());

        Confirm();

        cut.WaitForAssertion(() => Assert.False(Drawer.IsConfirmDrawerOpen));
        Assert.Same(eggs, Assert.Single(Sent<DeleteIngredient.Command>()).Ingredient);
        Assert.Empty(Meals.MealToEdit.Ingredients);
        Assert.Contains("Ingredient deleted.", SnackbarMessages);
    }
}
