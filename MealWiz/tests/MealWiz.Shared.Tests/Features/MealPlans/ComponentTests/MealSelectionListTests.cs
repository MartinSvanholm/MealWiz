using Bunit;
using FluentResults;
using MealWiz.Shared.Features.MealPlans.AddMealToMealPlan;
using MealWiz.Shared.Features.MealPlans.CreateMealPlan;
using MealWiz.Shared.Features.MealPlans.Models;
using MealWiz.Shared.Features.MealPlans.SelectMealForDate;
using MealWiz.Shared.Features.MealPlans.SwapMealInMealPlan;
using MealWiz.Shared.Features.Meals.GetAllMeals;
using MealWiz.Shared.Features.Meals.Models;
using MealWiz.Shared.Tests.TestInfrastructure;
using MediatR;
using Moq;
using Xunit;

namespace MealWiz.Shared.Tests.Features.MealPlans.ComponentTests;

/// <summary>Picking a meal for the selected date.</summary>
public class MealSelectionListTests : ComponentTestBase
{
    private readonly Meal _pasta = TestData.Meal("Pasta", id: 1);
    private readonly Meal _tacos = TestData.Meal("Tacos", id: 2);
    private readonly Meal _leftovers = TestData.Meal("Leftovers", id: 3, type: MealType.Leftover);
    private readonly Meal _eatOut = TestData.Meal("Eat out", id: 4, type: MealType.EatOut);

    public MealSelectionListTests()
    {
        SetupSend<GetAllMeals.Query, Result<List<Meal>>>(Result.Ok(new List<Meal> { _pasta, _leftovers, _tacos, _eatOut }));
        SetupSend<CreateMealPlan.Command, Result<MealPlan>>(Result.Ok(new MealPlan(DateTime.Today) { Id = 3 }));
        // The handler returns the id of the planned day it inserted.
        Mediator
            .Setup(m => m.Send(It.IsAny<AddMealToMealPlan.Command>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Ok(42));
    }

    /// <summary>Plans the meal on the selected date and starts swapping it, as the card's Swap meal button does.</summary>
    private void StartSwapping(Meal current)
    {
        MealPlan.MealPlan = TestData.MealPlanWith(MealPlan.SelectedDate, current);
        MealPlan.StartSwap();
    }

    private List<string> Names(IRenderedComponent<MealSelectionList> cut) =>
        cut.FindAll(TestId("meal-list-item")).Select(r => r.QuerySelector(TestId("meal-selection-name"))!.TextContent.Trim()).ToList();

    [Fact]
    public void Lists_system_meals_first_then_the_users_meals()
    {
        var cut = Render<MealSelectionList>();

        var names = Names(cut);
        Assert.Equal(["Eat out", "Leftovers"], names.Take(2));
        Assert.Equal(["Pasta", "Tacos"], names.Skip(2).Order());
    }

    [Fact]
    public void Only_the_users_own_meals_can_be_edited()
    {
        var cut = Render<MealSelectionList>();

        var rows = cut.FindAll(TestId("meal-list-item"));
        Assert.Equal(2, cut.FindAll(TestId("meal-selection-edit")).Count);
        Assert.Empty(rows[0].QuerySelectorAll(TestId("meal-selection-edit")));
    }

    [Fact]
    public void Picking_a_meal_creates_the_plan_and_adds_the_meal_to_the_selected_date()
    {
        var date = DateTime.Today.AddDays(1);
        MealPlan.SelectedDate = date;
        var cut = Render<MealSelectionList>();

        cut.FindAll(TestId("meal-list-item")).Single(r => r.TextContent.Contains("Tacos")).QuerySelector(TestId("meal-selection-pick"))!.Click();

        cut.WaitForAssertion(() => Assert.True(MealPlan.DateHasMeal(date)));
        Assert.Single(Sent<CreateMealPlan.Command>());
        var added = Assert.Single(Sent<AddMealToMealPlan.Command>()).Meal;
        Assert.Same(_tacos, added);
        Assert.Equal(date.Date, added.MealDate);
    }

    [Fact]
    public void Edit_opens_the_meal_in_the_edit_page()
    {
        var cut = Render<MealSelectionList>();

        var row = cut.FindAll(TestId("meal-list-item")).Single(r => r.TextContent.Contains("Pasta"));
        row.QuerySelector(TestId("meal-selection-edit"))!.Click();

        Assert.Same(_pasta, Meals.MealToEdit);
        Assert.Equal("meals/edit", CurrentPath);
        Assert.Empty(Sent<AddMealToMealPlan.Command>());
    }

    [Fact]
    public void Shuffle_reloads_the_meals()
    {
        var cut = Render<MealSelectionList>();

        cut.Find(TestId("shuffle-meals-button")).Click();

        cut.WaitForAssertion(() => Assert.Equal(2, Sent<GetAllMeals.Query>().Count));
        Assert.Equal(4, Names(cut).Count);
    }

    [Fact]
    public void Asks_for_a_meal_for_the_selected_day()
    {
        MealPlan.SelectedDate = new DateTime(2026, 11, 8);

        var cut = Render<MealSelectionList>();

        Assert.Equal("Pick a meal for Sunday", cut.Find(TestId("meal-selection-heading")).TextContent.Trim());
    }

    [Fact]
    public void System_meals_get_neutral_tiles_and_the_users_meals_alternate_orange_and_green()
    {
        var cut = Render<MealSelectionList>();

        var colors = cut.FindAll(TestId("meal-list-item"))
            .Select(r => r.QuerySelector(TestId("meal-tile"))!.GetAttribute("data-color"))
            .ToList();
        Assert.Equal(["neutral", "neutral", "orange", "green"], colors);
    }

    [Fact]
    public void Search_filters_the_list_by_name()
    {
        var cut = Render<MealSelectionList>();

        cut.Find(InputOf("meal-selection-search")).Input("tac");

        Assert.Equal(["Tacos"], Names(cut));
    }

    [Fact]
    public void A_search_with_no_matches_says_so_instead_of_showing_an_empty_list()
    {
        var cut = Render<MealSelectionList>();

        cut.Find(InputOf("meal-selection-search")).Input("zzzz");

        Assert.Contains("No meals match \"zzzz\"", cut.Find(TestId("meal-list-no-match")).TextContent);
        Assert.Empty(cut.FindAll(TestId("meal-list-item")));
    }

    [Fact]
    public void Swapping_asks_for_a_replacement_and_leaves_out_the_days_meal()
    {
        MealPlan.SelectedDate = new DateTime(2026, 11, 8);
        StartSwapping(_pasta);

        var cut = Render<MealSelectionList>();

        Assert.Equal("Swap the meal for Sunday", cut.Find(TestId("meal-selection-heading")).TextContent.Trim());
        Assert.Contains("Pasta", cut.Find(TestId("meal-selection-hint")).TextContent);
        Assert.Equal(["Eat out", "Leftovers", "Tacos"], Names(cut));
    }

    [Fact]
    public void Picking_while_swapping_replaces_the_days_meal_instead_of_adding_one()
    {
        var date = DateTime.Today.AddDays(1);
        MealPlan.SelectedDate = date;
        StartSwapping(_pasta);
        SetupSend<SwapMealInMealPlan.Command, Result>(Result.Ok());
        var cut = Render<MealSelectionList>();

        cut.FindAll(TestId("meal-list-item")).Single(r => r.TextContent.Contains("Leftovers")).QuerySelector(TestId("meal-selection-pick"))!.Click();

        cut.WaitForAssertion(() => Assert.False(MealPlan.IsSwappingMeal));
        Assert.Equal(new SwapMealInMealPlan.Command(1, date.Date, _pasta.Id, _leftovers.Id), Assert.Single(Sent<SwapMealInMealPlan.Command>()));
        Assert.Same(_leftovers, MealPlan.GetMealFromSelectedDate());
        Assert.Empty(Sent<AddMealToMealPlan.Command>());
    }

    [Fact]
    public void Cancel_ends_the_swap_and_is_only_shown_while_swapping()
    {
        var cut = Render<MealSelectionList>();
        Assert.Empty(cut.FindAll(TestId("meal-selection-cancel")));

        cut.InvokeAsync(() => StartSwapping(_pasta));
        cut.Find(TestId("meal-selection-cancel")).Click();

        Assert.False(MealPlan.IsSwappingMeal);
        Assert.Empty(Sent<SwapMealInMealPlan.Command>());
    }

    [Fact]
    public void Says_so_when_there_are_no_meals()
    {
        SetupSend<GetAllMeals.Query, Result<List<Meal>>>(Result.Ok(new List<Meal>()));

        var cut = Render<MealSelectionList>();

        Assert.NotNull(cut.Find(TestId("meal-list-empty")));
        Assert.Empty(cut.FindAll(TestId("meal-list-item")));
    }
}
