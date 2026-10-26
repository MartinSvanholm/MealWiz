using Bunit;
using FluentResults;
using MealWiz.Shared.Features.Meals;
using MealWiz.Shared.Features.Meals.GetAllMeals;
using MealWiz.Shared.Features.Meals.Models;
using MealWiz.Shared.Tests.TestInfrastructure;
using Xunit;

namespace MealWiz.Shared.Tests.Features.Meals.ComponentTests;

public class MealsPageTests : ComponentTestBase
{
    private readonly Meal _pasta = TestData.Meal("Pasta", id: 1);
    private readonly Meal _leftovers = TestData.Meal("Leftovers", id: 2, type: MealType.Leftover);

    public MealsPageTests()
    {
        SetupSend<GetAllMeals.Query, Result<List<Meal>>>(Result.Ok(new List<Meal> { _leftovers, _pasta }));
    }

    [Fact]
    public void Loads_and_lists_the_users_meals()
    {
        var cut = RenderWithPopovers<MealsPage>();

        Assert.Single(Sent<GetAllMeals.Query>());
        var names = cut.FindAll(TestId("meal-list-item")).Select(r => r.TextContent.Trim());
        Assert.Equal(["Leftovers", "Pasta"], names);
    }

    [Fact]
    public void Only_regular_meals_can_be_deleted()
    {
        var cut = RenderWithPopovers<MealsPage>();

        var rows = cut.FindAll(TestId("meal-list-item"));
        Assert.Empty(rows[0].QuerySelectorAll(TestId("delete-meal-button")));
        Assert.Single(rows[1].QuerySelectorAll(TestId("delete-meal-button")));
    }

    [Fact]
    public void Clicking_a_regular_meal_opens_it_for_editing()
    {
        var cut = RenderWithPopovers<MealsPage>();

        cut.FindAll(TestId("meal-list-item"))[1].Click();

        Assert.Same(_pasta, Meals.MealToEdit);
        Assert.Equal("meals/edit", CurrentPath);
    }

    [Fact]
    public void Clicking_a_system_meal_does_nothing()
    {
        Navigation.NavigateTo("/meals");
        var cut = RenderWithPopovers<MealsPage>();

        cut.FindAll(TestId("meal-list-item"))[0].Click();

        Assert.NotSame(_leftovers, Meals.MealToEdit);
        Assert.Equal("meals", CurrentPath);
    }

    [Fact]
    public void Account_button_opens_the_account_page()
    {
        var cut = RenderWithPopovers<MealsPage>();

        cut.Find(TestId("account-button")).Click();

        Assert.Equal("account", CurrentPath);
    }
}
