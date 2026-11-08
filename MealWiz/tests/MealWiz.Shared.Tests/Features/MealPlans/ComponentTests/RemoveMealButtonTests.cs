using Bunit;
using FluentResults;
using MealWiz.Shared.Features.GroceryList.GetAllGroceryItems;
using MealWiz.Shared.Features.GroceryList.GetGroceryItemSources;
using MealWiz.Shared.Features.GroceryList.Models;
using MealWiz.Shared.Features.MealPlans.RemoveMealFromMealPlan;
using MealWiz.Shared.Tests.TestInfrastructure;
using Xunit;

namespace MealWiz.Shared.Tests.Features.MealPlans.ComponentTests;

public class RemoveMealButtonTests : ComponentTestBase
{
    private void PlanTacos() =>
        MealPlan.MealPlan = TestData.MealPlanWith(MealPlan.SelectedDate, TestData.Meal("Tacos", id: 8), plannedMealId: 501);

    [Fact]
    public void Renders_a_remove_button()
    {
        var cut = Render<RemoveMealButton>();

        Assert.NotNull(cut.Find(TestId("remove-meal-button")));
    }

    [Fact]
    public void Opens_the_remove_drawer_without_removing()
    {
        PlanTacos();
        RenderDrawers();
        var cut = Render<RemoveMealButton>();

        cut.Find(TestId("remove-meal-button")).Click();

        DrawerHost.WaitForAssertion(() => Assert.Equal("Remove Tacos?", DrawerHost.Find(TestId("drawer-title")).TextContent));
        Assert.Empty(DrawerHost.FindAll(TestId("remove-meal-grocery-row")));
        Assert.Empty(Sent<RemoveMealFromMealPlan.Command>());
        Assert.True(MealPlan.SelectedDateHasMeal);
    }

    [Fact]
    public void Counts_only_the_items_this_planned_day_alone_needs()
    {
        PlanTacos();
        var manual = TestData.GroceryItem("Milk", id: 13, isManual: true);
        var picked = TestData.GroceryItem("Basil", id: 14, isPicked: true, isManual: false);
        SetupSend<GetAllGroceryItems.Query, Result<List<GroceryItem>>>(Result.Ok(new List<GroceryItem>
        {
            TestData.GroceryItem("Pasta", id: 11, isManual: false),
            TestData.GroceryItem("Garlic", id: 12, isManual: false),
            manual,
            picked
        }));
        SetupSend<GetGroceryItemSources.Query, Result<List<GroceryItemSource>>>(Result.Ok(new List<GroceryItemSource>
        {
            Live(11, 501), Live(12, 501), Live(12, 502), Live(13, 501), Live(14, 501)
        }));
        RenderDrawers();
        var cut = Render<RemoveMealButton>();

        cut.Find(TestId("remove-meal-button")).Click();

        DrawerHost.WaitForAssertion(() =>
            Assert.Equal("Also remove 1 item from the grocery list", DrawerHost.Find(TestId("remove-meal-grocery-label")).TextContent));
    }

    [Fact]
    public void Without_a_meal_it_shows_an_error_and_sends_nothing()
    {
        var cut = Render<RemoveMealButton>();

        cut.Find(TestId("remove-meal-button")).Click();

        Assert.Contains("Meal or meal date cannot be empty", SnackbarMessages);
        Assert.Empty(SentRequests);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void A_failed_grocery_load_shows_the_error_and_opens_nothing(bool itemsFail)
    {
        PlanTacos();
        if (itemsFail) SetupSend<GetAllGroceryItems.Query, Result<List<GroceryItem>>>(Result.Fail<List<GroceryItem>>("Could not load"));
        else SetupSend<GetGroceryItemSources.Query, Result<List<GroceryItemSource>>>(Result.Fail<List<GroceryItemSource>>("Could not load"));
        RenderDrawers();
        var cut = Render<RemoveMealButton>();

        cut.Find(TestId("remove-meal-button")).Click();

        cut.WaitForAssertion(() => Assert.Contains("Could not load", SnackbarMessages));
        Assert.False(Drawer.IsDrawerOpen);
        Assert.Empty(Sent<RemoveMealFromMealPlan.Command>());
        Assert.False(cut.Find(TestId("remove-meal-button")).HasAttribute("disabled"));
    }

    private static GroceryItemSource Live(int itemId, int plannedMealId) =>
        new() { GroceryItemId = itemId, PlannedMealId = plannedMealId, IngredientId = 1, PlannedDayMealId = 7, IngredientMealId = 7 };
}
