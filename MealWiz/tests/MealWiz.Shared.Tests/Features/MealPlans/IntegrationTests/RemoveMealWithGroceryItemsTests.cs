using MealWiz.Shared.Features.MealPlans;
using MealWiz.Shared.Features.GroceryList;
using MealWiz.Shared.Features.GroceryList.AddGroceryItem;
using MealWiz.Shared.Features.GroceryList.AddPlanIngredientsToGroceryList;
using MealWiz.Shared.Features.GroceryList.DeleteGroceryItems;
using MealWiz.Shared.Features.GroceryList.DeleteGroceryItemSources;
using MealWiz.Shared.Features.GroceryList.GetAllGroceryItems;
using MealWiz.Shared.Features.GroceryList.GetGroceryItemSources;
using MealWiz.Shared.Features.GroceryList.Models;
using MealWiz.Shared.Features.GroceryList.UpdateGroceryItem;
using MealWiz.Shared.Features.MealPlans.Models;
using MealWiz.Shared.Features.MealPlans.RemoveMealFromMealPlan;
using MealWiz.Shared.Features.MealPlans.SwapMealInMealPlan;
using MealWiz.Shared.Features.Meals.Models;
using MealWiz.Shared.Tests.TestInfrastructure;
using MealWiz.Shared.Tests.TestInfrastructure.Builders;
using Xunit;

namespace MealWiz.Shared.Tests.Features.MealPlans.IntegrationTests;

/// <summary>
/// Removing a planned meal and then the grocery items only it needs (MEA-81), as a freshly signed-in user. Every test
/// plans its own week (<see cref="Week"/>) and only looks at the items it created.
/// </summary>
[Trait("Category", "Integration")]
public class RemoveMealWithGroceryItemsTests(LocalSupabaseFixture supabase) : IClassFixture<LocalSupabaseFixture>
{
    // A Monday no other class's grocery assertions plan around.
    private static readonly DateTime FirstMonday = new(2035, 1, 1);

    private static CancellationToken CancellationToken => TestContext.Current.CancellationToken;

    private static DateTime Week(int number) => FirstMonday.AddDays(7 * number);

    private sealed record Setup(
        MealPlan Plan, Meal Pasta, Meal Soup, int PastaDay, int SoupDay,
        GroceryItem Spaghetti, GroceryItem Garlic, GroceryItem Tomatoes);

    [Fact]
    public async Task RemoveThenDelete_ItemsOnlyThisMealNeeds_AreDeletedAndSharedItemsKeepTheirOtherLink()
    {
        Assert.SkipUnless(supabase.IsAvailable, supabase.SkipReason ?? string.Empty);
        var monday = Week(0);
        var setup = await PlanPastaAndSoupSharingGarlicAsync(monday);

        var onlyPasta = await OnlyNeededByAsync(setup.PastaDay);
        Assert.Equal([setup.Spaghetti.Id], onlyPasta.Select(i => i.Id));

        ResultAssert.Ok(await RemoveAsync(setup, monday));
        ResultAssert.Ok(await DeleteAsync(onlyPasta.ConvertAll(i => i.Id)));

        var items = await GetItemsAsync();
        Assert.DoesNotContain(items, i => i.Id == setup.Spaghetti.Id);
        Assert.Empty(await GetSourceRowsAsync(setup.Spaghetti.Id));
        Assert.Equal("3 cloves", Assert.Single(items, i => i.Id == setup.Garlic.Id).Amount);
        Assert.Contains(items, i => i.Id == setup.Tomatoes.Id);

        var garlicSources = await GetSourceRowsAsync(setup.Garlic.Id);
        Assert.Equal(2, garlicSources.Count);
        Assert.Contains(garlicSources, s => s.PlannedMealId == setup.SoupDay);
        Assert.Single(garlicSources, s => s.PlannedMealId is null);

        var sources = ResultAssert.Ok(await GetSourcesAsync());
        Assert.False(Assert.Single(sources, s => s.GroceryItemId == setup.Garlic.Id && s.PlannedMealId == setup.SoupDay).IsStale);
        Assert.True(Assert.Single(sources, s => s.GroceryItemId == setup.Garlic.Id && s.PlannedMealId is null).IsStale);

        var rows = await supabase.Client.From<MealPlanMealDb>().Where(x => x.MealPlanId == setup.Plan.Id).Get(CancellationToken);
        Assert.DoesNotContain(rows.Models, r => r.Id == setup.PastaDay);
        Assert.Contains(rows.Models, r => r.Id == setup.SoupDay);
    }

    [Fact]
    public async Task RemoveWithSwitchOff_LeavesTheItemsWithAStaleSource()
    {
        Assert.SkipUnless(supabase.IsAvailable, supabase.SkipReason ?? string.Empty);
        var monday = Week(1);
        var setup = await PlanPastaAndSoupSharingGarlicAsync(monday);

        ResultAssert.Ok(await RemoveAsync(setup, monday));

        var items = await GetItemsAsync();
        Assert.Contains(items, i => i.Id == setup.Spaghetti.Id);
        Assert.Contains(items, i => i.Id == setup.Garlic.Id);
        var source = Assert.Single(await GetSourceRowsAsync(setup.Spaghetti.Id));
        Assert.Null(source.PlannedMealId);
        var stale = Assert.Single(ResultAssert.Ok(await GetSourcesAsync()), s => s.GroceryItemId == setup.Spaghetti.Id);
        Assert.True(stale.IsStale);
    }

    [Fact]
    public async Task OnlyNeededBy_PickedAndManualItems_AreNeverPicked()
    {
        Assert.SkipUnless(supabase.IsAvailable, supabase.SkipReason ?? string.Empty);
        var monday = Week(2);
        var manualGarlic = ResultAssert.Ok(await new AddGroceryItem.Handler(supabase.Client, TestLocalizer.For<GroceryListResource>())
            .Handle(new AddGroceryItem.Command(new GroceryItemBuilder().WithName("Garlic").WithAmount("1 clove").Build()), CancellationToken));
        var setup = await PlanPastaAndSoupSharingGarlicAsync(monday, pastaGarlicMergesInto: manualGarlic.Id);
        setup.Spaghetti.IsPicked = true;
        ResultAssert.Ok(await new UpdateGroceryItem.Handler(supabase.Client, TestLocalizer.For<GroceryListResource>()).Handle(new UpdateGroceryItem.Command(setup.Spaghetti), CancellationToken));

        var onlyPasta = await OnlyNeededByAsync(setup.PastaDay);

        Assert.Empty(onlyPasta);
        ResultAssert.Ok(await RemoveAsync(setup, monday));
        var items = await GetItemsAsync();
        Assert.Contains(items, i => i.Id == setup.Spaghetti.Id);
        Assert.Contains(items, i => i.Id == manualGarlic.Id);
    }

    // ---- Dropping the day's links on shared items lowers their amount (MEA-99) ----

    private async Task<List<int>> SharedLinksAsync(int plannedMealId) =>
        PlannedMealGroceryItems.SourceIdsOnSharedItems(plannedMealId, await GetItemsAsync(), ResultAssert.Ok(await GetSourcesAsync()));

    private Task<FluentResults.Result<List<GroceryItem>>> DropAsync(List<int> sourceIds) =>
        new DeleteGroceryItemSources.Handler(supabase.Client, TestLocalizer.For<GroceryListResource>())
            .Handle(new DeleteGroceryItemSources.Command(sourceIds), CancellationToken);

    [Fact]
    public async Task RemoveThenDropSharedLinks_ReducesTheSharedItemsAmount()
    {
        Assert.SkipUnless(supabase.IsAvailable, supabase.SkipReason ?? string.Empty);
        var monday = Week(10);
        var setup = await PlanPastaAndSoupSharingGarlicAsync(monday);
        var own = (await OnlyNeededByAsync(setup.PastaDay)).ConvertAll(i => i.Id);
        var shared = await SharedLinksAsync(setup.PastaDay);
        Assert.Single(shared);

        ResultAssert.Ok(await RemoveAsync(setup, monday));
        ResultAssert.Ok(await DeleteAsync(own));
        ResultAssert.Ok(await DropAsync(shared));

        var garlic = (await GetItemsAsync()).Single(i => i.Id == setup.Garlic.Id);
        Assert.Equal("1 clove", garlic.Amount);
        var sources = ResultAssert.Ok(await GetSourcesAsync()).Where(s => s.GroceryItemId == setup.Garlic.Id).ToList();
        Assert.Equal(setup.SoupDay, Assert.Single(sources).PlannedMealId);
        Assert.DoesNotContain(await GetItemsAsync(), i => i.Id == setup.Spaghetti.Id);
    }

    [Fact]
    public async Task SwapThenDropSharedLinks_ReducesTheSharedItemsAmount()
    {
        Assert.SkipUnless(supabase.IsAvailable, supabase.SkipReason ?? string.Empty);
        var monday = Week(11);
        var setup = await PlanPastaAndSoupSharingGarlicAsync(monday);
        var tacos = await new MealBuilder().WithName("Tacos").WithIngredient(i => i.WithName("Tortillas").WithAmount("8")).InsertAsync(supabase.Client);
        var shared = await SharedLinksAsync(setup.PastaDay);

        ResultAssert.Ok(await new SwapMealInMealPlan.Handler(supabase.Client, TestLocalizer.For<MealPlansResource>())
            .Handle(new SwapMealInMealPlan.Command(setup.Plan.Id, monday, setup.Pasta.Id, tacos.Id), CancellationToken));
        ResultAssert.Ok(await DropAsync(shared));

        Assert.Equal("1 clove", (await GetItemsAsync()).Single(i => i.Id == setup.Garlic.Id).Amount);
    }

    /// <summary>
    /// Pasta (Spaghetti, Garlic) on Monday and Soup (Tomatoes, Garlic) on Tuesday, both added to the list, Garlic merged
    /// into one item "3 cloves". With <paramref name="pastaGarlicMergesInto"/> Pasta's Garlic goes into that existing item.
    /// </summary>
    private async Task<Setup> PlanPastaAndSoupSharingGarlicAsync(DateTime monday, int? pastaGarlicMergesInto = null)
    {
        var pasta = await new MealBuilder().WithName("Pasta")
            .WithIngredient(i => i.WithName("Spaghetti").WithAmount("400 g"))
            .WithIngredient(i => i.WithName("Garlic").WithAmount("2 cloves"))
            .InsertAsync(supabase.Client);
        var soup = await new MealBuilder().WithName("Soup")
            .WithIngredient(i => i.WithName("Tomatoes").WithAmount("6"))
            .WithIngredient(i => i.WithName("Garlic").WithAmount("1 clove"))
            .InsertAsync(supabase.Client);
        var plan = await new MealPlanBuilder().ForWeekOf(monday)
            .WithMeal(monday, pasta).WithMeal(monday.AddDays(1), soup).InsertAsync(supabase.Client);
        var pastaDay = plan.PlannedMealIdOnDate[monday];
        var soupDay = plan.PlannedMealIdOnDate[monday.AddDays(1)];

        var pastaItems = ResultAssert.Ok(await AddAsync(pasta.Ingredients.ConvertAll(i =>
            new PlanIngredientToAdd(pastaDay, i, pasta.Name, i.Name == "Garlic" ? pastaGarlicMergesInto : null, i.Amount))));
        var garlic = pastaItems.Single(i => i.Name == "Garlic");

        var soupItems = ResultAssert.Ok(await AddAsync(soup.Ingredients.ConvertAll(i =>
            i.Name == "Garlic"
                ? new PlanIngredientToAdd(soupDay, i, soup.Name, garlic.Id, "3 cloves")
                : new PlanIngredientToAdd(soupDay, i, soup.Name, null, i.Amount))));

        return new Setup(plan, pasta, soup, pastaDay, soupDay,
            pastaItems.Single(i => i.Name == "Spaghetti"), garlic, soupItems.Single(i => i.Name == "Tomatoes"));
    }

    private async Task<List<GroceryItem>> OnlyNeededByAsync(int plannedMealId) =>
        PlannedMealGroceryItems.OnlyNeededBy(plannedMealId, await GetItemsAsync(), ResultAssert.Ok(await GetSourcesAsync()));

    private Task<FluentResults.Result> RemoveAsync(Setup setup, DateTime date) =>
        new RemoveMealFromMealPlan.Handler(supabase.Client, TestLocalizer.For<MealPlansResource>())
            .Handle(new RemoveMealFromMealPlan.Command(setup.Pasta.Id, setup.Plan.Id, date), CancellationToken);

    private Task<FluentResults.Result<List<GroceryItem>>> AddAsync(List<PlanIngredientToAdd> items) =>
        new AddPlanIngredientsToGroceryList.Handler(supabase.Client, TestLocalizer.For<GroceryListResource>()).Handle(new AddPlanIngredientsToGroceryList.Command(items), CancellationToken);

    private Task<FluentResults.Result> DeleteAsync(List<int> ids) =>
        new DeleteGroceryItems.Handler(supabase.Client).Handle(new DeleteGroceryItems.Command(ids), CancellationToken);

    private Task<FluentResults.Result<List<GroceryItemSource>>> GetSourcesAsync() =>
        new GetGroceryItemSources.Handler(supabase.Client).Handle(new GetGroceryItemSources.Query(), CancellationToken);

    private async Task<List<GroceryItem>> GetItemsAsync() =>
        ResultAssert.Ok(await new GetAllGroceryItems.Handler(supabase.Client).Handle(new GetAllGroceryItems.Query(), CancellationToken));

    private async Task<List<GroceryItemSourceDb>> GetSourceRowsAsync(int groceryItemId)
    {
        var response = await supabase.Client.From<GroceryItemSourceDb>().Where(x => x.GroceryItemId == groceryItemId).Get(CancellationToken);
        return response.Models;
    }
}
