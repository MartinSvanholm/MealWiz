using System.Globalization;
using Bunit;
using FluentResults;
using MealWiz.Shared.Features.MealPlans;
using MealWiz.Shared.Features.MealPlans.GetMealPlanByDate;
using MealWiz.Shared.Features.MealPlans.Models;
using MealWiz.Shared.Features.MealPlans.RemoveMealFromMealPlan;
using MealWiz.Shared.Helpers;
using MealWiz.Shared.Tests.TestInfrastructure;
using Xunit;

namespace MealWiz.Shared.Tests.Features.MealPlans.ComponentTests;

public class MealPlanPageTests : ComponentTestBase
{
    private static readonly DateTime ThisMonday = DateTime.Today.StartOfWeek().Date;

    [Fact]
    public void A_day_without_a_meal_offers_meals_to_pick_from()
    {
        var cut = Render<MealPlanPage>();

        Assert.Contains($"Pick a meal for {DateTime.Today.DayOfWeek.ToString().ToLower()}", cut.Find(TestId("meal-selection-heading")).TextContent);
        Assert.Empty(cut.FindAll(TestId("remove-meal-button")));
    }

    [Fact]
    public void A_day_with_a_meal_shows_it_and_can_remove_it()
    {
        SetupSend<GetMealPlanByDate.Query, Result<MealPlan?>>(Result.Ok<MealPlan?>(TestData.MealPlanWith(DateTime.Today, TestData.Meal("Tacos", id: 8))));
        SetupSend<RemoveMealFromMealPlan.Command, Result>(Result.Ok());
        var cut = Render<MealPlanPage>();

        Assert.Contains("Tacos", cut.Markup);
        Assert.Empty(cut.FindAll(TestId("meal-selection-heading")));

        cut.Find(TestId("remove-meal-button")).Click();

        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll(TestId("meal-selection-heading"))));
        Assert.Equal(8, Assert.Single(Sent<RemoveMealFromMealPlan.Command>()).MealId);
    }

    [Fact]
    public void Shows_the_week_number_and_a_way_back_from_other_weeks()
    {
        var cut = Render<MealPlanPage>();
        Assert.Equal($"Week {ISOWeek.GetWeekOfYear(ThisMonday)}", cut.Find(TestId("meal-plan-week-title")).TextContent);
        Assert.Empty(cut.FindAll(TestId("meal-plan-current-week")));

        cut.Find(TestId("week-next")).Click();

        cut.WaitForAssertion(() => Assert.Contains($"Week {ISOWeek.GetWeekOfYear(ThisMonday.AddDays(7))}", cut.Find(TestId("meal-plan-current-week")).TextContent));

        cut.Find(TestId("meal-plan-current-week")).Click();

        cut.WaitForAssertion(() => Assert.Empty(cut.FindAll(TestId("meal-plan-current-week"))));
        Assert.Equal(ThisMonday, MealPlan.SelectedWeekStart);
    }

    [Fact]
    public void Account_button_opens_the_account_page()
    {
        var cut = Render<MealPlanPage>();

        cut.Find(TestId("account-button")).Click();

        Assert.Equal("account", CurrentPath);
    }
}
