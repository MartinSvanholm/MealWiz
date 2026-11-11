using Bunit;
using MealWiz.Shared.Features.Ingredients;
using MealWiz.Shared.Features.Ingredients.CreateIngredient;
using MealWiz.Shared.Tests.TestInfrastructure;
using Xunit;

namespace MealWiz.Shared.Tests.Features.Ingredients.ComponentTests;

/// <summary>
/// The ingredient row, card and drawer texts come from resources (UI-03): every localizer here echoes its key as
/// <c>[Key]</c>, so a hard-coded text would fail these tests. The English texts are asserted by the other tests.
/// </summary>
public class IngredientsTextTests() : ComponentTestBase(echoResourceKeys: true)
{
    [Fact]
    public void AddIngredientRow_and_its_drawer()
    {
        Meals.MealToEdit = TestData.Meal("Pancakes", id: 0);
        RenderDrawers();
        var cut = Render<AddIngredientRow>();

        Assert.Equal("[AddIngredient_Add]", cut.Find(TestId("create-ingredient-button")).GetAttribute("aria-label"));
        cut.Find(TestId("create-ingredient-button")).Click();

        DrawerHost.WaitForAssertion(() => Assert.Equal("[AddIngredient_Add]", DrawerHost.Find(TestId("drawer-title")).TextContent));
        Assert.Contains("[IngredientDrawer_Name]", DrawerHost.Markup);
        Assert.Contains("[IngredientDrawer_AddToGroceryList]", DrawerHost.Markup);
        Assert.Equal("[SaveIngredient_Button]", DrawerHost.Find(TestId("save-ingredient-button")).TextContent.Trim());
    }

    [Fact]
    public void IngredientCard_amount_placeholder_and_delete_label()
    {
        var cut = Render<IngredientCard>(p => p.Add(c => c.Ingredient, TestData.Ingredient("Salt", id: 2)));

        Assert.Equal("[IngredientCard_AddAmount]", cut.Find(TestId("ingredient-amount-chip")).TextContent.Trim());
        Assert.Equal("[DeleteIngredient_Aria]", cut.Find(TestId("delete-ingredient-button")).GetAttribute("aria-label"));
    }
}
