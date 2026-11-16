using Bunit;
using FluentResults;
using MealWiz.Shared.Features.GroceryCategories.RememberGroceryName;
using MealWiz.Shared.Features.GroceryList.AddGroceryItem;
using MealWiz.Shared.Features.GroceryList.Components;
using MealWiz.Shared.Features.GroceryList.Models;
using MealWiz.Shared.Features.GroceryList.UpdateGroceryItem;
using MealWiz.Shared.Features.Ingredients;
using MealWiz.Shared.Tests.TestInfrastructure;
using Xunit;

namespace MealWiz.Shared.Tests.Features.GroceryCategories.ComponentTests;

/// <summary>
/// The category chips in the grocery item drawer and the ingredient drawer: they show the name's category, follow the
/// name until one is tapped, and Save moves the name to a tapped category everywhere.
/// </summary>
public class CategoryChipsInDrawersTests : ComponentTestBase
{
    public CategoryChipsInDrawersTests()
    {
        SetupSend<RememberGroceryName.Command, Result>(Result.Ok());
        SetupSend<UpdateGroceryItem.Command, Result<int>>(Result.Ok(1));
    }

    private static string? PressedChip<T>(IRenderedComponent<T> cut) where T : Microsoft.AspNetCore.Components.IComponent =>
        cut.FindAll(TestId("category-chip")).SingleOrDefault(c => c.GetAttribute("aria-pressed") == "true")?.GetAttribute("data-category");

    private static void Tap<T>(IRenderedComponent<T> cut, string key) where T : Microsoft.AspNetCore.Components.IComponent =>
        cut.FindAll(TestId("category-chip")).Single(c => c.GetAttribute("data-category") == key).Click();

    [Fact]
    public void GroceryItemDrawer_PreselectsTheNamesCategory_AndLoadsTheCategoriesIfNeeded()
    {
        var cut = Render<GroceryItemDrawer>(p => p.Add(d => d.GroceryItem, TestData.GroceryItem("Milk", id: 1)));

        Assert.Equal(11, cut.FindAll(TestId("category-chip")).Count);
        Assert.Equal("dairy_eggs", PressedChip(cut));
    }

    [Fact]
    public void GroceryItemDrawer_TappingAnotherChipAndSaving_MovesTheNameThere()
    {
        var milk = TestData.GroceryItem("Milk", id: 1);
        GroceryList.GroceryList.Items.Add(milk);
        Drawer.OpenDrawer("Edit item", typeof(GroceryItemDrawer));
        var cut = Render<GroceryItemDrawer>(p => p.Add(d => d.GroceryItem, milk));

        Tap(cut, "drinks");
        Assert.Equal("drinks", PressedChip(cut));
        cut.Find(TestId("save-grocery-item-button")).Click();

        cut.WaitForAssertion(() => Assert.False(Drawer.IsDrawerOpen));
        var command = Assert.Single(Sent<RememberGroceryName.Command>());
        Assert.Equal(("Milk", 8), (command.Name, command.CategoryId));
        Assert.Equal(8, GroceryCategories.CategoryFor("milk")?.Id);
    }

    [Fact]
    public void GroceryItemDrawer_SavingWithTheChipItShowed_RemembersNothing()
    {
        var milk = TestData.GroceryItem("Milk", id: 1);
        GroceryList.GroceryList.Items.Add(milk);
        var cut = Render<GroceryItemDrawer>(p => p.Add(d => d.GroceryItem, milk));

        cut.Find(TestId("save-grocery-item-button")).Click();

        cut.WaitForAssertion(() => Assert.Single(Sent<UpdateGroceryItem.Command>()));
        Assert.Empty(Sent<RememberGroceryName.Command>());
    }

    [Fact]
    public void GroceryItemDrawer_AddingANewItem_MovesItsNameOnceItIsAdded()
    {
        SetupSend<AddGroceryItem.Command, Result<GroceryItem>>(Result.Ok(TestData.GroceryItem("Tofu", id: 7)));
        var cut = Render<GroceryItemDrawer>(p => p.Add(d => d.GroceryItem, TestData.GroceryItem("Tofu")));
        Assert.Equal("other", PressedChip(cut));

        Tap(cut, "meat_fish");
        cut.Find(TestId("save-grocery-item-button")).Click();

        cut.WaitForAssertion(() => Assert.Single(Sent<RememberGroceryName.Command>()));
        Assert.Equal(3, Sent<RememberGroceryName.Command>()[0].CategoryId);
    }

    [Fact]
    public void GroceryItemDrawer_AFailedSave_MovesNothing()
    {
        SetupSend<UpdateGroceryItem.Command, Result<int>>(Result.Fail<int>("Offline"));
        var milk = TestData.GroceryItem("Milk", id: 1);
        GroceryList.GroceryList.Items.Add(milk);
        var cut = Render<GroceryItemDrawer>(p => p.Add(d => d.GroceryItem, milk));

        Tap(cut, "drinks");
        cut.Find(TestId("save-grocery-item-button")).Click();

        cut.WaitForAssertion(() => Assert.Contains("Offline", SnackbarMessages));
        Assert.Empty(Sent<RememberGroceryName.Command>());
    }

    [Fact]
    public void GroceryItemDrawer_AFailedMove_ShowsTheErrorAndLeavesTheName()
    {
        SetupSend<RememberGroceryName.Command, Result>(Result.Fail("No response from server"));
        var milk = TestData.GroceryItem("Milk", id: 1);
        GroceryList.GroceryList.Items.Add(milk);
        var cut = Render<GroceryItemDrawer>(p => p.Add(d => d.GroceryItem, milk));

        Tap(cut, "drinks");
        cut.Find(TestId("save-grocery-item-button")).Click();

        cut.WaitForAssertion(() => Assert.Contains("No response from server", SnackbarMessages));
        Assert.Equal(5, GroceryCategories.CategoryFor("milk")?.Id);
    }

    [Fact]
    public void IngredientDrawer_TheChipFollowsTheName_UntilOneIsTapped()
    {
        var ingredient = TestData.Ingredient("", "");
        var cut = Render<IngredientDrawer>(p => p.Add(d => d.Ingredient, ingredient));
        Assert.Equal("other", PressedChip(cut));

        cut.Find(InputOf("ingredient-name-input")).Change("Smør");
        Assert.Equal("dairy_eggs", PressedChip(cut));

        Tap(cut, "pantry");
        cut.Find(InputOf("ingredient-name-input")).Change("Bread");
        Assert.Equal("pantry", PressedChip(cut));
    }

    [Fact]
    public void IngredientDrawer_APendingIngredient_MovesItsNameOnSave()
    {
        var tofu = TestData.Ingredient("Tofu", "200 g");
        Meals.MealToEdit = TestData.Meal("Stir fry", id: 0, ingredients: [tofu]);
        Drawer.OpenDrawer("Edit ingredient", typeof(IngredientDrawer));
        var cut = Render<IngredientDrawer>(p => p.Add(d => d.Ingredient, tofu));

        Tap(cut, "meat_fish");
        cut.Find(TestId("save-ingredient-button")).Click();

        cut.WaitForAssertion(() => Assert.False(Drawer.IsDrawerOpen));
        var command = Assert.Single(Sent<RememberGroceryName.Command>());
        Assert.Equal(("Tofu", 3), (command.Name, command.CategoryId));
    }
}
