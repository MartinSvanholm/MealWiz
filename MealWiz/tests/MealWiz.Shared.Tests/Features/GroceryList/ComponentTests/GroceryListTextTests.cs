using Bunit;
using FluentResults;
using MealWiz.Shared.Features.GroceryList;
using MealWiz.Shared.Features.GroceryList.AddMealPlanIngredientsToGroceryList;
using MealWiz.Shared.Features.GroceryList.GetGroceryItemSources;
using MealWiz.Shared.Features.GroceryList.GetAllGroceryItems;
using MealWiz.Shared.Features.GroceryList.Models;
using MealWiz.Shared.Tests.TestInfrastructure;
using Xunit;

namespace MealWiz.Shared.Tests.Features.GroceryList.ComponentTests;

/// <summary>
/// The grocery list's texts come from its resource (UI-03): every localizer here echoes its key as <c>[Key]</c>, so a
/// hard-coded text would fail these tests. The English texts are asserted by the other grocery list tests.
/// </summary>
public class GroceryListTextTests() : ComponentTestBase(echoResourceKeys: true)
{
    private readonly GroceryItem _milk = TestData.GroceryItem("Milk", "1 l", id: 1);
    private readonly GroceryItem _bread = TestData.GroceryItem("Bread", id: 2);

    private void HaveItems(params GroceryItem[] items) =>
        SetupSend<GetAllGroceryItems.Query, Result<List<GroceryItem>>>(Result.Ok(items.ToList()));

    [Fact]
    public void Page_header_progress_and_sections()
    {
        _bread.IsPicked = true;
        HaveItems(_milk, _bread);

        var cut = RenderWithPopovers<GroceryListPage>();

        Assert.Equal("[GroceryList_Title]", cut.Find(TestId("screen-header-title")).TextContent.Trim());
        Assert.Equal("[GroceryList_Header_Other]", cut.Find(TestId("screen-header-label")).TextContent.Trim());
        Assert.Equal("[GroceryList_Progress_Other]", cut.Find(TestId("grocery-progress-count")).TextContent.Trim());
        Assert.Equal("[GroceryList_ProgressAria]", cut.Find(TestId("grocery-progress")).GetAttribute("aria-label"));
        Assert.Equal("[GroceryList_ToBuy]", cut.Find(TestId("grocery-to-buy-heading")).TextContent.Trim());
        Assert.Equal("[GroceryList_InBasket]", cut.Find(TestId("grocery-basket-heading")).TextContent.Trim());
        Assert.Equal("[GroceryList_ClearBasket]", cut.Find(TestId("clear-basket-button")).TextContent.Trim());
        Assert.Equal("[GroceryList_FromMealPlan]", cut.Find(TestId("add-plan-ingredients-button")).TextContent.Trim());
        Assert.Equal("[GroceryList_AddItemPlaceholder]", cut.Find(TestId("add-grocery-item-input")).GetAttribute("placeholder"));
        Assert.Equal("[GroceryList_AddItemAria]", cut.Find(TestId("add-grocery-item-button")).GetAttribute("aria-label"));
        var checkboxes = cut.FindAll(TestId("grocery-item-checkbox")).Select(c => c.GetAttribute("aria-label")).ToList();
        Assert.Equal(["[GroceryList_CheckOffAria]", "[GroceryList_PutBackAria]"], checkboxes);
        Assert.Equal("[GroceryItem_EditAria]", cut.Find(TestId("grocery-item-name")).GetAttribute("aria-label"));
    }

    [Fact]
    public void Page_empty_state()
    {
        var cut = RenderWithPopovers<GroceryListPage>();

        Assert.Contains("[GroceryList_Empty]", cut.Find(TestId("grocery-list-empty")).TextContent);
        Assert.Contains("[GroceryList_EmptyHint]", cut.Find(TestId("grocery-list-empty")).TextContent);
    }

    [Fact]
    public void Clear_basket_confirm_drawer()
    {
        _milk.IsPicked = true;
        HaveItems(_milk);
        RenderDrawers();
        var cut = RenderWithPopovers<GroceryListPage>();

        cut.Find(TestId("clear-basket-button")).Click();

        Assert.Equal("[GroceryList_ClearConfirmTitle_One]", ConfirmDrawerHost.Find(TestId("confirm-drawer-title")).TextContent.Trim());
    }

    [Fact]
    public void Item_drawer_and_delete_confirm()
    {
        HaveItems(_milk);
        RenderDrawers();
        var cut = RenderWithPopovers<GroceryListPage>();

        cut.Find(TestId("grocery-item-name")).Click();

        Assert.Equal("[GroceryItem_EditTitle]", Drawer.StateParameters.Title);
        Assert.Equal("[GroceryItem_Save]", DrawerHost.Find(TestId("save-grocery-item-button")).TextContent.Trim());
        Assert.Equal("[Common_Delete]", DrawerHost.Find(TestId("delete-grocery-item-button")).GetAttribute("aria-label"));

        DrawerHost.Find(TestId("delete-grocery-item-button")).Click();

        Assert.Equal("[GroceryItem_DeleteConfirmTitle]", ConfirmDrawerHost.Find(TestId("confirm-drawer-title")).TextContent.Trim());
    }

    [Fact]
    public void Merge_drawer_rows_and_confirm()
    {
        HaveItems(_milk);
        MealPlan.MealPlan = TestData.MealPlanWith(DateTime.Today, TestData.Meal(
            "Pancakes", id: 5, ingredients: [TestData.Ingredient("Flour", "200g", id: 11), TestData.Ingredient("milk", "3 dl", id: 12)]),
            plannedMealId: 101);

        var cut = Render<AddPlanIngredientsToGroceryListDrawer>();
        cut.WaitForAssertion(() => Assert.Empty(cut.FindAll(TestId("plan-ingredients-loading"))));

        Assert.Equal("[MergeDrawer_Plan] · [MergeDrawer_InList]", cut.Find(TestId("plan-ingredient-existing")).TextContent.Trim());
        Assert.Equal("[MergeDrawer_Add]", cut.Find(TestId("plan-ingredients-confirm")).TextContent.Trim());
        Assert.Equal("[PlanIngredients_AmountAria]", cut.Find(InputOf("plan-ingredient-amount-input")).GetAttribute("aria-label"));
        Assert.Equal("[PlanIngredients_Total]", cut.Find(InputOf("plan-ingredient-amount-input")).GetAttribute("placeholder"));
    }

    [Fact]
    public void Merge_drawer_empty_states()
    {
        MealPlan.MealPlan = TestData.MealPlanWith(DateTime.Today, TestData.Meal("Eat out", id: 6), plannedMealId: 101);

        var cut = Render<AddPlanIngredientsToGroceryListDrawer>();

        cut.WaitForAssertion(() =>
            Assert.Equal("[PlanIngredients_NothingPlanned]", cut.Find(TestId("plan-ingredients-empty")).TextContent.Trim()));
    }
}
