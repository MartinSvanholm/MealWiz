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
    private readonly Meal _pasta = TestData.Meal("Pasta", id: 1, recipe: "Boil the penne.", ingredients: [TestData.Ingredient("Penne", "500 g", id: 1), TestData.Ingredient("Salt", id: 2)]);
    private readonly Meal _soup = TestData.Meal("Soup", id: 3);
    private readonly Meal _tacos = TestData.Meal("Tacos", id: 4, recipe: "  \n ");
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
    public void Search_matches_meals_by_ingredient_name_ignoring_case()
    {
        var cut = RenderWithPopovers<MealsPage>();

        cut.Find(InputOf("meal-list-search")).Input("PENN");

        Assert.Equal(["Pasta"], Names(cut));
    }

    [Fact]
    public void Search_by_ingredient_combines_with_a_filter()
    {
        var cut = RenderWithPopovers<MealsPage>();
        cut.Find(TestId("meal-filter-no-recipe")).Click();

        cut.Find(InputOf("meal-list-search")).Input("penn");

        Assert.Empty(cut.FindAll(TestId("meal-list-item")));
        Assert.Contains("penn", cut.Find(TestId("meal-list-no-match")).TextContent);
    }

    [Fact]
    public void Search_field_says_it_searches_ingredients_too()
    {
        var cut = RenderWithPopovers<MealsPage>();

        Assert.Equal("Search meals or ingredients", cut.Find(InputOf("meal-list-search")).GetAttribute("placeholder"));
    }

    [Fact]
    public void Search_without_matches_says_so()
    {
        var cut = RenderWithPopovers<MealsPage>();

        cut.Find(InputOf("meal-list-search")).Input("pizza");

        Assert.Contains("pizza", cut.Find(TestId("meal-list-no-match")).TextContent);
        Assert.Empty(cut.FindAll(TestId("meal-list-item")));
    }

    [Fact]
    public void Filter_chips_are_shown_with_All_selected()
    {
        var cut = RenderWithPopovers<MealsPage>();

        Assert.Equal("true", cut.Find(TestId("meal-filter-all")).GetAttribute("aria-pressed"));
        Assert.Equal("false", cut.Find(TestId("meal-filter-this-week")).GetAttribute("aria-pressed"));
        Assert.Equal("false", cut.Find(TestId("meal-filter-no-recipe")).GetAttribute("aria-pressed"));
    }

    private static IEnumerable<string?> Pressed(IRenderedComponent<MealsPage> cut) =>
        new[] { "meal-filter-all", "meal-filter-this-week", "meal-filter-no-recipe" }
            .Select(id => cut.Find(TestId(id)).GetAttribute("aria-pressed"));

    private void PlanSoupOnWednesday() =>
        SetupSend<GetMealPlanByDate.Query, Result<MealPlan?>>(
            Result.Ok<MealPlan?>(TestData.MealPlanWith(DateTime.Now.StartOfWeek().Date.AddDays(2), TestData.Meal("Soup", id: 3))));

    [Fact]
    public void This_week_shows_only_the_meals_planned_this_week()
    {
        PlanSoupOnWednesday();
        var cut = RenderWithPopovers<MealsPage>();

        cut.Find(TestId("meal-filter-this-week")).Click();

        Assert.Equal(["Soup"], Names(cut));
        Assert.Equal(["false", "true", "false"], Pressed(cut));
    }

    [Fact]
    public void No_recipe_shows_only_meals_without_recipe_text()
    {
        var cut = RenderWithPopovers<MealsPage>();

        cut.Find(TestId("meal-filter-no-recipe")).Click();

        Assert.Equal(["Soup", "Tacos"], Names(cut));
        Assert.Equal(["false", "false", "true"], Pressed(cut));
    }

    [Fact]
    public void All_shows_every_meal_again()
    {
        var cut = RenderWithPopovers<MealsPage>();
        cut.Find(TestId("meal-filter-no-recipe")).Click();

        cut.Find(TestId("meal-filter-all")).Click();

        Assert.Equal(["Pasta", "Soup", "Tacos"], Names(cut));
        Assert.Equal(["true", "false", "false"], Pressed(cut));
    }

    [Fact]
    public void Filter_and_search_combine()
    {
        var cut = RenderWithPopovers<MealsPage>();
        cut.Find(TestId("meal-filter-no-recipe")).Click();

        cut.Find(InputOf("meal-list-search")).Input("ta");

        Assert.Equal(["Tacos"], Names(cut));
    }

    [Fact]
    public void Filter_leaves_the_header_count_at_the_total()
    {
        var cut = RenderWithPopovers<MealsPage>();

        cut.Find(TestId("meal-filter-no-recipe")).Click();

        Assert.Equal("3 meals", cut.Find(TestId("screen-header-label")).TextContent.Trim());
    }

    [Fact]
    public void This_week_without_planned_meals_says_so()
    {
        var cut = RenderWithPopovers<MealsPage>();

        cut.Find(TestId("meal-filter-this-week")).Click();

        Assert.Empty(cut.FindAll(TestId("meal-list-item")));
        Assert.Equal("No meals planned this week.", cut.Find(TestId("meal-list-no-match")).TextContent.Trim());
    }

    [Fact]
    public void This_week_after_a_failed_plan_load_shows_no_meals()
    {
        SetupSend<GetMealPlanByDate.Query, Result<MealPlan?>>(Result.Fail("offline"));
        var cut = RenderWithPopovers<MealsPage>();

        cut.Find(TestId("meal-filter-this-week")).Click();

        Assert.Empty(cut.FindAll(TestId("meal-list-item")));
        Assert.Empty(SnackbarMessages);
    }

    [Fact]
    public void No_recipe_when_every_meal_has_one_says_so()
    {
        SetupSend<GetAllMeals.Query, Result<List<Meal>>>(Result.Ok(new List<Meal> { _pasta }));
        var cut = RenderWithPopovers<MealsPage>();

        cut.Find(TestId("meal-filter-no-recipe")).Click();

        Assert.Equal("Every meal has a recipe.", cut.Find(TestId("meal-list-no-match")).TextContent.Trim());
    }

    [Fact]
    public void Search_without_matches_under_a_filter_names_the_search()
    {
        var cut = RenderWithPopovers<MealsPage>();
        cut.Find(TestId("meal-filter-no-recipe")).Click();

        cut.Find(InputOf("meal-list-search")).Input("pizza");

        Assert.Contains("pizza", cut.Find(TestId("meal-list-no-match")).TextContent);
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
