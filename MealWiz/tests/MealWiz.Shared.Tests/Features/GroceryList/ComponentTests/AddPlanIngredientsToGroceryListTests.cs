using Bunit;
using FluentResults;
using MealWiz.Shared.Features.GroceryList.AddMealPlanIngredientsToGroceryList;
using MealWiz.Shared.Features.GroceryList.AddPlanIngredientsToGroceryList;
using MealWiz.Shared.Features.GroceryList.GetAllGroceryItems;
using MealWiz.Shared.Features.GroceryList.GetGroceryItemSources;
using MealWiz.Shared.Features.GroceryList.Models;
using MealWiz.Shared.Features.MealPlans.GetMealPlanByDate;
using MealWiz.Shared.Features.MealPlans.Models;
using MealWiz.Shared.Features.Meals.Models;
using MealWiz.Shared.Features.Meals.Models;
using MealWiz.Shared.Tests.TestInfrastructure;
using Xunit;

namespace MealWiz.Shared.Tests.Features.GroceryList.ComponentTests;

/// <summary>Merging the planned ingredients into the grocery list (the button and the drawer it opens).</summary>
public class AddPlanIngredientsToGroceryListTests : ComponentTestBase
{
    private readonly GroceryItem _milkInList = TestData.GroceryItem("Milk", "1 l", id: 1);
    private static readonly DateTime Day = DateTime.Today;

    public AddPlanIngredientsToGroceryListTests()
    {
        SetupItems(_milkInList);
        SetupSources();
        SetupSend<AddPlanIngredientsToGroceryList.Command, Result<List<GroceryItem>>>(Result.Ok(new List<GroceryItem>()));
        MealPlan.MealPlan = TestData.MealPlanWith(Day, Pancakes(), plannedMealId: 101);
    }

    private static Meal Pancakes() => TestData.Meal(
        "Pancakes", id: 5,
        ingredients: [TestData.Ingredient("Flour", "200g", id: 11), TestData.Ingredient("milk", "3 dl", id: 12)]);

    private void SetupItems(params GroceryItem[] items) =>
        SetupSend<GetAllGroceryItems.Query, Result<List<GroceryItem>>>(Result.Ok(items.ToList()));

    private void SetupSources(params GroceryItemSource[] sources) =>
        SetupSend<GetGroceryItemSources.Query, Result<List<GroceryItemSource>>>(Result.Ok(sources.ToList()));

    private IRenderedComponent<AddPlanIngredientsToGroceryListDrawer> RenderLoaded(int expectedRows = -1)
    {
        var cut = Render<AddPlanIngredientsToGroceryListDrawer>();
        cut.WaitForAssertion(() => Assert.Empty(cut.FindAll(TestId("plan-ingredients-loading"))));
        if (expectedRows >= 0) Assert.Equal(expectedRows, cut.FindAll(TestId("plan-ingredient-item")).Count);
        return cut;
    }

    private static string Amount(IRenderedComponent<AddPlanIngredientsToGroceryListDrawer> cut, int index) =>
        cut.FindAll(InputOf("plan-ingredient-amount-input"))[index].GetAttribute("value") ?? "";

    [Fact]
    public void Lists_the_plans_ingredients_and_flags_ones_already_on_the_list()
    {
        var cut = RenderLoaded(2);

        var items = cut.FindAll(TestId("plan-ingredient-item"));
        Assert.Equal("Flour", items[0].QuerySelector(TestId("plan-ingredient-name"))!.TextContent);
        Assert.Equal("200g", Amount(cut, 0));
        Assert.Empty(items[0].QuerySelectorAll(TestId("plan-ingredient-existing")));
        Assert.Equal("Plan 3 dl · In list 1 l", items[1].QuerySelector(TestId("plan-ingredient-existing"))!.TextContent.Trim());
        Assert.Equal("Add 2 to grocery list", cut.Find(TestId("plan-ingredients-confirm")).TextContent.Trim());
    }

    [Fact]
    public void An_ingredient_already_on_the_list_with_another_unit_gets_an_empty_highlighted_amount()
    {
        var cut = RenderLoaded(2);

        var amounts = cut.FindAll(TestId("plan-ingredient-amount"));
        Assert.DoesNotContain("is-duplicate", amounts[0].ClassList);
        Assert.Contains("is-duplicate", amounts[1].ClassList);
        Assert.Equal("", Amount(cut, 1));
    }

    [Fact]
    public void Suggests_the_total_when_the_amounts_have_the_same_unit()
    {
        SetupItems(TestData.GroceryItem("Flour", "300 g", id: 2));

        var cut = RenderLoaded(2);

        Assert.Equal("500 g", Amount(cut, 0));
        Assert.Equal("Plan 200g · In list 300 g", cut.FindAll(TestId("plan-ingredient-existing"))[0].TextContent.Trim());
    }

    [Fact]
    public void Leaves_out_ingredients_already_linked_from_that_planned_day()
    {
        SetupSources(TestData.Source(1, plannedMealId: 101, ingredientId: 11));

        var cut = RenderLoaded(1);

        Assert.Equal("milk", cut.Find(TestId("plan-ingredient-name")).TextContent);
    }

    [Fact]
    public void An_ingredient_linked_from_another_planned_day_is_offered_and_merged()
    {
        SetupSources(TestData.Source(1, plannedMealId: 999, ingredientId: 12));

        var cut = RenderLoaded(2);

        Assert.Contains("is-duplicate", cut.FindAll(TestId("plan-ingredient-amount"))[1].ClassList);
    }

    [Fact]
    public void Shows_everything_is_on_the_list_when_every_ingredient_is_linked()
    {
        SetupSources(TestData.Source(1, 101, 11), TestData.Source(1, 101, 12));

        var cut = RenderLoaded(0);

        Assert.Equal("Everything is already on your list", cut.Find(TestId("plan-ingredients-all-on-list")).TextContent.Trim());
        Assert.Empty(cut.FindAll(TestId("plan-ingredients-confirm")));
    }

    [Fact]
    public void Shows_nothing_to_add_without_planned_ingredients()
    {
        MealPlan.MealPlan = TestData.MealPlanWith(Day, TestData.Meal("Eat out", id: 6), plannedMealId: 101);

        var cut = RenderLoaded(0);

        Assert.Equal("There's nothing from the meal plan to add.", cut.Find(TestId("plan-ingredients-empty")).TextContent.Trim());
        Assert.Empty(cut.FindAll(TestId("plan-ingredients-confirm")));
    }

    [Fact]
    public void Leaves_out_ingredients_that_are_not_shown_on_the_grocery_list()
    {
        MealPlan.MealPlan = TestData.MealPlanWith(Day, TestData.Meal(
            "Pasta", id: 7,
            ingredients: [TestData.Ingredient("Penne", "500 g", id: 31), TestData.Ingredient("Salt", "1 tsp", id: 32, showOnGroceryList: false)]),
            plannedMealId: 101);

        var cut = RenderLoaded(1);

        Assert.Equal("Penne", cut.Find(TestId("plan-ingredient-name")).TextContent);
    }

    [Fact]
    public void Same_name_ingredients_from_two_planned_days_share_one_row_with_a_suggested_total()
    {
        SetupItems();
        MealPlan.MealPlan = TestData.MealPlanWithDays(
            (Day, TestData.Meal("Soup", id: 8, ingredients: [TestData.Ingredient("Tomatoes", "400 g", id: 21)]), 101),
            (Day.AddDays(1), TestData.Meal("Salad", id: 9, ingredients: [TestData.Ingredient("tomatoes", "200 g", id: 22)]), 102));
        var cut = RenderLoaded(1);

        Assert.Equal("Plan 400 g + 200 g", cut.Find(TestId("plan-ingredient-existing")).TextContent.Trim());
        Assert.Equal("600 g", Amount(cut, 0));

        cut.Find(TestId("plan-ingredients-confirm")).Click();

        cut.WaitForAssertion(() => Assert.Single(Sent<AddPlanIngredientsToGroceryList.Command>()));
        var items = Sent<AddPlanIngredientsToGroceryList.Command>()[0].Items;
        Assert.Equal([(101, 21, (int?)null, "600 g"), (102, 22, null, "600 g")],
            items.Select(i => (i.PlannedMealId, i.Ingredient.Id, i.ExistingGroceryItemId, i.FinalAmount)));
    }

    [Fact]
    public void Only_the_given_dates_are_offered()
    {
        MealPlan.MealPlan = TestData.MealPlanWithDays(
            (Day, TestData.Meal("Soup", id: 8, ingredients: [TestData.Ingredient("Tomatoes", "400 g", id: 21)]), 101),
            (Day.AddDays(1), TestData.Meal("Salad", id: 9, ingredients: [TestData.Ingredient("Feta", "150 g", id: 22)]), 102));

        var cut = Render<AddPlanIngredientsToGroceryListDrawer>(p => p.Add(x => x.Dates, new[] { Day.AddDays(1) }));
        cut.WaitForAssertion(() => Assert.Single(cut.FindAll(TestId("plan-ingredient-item"))));

        Assert.Equal("Feta", cut.Find(TestId("plan-ingredient-name")).TextContent);
    }

    [Fact]
    public void A_name_that_only_matches_a_basket_item_becomes_a_new_item()
    {
        SetupItems(TestData.GroceryItem("Flour", "1 kg", id: 2, isPicked: true));

        var cut = RenderLoaded(2);

        Assert.Empty(cut.FindAll(TestId("plan-ingredient-existing")));
        Assert.Equal("200g", Amount(cut, 0));

        cut.Find(TestId("plan-ingredients-confirm")).Click();
        cut.WaitForAssertion(() => Assert.Single(Sent<AddPlanIngredientsToGroceryList.Command>()));
        Assert.Null(Sent<AddPlanIngredientsToGroceryList.Command>()[0].Items[0].ExistingGroceryItemId);
    }

    [Fact]
    public void Confirm_requires_an_amount_for_each_item()
    {
        var cut = RenderLoaded(2);

        cut.Find(TestId("plan-ingredients-confirm")).Click();

        Assert.Empty(Sent<AddPlanIngredientsToGroceryList.Command>());
    }

    [Fact]
    public void Confirm_sends_one_command_with_new_and_merged_ingredients_and_closes_the_drawer()
    {
        Drawer.OpenDrawer("From meal plan", typeof(AddPlanIngredientsToGroceryListDrawer));
        var cut = RenderLoaded(2);

        cut.FindAll(InputOf("plan-ingredient-amount-input"))[1].Input("1.3 l");
        cut.Find(TestId("plan-ingredients-confirm")).Click();

        cut.WaitForAssertion(() => Assert.False(Drawer.IsDrawerOpen));
        var items = Assert.Single(Sent<AddPlanIngredientsToGroceryList.Command>()).Items;
        Assert.Equal([(101, 11, (int?)null, "200g"), (101, 12, 1, "1.3 l")],
            items.Select(i => (i.PlannedMealId, i.Ingredient.Id, i.ExistingGroceryItemId, i.FinalAmount)));
        Assert.Equal("Pancakes", items[0].MealName);
        Assert.Equal(2, Sent<GetAllGroceryItems.Query>().Count);
        Assert.Equal(2, Sent<GetGroceryItemSources.Query>().Count);
        Assert.Contains("Ingredients added to list", SnackbarMessages);
    }

    [Fact]
    public void A_failed_add_shows_the_error_and_keeps_the_drawer_open()
    {
        SetupSend<AddPlanIngredientsToGroceryList.Command, Result<List<GroceryItem>>>(Result.Fail<List<GroceryItem>>("Add failed"));
        Drawer.OpenDrawer("From meal plan", typeof(AddPlanIngredientsToGroceryListDrawer));
        var cut = RenderLoaded(2);

        cut.FindAll(InputOf("plan-ingredient-amount-input"))[1].Input("1.3 l");
        cut.Find(TestId("plan-ingredients-confirm")).Click();

        cut.WaitForAssertion(() => Assert.Contains("Add failed", SnackbarMessages));
        Assert.True(Drawer.IsDrawerOpen);
        Assert.Equal("1.3 l", Assert.Single(Sent<AddPlanIngredientsToGroceryList.Command>()).Items[1].FinalAmount);
        Assert.Single(Sent<GetAllGroceryItems.Query>());
    }

    [Fact]
    public void A_failed_load_shows_the_error_and_closes_the_drawer()
    {
        SetupSend<GetAllGroceryItems.Query, Result<List<GroceryItem>>>(Result.Fail<List<GroceryItem>>("Load failed"));
        Drawer.OpenDrawer("From meal plan", typeof(AddPlanIngredientsToGroceryListDrawer));

        Render<AddPlanIngredientsToGroceryListDrawer>();

        Assert.False(Drawer.IsDrawerOpen);
        Assert.Contains("Load failed", SnackbarMessages);
    }

    [Fact]
    public void Enter_in_an_amount_moves_to_the_next_amount()
    {
        var cut = RenderLoaded(2);

        PressEnter(cut.FindAll(InputOf("plan-ingredient-amount-input"))[0]);

        AssertFocused(cut.FindAll(InputOf("plan-ingredient-amount-input"))[1]);
        Assert.Empty(Sent<AddPlanIngredientsToGroceryList.Command>());
    }

    [Fact]
    public void Enter_in_the_last_amount_confirms()
    {
        Drawer.OpenDrawer("From meal plan", typeof(AddPlanIngredientsToGroceryListDrawer));
        var cut = RenderLoaded(2);

        cut.FindAll(InputOf("plan-ingredient-amount-input"))[1].Input("1.3 l");
        PressEnter(cut.FindAll(InputOf("plan-ingredient-amount-input"))[1]);

        cut.WaitForAssertion(() => Assert.False(Drawer.IsDrawerOpen));
        Assert.Equal("1.3 l", Assert.Single(Sent<AddPlanIngredientsToGroceryList.Command>()).Items[1].FinalAmount);
    }

    [Fact]
    public void Open_opens_the_drawer_with_its_title_and_dates()
    {
        var dates = new[] { Day };

        AddPlanIngredientsToGroceryListDrawer.Open(Drawer, "Add Monday", dates);

        Assert.True(Drawer.IsDrawerOpen);
        Assert.Equal(typeof(AddPlanIngredientsToGroceryListDrawer), Drawer.StateParameters.Content);
        Assert.Equal("Add Monday", Drawer.StateParameters.Title);
        Assert.Same(dates, Drawer.StateParameters.ContentParameters["Dates"]);
    }

    [Fact]
    public void Open_without_dates_passes_no_dates()
    {
        AddPlanIngredientsToGroceryListDrawer.Open(Drawer, "From meal plan");

        Assert.Empty(Drawer.StateParameters.ContentParameters);
    }

    [Fact]
    public void Button_loads_the_meal_plan_when_needed_and_opens_the_drawer()
    {
        MealPlan.MealPlan = null;
        SetupSend<GetMealPlanByDate.Query, Result<MealPlan?>>(Result.Ok<MealPlan?>(new MealPlan(Day)));
        var cut = RenderWithPopovers<AddIngredientsToGroceryListButton>();

        cut.Find(TestId("add-plan-ingredients-button")).Click();

        cut.WaitForAssertion(() => Assert.True(Drawer.IsDrawerOpen));
        Assert.Single(Sent<GetMealPlanByDate.Query>());
        Assert.Equal(typeof(AddPlanIngredientsToGroceryListDrawer), Drawer.StateParameters.Content);
        Assert.Equal("From meal plan", Drawer.StateParameters.Title);
    }

    [Fact]
    public void Button_reuses_a_loaded_meal_plan()
    {
        var cut = RenderWithPopovers<AddIngredientsToGroceryListButton>();

        cut.Find(TestId("add-plan-ingredients-button")).Click();

        cut.WaitForAssertion(() => Assert.True(Drawer.IsDrawerOpen));
        Assert.Empty(Sent<GetMealPlanByDate.Query>());
    }
}
