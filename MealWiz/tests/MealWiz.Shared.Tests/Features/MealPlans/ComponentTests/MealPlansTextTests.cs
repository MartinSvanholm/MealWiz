using Bunit;
using FluentResults;
using MealWiz.Shared.Features.MealPlans;
using MealWiz.Shared.Features.MealPlans.PlanWeek;
using MealWiz.Shared.Features.MealPlans.SelectMealForDate;
using MealWiz.Shared.Features.Meals.GetAllMeals;
using MealWiz.Shared.Features.Meals.Models;
using MealWiz.Shared.Tests.TestInfrastructure;
using Xunit;

namespace MealWiz.Shared.Tests.Features.MealPlans.ComponentTests;

/// <summary>
/// The Meal plan's texts come from resources (UI-03): every localizer here echoes its key as <c>[Key]</c>, so a
/// hard-coded text would fail these tests. The English texts themselves are asserted by the other Meal plan tests.
/// </summary>
public class MealPlansTextTests() : ComponentTestBase(echoResourceKeys: true)
{
    [Fact]
    public void MealPlanPage_HeaderStripAndPickList()
    {
        var cut = Render<MealPlanPage>();

        Assert.Equal("[WeekTitle_This]", cut.Find(TestId("meal-plan-week-title")).TextContent.Trim());
        Assert.Equal("[Week_Previous]", cut.Find(TestId("week-prev")).GetAttribute("aria-label"));
        Assert.Equal("[Week_Next]", cut.Find(TestId("week-next")).GetAttribute("aria-label"));
        Assert.Equal("[MealPlan_PlanWeek]", cut.Find(TestId("plan-week-button")).GetAttribute("aria-label"));
        Assert.Equal("[MealPlan_Account]", cut.Find(TestId("account-button")).GetAttribute("aria-label"));
        Assert.Equal("[WeekStrip_Aria]", cut.Find("nav.mw-week-strip").GetAttribute("aria-label"));
        Assert.Equal("[PickMeal_Heading]", cut.Find(TestId("meal-selection-heading")).TextContent.Trim());
        Assert.Equal("[PickMeal_Hint]", cut.Find(TestId("meal-selection-hint")).TextContent.Trim());
    }

    [Fact]
    public void MealSelectionList_EmptyAndShuffle()
    {
        SetupSend<GetAllMeals.Query, Result<List<Meal>>>(Result.Ok(new List<Meal>()));

        var cut = Render<MealSelectionList>();

        Assert.Equal("[PickMeal_Empty]", cut.Find(TestId("meal-list-empty")).TextContent.Trim());
        Assert.Equal("[PickMeal_Shuffle]", cut.Find(TestId("shuffle-meals-button")).GetAttribute("aria-label"));
    }

    [Fact]
    public void MealSelectionList_SystemMealsUseTheirDisplayName()
    {
        SetupSend<GetAllMeals.Query, Result<List<Meal>>>(Result.Ok(new List<Meal>
        {
            TestData.Meal("stored-leftovers-name", id: 3, type: MealType.Leftover),
        }));

        var cut = Render<MealSelectionList>();

        Assert.Equal("[Meal_SystemLeftovers]", cut.Find(TestId("meal-selection-name")).TextContent.Trim());
    }

    [Fact]
    public void PlanWeekPage_SettingsAndButtons()
    {
        SetupSend<GetAllMeals.Query, Result<List<Meal>>>(Result.Ok(Enumerable.Range(1, 10).Select(i => TestData.Meal($"Meal {i}", i)).ToList()));
        Auth.SetAuthorized("martin@example.com");
        Navigation.NavigateTo("/plan-week");

        var cut = Render<PlanWeekPage>();

        Assert.Equal("[Week_Previous]", cut.Find(TestId("plan-week-prev")).GetAttribute("aria-label"));
        Assert.Equal("[PlanWeek_Weeks_Other]", cut.Find(TestId("plan-week-weeks-back-value")).TextContent.Trim());
        Assert.Equal("[PlanWeek_FewerWeeks]", cut.Find(TestId("plan-week-weeks-back-decrease")).GetAttribute("aria-label"));
        Assert.Equal("[PlanWeek_Generate]", cut.Find(TestId("plan-week-generate")).TextContent.Trim());
        Assert.Equal("[PlanWeek_Save]", cut.Find(TestId("plan-week-save")).TextContent.Trim());
        Assert.Equal("[PlanWeek_EmptyDay]", cut.Find(TestId("plan-week-day-meal")).TextContent.Trim());
    }
}
