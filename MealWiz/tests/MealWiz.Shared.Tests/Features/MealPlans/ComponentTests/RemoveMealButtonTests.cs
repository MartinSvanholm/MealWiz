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
        Assert.Equal(new RemoveMealFromMealPlan.Command(8, 1, MealPlan.SelectedDate.Date), Assert.Single(Sent<RemoveMealFromMealPlan.Command>()));
    }

    // MEA-33: the meal used to disappear from the date even when the request failed.
    [Fact]
    public void A_failed_remove_shows_the_error_and_keeps_the_meal_on_the_date()
    {
        MealPlan.MealPlan = TestData.MealPlanWith(MealPlan.SelectedDate, TestData.Meal("Tacos", id: 8));
        SetupSend<RemoveMealFromMealPlan.Command, Result>(Result.Fail("Could not remove the meal"));
        var cut = Render<RemoveMealButton>();

        cut.Find(TestId("remove-meal-button")).Click();

        cut.WaitForAssertion(() => Assert.Contains("Could not remove the meal", SnackbarMessages));
        Assert.Equal(8, MealPlan.GetMealFromSelectedDate()?.Id);
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
