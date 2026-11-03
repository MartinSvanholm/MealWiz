using Bunit;
using MealWiz.Shared.Features.Ingredients;
using MealWiz.Shared.Tests.TestInfrastructure;
using Xunit;

namespace MealWiz.Shared.Tests.Features.Ingredients.ComponentTests;

/// <summary>IngredientCard (a row of the edit meal form) and the IngredientDrawer it opens.</summary>
public class IngredientCardTests : ComponentTestBase
{
    [Fact]
    public void Renders_the_name_and_a_green_amount_chip()
    {
        var cut = Render<IngredientCard>(p => p.Add(c => c.Ingredient, TestData.Ingredient("Flour", "200g", id: 1)));

        Assert.Contains("Flour", cut.Find(TestId("ingredient-card")).TextContent);
        var chip = cut.Find(TestId("ingredient-amount-chip"));
        Assert.Equal("200g", chip.TextContent.Trim());
        Assert.Equal("false", chip.GetAttribute("data-empty"));
        Assert.Empty(cut.FindAll(TestId("ingredient-drag-handle")));
        Assert.NotNull(cut.Find(TestId("delete-ingredient-button")));
    }

    [Fact]
    public void Without_an_amount_the_chip_offers_to_add_one()
    {
        var cut = Render<IngredientCard>(p => p.Add(c => c.Ingredient, TestData.Ingredient("Salt", "", id: 1)));

        var chip = cut.Find(TestId("ingredient-amount-chip"));
        Assert.Equal("+ amount", chip.TextContent.Trim());
        Assert.Equal("true", chip.GetAttribute("data-empty"));
    }

    [Fact]
    public void Clicking_the_row_opens_the_ingredient_for_editing_in_the_drawer()
    {
        var flour = TestData.Ingredient("Flour", "200g", id: 1);
        RenderDrawers();
        var cut = Render<IngredientCard>(p => p.Add(c => c.Ingredient, flour));

        cut.Find(TestId("ingredient-card")).Click();

        Assert.True(Drawer.IsDrawerOpen);
        Assert.Equal("Edit ingredient", Drawer.StateParameters.Title);
        Assert.Same(flour, Drawer.StateParameters.ContentParameters[nameof(IngredientDrawer.Ingredient)]);
        Assert.Equal("Flour", DrawerHost.Find(InputOf("ingredient-name-input")).GetAttribute("value"));
    }

    [Fact]
    public void Clicking_the_amount_chip_opens_the_drawer()
    {
        RenderDrawers();
        var cut = Render<IngredientCard>(p => p.Add(c => c.Ingredient, TestData.Ingredient("Salt", "", id: 1)));

        cut.Find(TestId("ingredient-amount-chip")).Click();

        Assert.True(Drawer.IsDrawerOpen);
    }

    [Fact]
    public void The_drawer_has_a_delete_action_in_its_header()
    {
        var eggs = TestData.Ingredient("Eggs");
        Meals.MealToEdit = TestData.Meal("Pancakes", id: 0, ingredients: [eggs]);
        RenderDrawers();
        var cut = Render<IngredientCard>(p => p.Add(c => c.Ingredient, eggs));
        cut.Find(TestId("ingredient-card")).Click();

        DrawerHost.Find($"{TestId("drawer-header-action")} {TestId("drawer-delete-ingredient")}").Click();

        Assert.Empty(Meals.MealToEdit.Ingredients);
        Assert.False(Drawer.IsDrawerOpen);
    }

    [Fact]
    public void The_drawers_save_button_sits_in_the_drawer_footer()
    {
        var cut = Render<IngredientDrawer>(p => p.Add(d => d.Ingredient, TestData.Ingredient("Salt")));

        var button = cut.Find($"{TestId("drawer-footer")} {TestId("save-ingredient-button")}");
        Assert.Contains("Save ingredient", button.TextContent);
    }

    [Fact]
    public void Drawer_does_not_focus_a_field_when_it_opens()
    {
        // No autofocus: on a phone a focused field opens the keyboard over the drawer straight away.
        Render<IngredientDrawer>(p => p.Add(d => d.Ingredient, TestData.Ingredient("Salt")));

        Assert.DoesNotContain(JSInterop.Invocations, i => i.Identifier == "Blazor._internal.domWrapper.focus");
    }

    [Fact]
    public void Enter_in_the_drawers_name_moves_to_the_amount()
    {
        var cut = Render<IngredientDrawer>(p => p.Add(d => d.Ingredient, TestData.Ingredient("Salt")));

        PressEnter(cut.Find(InputOf("ingredient-name-input")));

        AssertFocused(cut.Find(InputOf("ingredient-amount-input")));
    }

    [Fact]
    public void Enter_in_the_drawers_amount_saves_the_ingredient()
    {
        Drawer.OpenDrawer("Edit ingredient", typeof(IngredientDrawer));
        var salt = TestData.Ingredient("Salt");
        var cut = Render<IngredientDrawer>(p => p.Add(d => d.Ingredient, salt));

        cut.Find(InputOf("ingredient-amount-input")).Input("1 tsp");
        PressEnter(cut.Find(InputOf("ingredient-amount-input")));

        cut.WaitForAssertion(() => Assert.False(Drawer.IsDrawerOpen));
        Assert.Equal("1 tsp", salt.Amount);
        Assert.Contains(salt, Meals.MealToEdit.Ingredients);
    }

    [Fact]
    public void Drawer_switch_sets_whether_the_ingredient_goes_on_the_grocery_list()
    {
        Drawer.OpenDrawer("Edit ingredient", typeof(IngredientDrawer));
        var salt = TestData.Ingredient("Salt");
        var cut = Render<IngredientDrawer>(p => p.Add(d => d.Ingredient, salt));

        cut.Find(InputOf("ingredient-show-on-grocery-list-switch")).Change(false);
        cut.Find(TestId("save-ingredient-button")).Click();

        cut.WaitForAssertion(() => Assert.False(Drawer.IsDrawerOpen));
        Assert.False(salt.ShowOnGroceryList);
    }

    // MEA-77: the drawer used to edit the listed ingredient itself, so closing it without saving left the change
    // showing (and, for a new meal, saved it with the meal after all).
    [Fact]
    public void Editing_in_the_drawer_leaves_the_ingredient_alone_until_saved()
    {
        var salt = TestData.Ingredient("Salt", "1 tsp");
        Meals.MealToEdit = TestData.Meal("Soup", id: 0, ingredients: [salt]);
        var cut = Render<IngredientDrawer>(p => p.Add(d => d.Ingredient, salt));

        cut.Find(InputOf("ingredient-name-input")).Change("Sea salt");
        cut.Find(InputOf("ingredient-amount-input")).Input("2 tsp");
        cut.Find(InputOf("ingredient-show-on-grocery-list-switch")).Change(false);

        Assert.Equal(("Salt", "1 tsp", true), (salt.Name, salt.Amount, salt.ShowOnGroceryList));
        Assert.Same(salt, Assert.Single(Meals.MealToEdit.Ingredients));
    }

    [Fact]
    public void Saving_a_pending_ingredient_updates_it_in_place()
    {
        var salt = TestData.Ingredient("Salt", "1 tsp");
        Meals.MealToEdit = TestData.Meal("Soup", id: 0, ingredients: [salt]);
        Drawer.OpenDrawer("Edit ingredient", typeof(IngredientDrawer));
        var cut = Render<IngredientDrawer>(p => p.Add(d => d.Ingredient, salt));

        cut.Find(InputOf("ingredient-name-input")).Change("Sea salt");
        cut.Find(TestId("save-ingredient-button")).Click();

        cut.WaitForAssertion(() => Assert.False(Drawer.IsDrawerOpen));
        Assert.Same(salt, Assert.Single(Meals.MealToEdit.Ingredients));
        Assert.Equal("Sea salt", salt.Name);
    }
}
