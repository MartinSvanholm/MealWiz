using MealWiz.Shared.Features.GroceryList.AddGroceryItem;
using MealWiz.Shared.Features.GroceryList.AddMealPlanIngredientsToGroceryList;
using MealWiz.Shared.Features.GroceryList.DeleteGroceryItem;
using MealWiz.Shared.Features.GroceryList.GetAllGroceryItems;
using MealWiz.Shared.Features.GroceryList.Models;
using MealWiz.Shared.Features.GroceryList.UpdateGroceryItem;
using MealWiz.Shared.Features.MealPlans.GetMealPlanByDate;
using MealWiz.Shared.Tests.TestInfrastructure;
using MealWiz.Shared.Tests.TestInfrastructure.Builders;
using Xunit;

namespace MealWiz.Shared.Tests.Features.GroceryList.IntegrationTests;

/// <summary>
/// Runs the GroceryList handlers as a freshly signed-in user. The tests share the class's user, so each one only
/// looks at the items it created.
/// </summary>
[Trait("Category", "Integration")]
public class GroceryListHandlerTests(LocalSupabaseFixture supabase) : IClassFixture<LocalSupabaseFixture>
{
    private static CancellationToken CancellationToken => TestContext.Current.CancellationToken;

    [Fact]
    public async Task AddGroceryItem_InsertsItOwnedByTheUserAndReturnsTheRow()
    {
        Assert.SkipUnless(supabase.IsAvailable, supabase.SkipReason ?? string.Empty);
        var before = DateTime.UtcNow;
        var item = new GroceryItemBuilder().WithName("Apples").WithAmount("4").Build();

        var added = ResultAssert.Ok(await new AddGroceryItem.Handler(supabase.Client).Handle(new AddGroceryItem.Command(item), CancellationToken));

        var row = await GetRowAsync(added.Id);
        Assert.NotNull(row);
        Assert.Equal("Apples", row.Name);
        Assert.Equal("4", row.Amount);
        Assert.False(row.IsPicked);
        Assert.Equal(supabase.UserId, row.CreatedBy);
        TimeAssert.CloseTo(before, row.CreatedAt);
        // The returned item carries its owner, so it can be updated straight away (MEA-42)
        Assert.Equal(supabase.UserId, added.CreatedBy);
        Assert.Equal(row.CreatedAt, added.CreatedAt);
        Assert.Equal("Apples", added.Name);
    }

    [Fact]
    public async Task AddGroceryItem_ThenUpdateTheReturnedItem_SavesTheChange()
    {
        Assert.SkipUnless(supabase.IsAvailable, supabase.SkipReason ?? string.Empty);
        var item = new GroceryItemBuilder().WithName("Milk").Build();
        var added = ResultAssert.Ok(await new AddGroceryItem.Handler(supabase.Client).Handle(new AddGroceryItem.Command(item), CancellationToken));

        added.IsPicked = true;
        ResultAssert.Ok(await UpdateAsync(added));

        var row = await GetRowAsync(added.Id);
        Assert.NotNull(row);
        Assert.True(row.IsPicked);
    }

    [Fact]
    public async Task GetAllGroceryItems_ReturnsTheUsersItemsButNotAnotherUsers()
    {
        Assert.SkipUnless(supabase.IsAvailable, supabase.SkipReason ?? string.Empty);
        var own = await new GroceryItemBuilder().Picked().InsertAsync(supabase.Client);
        var otherUser = await supabase.GetOtherUserAsync();
        var others = await new GroceryItemBuilder().InsertAsync(otherUser.Client);

        var items = ResultAssert.Ok(await new GetAllGroceryItems.Handler(supabase.Client).Handle(new GetAllGroceryItems.Query(), CancellationToken));

        Assert.Contains(items, i => i.Id == own.Id && i.Name == own.Name && i.IsPicked);
        Assert.DoesNotContain(items, i => i.Id == others.Id);
        Assert.All(items, i => Assert.Equal(supabase.UserId, i.CreatedBy));
    }

    [Fact]
    public async Task UpdateGroceryItem_SavesTheChangesAndSetsUpdatedAt()
    {
        Assert.SkipUnless(supabase.IsAvailable, supabase.SkipReason ?? string.Empty);
        var item = await new GroceryItemBuilder().WithAmount("1").InsertAsync(supabase.Client);
        var before = DateTime.UtcNow;

        item.IsPicked = true;
        item.Amount = "3";
        var id = ResultAssert.Ok(await UpdateAsync(item));

        Assert.Equal(item.Id, id);
        var row = await GetRowAsync(item.Id);
        Assert.NotNull(row);
        Assert.True(row.IsPicked);
        Assert.Equal("3", row.Amount);
        TimeAssert.CloseTo(before, row.UpdatedAt);
    }

    [Fact]
    public async Task UpdateGroceryItem_ItemThatDoesNotExist_Fails()
    {
        Assert.SkipUnless(supabase.IsAvailable, supabase.SkipReason ?? string.Empty);
        var missing = new GroceryItemBuilder().Build();
        missing.Id = int.MaxValue;
        missing.CreatedBy = supabase.UserId;

        ResultAssert.Failed(await UpdateAsync(missing), "The grocery item could not be found.");
    }

    [Fact]
    public async Task DeleteGroceryItem_RemovesTheItem()
    {
        Assert.SkipUnless(supabase.IsAvailable, supabase.SkipReason ?? string.Empty);
        var item = await new GroceryItemBuilder().InsertAsync(supabase.Client);

        var id = ResultAssert.Ok(await DeleteAsync(item));

        Assert.Equal(item.Id, id);
        Assert.Null(await GetRowAsync(item.Id));
    }

    [Fact]
    public async Task DeleteGroceryItem_AnotherUsersItem_FailsAndKeepsIt()
    {
        Assert.SkipUnless(supabase.IsAvailable, supabase.SkipReason ?? string.Empty);
        var otherUser = await supabase.GetOtherUserAsync();
        var others = await new GroceryItemBuilder().InsertAsync(otherUser.Client);

        // RLS hides the row, so the delete matches nothing
        ResultAssert.Failed(await DeleteAsync(others), "The grocery item could not be found.");

        var response = await otherUser.Client.From<GroceryItemDb>().Where(x => x.Id == others.Id).Get(CancellationToken);
        Assert.Single(response.Models);
    }

    [Fact]
    public async Task AddMealPlanIngredientsToGroceryList_InsertsThePlansIngredientsAndReturnsTheirIds()
    {
        Assert.SkipUnless(supabase.IsAvailable, supabase.SkipReason ?? string.Empty);
        var meal = await new MealBuilder()
            .WithIngredient(i => i.WithName("Flour").WithAmount("500 g"))
            .WithIngredient(i => i.WithName("Yeast").WithAmount("1 packet"))
            .WithIngredient(i => i.WithName("Water").WithAmount("3 dl").ShowOnGroceryList(false))
            .InsertAsync(supabase.Client);
        var monday = new DateTime(2032, 2, 2);
        await new MealPlanBuilder().ForWeekOf(monday).WithMeal(monday, meal).InsertAsync(supabase.Client);

        // What AddPlanIngredientsToGroceryListDrawer does: load the week's plan, then turn the planned meals'
        // ingredients that are shown on the grocery list into grocery items (none of them are on the list here).
        var plan = ResultAssert.Ok(await new GetMealPlanByDate.Handler(supabase.Client).Handle(new GetMealPlanByDate.Query(monday), CancellationToken));
        Assert.NotNull(plan);
        var items = plan.MealOnDate.Values.SelectMany(m => m.Ingredients)
            .Where(i => i.ShowOnGroceryList)
            .Select(i => new GroceryItem(i)).ToList();

        var ids = ResultAssert.Ok(await new AddMealPlanIngredientsToGroceryList.Handler(supabase.Client)
            .Handle(new AddMealPlanIngredientsToGroceryList.Command(items), CancellationToken));

        Assert.Equal(items.Count, ids.Distinct().Count());
        var response = await supabase.Client.From<GroceryItemDb>().Get(CancellationToken);
        var inserted = response.Models.Where(r => ids.Contains(r.Id)).ToList();
        Assert.Equal(ids.Order(), inserted.Select(r => r.Id).Order());
        Assert.Contains(inserted, r => r is { Name: "Flour", Amount: "500 g", IsPicked: false });
        Assert.Contains(inserted, r => r is { Name: "Yeast", Amount: "1 packet" });
        Assert.All(inserted, r => Assert.Equal(supabase.UserId, r.CreatedBy));
        Assert.DoesNotContain(inserted, r => r.Name == "Water");
    }

    [Fact]
    public async Task AddMealPlanIngredientsToGroceryList_ItemWithoutAnOwner_FailsAndInsertsNothing()
    {
        Assert.SkipUnless(supabase.IsAvailable, supabase.SkipReason ?? string.Empty);
        var marker = $"Batch {ClientExtensions.UniqueSuffix()}";
        var owned = new GroceryItemBuilder().WithName($"{marker} owned").Build();
        owned.CreatedBy = supabase.UserId;
        owned.CreatedAt = DateTime.UtcNow;
        // The handler copies created_by from the items (from the ingredients), so an item without one breaks RLS.
        var unowned = new GroceryItemBuilder().WithName($"{marker} unowned").Build();
        unowned.CreatedAt = DateTime.UtcNow;

        var result = await new AddMealPlanIngredientsToGroceryList.Handler(supabase.Client)
            .Handle(new AddMealPlanIngredientsToGroceryList.Command([owned, unowned]), CancellationToken);

        ResultAssert.Failed(result);
        var response = await supabase.Client.From<GroceryItemDb>().Get(CancellationToken);
        Assert.DoesNotContain(response.Models, r => r.Name.StartsWith(marker));
    }

    private Task<FluentResults.Result<int>> UpdateAsync(GroceryItem item) =>
        new UpdateGroceryItem.Handler(supabase.Client).Handle(new UpdateGroceryItem.Command(item), CancellationToken);

    private Task<FluentResults.Result<int>> DeleteAsync(GroceryItem item) =>
        new DeleteGroceryItem.Handler(supabase.Client).Handle(new DeleteGroceryItem.Command(item), CancellationToken);

    private async Task<GroceryItemDb?> GetRowAsync(int id)
    {
        var response = await supabase.Client.From<GroceryItemDb>().Where(x => x.Id == id).Get(CancellationToken);
        return response.Models.SingleOrDefault();
    }
}
