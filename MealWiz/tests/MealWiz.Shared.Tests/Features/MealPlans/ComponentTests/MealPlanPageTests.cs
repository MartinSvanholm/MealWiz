using System.Globalization;
using Bunit;
using FluentResults;
using MealWiz.Shared.Features.MealPlans;
using MealWiz.Shared.Features.MealPlans.GetMealPlanByDate;
using MealWiz.Shared.Features.MealPlans.Models;
using MealWiz.Shared.Features.MealPlans.RemoveMealFromMealPlan;
using MealWiz.Shared.Helpers;
using MealWiz.Shared.Tests.TestInfrastructure;
using Moq;
using Xunit;

namespace MealWiz.Shared.Tests.Features.MealPlans.ComponentTests;

public class MealPlanPageTests : ComponentTestBase
{
    private static readonly DateTime ThisMonday = DateTime.Today.StartOfWeek().Date;

    [Fact]
    public void A_day_without_a_meal_offers_meals_to_pick_from()
    {
        var cut = Render<MealPlanPage>();

        Assert.Equal($"Pick a meal for {DateTime.Today.DayOfWeek}", cut.Find(TestId("meal-selection-heading")).TextContent.Trim());
        Assert.Empty(cut.FindAll(TestId("remove-meal-button")));
    }

    [Fact]
    public void A_day_with_a_meal_shows_it_and_can_remove_it()
    {
        SetupSend<GetMealPlanByDate.Query, Result<MealPlan?>>(Result.Ok<MealPlan?>(TestData.MealPlanWith(DateTime.Today, TestData.Meal("Tacos", id: 8))));
        SetupSend<RemoveMealFromMealPlan.Command, Result>(Result.Ok());
        RenderDrawers();
        var cut = Render<MealPlanPage>();

        Assert.Contains("Tacos", cut.Markup);
        Assert.Empty(cut.FindAll(TestId("meal-selection-heading")));

        cut.Find(TestId("remove-meal-button")).Click();
        DrawerHost.WaitForAssertion(() => Assert.NotEmpty(DrawerHost.FindAll(TestId("remove-meal-confirm"))));
        DrawerHost.Find(TestId("remove-meal-confirm")).Click();

        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll(TestId("meal-selection-heading"))));
        Assert.Equal(new RemoveMealFromMealPlan.Command(8, 1, DateTime.Today), Assert.Single(Sent<RemoveMealFromMealPlan.Command>()));
    }

    [Fact]
    public void Swap_meal_shows_the_pick_list_in_place_of_the_card_and_cancel_brings_it_back()
    {
        SetupSend<GetMealPlanByDate.Query, Result<MealPlan?>>(Result.Ok<MealPlan?>(TestData.MealPlanWith(DateTime.Today, TestData.Meal("Tacos", id: 8))));
        var cut = Render<MealPlanPage>();

        cut.Find(TestId("swap-meal-button")).Click();

        cut.WaitForAssertion(() => Assert.Equal($"Swap the meal for {DateTime.Today.DayOfWeek}", cut.Find(TestId("meal-selection-heading")).TextContent.Trim()));
        Assert.Empty(cut.FindAll(TestId("meal-display-name")));

        cut.Find(TestId("meal-selection-cancel")).Click();

        cut.WaitForAssertion(() => Assert.Equal("Tacos", cut.Find(TestId("meal-display-name")).TextContent.Trim()));
    }

    [Fact]
    public void The_header_shows_the_week_number_and_month_and_this_week()
    {
        var cut = Render<MealPlanPage>();

        var month = ThisMonday.ToString("MMMM", CultureInfo.InvariantCulture);
        Assert.Equal($"Week {ISOWeek.GetWeekOfYear(ThisMonday)} · {month}", cut.Find(TestId("screen-header-label")).TextContent.Trim());
        Assert.Equal("This week", cut.Find(TestId("meal-plan-week-title")).TextContent.Trim());
        Assert.Empty(cut.FindAll(TestId("meal-plan-current-week")));
    }

    [Fact]
    public void Other_weeks_are_named_relative_to_this_week_with_a_way_back()
    {
        var cut = Render<MealPlanPage>();

        cut.Find(TestId("week-next")).Click();
        cut.WaitForAssertion(() => Assert.Equal("Next week", cut.Find(TestId("meal-plan-current-week")).TextContent.Trim()));
        Assert.Contains($"Week {ISOWeek.GetWeekOfYear(ThisMonday.AddDays(7))}", cut.Find(TestId("screen-header-label")).TextContent);

        cut.Find(TestId("week-next")).Click();
        cut.WaitForAssertion(() => Assert.Equal("In 2 weeks", cut.Find(TestId("meal-plan-current-week")).TextContent.Trim()));

        cut.Find(TestId("meal-plan-current-week")).Click();
        cut.WaitForAssertion(() => Assert.Empty(cut.FindAll(TestId("meal-plan-current-week"))));
        Assert.Equal(ThisMonday, MealPlan.SelectedWeekStart);

        cut.Find(TestId("week-prev")).Click();
        cut.WaitForAssertion(() => Assert.Equal("Last week", cut.Find(TestId("meal-plan-current-week")).TextContent.Trim()));
        cut.Find(TestId("week-prev")).Click();
        cut.WaitForAssertion(() => Assert.Equal("2 weeks ago", cut.Find(TestId("meal-plan-current-week")).TextContent.Trim()));
    }

    [Fact]
    public void Next_and_previous_move_a_week_and_load_its_plan()
    {
        var cut = Render<MealPlanPage>();

        cut.Find(TestId("week-next")).Click();
        cut.WaitForAssertion(() => Assert.Equal(ThisMonday.AddDays(7), MealPlan.SelectedWeekStart));
        Assert.Equal(ThisMonday.AddDays(7), MealPlan.SelectedDate.Date);

        cut.Find(TestId("week-prev")).Click();
        cut.WaitForAssertion(() => Assert.Equal(ThisMonday, MealPlan.SelectedWeekStart));
        cut.Find(TestId("week-prev")).Click();
        cut.WaitForAssertion(() => Assert.Equal(ThisMonday.AddDays(-7), MealPlan.SelectedWeekStart));

        Assert.Equal(
            [ThisMonday, ThisMonday.AddDays(7), ThisMonday, ThisMonday.AddDays(-7)],
            Sent<GetMealPlanByDate.Query>().Select(q => q.Date));
        LocalStorage.Verify(s => s.SetItemAsync("mealplan_selected_week", ThisMonday.AddDays(-7), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public void Plan_the_week_opens_the_plan_week_page_on_the_viewed_week()
    {
        var cut = Render<MealPlanPage>();
        cut.Find(TestId("week-next")).Click();
        cut.WaitForAssertion(() => Assert.False(cut.Find(TestId("plan-week-button")).HasAttribute("disabled")));

        cut.Find(TestId("plan-week-button")).Click();

        Assert.EndsWith($"plan-week?week={ThisMonday.AddDays(7).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)}", Navigation.Uri);
    }

    [Fact]
    public void The_avatar_shows_the_signed_in_users_initial()
    {
        Auth.SetAuthorized("martin@example.com");

        var cut = Render<MealPlanPage>();

        Assert.Equal("M", cut.Find(TestId("account-button")).TextContent.Trim());
    }

    [Fact]
    public void Account_button_opens_the_account_page()
    {
        var cut = Render<MealPlanPage>();

        cut.Find(TestId("account-button")).Click();

        Assert.Equal("account", CurrentPath);
    }
}
