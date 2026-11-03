using Bunit;
using FluentResults;
using MealWiz.Shared.Features.Ingredients;
using MealWiz.Shared.Features.Ingredients.CreateIngredient;
using MealWiz.Shared.Features.Ingredients.GetIngredientsByMealId;
using MealWiz.Shared.Features.Ingredients.Models;
using MealWiz.Shared.Features.Ingredients.SaveIngredient;
using MealWiz.Shared.Tests.TestInfrastructure;
using Xunit;

namespace MealWiz.Shared.Tests.Features.Ingredients.ComponentTests;

/// <summary>
/// The inline "add ingredient" row on the edit meal form: name, amount and an add button that opens the ingredient
/// drawer. The ingredient is saved only from the drawer.
/// </summary>
public class AddIngredientRowTests : ComponentTestBase
{
    [Fact]
    public void The_add_button_opens_an_empty_drawer_when_nothing_was_typed()
    {
        Meals.MealToEdit = TestData.Meal("Pancakes", id: 0);
        RenderDrawers();
        var cut = Render<AddIngredientRow>();

        cut.Find(TestId("create-ingredient-button")).Click();

        DrawerHost.WaitForAssertion(() => Assert.Equal("Add ingredient", DrawerHost.Find(TestId("drawer-title")).TextContent));
        Assert.Equal(typeof(IngredientDrawer), Drawer.StateParameters.Content);
        Assert.Equal("", DrawerHost.Find(InputOf("ingredient-name-input")).GetAttribute("value") ?? "");
        Assert.Equal("", DrawerHost.Find(InputOf("ingredient-amount-input")).GetAttribute("value") ?? "");
        Assert.Empty(Meals.MealToEdit.Ingredients);
    }

    [Fact]
    public void The_add_button_opens_the_drawer_with_what_was_typed_and_saves_nothing_yet()
    {
        Meals.MealToEdit = TestData.Meal("Pancakes", id: 0);
        RenderDrawers();
        var cut = Render<AddIngredientRow>();

        cut.Find(InputOf("add-ingredient-name")).Input("Eggs");
        cut.Find(InputOf("add-ingredient-amount")).Input("3");
        cut.Find(TestId("create-ingredient-button")).Click();

        DrawerHost.WaitForAssertion(() => Assert.Equal("Eggs", DrawerHost.Find(InputOf("ingredient-name-input")).GetAttribute("value")));
        Assert.Equal("3", DrawerHost.Find(InputOf("ingredient-amount-input")).GetAttribute("value"));
        Assert.Empty(Meals.MealToEdit.Ingredients);
        Assert.Empty(SentRequests);
    }

    [Fact]
    public void Only_the_amount_typed_opens_the_drawer_with_just_the_amount()
    {
        Meals.MealToEdit = TestData.Meal("Pancakes", id: 0);
        RenderDrawers();
        var cut = Render<AddIngredientRow>();

        cut.Find(InputOf("add-ingredient-amount")).Input("200 g");
        cut.Find(TestId("create-ingredient-button")).Click();

        DrawerHost.WaitForAssertion(() => Assert.Equal("200 g", DrawerHost.Find(InputOf("ingredient-amount-input")).GetAttribute("value")));
        Assert.Equal("", DrawerHost.Find(InputOf("ingredient-name-input")).GetAttribute("value") ?? "");
    }

    [Fact]
    public void Saving_from_the_drawer_adds_a_pending_ingredient_to_a_new_meal_and_clears_the_row()
    {
        Meals.MealToEdit = TestData.Meal("Pancakes", id: 0);
        RenderDrawers();
        var cut = Render<AddIngredientRow>();

        cut.Find(InputOf("add-ingredient-name")).Input("Eggs");
        cut.Find(InputOf("add-ingredient-amount")).Input("3");
        cut.Find(TestId("create-ingredient-button")).Click();
        DrawerHost.Find(TestId("save-ingredient-button")).Click();

        var eggs = Assert.Single(Meals.MealToEdit.Ingredients);
        Assert.Equal("Eggs", eggs.Name);
        Assert.Equal("3", eggs.Amount);
        Assert.True(eggs.ShowOnGroceryList);
        Assert.Empty(SentRequests);
        Assert.False(Drawer.IsDrawerOpen);

        // The row was cleared when the drawer opened: the next add starts empty.
        Assert.Equal("", cut.Find(InputOf("add-ingredient-name")).GetAttribute("value") ?? "");
    }

    [Fact]
    public void Saving_from_the_drawer_saves_the_ingredient_of_a_saved_meal()
    {
        Meals.MealToEdit = TestData.Meal("Pancakes", id: 42);
        SetupSend<SaveIngredient.Command, Result>(Result.Ok());
        SetupSend<GetIngredientsByMealId.Query, Result<List<Ingredient>>>(Result.Ok(new List<Ingredient> { TestData.Ingredient("Eggs", id: 9) }));
        RenderDrawers();
        var cut = Render<AddIngredientRow>();

        cut.Find(InputOf("add-ingredient-name")).Input("Eggs");
        cut.Find(TestId("create-ingredient-button")).Click();
        DrawerHost.Find(TestId("save-ingredient-button")).Click();

        DrawerHost.WaitForAssertion(() => Assert.Single(Sent<SaveIngredient.Command>()));
        Assert.Equal(42, Sent<SaveIngredient.Command>()[0].MealId);
        Assert.Equal("Eggs", Sent<SaveIngredient.Command>()[0].Ingredient.Name);
    }

    [Fact]
    public void An_ingredient_without_a_name_is_not_saved()
    {
        Meals.MealToEdit = TestData.Meal("Pancakes", id: 0);
        RenderDrawers();
        var cut = Render<AddIngredientRow>();

        cut.Find(InputOf("add-ingredient-amount")).Input("3");
        cut.Find(TestId("create-ingredient-button")).Click();
        DrawerHost.Find(TestId("save-ingredient-button")).Click();

        Assert.Empty(Meals.MealToEdit.Ingredients);
        Assert.Contains("Give the ingredient a name.", SnackbarMessages);
        Assert.True(Drawer.IsDrawerOpen);
    }

    [Fact]
    public void Enter_moves_from_name_to_amount_and_opens_the_drawer_from_the_amount()
    {
        Meals.MealToEdit = TestData.Meal("Pancakes", id: 0);
        RenderDrawers();
        var cut = Render<AddIngredientRow>();

        cut.Find(InputOf("add-ingredient-name")).Input("Eggs");
        PressEnter(cut.Find(InputOf("add-ingredient-name")));
        AssertFocused(cut.Find(InputOf("add-ingredient-amount")));

        PressEnter(cut.Find(InputOf("add-ingredient-amount")));

        DrawerHost.WaitForAssertion(() => Assert.Equal("Eggs", DrawerHost.Find(InputOf("ingredient-name-input")).GetAttribute("value")));
        Assert.Empty(Meals.MealToEdit.Ingredients);
    }
}
