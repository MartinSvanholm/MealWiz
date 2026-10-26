using Bunit;
using FluentResults;
using MealWiz.Shared.Features.MealPlans.AddMealToMealPlan;
using MealWiz.Shared.Features.MealPlans.CreateMealPlan;
using MealWiz.Shared.Features.MealPlans.Models;
using MealWiz.Shared.Features.MealPlans.SelectMealForDate;
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
        // The handler echoes the meal back with its date set.
        Mediator
            .Setup(m => m.Send(It.IsAny<AddMealToMealPlan.Command>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IRequest<Result<Meal>> request, CancellationToken _) => Result.Ok(((AddMealToMealPlan.Command)request).Meal));
    }

    private List<string> Names(IRenderedComponent<MealSelectionList> cut) =>
        cut.FindAll(TestId("meal-list-item")).Select(r => r.TextContent.Trim()).ToList();

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

        cut.FindAll(TestId("meal-list-item")).Single(r => r.TextContent.Contains("Tacos")).Click();

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
}
