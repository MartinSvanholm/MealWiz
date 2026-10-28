using Bunit;
using MealWiz.Shared.Features.Ingredients;
using MealWiz.Shared.Features.Ingredients.CreateIngredient;
using MealWiz.Shared.Features.Ingredients.Models;
using MealWiz.Shared.Tests.TestInfrastructure;
using Xunit;

namespace MealWiz.Shared.Tests.Features.Ingredients.ComponentTests;

/// <summary>IngredientCard and CreateIngredientCard: the rows of the edit meal form and the "add" card above them.</summary>
public class IngredientCardTests : ComponentTestBase
{
    [Fact]
    public void Renders_the_ingredient_name_and_amount()
    {
        var cut = Render<IngredientCard>(p => p.Add(c => c.Ingredient, TestData.Ingredient("Flour", "200g", id: 1)));

        var card = cut.Find(TestId("ingredient-card")).TextContent;
        Assert.Contains("Flour", card);
        Assert.Contains("200g", card);
    }

    [Fact]
    public void Clicking_the_card_opens_the_ingredient_for_editing_in_the_drawer()
    {
        var flour = TestData.Ingredient("Flour", "200g", id: 1);
        RenderDrawers();
        var cut = Render<IngredientCard>(p => p.Add(c => c.Ingredient, flour));

        cut.Find(TestId("ingredient-card")).Click();

        Assert.True(Drawer.IsDrawerOpen);
        Assert.Equal("Edit Ingredient", Drawer.StateParameters.Title);
        Assert.Same(flour, Drawer.StateParameters.ContentParameters[nameof(IngredientDrawer.Ingredient)]);
        Assert.Equal("Flour", DrawerHost.Find(InputOf("ingredient-name-input")).GetAttribute("value"));
    }

    [Fact]
    public void Create_card_opens_an_empty_ingredient_in_the_drawer()
    {
        RenderDrawers();
        var cut = Render<CreateIngredientCard>();

        cut.Find(TestId("create-ingredient-card")).Click();

        Assert.True(Drawer.IsDrawerOpen);
        Assert.Equal("Create Ingredient", Drawer.StateParameters.Title);
        var ingredient = Assert.IsType<Ingredient>(Drawer.StateParameters.ContentParameters[nameof(IngredientDrawer.Ingredient)]);
        Assert.Equal(0, ingredient.Id);
        Assert.True(ingredient.ShowOnGroceryList);
        Assert.NotNull(DrawerHost.Find(TestId("save-ingredient-button")));
    }

    [Fact]
    public void Drawer_switch_sets_whether_the_ingredient_goes_on_the_grocery_list()
    {
        var salt = TestData.Ingredient("Salt");
        var cut = Render<IngredientDrawer>(p => p.Add(d => d.Ingredient, salt));

        cut.Find(InputOf("ingredient-show-on-grocery-list-switch")).Change(false);
        Assert.False(salt.ShowOnGroceryList);

        cut.Find(InputOf("ingredient-show-on-grocery-list-switch")).Change(true);
        Assert.True(salt.ShowOnGroceryList);
    }
}
