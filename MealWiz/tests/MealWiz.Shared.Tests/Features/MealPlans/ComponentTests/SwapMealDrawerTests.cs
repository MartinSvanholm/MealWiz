using Bunit;
using FluentResults;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using MealWiz.Shared.Features.GroceryList.DeleteGroceryItems;
using MealWiz.Shared.Features.Meals;
using MealWiz.Shared.Features.MealPlans;
using MealWiz.Shared.Features.MealPlans.SwapMealInMealPlan;
using MealWiz.Shared.Features.Meals.Models;
using MealWiz.Shared.Tests.TestInfrastructure;
using Xunit;

namespace MealWiz.Shared.Tests.Features.MealPlans.ComponentTests;

/// <summary>The drawer that asks before a swap when the old meal has grocery items only it needs (MEA-98).</summary>
public class SwapMealDrawerTests : ComponentTestBase
{
    private readonly DateTime _date;
    private readonly Meal _pasta = TestData.Meal("Pasta", id: 1);
    private readonly Meal _tacos = TestData.Meal("Tacos", id: 2);

    public SwapMealDrawerTests()
    {
        _date = MealPlan.SelectedDate.Date;
        MealPlan.MealPlan = TestData.MealPlanWith(_date, _pasta, plannedMealId: 501);
        MealPlan.StartSwap();
        SetupSend<SwapMealInMealPlan.Command, Result>(Result.Ok().WithSuccess("Meal swapped"));
        SetupSend<DeleteGroceryItems.Command, Result>(Result.Ok());
        RenderDrawers();
    }

    private void Open(params int[] groceryItemIds) => Open(_tacos, groceryItemIds);

    private void Open(Meal newMeal, params int[] groceryItemIds)
    {
        var localizer = Services.GetRequiredService<IStringLocalizer<MealPlansResource>>();
        var mealsLocalizer = Services.GetRequiredService<IStringLocalizer<MealsResource>>();
        DrawerHost.InvokeAsync(() => SwapMealDrawer.Open(Drawer, localizer, mealsLocalizer, _pasta, newMeal, _date, groceryItemIds));
        DrawerHost.WaitForAssertion(() => Assert.NotEmpty(DrawerHost.FindAll(TestId("swap-meal-confirm"))));
    }

    [Theory]
    [InlineData(new[] { 11 }, "Also remove 1 item from the grocery list")]
    [InlineData(new[] { 11, 12 }, "Also remove 2 items from the grocery list")]
    public void Asks_to_swap_with_the_count_and_the_switch_on(int[] ids, string label)
    {
        Open(ids);

        Assert.Equal("Swap Pasta for Tacos?", DrawerHost.Find(TestId("drawer-title")).TextContent);
        Assert.Equal($"Tacos goes on {_date:dddd}'s plan.", DrawerHost.Find(TestId("swap-meal-description")).TextContent);
        Assert.Equal(label, DrawerHost.Find(TestId("swap-meal-grocery-label")).TextContent);
        Assert.True(DrawerHost.Find(InputOf("swap-meal-grocery-switch")).HasAttribute("checked"));
        Assert.Equal("Cancel", DrawerHost.Find(TestId("swap-meal-cancel")).TextContent.Trim());
        Assert.Equal("Swap", DrawerHost.Find(TestId("swap-meal-confirm")).TextContent.Trim());
        Assert.Empty(SentRequests);
    }

    [Fact]
    public void A_system_meal_is_named_by_its_type()
    {
        Open(new Meal { Id = 3, Name = "stored-leftovers-name", Type = MealType.Leftover }, 11);

        Assert.Equal("Swap Pasta for Leftovers?", DrawerHost.Find(TestId("drawer-title")).TextContent);
        Assert.DoesNotContain("stored-leftovers-name", DrawerHost.Markup);
    }

    [Fact]
    public void Swap_swaps_the_meal_then_deletes_the_items()
    {
        Open(11, 12);

        DrawerHost.Find(TestId("swap-meal-confirm")).Click();

        DrawerHost.WaitForAssertion(() => Assert.False(Drawer.IsDrawerOpen));
        Assert.Equal(
            [typeof(SwapMealInMealPlan.Command), typeof(DeleteGroceryItems.Command)],
            SentRequests.Select(r => r.GetType()));
        Assert.Equal(new SwapMealInMealPlan.Command(1, _date, 1, 2), Assert.Single(Sent<SwapMealInMealPlan.Command>()));
        Assert.Equal([11, 12], Assert.Single(Sent<DeleteGroceryItems.Command>()).GroceryItemIds);
        Assert.Same(_tacos, MealPlan.GetMealFromSelectedDate());
        Assert.False(MealPlan.IsSwappingMeal);
        Assert.Contains("Meal swapped", SnackbarMessages);
        Assert.Contains("2 items removed from the grocery list", SnackbarMessages);
    }

    [Fact]
    public void With_the_switch_off_it_swaps_and_deletes_no_items()
    {
        Open(11, 12);
        DrawerHost.Find(InputOf("swap-meal-grocery-switch")).Change(false);

        DrawerHost.Find(TestId("swap-meal-confirm")).Click();

        DrawerHost.WaitForAssertion(() => Assert.False(Drawer.IsDrawerOpen));
        Assert.Same(_tacos, MealPlan.GetMealFromSelectedDate());
        Assert.Empty(Sent<DeleteGroceryItems.Command>());
        Assert.DoesNotContain(SnackbarMessages, m => m.Contains("grocery list"));
    }

    // MEA-33: a failed request must not look like a success.
    [Fact]
    public void A_failed_swap_deletes_nothing_and_keeps_the_drawer_open()
    {
        SetupSend<SwapMealInMealPlan.Command, Result>(Result.Fail("Meal not found in plan"));
        Open(11, 12);

        DrawerHost.Find(TestId("swap-meal-confirm")).Click();

        DrawerHost.WaitForAssertion(() => Assert.Contains("Meal not found in plan", SnackbarMessages));
        Assert.Same(_pasta, MealPlan.GetMealFromSelectedDate());
        Assert.True(MealPlan.IsSwappingMeal);
        Assert.Empty(Sent<DeleteGroceryItems.Command>());
        Assert.True(Drawer.IsDrawerOpen);
        Assert.False(DrawerHost.Find(TestId("swap-meal-confirm")).HasAttribute("disabled"));
    }

    [Fact]
    public void A_failed_item_delete_still_swaps_and_says_the_items_stayed()
    {
        SetupSend<DeleteGroceryItems.Command, Result>(Result.Fail("boom"));
        Open(11, 12);

        DrawerHost.Find(TestId("swap-meal-confirm")).Click();

        DrawerHost.WaitForAssertion(() => Assert.False(Drawer.IsDrawerOpen));
        Assert.Same(_tacos, MealPlan.GetMealFromSelectedDate());
        Assert.Contains("The grocery items couldn't be removed. They're still on your grocery list.", SnackbarMessages);
        Assert.DoesNotContain("boom", SnackbarMessages);
    }

    [Fact]
    public void Cancel_closes_and_stays_in_swap_mode()
    {
        Open(11);

        DrawerHost.Find(TestId("swap-meal-cancel")).Click();

        Assert.False(Drawer.IsDrawerOpen);
        Assert.Same(_pasta, MealPlan.GetMealFromSelectedDate());
        Assert.True(MealPlan.IsSwappingMeal);
        Assert.Empty(SentRequests);
    }
}

/// <summary>The drawer's texts come from resources (UI-03): every localizer echoes its key as <c>[Key]</c>.</summary>
public class SwapMealDrawerTextTests() : ComponentTestBase(echoResourceKeys: true)
{
    [Fact]
    public void The_drawer_texts_come_from_the_resource()
    {
        var pasta = TestData.Meal("Pasta", id: 1);
        MealPlan.MealPlan = TestData.MealPlanWith(MealPlan.SelectedDate, pasta, plannedMealId: 501);
        RenderDrawers();
        var localizer = Services.GetRequiredService<IStringLocalizer<MealPlansResource>>();
        var mealsLocalizer = Services.GetRequiredService<IStringLocalizer<MealsResource>>();

        DrawerHost.InvokeAsync(() => SwapMealDrawer.Open(Drawer, localizer, mealsLocalizer, pasta, TestData.Meal("Tacos", id: 2), MealPlan.SelectedDate.Date, [11]));

        Assert.Equal("[SwapMeal_Title]", Drawer.StateParameters.Title);
        DrawerHost.WaitForAssertion(() =>
        {
            Assert.Equal("[SwapMeal_Description]", DrawerHost.Find(TestId("swap-meal-description")).TextContent);
            Assert.Equal("[Common_Cancel]", DrawerHost.Find(TestId("swap-meal-cancel")).TextContent.Trim());
            Assert.Equal("[SwapMeal_Confirm]", DrawerHost.Find(TestId("swap-meal-confirm")).TextContent.Trim());
        });
    }
}
