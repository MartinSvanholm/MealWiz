using Bunit;
using FluentResults;
using MealWiz.Shared.Features.MealPlans.RemoveMealFromMealPlan;
using MealWiz.Shared.Tests.TestInfrastructure;
using Xunit;

namespace MealWiz.Shared.Tests.Features.MealPlans.ComponentTests;

public class RemoveMealButtonTests : ComponentTestBase
{
    [Fact]
    public void Renders_a_remove_button()
    {
        var cut = Render<RemoveMealButton>();

        Assert.NotNull(cut.Find(TestId("remove-meal-button")));
    }

    [Fact]
    public void Removes_the_selected_dates_meal_from_the_plan()
    {
        MealPlan.MealPlan = TestData.MealPlanWith(MealPlan.SelectedDate, TestData.Meal("Tacos", id: 8));
        SetupSend<RemoveMealFromMealPlan.Command, Result>(Result.Ok());
        var cut = Render<RemoveMealButton>();

        cut.Find(TestId("remove-meal-button")).Click();

        cut.WaitForAssertion(() => Assert.False(MealPlan.SelectedDateHasMeal));
        Assert.Equal(8, Assert.Single(Sent<RemoveMealFromMealPlan.Command>()).MealId);
    }

    [Fact]
    public void Without_a_meal_it_shows_an_error_and_sends_nothing()
    {
        var cut = Render<RemoveMealButton>();

        cut.Find(TestId("remove-meal-button")).Click();

        Assert.Contains("Meal or meal date cannot be empty", SnackbarMessages);
        Assert.Empty(SentRequests);
    }
}
