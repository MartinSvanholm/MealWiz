using Bunit;
using FluentResults;
using MealWiz.Shared.Features.GroceryList.AddMealPlanIngredientsToGroceryList;
using MealWiz.Shared.Features.GroceryList.GetAllGroceryItems;
using MealWiz.Shared.Features.GroceryList.Models;
using MealWiz.Shared.Features.GroceryList.UpdateGroceryItem;
using MealWiz.Shared.Features.MealPlans.GetMealPlanByDate;
using MealWiz.Shared.Features.MealPlans.Models;
using MealWiz.Shared.Tests.TestInfrastructure;
using Xunit;

namespace MealWiz.Shared.Tests.Features.GroceryList.ComponentTests;

/// <summary>Merging the meal plan's ingredients into the grocery list (the button and the drawer it opens).</summary>
public class AddPlanIngredientsToGroceryListTests : ComponentTestBase
{
    private readonly GroceryItem _milkInList = TestData.GroceryItem("Milk", "1 l", id: 1);

    public AddPlanIngredientsToGroceryListTests()
    {
        GroceryList.GroceryList.Items.Add(_milkInList);
        MealPlan.MealPlan = TestData.MealPlanWith(DateTime.Today, TestData.Meal(
            "Pancakes",
            ingredients: [TestData.Ingredient("Flour", "200g"), TestData.Ingredient("milk", "3 dl")]));

        SetupSend<AddMealPlanIngredientsToGroceryList.Command, Result<List<int>>>(Result.Ok(new List<int> { 10 }));
        SetupSend<UpdateGroceryItem.Command, Result<int>>(Result.Ok(1));
        SetupSend<GetAllGroceryItems.Query, Result<List<GroceryItem>>>(Result.Ok(new List<GroceryItem>()));
    }

    [Fact]
    public void Lists_the_plans_ingredients_and_flags_ones_already_on_the_list()
    {
        var cut = Render<AddPlanIngredientsToGroceryListDrawer>();

        var items = cut.FindAll(TestId("plan-ingredient-item"));
        Assert.Equal(2, items.Count);
        Assert.Contains("Flour", items[0].TextContent);
        Assert.Equal("200g", items[0].QuerySelector("input")!.GetAttribute("value"));
        Assert.Contains("Plan: 3 dl", items[1].TextContent);
        Assert.Contains("Already in list: 1 l", items[1].TextContent);
    }

    [Fact]
    public void Confirm_requires_an_amount_for_each_item()
    {
        var cut = Render<AddPlanIngredientsToGroceryListDrawer>();

        cut.Find(TestId("plan-ingredients-confirm")).Click();

        Assert.Empty(SentRequests);
    }

    [Fact]
    public void Confirm_adds_new_items_updates_duplicates_and_closes_the_drawer()
    {
        Drawer.OpenDrawer("Add items to grocery list", typeof(AddPlanIngredientsToGroceryListDrawer));
        var cut = Render<AddPlanIngredientsToGroceryListDrawer>();

        cut.FindAll(InputOf("plan-ingredient-amount-input"))[1].Change("1.3 l");
        cut.Find(TestId("plan-ingredients-confirm")).Click();

        cut.WaitForAssertion(() => Assert.False(Drawer.IsDrawerOpen));
        var updated = Assert.Single(Sent<UpdateGroceryItem.Command>()).GroceryItem;
        Assert.Same(_milkInList, updated);
        Assert.Equal("1.3 l", updated.Amount);
        var added = Assert.Single(Assert.Single(Sent<AddMealPlanIngredientsToGroceryList.Command>()).GroceryItems);
        Assert.Equal(("Flour", "200g"), (added.Name, added.Amount));
        Assert.Single(Sent<GetAllGroceryItems.Query>());
        Assert.Contains("Ingredients added to list", SnackbarMessages);
    }

    [Fact]
    public void Button_loads_the_meal_plan_when_needed_and_opens_the_drawer()
    {
        MealPlan.MealPlan = null;
        SetupSend<GetMealPlanByDate.Query, Result<MealPlan?>>(Result.Ok<MealPlan?>(new MealPlan(DateTime.Today)));
        var cut = RenderWithPopovers<AddIngredientsToGroceryListButton>();

        cut.Find(TestId("add-plan-ingredients-button")).Click();

        cut.WaitForAssertion(() => Assert.True(Drawer.IsDrawerOpen));
        Assert.Single(Sent<GetMealPlanByDate.Query>());
        Assert.Equal(typeof(AddPlanIngredientsToGroceryListDrawer), Drawer.StateParameters.Content);
    }

    [Fact]
    public void Button_reuses_a_loaded_meal_plan()
    {
        var cut = RenderWithPopovers<AddIngredientsToGroceryListButton>();

        cut.Find(TestId("add-plan-ingredients-button")).Click();

        cut.WaitForAssertion(() => Assert.True(Drawer.IsDrawerOpen));
        Assert.Empty(Sent<GetMealPlanByDate.Query>());
    }
}
