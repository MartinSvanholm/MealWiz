using Bunit;
using FluentResults;
using MealWiz.Shared.Features.MealPlans.GetMealPlanByDate;
using MealWiz.Shared.Features.MealPlans.Models;
using MealWiz.Shared.Features.Meals;
using MealWiz.Shared.Features.Meals.GetAllMeals;
using MealWiz.Shared.Features.Meals.Models;
using MealWiz.Shared.Helpers;
using MealWiz.Shared.Tests.TestInfrastructure;
using Xunit;

namespace MealWiz.Shared.Tests.Features.Meals.ComponentTests;

/// <summary>The Meals page: header with the meal count, search, filter chips, the meal grid and the import entry.</summary>
public class MealsPageTests : ComponentTestBase
{
    private readonly Meal _pasta = TestData.Meal("Pasta", id: 1, ingredients: [TestData.Ingredient("Penne", "500 g", id: 1), TestData.Ingredient("Salt", id: 2)]);
    private readonly Meal _soup = TestData.Meal("Soup", id: 3);
    private readonly Meal _tacos = TestData.Meal("Tacos", id: 4);
    private readonly Meal _leftovers = TestData.Meal("Leftovers", id: 2, type: MealType.Leftover);

    public MealsPageTests()
    {
        SetupSend<GetAllMeals.Query, Result<List<Meal>>>(Result.Ok(new List<Meal> { _leftovers, _pasta, _soup, _tacos }));
    }

    private static IEnumerable<string> Names(IRenderedComponent<MealsPage> cut) =>
        cut.FindAll(TestId("meal-list-item")).Select(c => c.QuerySelector(TestId("meal-card-name"))!.TextContent.Trim());

    [Fact]
    public void Shows_only_regular_meals_in_the_grid()
    {
        var cut = RenderWithPopovers<MealsPage>();

        Assert.Single(Sent<GetAllMeals.Query>());
        Assert.Equal(["Pasta", "Soup", "Tacos"], Names(cut));
    }

    [Fact]
    public void Header_counts_the_regular_meals()
    {
        var cut = RenderWithPopovers<MealsPage>();

        Assert.Equal("Your meals", cut.Find(TestId("screen-header-title")).TextContent.Trim());
        Assert.Equal("3 meals", cut.Find(TestId("screen-header-label")).TextContent.Trim());
        Assert.NotNull(cut.Find(TestId("add-meal-menu")));
    }

    [Fact]
    public void Cards_alternate_tile_colours_and_show_the_ingredient_count()
    {
        var cut = RenderWithPopovers<MealsPage>();

        var colours = cut.FindAll(TestId("meal-tile")).Select(t => t.GetAttribute("data-color"));
        Assert.Equal(["orange", "green", "orange"], colours);
        var cards = cut.FindAll(TestId("meal-list-item"));
        Assert.Equal("2 ingredients", cards[0].QuerySelector(TestId("meal-card-meta"))!.TextContent.Trim());
        Assert.Equal("0 ingredients", cards[1].QuerySelector(TestId("meal-card-meta"))!.TextContent.Trim());
    }

    [Fact]
    public void Card_shows_the_day_it_is_planned_this_week()
    {
        var monday = DateTime.Now.StartOfWeek().Date;
        SetupSend<GetMealPlanByDate.Query, Result<MealPlan?>>(Result.Ok<MealPlan?>(TestData.MealPlanWith(monday.AddDays(2), TestData.Meal("Soup", id: 3))));

        var cut = RenderWithPopovers<MealsPage>();

        Assert.Equal(monday, Assert.Single(Sent<GetMealPlanByDate.Query>()).Date);
        var cards = cut.FindAll(TestId("meal-list-item"));
        Assert.Empty(cards[0].QuerySelectorAll(TestId("meal-tile-day")));
        Assert.Equal("WED", cards[1].QuerySelector(TestId("meal-tile-day"))!.TextContent.Trim());
    }

    [Fact]
    public void A_failed_plan_load_leaves_the_day_tags_out_quietly()
    {
        SetupSend<GetMealPlanByDate.Query, Result<MealPlan?>>(Result.Fail("offline"));

        var cut = RenderWithPopovers<MealsPage>();

        Assert.Equal(3, cut.FindAll(TestId("meal-list-item")).Count);
        Assert.Empty(cut.FindAll(TestId("meal-tile-day")));
        Assert.Empty(SnackbarMessages);
    }

    [Fact]
    public void Search_filters_meals_by_name()
    {
        var cut = RenderWithPopovers<MealsPage>();

        cut.Find(InputOf("meal-list-search")).Input("so");

        Assert.Equal(["Soup"], Names(cut));
    }

    [Fact]
    public void Filter_chips_are_shown_with_All_selected()
    {
        var cut = RenderWithPopovers<MealsPage>();

        Assert.Equal("true", cut.Find(TestId("meal-filter-all")).GetAttribute("aria-pressed"));
        Assert.Equal("false", cut.Find(TestId("meal-filter-this-week")).GetAttribute("aria-pressed"));
        Assert.Equal("false", cut.Find(TestId("meal-filter-no-recipe")).GetAttribute("aria-pressed"));
    }

    [Fact]
    public void Clicking_a_meal_opens_it_for_editing()
    {
        var cut = RenderWithPopovers<MealsPage>();

        cut.FindAll(TestId("meal-list-item"))[0].Click();

        Assert.Same(_pasta, Meals.MealToEdit);
        Assert.Equal("meals/edit", CurrentPath);
    }

    [Fact]
    public void Import_entry_opens_the_import_page()
    {
        var cut = RenderWithPopovers<MealsPage>();

        cut.Find(TestId("meals-import-link")).Click();

        Assert.Equal("meals/import", CurrentPath);
    }

    [Fact]
    public void Shows_an_empty_state_without_regular_meals()
    {
        SetupSend<GetAllMeals.Query, Result<List<Meal>>>(Result.Ok(new List<Meal> { _leftovers }));

        var cut = RenderWithPopovers<MealsPage>();

        Assert.NotNull(cut.Find(TestId("meal-list-empty")));
        Assert.Empty(cut.FindAll(TestId("meal-list-item")));
        Assert.Equal("0 meals", cut.Find(TestId("screen-header-label")).TextContent.Trim());
    }
}
