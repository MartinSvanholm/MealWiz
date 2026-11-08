using Bunit;
using FluentResults;
using MealWiz.Shared.Features.Meals.GetAllMeals;
using MealWiz.Shared.Features.Meals.Models;
using MealWiz.Shared.Features.MealPlans.SelectMealForDate;
using MealWiz.Shared.Tests.TestInfrastructure;
using Xunit;

namespace MealWiz.Shared.Tests.Features.MealPlans.ComponentTests;

/// <summary>The parameter-driven pick list shared by the Meal plan and the Plan week pick drawer.</summary>
public class MealPickListTests : ComponentTestBase
{
    private readonly Meal _pasta = TestData.Meal("Pasta", id: 1);
    private readonly Meal _tacos = TestData.Meal("Tacos", id: 2);
    private readonly Meal _leftovers = TestData.Meal("Leftovers", id: 3, type: MealType.Leftover);
    private readonly Meal _eatOut = TestData.Meal("Eat out", id: 4, type: MealType.EatOut);

    public MealPickListTests()
    {
        SetupSend<GetAllMeals.Query, Result<List<Meal>>>(Result.Ok(new List<Meal> { _pasta, _leftovers, _tacos, _eatOut }));
    }

    private List<string> Names(IRenderedComponent<MealPickList> cut) =>
        cut.FindAll(TestId("meal-list-item")).Select(r => r.QuerySelector(TestId("meal-selection-name"))!.TextContent.Trim()).ToList();

    [Fact]
    public void Shows_the_heading_and_hint_it_is_given()
    {
        var cut = Render<MealPickList>(p => p
            .Add(x => x.Heading, "Pick a meal for Monday")
            .Add(x => x.Hint, "A hint.")
            .Add(x => x.OnPick, _ => { }));

        Assert.Equal("Pick a meal for Monday", cut.Find(TestId("meal-selection-heading")).TextContent);
        Assert.Equal("A hint.", cut.Find(TestId("meal-selection-hint")).TextContent);
    }

    [Fact]
    public void Shows_no_heading_or_hint_without_them()
    {
        var cut = Render<MealPickList>(p => p.Add(x => x.OnPick, _ => { }));

        Assert.Empty(cut.FindAll(TestId("meal-selection-heading")));
        Assert.Empty(cut.FindAll(TestId("meal-selection-hint")));
    }

    [Fact]
    public void Leaves_out_the_excluded_meal()
    {
        var cut = Render<MealPickList>(p => p.Add(x => x.ExcludedMealId, 1).Add(x => x.OnPick, _ => { }));

        Assert.Equal(3, Names(cut).Count);
        Assert.DoesNotContain("Pasta", Names(cut));
    }

    [Fact]
    public void Picking_a_row_raises_OnPick_with_that_meal_and_sends_nothing_else()
    {
        Meal? picked = null;
        var cut = Render<MealPickList>(p => p.Add(x => x.OnPick, m => picked = m));
        var sentBefore = Sent<object>().Count();

        cut.FindAll(TestId("meal-list-item")).Single(r => r.TextContent.Contains("Tacos")).QuerySelector(TestId("meal-selection-pick"))!.Click();

        Assert.Same(_tacos, picked);
        Assert.Equal(sentBefore, Sent<object>().Count());
    }

    [Fact]
    public void Eat_out_and_leftovers_can_be_picked()
    {
        var picked = new List<Meal>();
        var cut = Render<MealPickList>(p => p.Add(x => x.OnPick, m => picked.Add(m)));

        // System meals show their display name by type ("Eat Out"), whatever is stored ("Eat out" in the test data).
        foreach (var name in new[] { "Eat Out", "Leftovers" })
        {
            cut.FindAll(TestId("meal-list-item")).Single(r => r.TextContent.Contains(name)).QuerySelector(TestId("meal-selection-pick"))!.Click();
        }

        Assert.Equal([_eatOut, _leftovers], picked);
    }

    [Fact]
    public void Edit_buttons_can_be_hidden()
    {
        var cut = Render<MealPickList>(p => p.Add(x => x.ShowEdit, false).Add(x => x.OnPick, _ => { }));

        Assert.Empty(cut.FindAll(TestId("meal-selection-edit")));
        Assert.Equal(4, cut.FindAll(TestId("meal-list-item")).Count);
    }

    [Fact]
    public void Cancel_shows_only_with_ShowCancel_and_raises_OnCancel()
    {
        var cancelled = 0;
        var without = Render<MealPickList>(p => p.Add(x => x.Heading, "H").Add(x => x.OnPick, _ => { }));
        Assert.Empty(without.FindAll(TestId("meal-selection-cancel")));

        var cut = Render<MealPickList>(p => p
            .Add(x => x.Heading, "H")
            .Add(x => x.ShowCancel, true)
            .Add(x => x.OnCancel, () => cancelled++)
            .Add(x => x.OnPick, _ => { }));
        cut.Find(TestId("meal-selection-cancel")).Click();

        Assert.Equal(1, cancelled);
    }
}
