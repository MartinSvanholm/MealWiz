using Bunit;
using FluentResults;
using MealWiz.Shared.Features.GroceryList.AddMealPlanIngredientsToGroceryList;
using MealWiz.Shared.Features.GroceryList.AddPlanIngredientsToGroceryList;
using MealWiz.Shared.Features.GroceryList.GetAllGroceryItems;
using MealWiz.Shared.Features.GroceryList.GetGroceryItemSources;
using MealWiz.Shared.Features.GroceryList.Models;
using MealWiz.Shared.Features.Ingredients.Models;
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
        Assert.Empty(cut.FindAll(TestId("add-all-to-list")));
    }

    [Fact]
    public void A_system_meal_shows_its_display_name_not_its_stored_name()
    {
        Plan(TestData.Meal("stored-leftovers-name", type: MealType.Leftover));

        var cut = Render<MealDisplayComponent>();

        Assert.Equal("Leftovers", cut.Find(TestId("meal-display-name")).TextContent.Trim());
        Assert.DoesNotContain("stored-leftovers-name", cut.Markup);
    }

    [Fact]
    public void Add_all_to_list_is_enabled_for_a_meal_with_grocery_ingredients_and_there_is_no_eaten_out_button()
    {
        Plan(TestData.Meal("Lasagne", ingredients: [TestData.Ingredient("Pasta sheets", "1 pk")]));

        var cut = Render<MealDisplayComponent>();

        Assert.Empty(cut.FindAll(TestId("mark-eaten-out-button")));
        Assert.False(cut.Find(TestId("add-all-to-list")).HasAttribute("disabled"));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Add_all_to_list_is_disabled_when_no_ingredient_goes_on_the_grocery_list(bool hasHiddenIngredient)
    {
        Plan(TestData.Meal("Toast", ingredients: hasHiddenIngredient
            ? [TestData.Ingredient("Salt", "1 tsp", showOnGroceryList: false)]
            : []));

        var cut = Render<MealDisplayComponent>();

        Assert.True(cut.Find(TestId("add-all-to-list")).HasAttribute("disabled"));
    }

    private IRenderedComponent<MealDisplayComponent> RenderWithDrawers()
    {
        SetupSend<AddPlanIngredientsToGroceryList.Command, Result<List<GroceryItem>>>(Result.Ok(new List<GroceryItem>()));
        RenderDrawers();
        return RenderWithPopovers<MealDisplayComponent>();
    }

    /// <summary>Pasta (Penne, Basil) on the selected day as planned day 42, Soup (Carrots) on the next as 43.</summary>
    private DateTime PlanPastaAndSoup(params Ingredient[] pastaExtra)
    {
        var date = MealPlan.SelectedDate.Date;
        var pasta = TestData.Meal("Pasta", id: 1, ingredients:
            [TestData.Ingredient("Penne", "500 g", id: 7), TestData.Ingredient("Basil", "1 bunch", id: 8), .. pastaExtra]);
        var soup = TestData.Meal("Soup", id: 2, ingredients: [TestData.Ingredient("Carrots", "6", id: 9)]);
        MealPlan.MealPlan = TestData.MealPlanWithDays((date, pasta, 42), (date.AddDays(1), soup, 43));
        return date;
    }

    [Fact]
    public void Add_all_to_list_opens_the_merge_drawer_for_the_selected_day_only()
    {
        PlanPastaAndSoup();
        var cut = RenderWithDrawers();

        cut.Find(TestId("add-all-to-list")).Click();

        Assert.True(Drawer.IsDrawerOpen);
        Assert.Equal(typeof(AddPlanIngredientsToGroceryListDrawer), Drawer.StateParameters.Content);
        Assert.Equal("Add Pasta to list", Drawer.StateParameters.Title);
        DrawerHost.WaitForAssertion(() =>
        {
            var rows = DrawerHost.FindAll(TestId("plan-ingredient-item"));
            Assert.Equal(2, rows.Count);
            Assert.Contains("Penne", rows[0].TextContent);
            Assert.Contains("Basil", rows[1].TextContent);
            Assert.DoesNotContain("Carrots", DrawerHost.Markup);
        });
    }

    [Fact]
    public void Add_all_to_list_leaves_out_ingredients_not_shown_on_the_grocery_list()
    {
        PlanPastaAndSoup(TestData.Ingredient("Salt", "1 tsp", id: 10, showOnGroceryList: false));
        var cut = RenderWithDrawers();

        cut.Find(TestId("add-all-to-list")).Click();

        DrawerHost.WaitForAssertion(() =>
        {
            Assert.Equal(2, DrawerHost.FindAll(TestId("plan-ingredient-item")).Count);
            Assert.DoesNotContain("Salt", DrawerHost.Markup);
        });
    }

    [Fact]
    public void Add_all_to_list_says_everything_is_on_the_list_when_the_days_ingredients_are_linked()
    {
        PlanPastaAndSoup();
        SetupSend<GetAllGroceryItems.Query, Result<List<GroceryItem>>>(Result.Ok(new List<GroceryItem>
        {
            TestData.LinkedGroceryItem("Penne", 3, "500 g"), TestData.LinkedGroceryItem("Basil", 4, "1 bunch"),
        }));
        SetupSend<GetGroceryItemSources.Query, Result<List<GroceryItemSource>>>(Result.Ok(new List<GroceryItemSource>
        {
            TestData.Source(3, 42, 7, "500 g", id: 1), TestData.Source(4, 42, 8, "1 bunch", id: 2),
        }));
        var cut = RenderWithDrawers();

        cut.Find(TestId("add-all-to-list")).Click();

        DrawerHost.WaitForAssertion(() =>
        {
            Assert.Contains("Everything is already on your list", DrawerHost.Find(TestId("plan-ingredients-all-on-list")).TextContent);
            Assert.Empty(DrawerHost.FindAll(TestId("plan-ingredient-item")));
        });
    }

    [Fact]
    public void Adding_links_the_items_to_the_selected_planned_day()
    {
        PlanPastaAndSoup();
        var cut = RenderWithDrawers();
        cut.Find(TestId("add-all-to-list")).Click();
        DrawerHost.WaitForAssertion(() => Assert.Equal(2, DrawerHost.FindAll(TestId("plan-ingredient-item")).Count));

        DrawerHost.Find(TestId("plan-ingredients-confirm")).Click();

        DrawerHost.WaitForAssertion(() => Assert.False(Drawer.IsDrawerOpen));
        var items = Assert.Single(Sent<AddPlanIngredientsToGroceryList.Command>()).Items;
        Assert.Equal(2, items.Count);
        var penne = items.Single(i => i.Ingredient.Id == 7);
        Assert.Equal((42, "Pasta", (int?)null, "500 g"), (penne.PlannedMealId, penne.MealName, penne.ExistingGroceryItemId, penne.FinalAmount));
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

/// <summary>The day card's texts come from resources (UI-03): every localizer echoes its key as <c>[Key]</c>.</summary>
public class MealDisplayComponentTextTests() : ComponentTestBase(echoResourceKeys: true)
{
    [Fact]
    public void The_card_and_its_sections_read_from_resources()
    {
        MealPlan.MealPlan = TestData.MealPlanWith(MealPlan.SelectedDate, TestData.Meal("Lasagne", ingredients: [TestData.Ingredient("Pasta sheets", "1 pk")]));

        var cut = Render<MealDisplayComponent>();

        Assert.Equal("[DayCard_HeroAria]", cut.Find(TestId("meal-display-hero")).GetAttribute("aria-label"));
        Assert.Equal("[DayCard_DayDinner]", cut.Find(TestId("meal-display-day")).TextContent.Trim());
        Assert.Equal("[DayCard_Summary]", cut.Find(TestId("meal-display-summary")).TextContent.Trim());
        Assert.Equal("[DayCard_Swap]", cut.Find(TestId("swap-meal-button")).TextContent.Trim());
        Assert.Equal("[DayCard_AddAllToList]", cut.Find(TestId("add-all-to-list")).TextContent.Trim());
        Assert.Equal("[DayCard_AddRecipe]", cut.Find(TestId("meal-display-add-recipe")).TextContent.Trim());
    }

    [Fact]
    public void The_merge_drawer_title_reads_from_a_resource()
    {
        MealPlan.MealPlan = TestData.MealPlanWith(MealPlan.SelectedDate, TestData.Meal("Lasagne", ingredients: [TestData.Ingredient("Pasta sheets", "1 pk")]));
        var cut = Render<MealDisplayComponent>();

        cut.Find(TestId("add-all-to-list")).Click();

        Assert.Equal("[DayCard_AddAllTitle]", Drawer.StateParameters.Title);
    }
}
