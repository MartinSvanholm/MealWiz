using Bunit;
using FluentResults;
using MealWiz.Shared.Features.GroceryList.AddMealPlanIngredientsToGroceryList;
using MealWiz.Shared.Features.GroceryList.AddPlanIngredientsToGroceryList;
using MealWiz.Shared.Features.GroceryList.GetAllGroceryItems;
using MealWiz.Shared.Features.GroceryList.GetGroceryItemSources;
using MealWiz.Shared.Features.GroceryList.Models;
using MealWiz.Shared.Features.GroceryList.UpdateLinkedGroceryAmounts;
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
        SetupSend<UpdateLinkedGroceryAmounts.Command, Result>(Result.Ok());
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

    // ---- Changed amounts (Was / Now) ----

    [Fact]
    public void The_changed_row_texts_come_from_the_resource()
    {
        using var echo = new EchoedDrawerTests();
        echo.AssertChangedRowTextsAreKeys();
    }

    /// <summary>A second test base with resource keys echoed instead of the English texts.</summary>
    private sealed class EchoedDrawerTests : ComponentTestBase
    {
        public EchoedDrawerTests() : base(echoResourceKeys: true)
        {
            SetupSend<GetAllGroceryItems.Query, Result<List<GroceryItem>>>(Result.Ok(new List<GroceryItem> { TestData.GroceryItem("Flour", "300 g", id: 2) }));
            SetupSend<GetGroceryItemSources.Query, Result<List<GroceryItemSource>>>(Result.Ok(new List<GroceryItemSource> { TestData.ChangedSource(2, 101, 11, "200 g", "250 g", id: 1), TestData.Source(2, 101, 12, "3 dl") }));
            MealPlan.MealPlan = TestData.MealPlanWith(Day, Pancakes(), plannedMealId: 101);
        }

        public void AssertChangedRowTextsAreKeys()
        {
            var cut = Render<AddPlanIngredientsToGroceryListDrawer>();
            cut.WaitForAssertion(() => Assert.Single(cut.FindAll(TestId("plan-ingredient-item"))));

            Assert.Contains("[MergeDrawer_Was]", cut.Find(TestId("plan-ingredient-existing")).TextContent);
            Assert.Equal("[MergeDrawer_Update]", cut.Find(TestId("plan-ingredients-confirm")).TextContent.Trim());
        }
    }

    /// <summary>Flour on the list as 300 g, linked from Pancakes' Flour, which went from 200 g to 250 g.</summary>
    private void SetupChangedFlour(string flourInList = "300 g", bool isPicked = false)
    {
        SetupItems(_milkInList, TestData.GroceryItem("Flour", flourInList, id: 2, isPicked: isPicked));
        SetupSources(TestData.ChangedSource(2, 101, 11, "200 g", "250 g", id: 1), TestData.Source(1, 101, 12, "3 dl"));
    }

    private static IEnumerable<string> RowTexts(IRenderedComponent<AddPlanIngredientsToGroceryListDrawer> cut) =>
        cut.FindAll(TestId("plan-ingredient-existing")).Select(e => e.TextContent.Trim());

    [Fact]
    public void A_linked_ingredient_whose_amount_changed_is_offered_with_old_new_and_the_adjusted_total()
    {
        SetupChangedFlour();

        var cut = RenderLoaded(1);

        Assert.Equal("Flour", cut.Find(TestId("plan-ingredient-name")).TextContent);
        Assert.Equal("Was 200 g · Now 250 g · In list 300 g", cut.Find(TestId("plan-ingredient-existing")).TextContent.Trim());
        Assert.Equal("250 g", cut.Find(".plan-ingredient-now").TextContent.Replace("Now ", ""));
        Assert.Equal("350 g", Amount(cut, 0));
        Assert.Contains("is-duplicate", cut.Find(TestId("plan-ingredient-amount")).ClassList);
        Assert.Equal("Update 1 on grocery list", cut.Find(TestId("plan-ingredients-confirm")).TextContent.Trim());
    }

    [Fact]
    public void A_changed_amount_in_another_unit_leaves_the_total_empty_and_required()
    {
        SetupChangedFlour(flourInList: "1 pack");
        var cut = RenderLoaded(1);

        Assert.Equal("", Amount(cut, 0));
        cut.Find(TestId("plan-ingredients-confirm")).Click();
        Assert.Empty(Sent<UpdateLinkedGroceryAmounts.Command>());
    }

    [Fact]
    public void A_change_only_in_case_or_spaces_is_not_offered()
    {
        SetupItems(_milkInList, TestData.GroceryItem("Flour", "300 g", id: 2));
        SetupSources(TestData.ChangedSource(2, 101, 11, "200 G", " 200 g "), TestData.Source(1, 101, 12, "3 dl"));

        var cut = RenderLoaded(0);

        Assert.NotEmpty(cut.FindAll(TestId("plan-ingredients-all-on-list")));
    }

    [Fact]
    public void A_changed_ingredient_whose_item_is_in_the_basket_is_left_out()
    {
        SetupChangedFlour(isPicked: true);

        var cut = RenderLoaded(0);

        Assert.NotEmpty(cut.FindAll(TestId("plan-ingredients-all-on-list")));
    }

    [Fact]
    public void A_changed_ingredient_not_shown_on_the_grocery_list_is_left_out()
    {
        SetupChangedFlour();
        MealPlan.MealPlan = TestData.MealPlanWith(Day, TestData.Meal(
            "Pancakes", id: 5,
            ingredients: [TestData.Ingredient("Flour", "200g", id: 11, showOnGroceryList: false), TestData.Ingredient("milk", "3 dl", id: 12)]),
            plannedMealId: 101);

        var cut = RenderLoaded(0);

        Assert.NotEmpty(cut.FindAll(TestId("plan-ingredients-all-on-list")));
    }

    [Fact]
    public void A_stale_source_is_not_offered_as_changed()
    {
        SetupItems(_milkInList, TestData.GroceryItem("Flour", "300 g", id: 2));
        var stale = TestData.ChangedSource(2, 101, 11, "200 g", "250 g", id: 1);
        stale.PlannedDayMealId = 99;
        SetupSources(stale, TestData.Source(1, 101, 12, "3 dl"));

        var cut = RenderLoaded(0);

        Assert.NotEmpty(cut.FindAll(TestId("plan-ingredients-all-on-list")));
    }

    [Fact]
    public void Changed_ingredients_outside_the_given_dates_are_not_offered()
    {
        SetupChangedFlour();
        MealPlan.MealPlan = TestData.MealPlanWithDays(
            (Day, Pancakes(), 101),
            (Day.AddDays(1), TestData.Meal("Soup", id: 8, ingredients: [TestData.Ingredient("Feta", "150 g", id: 22)]), 102));

        var cut = Render<AddPlanIngredientsToGroceryListDrawer>(p => p.Add(x => x.Dates, new[] { Day.AddDays(1) }));
        cut.WaitForAssertion(() => Assert.Single(cut.FindAll(TestId("plan-ingredient-item"))));

        Assert.Equal("Feta", cut.Find(TestId("plan-ingredient-name")).TextContent);
    }

    [Fact]
    public void New_and_changed_rows_are_listed_in_plan_order_and_counted_together()
    {
        SetupChangedFlour();
        MealPlan.MealPlan = TestData.MealPlanWithDays(
            (Day, Pancakes(), 101),
            (Day.AddDays(1), TestData.Meal("Soup", id: 8, ingredients: [TestData.Ingredient("Feta", "150 g", id: 22)]), 102));

        var cut = RenderLoaded(2);

        Assert.Equal(["Flour", "Feta"], cut.FindAll(TestId("plan-ingredient-name")).Select(e => e.TextContent));
        Assert.Equal("Add 2 to grocery list", cut.Find(TestId("plan-ingredients-confirm")).TextContent.Trim());
    }

    private void SetupTwoChangedGarlic()
    {
        SetupItems(TestData.GroceryItem("Garlic", "30 g", id: 3));
        SetupSources(
            TestData.ChangedSource(3, 101, 21, "20 g", "30 g", id: 1),
            TestData.ChangedSource(3, 102, 22, "10 g", "15 g", id: 2));
        MealPlan.MealPlan = TestData.MealPlanWithDays(
            (Day, TestData.Meal("Soup", id: 8, ingredients: [TestData.Ingredient("Garlic", "30 g", id: 21)]), 101),
            (Day.AddDays(1), TestData.Meal("Salad", id: 9, ingredients: [TestData.Ingredient("Garlic", "15 g", id: 22)]), 102));
    }

    [Fact]
    public void Two_changed_sources_on_one_item_share_one_row_and_adjust_in_turn()
    {
        SetupTwoChangedGarlic();

        var cut = RenderLoaded(1);

        Assert.Equal("Was 20 g + 10 g · Now 30 g + 15 g · In list 30 g", cut.Find(TestId("plan-ingredient-existing")).TextContent.Trim());
        Assert.Equal("45 g", Amount(cut, 0));

        cut.Find(TestId("plan-ingredients-confirm")).Click();

        cut.WaitForAssertion(() => Assert.Single(Sent<UpdateLinkedGroceryAmounts.Command>()));
        Assert.Equal([(1, 3, "30 g", "45 g"), (2, 3, "15 g", "45 g")],
            Sent<UpdateLinkedGroceryAmounts.Command>()[0].Updates.Select(u => (u.SourceId, u.GroceryItemId, u.SourceAmount, u.FinalAmount)));
    }

    private void SetupChangedFlourAndNewBreadFlour()
    {
        SetupChangedFlour();
        MealPlan.MealPlan = TestData.MealPlanWithDays(
            (Day, Pancakes(), 101),
            (Day.AddDays(1), TestData.Meal("Bread", id: 10, ingredients: [TestData.Ingredient("Flour", "500 g", id: 31)]), 102));
    }

    [Fact]
    public void A_changed_item_that_a_new_ingredient_also_matches_gets_one_row()
    {
        SetupChangedFlourAndNewBreadFlour();

        var cut = RenderLoaded(1);

        Assert.Equal("Plan 500 g · Was 200 g · Now 250 g · In list 300 g", cut.Find(TestId("plan-ingredient-existing")).TextContent.Trim());
        Assert.Equal("850 g", Amount(cut, 0));

        cut.Find(TestId("plan-ingredients-confirm")).Click();

        cut.WaitForAssertion(() => Assert.Single(Sent<UpdateLinkedGroceryAmounts.Command>()));
        var added = Assert.Single(Sent<AddPlanIngredientsToGroceryList.Command>()).Items;
        Assert.Equal([(102, 31, (int?)2, "850 g")], added.Select(i => (i.PlannedMealId, i.Ingredient.Id, i.ExistingGroceryItemId, i.FinalAmount)));
        var update = Assert.Single(Sent<UpdateLinkedGroceryAmounts.Command>()[0].Updates);
        Assert.Equal((1, 2, "250 g", "850 g"), (update.SourceId, update.GroceryItemId, update.SourceAmount, update.FinalAmount));
    }

    [Fact]
    public void Confirm_updates_changed_rows_and_closes_the_drawer()
    {
        SetupChangedFlour();
        Drawer.OpenDrawer("From meal plan", typeof(AddPlanIngredientsToGroceryListDrawer));
        var cut = RenderLoaded(1);

        cut.Find(TestId("plan-ingredients-confirm")).Click();

        cut.WaitForAssertion(() => Assert.False(Drawer.IsDrawerOpen));
        Assert.Empty(Sent<AddPlanIngredientsToGroceryList.Command>());
        var update = Assert.Single(Assert.Single(Sent<UpdateLinkedGroceryAmounts.Command>()).Updates);
        Assert.Equal((1, 2, "250 g", "350 g"), (update.SourceId, update.GroceryItemId, update.SourceAmount, update.FinalAmount));
        Assert.Equal(2, Sent<GetAllGroceryItems.Query>().Count);
        Assert.Equal(2, Sent<GetGroceryItemSources.Query>().Count);
        Assert.Contains("Grocery list updated", SnackbarMessages);
    }

    [Fact]
    public void A_typed_total_is_sent_instead_of_the_suggestion()
    {
        SetupChangedFlour();
        var cut = RenderLoaded(1);

        cut.FindAll(InputOf("plan-ingredient-amount-input"))[0].Input("400 g");
        cut.Find(TestId("plan-ingredients-confirm")).Click();

        cut.WaitForAssertion(() => Assert.Single(Sent<UpdateLinkedGroceryAmounts.Command>()));
        Assert.Equal("400 g", Sent<UpdateLinkedGroceryAmounts.Command>()[0].Updates[0].FinalAmount);
    }

    [Fact]
    public void A_failed_update_shows_the_error_and_keeps_the_drawer_open_with_the_typed_amount()
    {
        SetupChangedFlour();
        SetupSend<UpdateLinkedGroceryAmounts.Command, Result>(Result.Fail("Update failed"));
        Drawer.OpenDrawer("From meal plan", typeof(AddPlanIngredientsToGroceryListDrawer));
        var cut = RenderLoaded(1);

        cut.FindAll(InputOf("plan-ingredient-amount-input"))[0].Input("400 g");
        cut.Find(TestId("plan-ingredients-confirm")).Click();

        cut.WaitForAssertion(() => Assert.Contains("Update failed", SnackbarMessages));
        Assert.True(Drawer.IsDrawerOpen);
        Assert.Equal("400 g", Assert.Single(Sent<UpdateLinkedGroceryAmounts.Command>()).Updates[0].FinalAmount);
        Assert.Single(Sent<GetAllGroceryItems.Query>());
    }

    [Fact]
    public void A_failed_add_sends_no_update_and_keeps_the_drawer_open()
    {
        SetupChangedFlourAndNewBreadFlour();
        SetupSend<AddPlanIngredientsToGroceryList.Command, Result<List<GroceryItem>>>(Result.Fail<List<GroceryItem>>("Add failed"));
        Drawer.OpenDrawer("From meal plan", typeof(AddPlanIngredientsToGroceryListDrawer));
        var cut = RenderLoaded(1);

        cut.Find(TestId("plan-ingredients-confirm")).Click();

        cut.WaitForAssertion(() => Assert.Contains("Add failed", SnackbarMessages));
        Assert.True(Drawer.IsDrawerOpen);
        Assert.Empty(Sent<UpdateLinkedGroceryAmounts.Command>());
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
