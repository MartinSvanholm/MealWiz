using Bunit;
using FluentResults;
using MealWiz.Shared.Features.GroceryList.DeleteGroceryItems;
using MealWiz.Shared.Features.GroceryList.DeleteGroceryItemSources;
using MealWiz.Shared.Features.GroceryList.GetAllGroceryItems;
using MealWiz.Shared.Features.GroceryList.GetGroceryItemSources;
using MealWiz.Shared.Features.GroceryList.Models;
using MealWiz.Shared.Features.GroceryList.UnlinkGroceryItems;
using MealWiz.Shared.Features.Meals.DeleteMeal;
using MealWiz.Shared.Features.Meals.GetAllMeals;
using MealWiz.Shared.Tests.TestInfrastructure;
using Xunit;

namespace MealWiz.Shared.Tests.Features.Meals.ComponentTests;

public class DeleteMealButtonTests : ComponentTestBase
{
    [Fact]
    public void Clicking_delete_asks_for_confirmation_first()
    {
        RenderDrawers();
        var cut = Render<DeleteMealButton>(p => p.Add(b => b.Meal, TestData.Meal("Pasta", id: 5)));

        cut.Find(TestId("delete-meal-button")).Click();

        Assert.True(Drawer.IsConfirmDrawerOpen);
        Assert.Contains("removed from any meal plans", ConfirmDrawerHost.Find(TestId("confirm-drawer-description")).TextContent);
        Assert.Empty(Sent<DeleteMeal.Command>());
    }

    [Fact]
    public void Confirming_deletes_the_meal_and_returns_to_the_meals_page()
    {
        SetupSend<DeleteMeal.Command, Result>(Result.Ok());
        RenderDrawers();
        var cut = Render<DeleteMealButton>(p => p.Add(b => b.Meal, TestData.Meal("Pasta", id: 5)));

        cut.Find(TestId("delete-meal-button")).Click();
        Confirm();

        cut.WaitForAssertion(() => Assert.False(Drawer.IsConfirmDrawerOpen));
        Assert.Equal(5, Assert.Single(Sent<DeleteMeal.Command>()).MealId);
        Assert.Equal("meals", CurrentPath);
        Assert.Contains("Meal deleted", SnackbarMessages);
    }

    // MEA-33: a failed delete used to reload the list, close the drawer and report success next to the error.
    [Fact]
    public void A_failed_delete_shows_the_error_and_keeps_the_confirmation_open()
    {
        SetupSend<DeleteMeal.Command, Result>(Result.Fail("The meal is in a meal plan"));
        RenderDrawers();
        var cut = Render<DeleteMealButton>(p => p.Add(b => b.Meal, TestData.Meal("Pasta", id: 5)));

        cut.Find(TestId("delete-meal-button")).Click();
        Confirm();

        cut.WaitForAssertion(() => Assert.Contains("The meal is in a meal plan", SnackbarMessages));
        Assert.True(Drawer.IsConfirmDrawerOpen);
        Assert.Empty(Sent<GetAllMeals.Query>());
        Assert.DoesNotContain("Meal deleted", SnackbarMessages);
    }

    // MEA-32: a dismissed confirmation used to leave its handler subscribed, so the next confirm deleted it too.
    [Fact]
    public void Dismissing_the_confirmation_then_confirming_another_deletes_only_the_confirmed_meal()
    {
        SetupSend<DeleteMeal.Command, Result>(Result.Ok());
        RenderDrawers();
        var pasta = Render<DeleteMealButton>(p => p.Add(b => b.Meal, TestData.Meal("Pasta", id: 5)));
        var soup = Render<DeleteMealButton>(p => p.Add(b => b.Meal, TestData.Meal("Soup", id: 6)));

        pasta.Find(TestId("delete-meal-button")).Click();
        ConfirmDrawerHost.InvokeAsync(() => Drawer.IsConfirmDrawerOpen = false);
        soup.Find(TestId("delete-meal-button")).Click();
        Confirm();

        soup.WaitForAssertion(() => Assert.False(Drawer.IsConfirmDrawerOpen));
        Assert.Equal(6, Assert.Single(Sent<DeleteMeal.Command>()).MealId);
    }

    [Fact]
    public void Clicking_delete_twice_then_confirming_deletes_the_meal_once()
    {
        SetupSend<DeleteMeal.Command, Result>(Result.Ok());
        RenderDrawers();
        var cut = Render<DeleteMealButton>(p => p.Add(b => b.Meal, TestData.Meal("Pasta", id: 5)));

        cut.Find(TestId("delete-meal-button")).Click();
        cut.Find(TestId("delete-meal-button")).Click();
        Confirm();

        cut.WaitForAssertion(() => Assert.False(Drawer.IsConfirmDrawerOpen));
        Assert.Single(Sent<DeleteMeal.Command>());
    }

    // ---- A meal with grocery items still to buy is reviewed first (MEA-93) ----

    private static GroceryItemSource LinkedTo(int mealId, int id, int itemId) =>
        new() { Id = id, GroceryItemId = itemId, PlannedMealId = 500 + id, IngredientId = 100 + id, IngredientMealId = mealId, PlannedDayMealId = mealId };

    /// <summary>Tortillas (1) and Salsa (2) are still to buy for meal 5; Beef (3) is in the basket.</summary>
    private IRenderedComponent<DeleteMealButton> RenderWithLinkedItems(bool withToBuy = true)
    {
        var items = new List<GroceryItem> { TestData.LinkedGroceryItem("Beef", 3, isPicked: true) };
        var sources = new List<GroceryItemSource> { LinkedTo(5, 13, 3) };
        if (withToBuy)
        {
            items.AddRange([TestData.LinkedGroceryItem("Tortillas", 1), TestData.LinkedGroceryItem("Salsa", 2)]);
            sources.AddRange([LinkedTo(5, 11, 1), LinkedTo(5, 12, 2)]);
        }

        SetupSend<GetAllGroceryItems.Query, Result<List<GroceryItem>>>(Result.Ok(items));
        SetupSend<GetGroceryItemSources.Query, Result<List<GroceryItemSource>>>(Result.Ok(sources));
        SetupSend<DeleteGroceryItems.Command, Result>(Result.Ok());
        SetupSend<UnlinkGroceryItems.Command, Result>(Result.Ok());
        SetupSend<DeleteGroceryItemSources.Command, Result<List<GroceryItem>>>(Result.Ok(new List<GroceryItem>()));
        SetupSend<DeleteMeal.Command, Result>(Result.Ok());
        RenderDrawers();
        return Render<DeleteMealButton>(p => p.Add(b => b.Meal, TestData.Meal("Tacos", id: 5)));
    }

    private void ConfirmDelete(IRenderedComponent<DeleteMealButton> cut)
    {
        cut.Find(TestId("delete-meal-button")).Click();
        Confirm();
    }

    [Fact]
    public void Confirming_with_items_to_buy_opens_the_review_instead_of_deleting()
    {
        var cut = RenderWithLinkedItems();

        ConfirmDelete(cut);

        DrawerHost.WaitForAssertion(() => Assert.Equal(2, DrawerHost.FindAll(TestId("review-item")).Count));
        Assert.True(Drawer.IsDrawerOpen);
        Assert.False(Drawer.IsConfirmDrawerOpen);
        Assert.Equal("Remove from grocery list?", Drawer.StateParameters.Title);
        Assert.Equal("Tacos", DrawerHost.Find(TestId("review-group-heading")).TextContent.Trim());
        Assert.Empty(Sent<DeleteMeal.Command>());
    }

    [Fact]
    public void Remove_in_the_review_deletes_the_meal_and_returns_to_meals()
    {
        var cut = RenderWithLinkedItems();
        ConfirmDelete(cut);
        DrawerHost.WaitForAssertion(() => Assert.Equal(2, DrawerHost.FindAll(TestId("review-item")).Count));

        DrawerHost.Find(TestId("review-items-confirm")).Click();

        cut.WaitForAssertion(() => Assert.Equal("meals", CurrentPath));
        Assert.Equal(
            [typeof(GetAllGroceryItems.Query), typeof(GetGroceryItemSources.Query), typeof(DeleteGroceryItems.Command), typeof(UnlinkGroceryItems.Command), typeof(DeleteMeal.Command)],
            SentRequests.Select(r => r.GetType()));
        Assert.Equal([1, 2], Assert.Single(Sent<DeleteGroceryItems.Command>()).GroceryItemIds);
        Assert.Equal([3], Assert.Single(Sent<UnlinkGroceryItems.Command>()).GroceryItemIds);
        Assert.Equal(5, Assert.Single(Sent<DeleteMeal.Command>()).MealId);
        Assert.Contains("Meal deleted", SnackbarMessages);
    }

    [Fact]
    public void Closing_the_review_deletes_nothing()
    {
        var cut = RenderWithLinkedItems();
        ConfirmDelete(cut);
        DrawerHost.WaitForAssertion(() => Assert.Equal(2, DrawerHost.FindAll(TestId("review-item")).Count));

        DrawerHost.Find(TestId("drawer-close")).Click();

        Assert.False(Drawer.IsDrawerOpen);
        Assert.Equal([typeof(GetAllGroceryItems.Query), typeof(GetGroceryItemSources.Query)], SentRequests.Select(r => r.GetType()));
    }

    [Fact]
    public void Confirming_without_items_to_buy_unlinks_the_basket_and_deletes()
    {
        var cut = RenderWithLinkedItems(withToBuy: false);

        ConfirmDelete(cut);

        cut.WaitForAssertion(() => Assert.Equal("meals", CurrentPath));
        Assert.Equal([3], Assert.Single(Sent<UnlinkGroceryItems.Command>()).GroceryItemIds);
        Assert.Empty(Sent<DeleteGroceryItems.Command>());
        Assert.Single(Sent<DeleteMeal.Command>());
        Assert.False(Drawer.IsDrawerOpen);
    }

    [Fact]
    public void A_failed_load_keeps_the_confirmation_open()
    {
        RenderWithLinkedItems();
        SetupSend<GetGroceryItemSources.Query, Result<List<GroceryItemSource>>>(Result.Fail<List<GroceryItemSource>>("Sources broke"));
        var cut = Render<DeleteMealButton>(p => p.Add(b => b.Meal, TestData.Meal("Tacos", id: 5)));

        ConfirmDelete(cut);

        cut.WaitForAssertion(() => Assert.Contains("Sources broke", SnackbarMessages));
        Assert.True(Drawer.IsConfirmDrawerOpen);
        Assert.Empty(Sent<DeleteMeal.Command>());
    }
}
