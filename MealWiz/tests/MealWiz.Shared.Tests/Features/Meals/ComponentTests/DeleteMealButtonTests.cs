using Bunit;
using FluentResults;
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
        Assert.Contains("Are you sure you want to delete this meal?", ConfirmDrawerHost.Find(TestId("confirm-drawer-description")).TextContent);
        Assert.Empty(Sent<DeleteMeal.Command>());
    }

    [Fact]
    public void Confirming_deletes_the_meal_and_reloads_the_list()
    {
        SetupSend<DeleteMeal.Command, Result>(Result.Ok());
        RenderDrawers();
        var cut = Render<DeleteMealButton>(p => p.Add(b => b.Meal, TestData.Meal("Pasta", id: 5)));

        cut.Find(TestId("delete-meal-button")).Click();
        Confirm();

        cut.WaitForAssertion(() => Assert.False(Drawer.IsConfirmDrawerOpen));
        Assert.Equal(5, Assert.Single(Sent<DeleteMeal.Command>()).MealId);
        Assert.Single(Sent<GetAllMeals.Query>());
        Assert.Contains("Meal deleted", SnackbarMessages);
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
}
