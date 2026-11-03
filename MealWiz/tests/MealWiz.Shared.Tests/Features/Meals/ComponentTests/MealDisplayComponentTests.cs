using Bunit;
using MealWiz.Shared.Features.Meals;
using MealWiz.Shared.Features.Meals.Models;
using MealWiz.Shared.Tests.TestInfrastructure;
using Xunit;

namespace MealWiz.Shared.Tests.Features.Meals.ComponentTests;

/// <summary>The selected day's meal on the Meal plan: the orange card, its ingredients and the recipe.</summary>
public class MealDisplayComponentTests : ComponentTestBase
{
    private void Plan(Meal meal) => MealPlan.MealPlan = TestData.MealPlanWith(MealPlan.SelectedDate, meal);

    [Fact]
    public void Shows_the_meal_with_its_ingredient_count_ingredients_and_recipe_steps()
    {
        Plan(TestData.Meal(
            "Pancakes",
            recipe: "Whisk everything\nFry in butter",
            ingredients: [TestData.Ingredient("Flour", "200g"), TestData.Ingredient("Milk", "3 dl")]));

        var cut = Render<MealDisplayComponent>();

        Assert.Equal("Pancakes", cut.Find(TestId("meal-display-name")).TextContent.Trim());
        Assert.Equal("2 ingredients · Recipe ready", cut.Find(TestId("meal-display-summary")).TextContent.Trim());
        var ingredients = cut.FindAll(TestId("meal-display-ingredient"));
        Assert.Equal(["Flour", "Milk"], ingredients.Select(i => i.QuerySelector(TestId("meal-display-ingredient-name"))!.TextContent.Trim()));
        Assert.Equal(["200g", "3 dl"], ingredients.Select(i => i.QuerySelector(TestId("meal-display-ingredient-amount"))!.TextContent.Trim()));
        var recipe = cut.Find(TestId("meal-display-recipe")).TextContent;
        Assert.Contains("Whisk everything", recipe);
        Assert.Contains("Fry in butter", recipe);
        Assert.Empty(cut.FindAll(TestId("meal-display-no-recipe")));
    }

    [Fact]
    public void A_meal_without_a_recipe_offers_to_add_one_in_edit_meal()
    {
        var meal = TestData.Meal("Lasagne", ingredients: [TestData.Ingredient("Pasta sheets", "1 pk")]);
        Plan(meal);

        var cut = Render<MealDisplayComponent>();

        Assert.Equal("1 ingredient · No recipe yet", cut.Find(TestId("meal-display-summary")).TextContent.Trim());
        Assert.Empty(cut.FindAll(TestId("meal-display-recipe")));

        cut.Find(TestId("meal-display-add-recipe")).Click();

        Assert.Same(meal, Meals.MealToEdit);
        Assert.Equal("meals/edit", CurrentPath);
    }

    [Fact]
    public void A_meal_without_ingredients_says_so()
    {
        Plan(TestData.Meal("Toast", recipe: "Toast it"));

        var cut = Render<MealDisplayComponent>();

        Assert.Equal("0 ingredients · Recipe ready", cut.Find(TestId("meal-display-summary")).TextContent.Trim());
        Assert.Empty(cut.FindAll(TestId("meal-display-ingredient")));
        Assert.NotNull(cut.Find(TestId("meal-display-no-ingredients")));
    }

    [Theory]
    [InlineData("Leftovers", MealType.Leftover)]
    [InlineData("Eat Out", MealType.EatOut)]
    public void A_system_meal_shows_only_the_card(string name, MealType type)
    {
        Plan(TestData.Meal(name, type: type));

        var cut = Render<MealDisplayComponent>();

        Assert.Equal(name, cut.Find(TestId("meal-display-name")).TextContent.Trim());
        Assert.Equal("Nothing to cook", cut.Find(TestId("meal-display-summary")).TextContent.Trim());
        Assert.Empty(cut.FindAll(TestId("meal-display-ingredients")));
        Assert.Empty(cut.FindAll(TestId("meal-display-no-recipe")));
        Assert.Empty(cut.FindAll(TestId("meal-display-recipe")));
    }

    [Fact]
    public void Add_all_to_list_is_shown_but_not_wired_yet_and_there_is_no_eaten_out_button()
    {
        Plan(TestData.Meal("Lasagne", ingredients: [TestData.Ingredient("Pasta sheets", "1 pk")]));

        var cut = Render<MealDisplayComponent>();

        Assert.Empty(cut.FindAll(TestId("mark-eaten-out-button")));
        Assert.True(cut.Find(TestId("add-all-to-list")).HasAttribute("disabled"));
    }

    [Theory]
    [InlineData(MealType.Regular)]
    [InlineData(MealType.EatOut)]
    public void Swap_meal_starts_picking_a_replacement(MealType type)
    {
        Plan(TestData.Meal("Lasagne", type: type));
        var cut = Render<MealDisplayComponent>();

        var swap = cut.Find(TestId("swap-meal-button"));
        Assert.False(swap.HasAttribute("disabled"));
        swap.Click();

        Assert.True(MealPlan.IsSwappingMeal);
    }

    [Fact]
    public void Shows_the_weekday_on_the_card_and_renders_the_hero_action()
    {
        Plan(TestData.Meal("Pancakes"));

        var cut = Render<MealDisplayComponent>(p => p.Add(c => c.HeroAction, "<button data-testid=\"hero-action\"></button>"));

        Assert.Equal($"{MealPlan.SelectedDate.DayOfWeek} · Dinner", cut.Find(TestId("meal-display-day")).TextContent.Trim());
        Assert.NotNull(cut.Find(TestId("hero-action")));
    }

    [Fact]
    public void Follows_the_meal_plan_when_the_selected_date_changes()
    {
        var today = MealPlan.SelectedDate.Date;
        var plan = TestData.MealPlanWith(today, TestData.Meal("Pancakes", id: 1));
        var tomorrowsMeal = TestData.Meal("Lasagne", id: 2);
        tomorrowsMeal.MealDate = today.AddDays(1);
        plan.MealOnDate[today.AddDays(1)] = tomorrowsMeal;
        MealPlan.MealPlan = plan;
        var cut = Render<MealDisplayComponent>();

        cut.InvokeAsync(() => MealPlan.SelectedDate = today.AddDays(1));

        Assert.Equal("Lasagne", cut.Find(TestId("meal-display-name")).TextContent.Trim());
    }
}
