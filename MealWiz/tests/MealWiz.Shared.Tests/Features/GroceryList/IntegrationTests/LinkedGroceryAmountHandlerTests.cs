using MealWiz.Shared.Features.GroceryList.AddPlanIngredientsToGroceryList;
using MealWiz.Shared.Features.GroceryList.GetGroceryItemSources;
using MealWiz.Shared.Features.GroceryList.Models;
using MealWiz.Shared.Features.GroceryList.UpdateLinkedGroceryAmounts;
using MealWiz.Shared.Features.Ingredients.Models;
using MealWiz.Shared.Features.Ingredients.SaveIngredient;
using MealWiz.Shared.Features.Meals.Models;
using MealWiz.Shared.Helpers;
using MealWiz.Shared.Tests.TestInfrastructure;
using MealWiz.Shared.Tests.TestInfrastructure.Builders;
using Xunit;

namespace MealWiz.Shared.Tests.Features.GroceryList.IntegrationTests;

/// <summary>
/// The joined ingredient amount on grocery item sources and the UpdateLinkedGroceryAmounts handler, as a freshly
/// signed-in user. Each test plans its own week (a plan and a planned date are unique per user).
/// </summary>
[Trait("Category", "Integration")]
public class LinkedGroceryAmountHandlerTests(LocalSupabaseFixture supabase) : IClassFixture<LocalSupabaseFixture>
{
    private static CancellationToken CancellationToken => TestContext.Current.CancellationToken;

    [Fact]
    public async Task GetGroceryItemSources_HasTheIngredientsCurrentAmount()
    {
        Assert.SkipUnless(supabase.IsAvailable, supabase.SkipReason ?? string.Empty);
        var (_, _, items) = await PlanAndAddAsync(supabase.Client, Week(0), ("Penne", "500 g"));

        var source = Assert.Single(await SourcesOfAsync(items[0].Id));

        Assert.Equal("500 g", source.Amount);
        Assert.Equal("500 g", source.IngredientAmount);
        Assert.False(source.HasChangedAmount);
    }

    [Fact]
    public async Task GetGroceryItemSources_AfterTheIngredientsAmountChanges_HasTheNewAmountAndIsChanged()
    {
        Assert.SkipUnless(supabase.IsAvailable, supabase.SkipReason ?? string.Empty);
        var (meal, _, items) = await PlanAndAddAsync(supabase.Client, Week(1), ("Penne", "500 g"));
        await ChangeAmountAsync(meal.Ingredients[0], meal.Id, "750 g");

        var source = Assert.Single(await SourcesOfAsync(items[0].Id));

        Assert.Equal("500 g", source.Amount);
        Assert.Equal("750 g", source.IngredientAmount);
        Assert.True(source.HasChangedAmount);
    }

    [Fact]
    public async Task UpdateLinkedGroceryAmounts_UpdatesTheItemAndItsSourcesAmount()
    {
        Assert.SkipUnless(supabase.IsAvailable, supabase.SkipReason ?? string.Empty);
        var (meal, _, items) = await PlanAndAddAsync(supabase.Client, Week(2), ("Penne", "500 g"));
        await ChangeAmountAsync(meal.Ingredients[0], meal.Id, "750 g");
        var source = Assert.Single(await SourcesOfAsync(items[0].Id));
        var before = DateTime.UtcNow;

        ResultAssert.Ok(await UpdateAsync(new LinkedAmountUpdate(source.Id, items[0].Id, "750 g", "950 g")));

        var row = await GetItemRowAsync(items[0].Id);
        Assert.Equal("950 g", row?.Amount);
        Assert.False(row?.IsManual);
        TimeAssert.CloseTo(before, row!.UpdatedAt!.Value);
        var reread = Assert.Single(await SourcesOfAsync(items[0].Id));
        Assert.Equal("750 g", reread.Amount);
        Assert.False(reread.HasChangedAmount);
    }

    [Fact]
    public async Task UpdateLinkedGroceryAmounts_TwoSourcesOfOneItem_UpdatesTheItemOnceAndBothSources()
    {
        Assert.SkipUnless(supabase.IsAvailable, supabase.SkipReason ?? string.Empty);
        var first = await PlanAndAddAsync(supabase.Client, Week(3), ("Garlic", "20 g"));
        var second = await PlanAndAddAsync(supabase.Client, Week(4), ("Garlic", "10 g"));
        // Same name in two weeks makes two items here, so point the second link at the first item
        var itemId = first.Items[0].Id;
        var secondSource = Assert.Single(await SourcesOfAsync(second.Items[0].Id));
        await supabase.Client.From<GroceryItemSourceDb>().Where(x => x.Id == secondSource.Id).Set(x => x.GroceryItemId, itemId).Update(cancellationToken: CancellationToken);
        var sources = await SourcesOfAsync(itemId);
        Assert.Equal(2, sources.Count);

        ResultAssert.Ok(await UpdateAsync(
            new LinkedAmountUpdate(sources[0].Id, itemId, "30 g", "45 g"),
            new LinkedAmountUpdate(sources[1].Id, itemId, "15 g", "45 g")));

        Assert.Equal("45 g", (await GetItemRowAsync(itemId))?.Amount);
        var reread = await SourcesOfAsync(itemId);
        Assert.Equal(["15 g", "30 g"], reread.Select(s => s.Amount).Order());
    }

    [Fact]
    public async Task UpdateLinkedGroceryAmounts_EmptyCommand_ReturnsOk()
    {
        Assert.SkipUnless(supabase.IsAvailable, supabase.SkipReason ?? string.Empty);
        var signedOut = await supabase.CreateSignedOutClientAsync();

        // No request is made, so not even a signed-out client fails
        ResultAssert.Ok(await new UpdateLinkedGroceryAmounts.Handler(signedOut).Handle(new UpdateLinkedGroceryAmounts.Command([]), CancellationToken));
    }

    [Fact]
    public async Task UpdateLinkedGroceryAmounts_SignedOut_FailsWithNotSignedIn()
    {
        Assert.SkipUnless(supabase.IsAvailable, supabase.SkipReason ?? string.Empty);
        var signedOut = await supabase.CreateSignedOutClientAsync();

        var result = await new UpdateLinkedGroceryAmounts.Handler(signedOut)
            .Handle(new UpdateLinkedGroceryAmounts.Command([new LinkedAmountUpdate(1, 1, "1", "2")]), CancellationToken);

        ResultAssert.Failed(result, ErrorMessages.NotSignedIn);
    }

    [Fact]
    public async Task UpdateLinkedGroceryAmounts_InvalidIds_FailsWithoutWriting()
    {
        Assert.SkipUnless(supabase.IsAvailable, supabase.SkipReason ?? string.Empty);
        var item = await new GroceryItemBuilder().WithAmount("1 l").InsertAsync(supabase.Client);

        ResultAssert.Failed(await UpdateAsync(new LinkedAmountUpdate(0, item.Id, "2 l", "2 l")), "Only linked grocery items can be updated.");

        Assert.Equal("1 l", (await GetItemRowAsync(item.Id))?.Amount);
    }

    [Fact]
    public async Task UpdateLinkedGroceryAmounts_PickedItem_FailsAndChangesNothing()
    {
        Assert.SkipUnless(supabase.IsAvailable, supabase.SkipReason ?? string.Empty);
        var (meal, _, items) = await PlanAndAddAsync(supabase.Client, Week(5), ("Basil", "1 bunch"));
        await ChangeAmountAsync(meal.Ingredients[0], meal.Id, "2 bunch");
        var source = Assert.Single(await SourcesOfAsync(items[0].Id));
        await supabase.Client.From<GroceryItemDb>().Where(x => x.Id == items[0].Id).Set(x => x.IsPicked, true).Update(cancellationToken: CancellationToken);

        ResultAssert.Failed(await UpdateAsync(new LinkedAmountUpdate(source.Id, items[0].Id, "2 bunch", "2 bunch")), "The grocery item is already in the basket.");

        Assert.Equal("1 bunch", (await GetItemRowAsync(items[0].Id))?.Amount);
        Assert.Equal("1 bunch", Assert.Single(await SourcesOfAsync(items[0].Id)).Amount);
    }

    [Fact]
    public async Task UpdateLinkedGroceryAmounts_SourceOfAnotherItem_FailsBeforeWriting()
    {
        Assert.SkipUnless(supabase.IsAvailable, supabase.SkipReason ?? string.Empty);
        var (_, _, items) = await PlanAndAddAsync(supabase.Client, Week(6), ("Rice", "2 dl"));
        var other = await new GroceryItemBuilder().WithAmount("1 l").InsertAsync(supabase.Client);
        var source = Assert.Single(await SourcesOfAsync(items[0].Id));

        ResultAssert.Failed(await UpdateAsync(new LinkedAmountUpdate(source.Id, other.Id, "3 dl", "9 l")), "The grocery item's link could not be found.");

        Assert.Equal("1 l", (await GetItemRowAsync(other.Id))?.Amount);
        Assert.Equal("2 dl", Assert.Single(await SourcesOfAsync(items[0].Id)).Amount);
    }

    [Fact]
    public async Task UpdateLinkedGroceryAmounts_AnotherUsersItem_FailsAndChangesNothing()
    {
        Assert.SkipUnless(supabase.IsAvailable, supabase.SkipReason ?? string.Empty);
        var otherUser = await supabase.GetOtherUserAsync();
        var (_, _, items) = await PlanAndAddAsync(otherUser.Client, Week(7), ("Beans", "1 can"));
        var others = Assert.Single((await otherUser.Client.From<GroceryItemSourceDb>().Where(x => x.GroceryItemId == items[0].Id).Get(CancellationToken)).Models);

        // RLS hides the other user's rows, so they can't be found
        ResultAssert.Failed(await UpdateAsync(new LinkedAmountUpdate(others.Id, items[0].Id, "2 can", "2 can")), "The grocery item could not be found.");

        var row = (await otherUser.Client.From<GroceryItemDb>().Where(x => x.Id == items[0].Id).Get(CancellationToken)).Models.Single();
        Assert.Equal("1 can", row.Amount);
    }

    /// <summary>One week per test, far from the other test classes' weeks.</summary>
    private static DateTime Week(int n) => new DateTime(2035, 1, 1).AddDays(7 * n);

    private static async Task<(Meal Meal, int PlannedMealId, List<GroceryItem> Items)> PlanAndAddAsync(
        Supabase.Client client, DateTime day, params (string Name, string Amount)[] ingredients)
    {
        var builder = new MealBuilder();
        foreach (var (name, amount) in ingredients) builder.WithIngredient(i => i.WithName(name).WithAmount(amount));
        var meal = await builder.InsertAsync(client);
        var plan = await new MealPlanBuilder().ForWeekOf(day).WithMeal(day, meal).InsertAsync(client);
        var plannedMealId = plan.PlannedMealIdOnDate[day];

        var entries = meal.Ingredients.ConvertAll(i => new PlanIngredientToAdd(plannedMealId, i, meal.Name, null, i.Amount ?? string.Empty));
        var added = ResultAssert.Ok(await new AddPlanIngredientsToGroceryList.Handler(client)
            .Handle(new AddPlanIngredientsToGroceryList.Command(entries), CancellationToken));
        return (meal, plannedMealId, added);
    }

    private async Task ChangeAmountAsync(Ingredient ingredient, int mealId, string amount)
    {
        ingredient.Amount = amount;
        ResultAssert.Ok(await new SaveIngredient.Handler(supabase.Client).Handle(new SaveIngredient.Command(ingredient, mealId), CancellationToken));
    }

    private async Task<List<GroceryItemSource>> SourcesOfAsync(int itemId) =>
        ResultAssert.Ok(await new GetGroceryItemSources.Handler(supabase.Client).Handle(new GetGroceryItemSources.Query(), CancellationToken))
            .Where(s => s.GroceryItemId == itemId).OrderBy(s => s.Id).ToList();

    private Task<FluentResults.Result> UpdateAsync(params LinkedAmountUpdate[] updates) =>
        new UpdateLinkedGroceryAmounts.Handler(supabase.Client).Handle(new UpdateLinkedGroceryAmounts.Command([.. updates]), CancellationToken);

    private async Task<GroceryItemDb?> GetItemRowAsync(int id) =>
        (await supabase.Client.From<GroceryItemDb>().Where(x => x.Id == id).Get(CancellationToken)).Models.SingleOrDefault();
}
