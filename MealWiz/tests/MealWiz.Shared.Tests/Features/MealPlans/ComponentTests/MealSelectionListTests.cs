using Bunit;
using FluentResults;
using MealWiz.Shared.Features.GroceryList.DeleteGroceryItems;
using MealWiz.Shared.Features.GroceryList.DeleteGroceryItemSources;
using MealWiz.Shared.Features.GroceryList.GetAllGroceryItems;
using MealWiz.Shared.Features.GroceryList.GetGroceryItemSources;
using MealWiz.Shared.Features.GroceryList.Models;
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
        // System meals show their display name, whatever is stored ("Eat out" here).
        Assert.Equal(["Eat Out", "Leftovers"], names.Take(2));
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
    public void A_system_meal_is_listed_and_found_by_its_display_name_not_its_stored_name()
    {
        var leftovers = TestData.Meal("stored-leftovers-name", id: 5, type: MealType.Leftover);
        SetupSend<GetAllMeals.Query, Result<List<Meal>>>(Result.Ok(new List<Meal> { _pasta, leftovers }));
        var cut = Render<MealSelectionList>();

        Assert.Equal(["Leftovers", "Pasta"], Names(cut));
        Assert.DoesNotContain("stored-leftovers-name", cut.Markup);

        cut.Find(InputOf("meal-selection-search")).Input("leftov");
        Assert.Equal(["Leftovers"], Names(cut));

        cut.Find(InputOf("meal-selection-search")).Input("stored");
        Assert.Empty(cut.FindAll(TestId("meal-list-item")));
    }

    [Fact]
    public void Swapping_out_a_system_meal_names_it_by_its_display_name()
    {
        StartSwapping(TestData.Meal("stored-leftovers-name", id: 5, type: MealType.Leftover));

        var cut = Render<MealSelectionList>();

        Assert.Equal("Tap a meal to replace Leftovers.", cut.Find(TestId("meal-selection-hint")).TextContent.Trim());
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
        Assert.Equal(["Eat Out", "Leftovers", "Tacos"], Names(cut));
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

    // ---- A swap asks first when the old meal has grocery items only it needs (MEA-98) ----

    /// <summary>Spaghetti (11) is only Pasta's; shared Garlic (12) is also linked to another planned day.</summary>
    private void PlanPastaWithGroceryItems(bool withOwnItem = true)
    {
        StartSwapping(_pasta);
        MealPlan.MealPlan!.PlannedMealIdOnDate[MealPlan.SelectedDate.Date] = 501;
        var items = new List<GroceryItem> { TestData.LinkedGroceryItem("Garlic", 12, "3 cloves") };
        var sources = new List<GroceryItemSource>
        {
            new() { Id = 22, GroceryItemId = 12, PlannedMealId = 501, IngredientId = 2, PlannedDayMealId = 1, IngredientMealId = 1 },
            new() { Id = 23, GroceryItemId = 12, PlannedMealId = 502, IngredientId = 3, PlannedDayMealId = 2, IngredientMealId = 2 },
        };
        if (withOwnItem)
        {
            items.Add(TestData.LinkedGroceryItem("Spaghetti", 11, "400 g"));
            sources.Add(new() { Id = 21, GroceryItemId = 11, PlannedMealId = 501, IngredientId = 1, PlannedDayMealId = 1, IngredientMealId = 1 });
        }

        SetupSend<GetAllGroceryItems.Query, Result<List<GroceryItem>>>(Result.Ok(items));
        SetupSend<GetGroceryItemSources.Query, Result<List<GroceryItemSource>>>(Result.Ok(sources));
        SetupSend<SwapMealInMealPlan.Command, Result>(Result.Ok());
        SetupSend<DeleteGroceryItems.Command, Result>(Result.Ok());
        RenderDrawers();
    }

    private void PickTacos(IRenderedComponent<MealSelectionList> cut) =>
        cut.FindAll(TestId("meal-list-item")).Single(r => r.TextContent.Contains("Tacos")).QuerySelector(TestId("meal-selection-pick"))!.Click();

    [Fact]
    public void Swapping_a_meal_with_items_only_it_needs_opens_the_swap_drawer()
    {
        PlanPastaWithGroceryItems();
        var cut = Render<MealSelectionList>();

        PickTacos(cut);

        DrawerHost.WaitForAssertion(() => Assert.Equal("Swap Pasta for Tacos?", DrawerHost.Find(TestId("drawer-title")).TextContent));
        Assert.Equal("Also remove 1 item from the grocery list", DrawerHost.Find(TestId("swap-meal-grocery-label")).TextContent);
        Assert.Empty(Sent<SwapMealInMealPlan.Command>());
        Assert.True(MealPlan.IsSwappingMeal);
    }

    [Fact]
    public void Swapping_a_meal_without_such_items_swaps_straight_away()
    {
        PlanPastaWithGroceryItems(withOwnItem: false);
        var cut = Render<MealSelectionList>();

        PickTacos(cut);

        cut.WaitForAssertion(() => Assert.False(MealPlan.IsSwappingMeal));
        Assert.Single(Sent<SwapMealInMealPlan.Command>());
        Assert.False(Drawer.IsDrawerOpen);
        Assert.Empty(Sent<DeleteGroceryItems.Command>());
    }

    [Fact]
    public void Swapping_on_a_day_without_a_planned_day_id_does_not_load_the_grocery_list()
    {
        PlanPastaWithGroceryItems();
        MealPlan.MealPlan!.PlannedMealIdOnDate.Clear();
        var cut = Render<MealSelectionList>();

        PickTacos(cut);

        cut.WaitForAssertion(() => Assert.False(MealPlan.IsSwappingMeal));
        Assert.Empty(Sent<GetAllGroceryItems.Query>());
        Assert.Empty(Sent<GetGroceryItemSources.Query>());
        Assert.Single(Sent<SwapMealInMealPlan.Command>());
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void A_failed_grocery_load_shows_the_error_and_swaps_nothing(bool itemsFail)
    {
        PlanPastaWithGroceryItems();
        if (itemsFail) SetupSend<GetAllGroceryItems.Query, Result<List<GroceryItem>>>(Result.Fail<List<GroceryItem>>("Boom"));
        else SetupSend<GetGroceryItemSources.Query, Result<List<GroceryItemSource>>>(Result.Fail<List<GroceryItemSource>>("Boom"));
        var cut = Render<MealSelectionList>();

        PickTacos(cut);

        cut.WaitForAssertion(() => Assert.Contains("Boom", SnackbarMessages));
        Assert.Empty(Sent<SwapMealInMealPlan.Command>());
        Assert.False(Drawer.IsDrawerOpen);
        Assert.True(MealPlan.IsSwappingMeal);
        cut.WaitForAssertion(() => Assert.All(cut.FindAll(TestId("meal-selection-pick")), b => Assert.False(b.HasAttribute("disabled"))));
    }

    [Fact]
    public void Pick_rows_are_disabled_while_the_grocery_items_load()
    {
        PlanPastaWithGroceryItems();
        var load = new TaskCompletionSource<Result<List<GroceryItem>>>();
        Mediator
            .Setup(m => m.Send(It.IsAny<GetAllGroceryItems.Query>(), It.IsAny<CancellationToken>()))
            .Returns(load.Task);
        var cut = Render<MealSelectionList>();

        PickTacos(cut);

        cut.WaitForAssertion(() => Assert.All(cut.FindAll(TestId("meal-selection-pick")), b => Assert.True(b.HasAttribute("disabled"))));
        load.SetResult(Result.Ok(new List<GroceryItem>()));
        cut.WaitForAssertion(() => Assert.False(MealPlan.IsSwappingMeal));
    }

    // ---- The old day's links on shared items are dropped on a swap (MEA-99) ----

    [Fact]
    public void Swapping_without_own_items_swaps_straight_and_drops_this_days_links_on_shared_items()
    {
        PlanPastaWithGroceryItems(withOwnItem: false);
        var cut = Render<MealSelectionList>();

        PickTacos(cut);

        cut.WaitForAssertion(() => Assert.Single(Sent<DeleteGroceryItemSources.Command>()));
        Assert.False(Drawer.IsDrawerOpen);
        Assert.Equal([typeof(SwapMealInMealPlan.Command), typeof(DeleteGroceryItemSources.Command)],
            SentRequests.Where(r => r is SwapMealInMealPlan.Command or DeleteGroceryItemSources.Command).Select(r => r.GetType()));
        Assert.Equal([22], Sent<DeleteGroceryItemSources.Command>()[0].SourceIds);
    }

    [Fact]
    public void Swapping_with_own_items_passes_the_shared_links_to_the_drawer()
    {
        PlanPastaWithGroceryItems();
        var cut = Render<MealSelectionList>();
        PickTacos(cut);
        DrawerHost.WaitForAssertion(() => Assert.NotEmpty(DrawerHost.FindAll(TestId("swap-meal-confirm"))));

        DrawerHost.Find(TestId("swap-meal-confirm")).Click();

        DrawerHost.WaitForAssertion(() => Assert.False(Drawer.IsDrawerOpen));
        Assert.Equal([22], Assert.Single(Sent<DeleteGroceryItemSources.Command>()).SourceIds);
    }
}
