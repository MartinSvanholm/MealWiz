using Bunit;
using FluentResults;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using MealWiz.Shared.Features.GroceryList.DeleteGroceryItems;
using MealWiz.Shared.Features.Meals;
using MealWiz.Shared.Features.MealPlans;
using MealWiz.Shared.Features.MealPlans.RemoveMealFromMealPlan;
using MealWiz.Shared.Features.Meals.Models;
using MealWiz.Shared.Tests.TestInfrastructure;
using Xunit;

namespace MealWiz.Shared.Tests.Features.MealPlans.ComponentTests;

/// <summary>The drawer that asks before a day's meal is removed, and its switch for the grocery items.</summary>
public class RemoveMealDrawerTests : ComponentTestBase
{
    private readonly DateTime _date;
    private readonly Meal _tacos = TestData.Meal("Tacos", id: 8);

    public RemoveMealDrawerTests()
    {
        _date = MealPlan.SelectedDate.Date;
        MealPlan.MealPlan = TestData.MealPlanWith(_date, _tacos);
        MealPlan.MealPlan.PlannedMealIdOnDate[_date] = 501;
        SetupSend<RemoveMealFromMealPlan.Command, Result>(Result.Ok().WithSuccess("Meal removed"));
        SetupSend<DeleteGroceryItems.Command, Result>(Result.Ok());
        RenderDrawers();
    }

    private void Open(params int[] groceryItemIds) => Open(_tacos, groceryItemIds);

    private void Open(Meal meal, params int[] groceryItemIds)
    {
        var localizer = Services.GetRequiredService<IStringLocalizer<MealPlansResource>>();
        var mealsLocalizer = Services.GetRequiredService<IStringLocalizer<MealsResource>>();
        DrawerHost.InvokeAsync(() => RemoveMealDrawer.Open(Drawer, localizer, mealsLocalizer, meal, _date, groceryItemIds));
        DrawerHost.WaitForAssertion(() => Assert.NotEmpty(DrawerHost.FindAll(TestId("remove-meal-confirm"))));
    }

    [Fact]
    public void Without_grocery_items_it_asks_without_a_switch()
    {
        Open();

        Assert.Equal("Remove Tacos?", DrawerHost.Find(TestId("drawer-title")).TextContent);
        Assert.Equal($"Tacos comes off {_date:dddd}'s plan.", DrawerHost.Find(TestId("remove-meal-description")).TextContent);
        Assert.Empty(DrawerHost.FindAll(TestId("remove-meal-grocery-row")));
        Assert.Equal("Cancel", DrawerHost.Find(TestId("remove-meal-cancel")).TextContent.Trim());
        Assert.Equal("Remove", DrawerHost.Find(TestId("remove-meal-confirm")).TextContent.Trim());
    }

    [Fact]
    public void A_system_meal_is_named_by_its_type()
    {
        Open(new Meal { Id = 2, Name = "stored-leftovers-name", Type = MealType.Leftover });

        Assert.Equal("Remove Leftovers?", DrawerHost.Find(TestId("drawer-title")).TextContent);
        Assert.StartsWith("Leftovers comes off", DrawerHost.Find(TestId("remove-meal-description")).TextContent);
        Assert.DoesNotContain("stored-leftovers-name", DrawerHost.Markup);
    }

    [Theory]
    [InlineData(1, "Also remove 1 item from the grocery list")]
    [InlineData(2, "Also remove 2 items from the grocery list")]
    public void The_switch_counts_the_items_and_starts_on(int count, string label)
    {
        Open([.. Enumerable.Range(11, count)]);

        Assert.Equal(label, DrawerHost.Find(TestId("remove-meal-grocery-label")).TextContent);
        Assert.True(DrawerHost.Find(InputOf("remove-meal-grocery-switch")).HasAttribute("checked"));
        Assert.Equal(label, DrawerHost.Find(InputOf("remove-meal-grocery-switch")).GetAttribute("aria-label"));
    }

    [Fact]
    public void Remove_removes_the_planned_day_and_closes()
    {
        Open();

        DrawerHost.Find(TestId("remove-meal-confirm")).Click();

        DrawerHost.WaitForAssertion(() => Assert.False(Drawer.IsDrawerOpen));
        Assert.False(MealPlan.SelectedDateHasMeal);
        Assert.Empty(MealPlan.MealPlan!.PlannedMealIdOnDate);
        Assert.Equal(new RemoveMealFromMealPlan.Command(8, 1, _date), Assert.Single(Sent<RemoveMealFromMealPlan.Command>()));
        Assert.Empty(Sent<DeleteGroceryItems.Command>());
        Assert.Contains("Meal removed", SnackbarMessages);
    }

    [Fact]
    public void With_the_switch_on_it_deletes_the_items_after_the_meal()
    {
        Open(11, 12);

        DrawerHost.Find(TestId("remove-meal-confirm")).Click();

        DrawerHost.WaitForAssertion(() => Assert.False(Drawer.IsDrawerOpen));
        Assert.Equal([11, 12], Assert.Single(Sent<DeleteGroceryItems.Command>()).GroceryItemIds);
        Assert.Equal(
            [typeof(RemoveMealFromMealPlan.Command), typeof(DeleteGroceryItems.Command)],
            SentRequests.Select(r => r.GetType()));
        Assert.Contains("2 items removed from the grocery list", SnackbarMessages);
    }

    [Fact]
    public void With_the_switch_off_it_deletes_no_items()
    {
        Open(11, 12);
        DrawerHost.Find(InputOf("remove-meal-grocery-switch")).Change(false);

        DrawerHost.Find(TestId("remove-meal-confirm")).Click();

        DrawerHost.WaitForAssertion(() => Assert.False(Drawer.IsDrawerOpen));
        Assert.False(MealPlan.SelectedDateHasMeal);
        Assert.Empty(Sent<DeleteGroceryItems.Command>());
        Assert.DoesNotContain(SnackbarMessages, m => m.Contains("grocery list"));
    }

    // MEA-33: the meal used to disappear from the date even when the request failed.
    [Fact]
    public void A_failed_remove_deletes_nothing_and_keeps_the_meal()
    {
        SetupSend<RemoveMealFromMealPlan.Command, Result>(Result.Fail("Could not remove the meal"));
        Open(11, 12);

        DrawerHost.Find(TestId("remove-meal-confirm")).Click();

        DrawerHost.WaitForAssertion(() => Assert.Contains("Could not remove the meal", SnackbarMessages));
        Assert.Equal(8, MealPlan.GetMealFromSelectedDate()?.Id);
        Assert.Empty(Sent<DeleteGroceryItems.Command>());
        Assert.True(Drawer.IsDrawerOpen);
        Assert.False(DrawerHost.Find(TestId("remove-meal-confirm")).HasAttribute("disabled"));
    }

    [Fact]
    public void A_failed_item_delete_still_removes_the_meal_and_says_the_items_stayed()
    {
        SetupSend<DeleteGroceryItems.Command, Result>(Result.Fail("boom"));
        Open(11, 12);

        DrawerHost.Find(TestId("remove-meal-confirm")).Click();

        DrawerHost.WaitForAssertion(() => Assert.False(Drawer.IsDrawerOpen));
        Assert.False(MealPlan.SelectedDateHasMeal);
        Assert.Contains("The grocery items couldn't be removed. They're still on your grocery list.", SnackbarMessages);
    }

    [Fact]
    public void Cancel_closes_without_removing()
    {
        Open(11);

        DrawerHost.Find(TestId("remove-meal-cancel")).Click();

        Assert.False(Drawer.IsDrawerOpen);
        Assert.True(MealPlan.SelectedDateHasMeal);
        Assert.Empty(SentRequests);
    }
}

/// <summary>The drawer's texts come from the resource, not from literals (UI-03).</summary>
public class RemoveMealDrawerResourceTests() : ComponentTestBase(echoResourceKeys: true)
{
    [Fact]
    public void The_drawer_texts_come_from_the_resource()
    {
        var date = MealPlan.SelectedDate.Date;
        var tacos = TestData.Meal("Tacos", id: 8);
        MealPlan.MealPlan = TestData.MealPlanWith(date, tacos);
        RenderDrawers();

        DrawerHost.InvokeAsync(() => RemoveMealDrawer.Open(Drawer, Services.GetRequiredService<IStringLocalizer<MealPlansResource>>(),
            Services.GetRequiredService<IStringLocalizer<MealsResource>>(), tacos, date, [11]));

        DrawerHost.WaitForAssertion(() => Assert.NotEmpty(DrawerHost.FindAll(TestId("remove-meal-confirm"))));
        Assert.Equal("[RemoveMeal_Title]", DrawerHost.Find(TestId("drawer-title")).TextContent);
        Assert.Equal("[RemoveMeal_Description]", DrawerHost.Find(TestId("remove-meal-description")).TextContent);
        Assert.Equal("[GroceryItems_AlsoRemove_One]", DrawerHost.Find(TestId("remove-meal-grocery-label")).TextContent);
        Assert.Equal("[Common_Cancel]", DrawerHost.Find(TestId("remove-meal-cancel")).TextContent.Trim());
        Assert.Equal("[RemoveMeal_Confirm]", DrawerHost.Find(TestId("remove-meal-confirm")).TextContent.Trim());
    }
}
