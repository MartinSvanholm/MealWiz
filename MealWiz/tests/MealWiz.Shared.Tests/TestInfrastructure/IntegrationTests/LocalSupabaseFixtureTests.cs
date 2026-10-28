using MealWiz.Shared.Features.GroceryList.Models;
using MealWiz.Shared.Features.Ingredients.Models;
using MealWiz.Shared.Features.MealPlans.Models;
using MealWiz.Shared.Features.Meals.Models;
using MealWiz.Shared.Tests.TestInfrastructure.Builders;
using Xunit;

namespace MealWiz.Shared.Tests.TestInfrastructure.IntegrationTests;

/// <summary>
/// Checks the pieces the handler integration tests rely on: users are isolated from each other, and every
/// builder's rows can be read back through RLS by the user that inserted them.
/// </summary>
[Trait("Category", "Integration")]
public class LocalSupabaseFixtureTests(LocalSupabaseFixture supabase) : IClassFixture<LocalSupabaseFixture>
{
    [Fact]
    public async Task AnotherUser_CannotSeeTheFirstUsersMeal()
    {
        Assert.SkipUnless(supabase.IsAvailable, supabase.SkipReason ?? string.Empty);
        var meal = await new MealBuilder().InsertAsync(supabase.Client);

        var otherUser = await supabase.CreateSignedInUserAsync();
        var seenByOther = await otherUser.Client.From<MealDb>().Where(x => x.Id == meal.Id).Get(TestContext.Current.CancellationToken);
        var seenByOwner = await supabase.Client.From<MealDb>().Where(x => x.Id == meal.Id).Get(TestContext.Current.CancellationToken);

        Assert.NotEqual(supabase.UserId, otherUser.Id);
        Assert.Empty(seenByOther.Models);
        Assert.Single(seenByOwner.Models);
    }

    [Fact]
    public async Task MealBuilder_InsertsMealWithIngredients()
    {
        Assert.SkipUnless(supabase.IsAvailable, supabase.SkipReason ?? string.Empty);

        var meal = await new MealBuilder()
            .WithName("Pancakes")
            .WithIngredient(i => i.WithName("Flour").WithAmount("200 g"))
            .WithIngredient(i => i.WithName("Milk").ShowOnGroceryList(false))
            .InsertAsync(supabase.Client);

        var ingredients = await supabase.Client.From<IngredientDb>().Where(x => x.MealId == meal.Id).Get(TestContext.Current.CancellationToken);

        Assert.Equal("Pancakes", meal.Name);
        Assert.Equal(supabase.UserId, meal.CreatedBy);
        Assert.Equal(new[] { "Flour", "Milk" }, ingredients.Models.Select(i => i.Name).Order());
        Assert.Contains(ingredients.Models, i => i is { Name: "Flour", Amount: "200 g", ShowOnGroceryList: true });
        Assert.Contains(ingredients.Models, i => i is { Name: "Milk", ShowOnGroceryList: false });
    }

    [Fact]
    public async Task GroceryItemBuilder_InsertsItem()
    {
        Assert.SkipUnless(supabase.IsAvailable, supabase.SkipReason ?? string.Empty);

        var item = await new GroceryItemBuilder().WithName("Bananas").WithAmount("6").Picked().InsertAsync(supabase.Client);

        var row = await supabase.Client.From<GroceryItemDb>().Where(x => x.Id == item.Id).Single(TestContext.Current.CancellationToken);

        Assert.NotNull(row);
        Assert.Equal("Bananas", row.Name);
        Assert.Equal("6", row.Amount);
        Assert.True(row.IsPicked);
        Assert.Equal(supabase.UserId, row.CreatedBy);
    }

    [Fact]
    public async Task MealPlanBuilder_InsertsPlanWithPlannedMeals()
    {
        Assert.SkipUnless(supabase.IsAvailable, supabase.SkipReason ?? string.Empty);
        var meal = await new MealBuilder().InsertAsync(supabase.Client);
        var monday = new DateTime(2026, 10, 26);

        var mealPlan = await new MealPlanBuilder()
            .ForWeekOf(monday)
            .WithMeal(monday, meal)
            .InsertAsync(supabase.Client);

        var planMeals = await supabase.Client.From<MealPlanMealDb>().Where(x => x.MealPlanId == mealPlan.Id).Get(TestContext.Current.CancellationToken);

        Assert.Equal(supabase.UserId, mealPlan.CreatedBy);
        Assert.True(mealPlan.StartDate <= monday && monday <= mealPlan.EndDate);
        var planMeal = Assert.Single(planMeals.Models);
        Assert.Equal(meal.Id, planMeal.MealId);
        Assert.Equal("2026-10-26", planMeal.MealDate);
    }
}
