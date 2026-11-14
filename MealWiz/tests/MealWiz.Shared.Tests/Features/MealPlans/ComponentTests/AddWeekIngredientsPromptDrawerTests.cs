using System.Globalization;
using Bunit;
using FluentResults;
using MealWiz.Shared.Features.GroceryList.AddPlanIngredientsToGroceryList;
using MealWiz.Shared.Features.GroceryList.GetAllGroceryItems;
using MealWiz.Shared.Features.GroceryList.GetGroceryItemSources;
using MealWiz.Shared.Features.GroceryList.Models;
using MealWiz.Shared.Features.MealPlans;
using MealWiz.Shared.Features.MealPlans.GetMealPlanByDate;
using MealWiz.Shared.Features.MealPlans.Models;
using MealWiz.Shared.Features.MealPlans.PlanWeek;
using MealWiz.Shared.Helpers;
using MealWiz.Shared.Tests.TestInfrastructure;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace MealWiz.Shared.Tests.Features.MealPlans.ComponentTests;

/// <summary>The prompt Plan week's Save leaves behind on the Meal plan: add the saved week's ingredients? (MEA-83)</summary>
public class AddWeekIngredientsPromptDrawerTests : ComponentTestBase
{
    private static readonly DateTime ThisMonday = DateTime.Today.StartOfWeek().Date;

    /// <summary>Monday: Tacos (Tortillas 81, planned day 11). Wednesday: Pesto pasta (Penne 91, planned day 12).</summary>
    private async Task OpenPromptAsync(List<GroceryItem>? items = null, List<GroceryItemSource>? sources = null)
    {
        var tacos = TestData.Meal("Tacos", 8, ingredients: TestData.Ingredient("Tortillas", "8", id: 81));
        var pasta = TestData.Meal("Pesto pasta", 9, ingredients: TestData.Ingredient("Penne", "500 g", id: 91));
        SetupSend<GetMealPlanByDate.Query, Result<MealPlan?>>(
            Result.Ok<MealPlan?>(TestData.MealPlanWithDays((ThisMonday, tacos, 11), (ThisMonday.AddDays(2), pasta, 12))));
        SetupSend<GetAllGroceryItems.Query, Result<List<GroceryItem>>>(Result.Ok(items ?? []));
        SetupSend<GetGroceryItemSources.Query, Result<List<GroceryItemSource>>>(Result.Ok(sources ?? []));
        await MealPlan.NavigateToWeek(ThisMonday);
        RenderDrawers();

        var localizer = Services.GetRequiredService<IStringLocalizer<MealPlansResource>>();
        await DrawerHost.InvokeAsync(() => AddWeekIngredientsPromptDrawer.Open(Drawer, localizer, ThisMonday));
    }

    [Fact]
    public async Task The_prompt_asks_to_add_the_weeks_ingredients()
    {
        await OpenPromptAsync();

        Assert.True(Drawer.IsDrawerOpen);
        Assert.Equal("Week saved", Drawer.StateParameters.Title);
        DrawerHost.WaitForAssertion(() =>
        {
            Assert.Equal("Add this week's ingredients to the grocery list?", DrawerHost.Find(TestId("week-ingredients-prompt-title")).TextContent.Trim());
            Assert.Contains("only what isn't on your list yet", DrawerHost.Find(TestId("week-ingredients-prompt-description")).TextContent);
            Assert.Equal("Add ingredients", DrawerHost.Find(TestId("week-ingredients-prompt-accept")).TextContent.Trim());
            Assert.Equal("Not now", DrawerHost.Find(TestId("week-ingredients-prompt-decline")).TextContent.Trim());
        });
    }

    [Fact]
    public async Task Add_ingredients_opens_the_merge_drawer_for_every_planned_day_of_the_week()
    {
        await OpenPromptAsync();

        DrawerHost.WaitForElement(TestId("week-ingredients-prompt-accept")).Click();

        Assert.Equal(typeof(AddPlanIngredientsToGroceryListDrawer), Drawer.StateParameters.Content);
        Assert.Equal($"Week {ISOWeek.GetWeekOfYear(ThisMonday)} ingredients", Drawer.StateParameters.Title);
        DrawerHost.WaitForAssertion(() =>
        {
            var names = DrawerHost.FindAll(TestId("plan-ingredient-name")).Select(n => n.TextContent.Trim());
            Assert.Equal(["Tortillas", "Penne"], names);
        });
    }

    [Fact]
    public async Task Add_ingredients_leaves_out_ingredients_already_linked_from_that_day()
    {
        await OpenPromptAsync(
            items: [TestData.LinkedGroceryItem("Tortillas", 1, "8")],
            sources: [new GroceryItemSource { Id = 1, GroceryItemId = 1, PlannedMealId = 11, IngredientId = 81, PlannedDayMealId = 8, IngredientMealId = 8 }]);

        DrawerHost.WaitForElement(TestId("week-ingredients-prompt-accept")).Click();

        DrawerHost.WaitForAssertion(() =>
            Assert.Equal(["Penne"], DrawerHost.FindAll(TestId("plan-ingredient-name")).Select(n => n.TextContent.Trim())));
    }

    [Fact]
    public async Task Not_now_closes_the_drawer_and_adds_nothing()
    {
        await OpenPromptAsync();

        DrawerHost.WaitForElement(TestId("week-ingredients-prompt-decline")).Click();

        Assert.False(Drawer.IsDrawerOpen);
        Assert.Empty(Sent<MealWiz.Shared.Features.GroceryList.AddPlanIngredientsToGroceryList.AddPlanIngredientsToGroceryList.Command>());
    }

    [Fact]
    public async Task Add_ingredients_without_a_planned_day_left_just_closes()
    {
        await OpenPromptAsync();
        MealPlan.MealPlan!.MealOnDate.Clear(); // e.g. the days were removed on another screen meanwhile

        DrawerHost.WaitForElement(TestId("week-ingredients-prompt-accept")).Click();

        Assert.False(Drawer.IsDrawerOpen);
    }
}

/// <summary>The prompt's texts come from resources (UI-03): every localizer echoes its key as <c>[Key]</c>.</summary>
public class AddWeekIngredientsPromptDrawerTextTests() : ComponentTestBase(echoResourceKeys: true)
{
    [Fact]
    public async Task The_prompt_texts_come_from_the_resource()
    {
        RenderDrawers();
        var localizer = Services.GetRequiredService<IStringLocalizer<MealPlansResource>>();

        await DrawerHost.InvokeAsync(() => AddWeekIngredientsPromptDrawer.Open(Drawer, localizer, DateTime.Today.StartOfWeek().Date));

        Assert.Equal("[WeekPrompt_Header]", Drawer.StateParameters.Title);
        DrawerHost.WaitForAssertion(() =>
        {
            Assert.Equal("[WeekPrompt_Question]", DrawerHost.Find(TestId("week-ingredients-prompt-title")).TextContent.Trim());
            Assert.Equal("[WeekPrompt_Accept]", DrawerHost.Find(TestId("week-ingredients-prompt-accept")).TextContent.Trim());
            Assert.Equal("[WeekPrompt_Decline]", DrawerHost.Find(TestId("week-ingredients-prompt-decline")).TextContent.Trim());
        });
    }
}
