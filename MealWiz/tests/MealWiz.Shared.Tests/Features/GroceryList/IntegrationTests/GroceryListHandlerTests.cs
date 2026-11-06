using MealWiz.Shared.Features.GroceryList.AddGroceryItem;
using MealWiz.Shared.Features.GroceryList.AddPlanIngredientsToGroceryList;
using MealWiz.Shared.Features.GroceryList.DeleteGroceryItem;
using MealWiz.Shared.Features.GroceryList.DeleteGroceryItems;
using MealWiz.Shared.Features.GroceryList.GetAllGroceryItems;
using MealWiz.Shared.Features.GroceryList.GetGroceryItemSources;
using MealWiz.Shared.Features.GroceryList.Models;
using MealWiz.Shared.Features.GroceryList.UnlinkGroceryItems;
using MealWiz.Shared.Features.GroceryList.UpdateGroceryItem;
using MealWiz.Shared.Features.Ingredients.Models;
using MealWiz.Shared.Features.MealPlans.RemoveMealFromMealPlan;
using MealWiz.Shared.Features.MealPlans.SwapMealInMealPlan;
using MealWiz.Shared.Features.Meals.Models;
using MealWiz.Shared.Features.MealPlans.Models;
using MealWiz.Shared.Helpers;
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
        Assert.True(row.IsManual);
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
    public async Task AddGroceryItem_SignedOut_FailsWithNotSignedIn()
    {
        Assert.SkipUnless(supabase.IsAvailable, supabase.SkipReason ?? string.Empty);
        var signedOut = await supabase.CreateSignedOutClientAsync();

        var result = await new AddGroceryItem.Handler(signedOut)
            .Handle(new AddGroceryItem.Command(new GroceryItemBuilder().Build()), CancellationToken);

        ResultAssert.Failed(result, ErrorMessages.NotSignedIn);
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
    public async Task DeleteGroceryItems_DeletesTheGivenItemsAndKeepsTheOthers()
    {
        Assert.SkipUnless(supabase.IsAvailable, supabase.SkipReason ?? string.Empty);
        var bread = await new GroceryItemBuilder().Picked().InsertAsync(supabase.Client);
        var eggs = await new GroceryItemBuilder().Picked().InsertAsync(supabase.Client);
        var milk = await new GroceryItemBuilder().InsertAsync(supabase.Client);

        ResultAssert.Ok(await DeleteItemsAsync(bread.Id, eggs.Id));

        Assert.Null(await GetRowAsync(bread.Id));
        Assert.Null(await GetRowAsync(eggs.Id));
        Assert.NotNull(await GetRowAsync(milk.Id));
    }

    [Fact]
    public async Task DeleteGroceryItems_AnotherUsersItem_IsKept()
    {
        Assert.SkipUnless(supabase.IsAvailable, supabase.SkipReason ?? string.Empty);
        var own = await new GroceryItemBuilder().Picked().InsertAsync(supabase.Client);
        var otherUser = await supabase.GetOtherUserAsync();
        var others = await new GroceryItemBuilder().Picked().InsertAsync(otherUser.Client);

        // RLS hides the other user's row, so only the user's own item is deleted
        ResultAssert.Ok(await DeleteItemsAsync(own.Id, others.Id));

        Assert.Null(await GetRowAsync(own.Id));
        Assert.NotNull(await GetRowAsync(others.Id, otherUser.Client));
    }

    [Fact]
    public async Task DeleteGroceryItems_NoItems_DeletesNothing()
    {
        Assert.SkipUnless(supabase.IsAvailable, supabase.SkipReason ?? string.Empty);
        var milk = await new GroceryItemBuilder().Picked().InsertAsync(supabase.Client);

        ResultAssert.Ok(await DeleteItemsAsync());

        Assert.NotNull(await GetRowAsync(milk.Id));
    }

    [Fact]
    public async Task DeleteGroceryItems_DeletesTheItemsSources()
    {
        Assert.SkipUnless(supabase.IsAvailable, supabase.SkipReason ?? string.Empty);
        var planned = await PlanAsync(0, supabase.Client, ("Flour", "500 g"));
        var added = ResultAssert.Ok(await AddAsync(planned.Entries()));
        Assert.NotEmpty(await GetSourceRowsAsync(added[0].Id));

        ResultAssert.Ok(await DeleteItemsAsync(added[0].Id));

        Assert.Empty(await GetSourceRowsAsync(added[0].Id));
    }

    [Fact]
    public async Task GetGroceryItemSources_ReturnsTheUsersSourcesWithTheirPlannedDayAndIngredient()
    {
        Assert.SkipUnless(supabase.IsAvailable, supabase.SkipReason ?? string.Empty);
        var planned = await PlanAsync(1, supabase.Client, ("Rice", "2 dl"));
        var added = ResultAssert.Ok(await AddAsync(planned.Entries()));
        var otherUser = await supabase.GetOtherUserAsync();
        var othersPlanned = await PlanAsync(1, otherUser.Client, ("Beans", "1 can"));
        var othersAdded = ResultAssert.Ok(await new AddPlanIngredientsToGroceryList.Handler(otherUser.Client)
            .Handle(new AddPlanIngredientsToGroceryList.Command(othersPlanned.Entries()), CancellationToken));

        var sources = ResultAssert.Ok(await GetSourcesAsync());

        var source = Assert.Single(sources, s => s.GroceryItemId == added[0].Id);
        Assert.Equal(planned.PlannedMealId, source.PlannedMealId);
        Assert.Equal(planned.Meal.Ingredients[0].Id, source.IngredientId);
        Assert.Equal("2 dl", source.Amount);
        Assert.Equal(planned.Date, source.PlannedDate);
        Assert.Equal(planned.Meal.Id, source.PlannedDayMealId);
        Assert.Equal(planned.Meal.Id, source.IngredientMealId);
        Assert.Equal("Rice", source.IngredientName);
        Assert.Equal(planned.Meal.Name, source.MealName);
        Assert.False(source.IsStale);
        Assert.All(sources, s => Assert.Equal(supabase.UserId, s.CreatedBy));
        Assert.DoesNotContain(sources, s => s.GroceryItemId == othersAdded[0].Id);
    }

    [Fact]
    public async Task GetGroceryItemSources_AfterASwap_IsStale()
    {
        Assert.SkipUnless(supabase.IsAvailable, supabase.SkipReason ?? string.Empty);
        var planned = await PlanAsync(2, supabase.Client, ("Pasta", "400 g"));
        var added = ResultAssert.Ok(await AddAsync(planned.Entries()));
        var other = await new MealBuilder().InsertAsync(supabase.Client);

        ResultAssert.Ok(await new SwapMealInMealPlan.Handler(supabase.Client)
            .Handle(new SwapMealInMealPlan.Command(planned.Plan.Id, planned.Date, planned.Meal.Id, other.Id), CancellationToken));

        var source = Assert.Single(ResultAssert.Ok(await GetSourcesAsync()), s => s.GroceryItemId == added[0].Id);
        Assert.Equal(planned.PlannedMealId, source.PlannedMealId);
        Assert.Equal(other.Id, source.PlannedDayMealId);
        Assert.Equal(planned.Meal.Id, source.IngredientMealId);
        Assert.True(source.IsStale);
    }

    [Fact]
    public async Task GetGroceryItemSources_AfterTheIngredientIsDeleted_HasNoIngredientAndIsStale()
    {
        Assert.SkipUnless(supabase.IsAvailable, supabase.SkipReason ?? string.Empty);
        var planned = await PlanAsync(3, supabase.Client, ("Cream", "2 dl"));
        var added = ResultAssert.Ok(await AddAsync(planned.Entries()));
        var ingredientId = planned.Meal.Ingredients[0].Id;

        await supabase.Client.From<IngredientDb>().Where(x => x.Id == ingredientId).Delete(cancellationToken: CancellationToken);

        var source = Assert.Single(ResultAssert.Ok(await GetSourcesAsync()), s => s.GroceryItemId == added[0].Id);
        Assert.Null(source.IngredientId);
        Assert.Null(source.IngredientName);
        Assert.Equal(planned.Meal.Name, source.MealName);
        Assert.True(source.IsStale);
    }

    [Fact]
    public async Task GetGroceryItemSources_AfterTheMealIsRemovedFromThePlan_HasNoPlannedDayAndIsStale()
    {
        Assert.SkipUnless(supabase.IsAvailable, supabase.SkipReason ?? string.Empty);
        var planned = await PlanAsync(4, supabase.Client, ("Butter", "50 g"));
        var added = ResultAssert.Ok(await AddAsync(planned.Entries()));

        ResultAssert.Ok(await new RemoveMealFromMealPlan.Handler(supabase.Client)
            .Handle(new RemoveMealFromMealPlan.Command(planned.Meal.Id, planned.Plan.Id, planned.Date), CancellationToken));

        var source = Assert.Single(ResultAssert.Ok(await GetSourcesAsync()), s => s.GroceryItemId == added[0].Id);
        Assert.Null(source.PlannedMealId);
        Assert.Null(source.PlannedDate);
        Assert.Equal("Butter", source.IngredientName);
        Assert.True(source.IsStale);
    }

    [Fact]
    public async Task AddPlanIngredientsToGroceryList_NewIngredients_InsertsPlanItemsWithASourceEach()
    {
        Assert.SkipUnless(supabase.IsAvailable, supabase.SkipReason ?? string.Empty);
        var planned = await PlanAsync(5, supabase.Client, ("Flour", "500 g"), ("Yeast", "1 packet"));

        var added = ResultAssert.Ok(await AddAsync(planned.Entries()));

        Assert.Equal(2, added.Count);
        var flour = Assert.Single(added, i => i.Name == "Flour");
        Assert.Equal("500 g", flour.Amount);
        Assert.False(flour.IsManual);
        Assert.False(flour.IsPicked);
        Assert.Equal(supabase.UserId, flour.CreatedBy);
        foreach (var item in added)
        {
            Assert.False((await GetRowAsync(item.Id))!.IsManual);
            var source = Assert.Single(await GetSourceRowsAsync(item.Id));
            var ingredient = planned.Meal.Ingredients.Single(i => i.Name == item.Name);
            Assert.Equal((planned.PlannedMealId, ingredient.Id, ingredient.Amount), (source.PlannedMealId, source.IngredientId, source.Amount));
            Assert.Equal(supabase.UserId, source.CreatedBy);
        }
    }

    [Fact]
    public async Task AddPlanIngredientsToGroceryList_ExistingItem_UpdatesItsAmountKeepsItManualAndLinksIt()
    {
        Assert.SkipUnless(supabase.IsAvailable, supabase.SkipReason ?? string.Empty);
        var milk = await new GroceryItemBuilder().WithName("Milk").WithAmount("1 l").InsertAsync(supabase.Client);
        var planned = await PlanAsync(6, supabase.Client, ("milk", "3 dl"));

        var added = ResultAssert.Ok(await AddAsync(planned.Entries(existingId: milk.Id, finalAmount: "1.3 l")));

        var updated = Assert.Single(added);
        Assert.Equal((milk.Id, "1.3 l"), (updated.Id, updated.Amount));
        var row = await GetRowAsync(milk.Id);
        Assert.NotNull(row);
        Assert.Equal("1.3 l", row.Amount);
        Assert.Equal(milk.Name, row.Name);
        Assert.True(row.IsManual);
        Assert.NotNull(row.UpdatedAt);
        var source = Assert.Single(await GetSourceRowsAsync(milk.Id));
        Assert.Equal((planned.PlannedMealId, "3 dl"), (source.PlannedMealId, source.Amount));
    }

    [Fact]
    public async Task AddPlanIngredientsToGroceryList_SameNameFromTwoPlannedDays_InsertsOneItemWithTwoSources()
    {
        Assert.SkipUnless(supabase.IsAvailable, supabase.SkipReason ?? string.Empty);
        var first = await new MealBuilder().WithIngredient(i => i.WithName("Tomatoes").WithAmount("400 g")).InsertAsync(supabase.Client);
        var second = await new MealBuilder().WithIngredient(i => i.WithName("tomatoes").WithAmount("200 g")).InsertAsync(supabase.Client);
        var monday = Monday(7);
        var plan = await new MealPlanBuilder().ForWeekOf(monday).WithMeal(monday, first).WithMeal(monday.AddDays(1), second).InsertAsync(supabase.Client);
        var items = new List<PlanIngredientToAdd>
        {
            new(plan.PlannedMealIdOnDate[monday], first.Ingredients[0], first.Name, null, "600 g"),
            new(plan.PlannedMealIdOnDate[monday.AddDays(1)], second.Ingredients[0], second.Name, null, "600 g")
        };

        var added = ResultAssert.Ok(await AddAsync(items));

        var item = Assert.Single(added);
        Assert.Equal(("Tomatoes", "600 g"), (item.Name, item.Amount));
        var sources = await GetSourceRowsAsync(item.Id);
        Assert.Equal(["200 g", "400 g"], sources.Select(s => s.Amount).Order());
    }

    [Fact]
    public async Task AddPlanIngredientsToGroceryList_AlreadyLinkedIngredient_IsSkipped()
    {
        Assert.SkipUnless(supabase.IsAvailable, supabase.SkipReason ?? string.Empty);
        var planned = await PlanAsync(8, supabase.Client, ("Oats", "100 g"));
        var first = ResultAssert.Ok(await AddAsync(planned.Entries()));

        var second = ResultAssert.Ok(await AddAsync(planned.Entries()));

        Assert.Single(first);
        Assert.Empty(second);
        Assert.Single(await GetSourceRowsAsync(first[0].Id));
    }

    [Fact]
    public async Task AddPlanIngredientsToGroceryList_EmptyCommand_ReturnsNoItems()
    {
        Assert.SkipUnless(supabase.IsAvailable, supabase.SkipReason ?? string.Empty);

        Assert.Empty(ResultAssert.Ok(await AddAsync([])));
    }

    [Fact]
    public async Task AddPlanIngredientsToGroceryList_SignedOut_FailsWithNotSignedIn()
    {
        Assert.SkipUnless(supabase.IsAvailable, supabase.SkipReason ?? string.Empty);
        var signedOut = await supabase.CreateSignedOutClientAsync();
        var entry = new PlanIngredientToAdd(1, new Ingredient { Id = 1, Name = "Salt" }, "Soup", null, "1 tsp");

        var result = await new AddPlanIngredientsToGroceryList.Handler(signedOut)
            .Handle(new AddPlanIngredientsToGroceryList.Command([entry]), CancellationToken);

        ResultAssert.Failed(result, ErrorMessages.NotSignedIn);
    }

    [Fact]
    public async Task AddPlanIngredientsToGroceryList_UnsavedIngredient_FailsWithoutWriting()
    {
        Assert.SkipUnless(supabase.IsAvailable, supabase.SkipReason ?? string.Empty);
        var name = $"Unsaved {ClientExtensions.UniqueSuffix()}";
        var entry = new PlanIngredientToAdd(5, new Ingredient { Id = 0, Name = name, Amount = "1" }, "Soup", null, "1");

        ResultAssert.Failed(await AddAsync([entry]), "Only saved ingredients of planned meals can be added.");

        var response = await supabase.Client.From<GroceryItemDb>().Get(CancellationToken);
        Assert.DoesNotContain(response.Models, r => r.Name == name);
    }

    [Fact]
    public async Task AddPlanIngredientsToGroceryList_AnotherUsersExistingItem_FailsAndLinksNothing()
    {
        Assert.SkipUnless(supabase.IsAvailable, supabase.SkipReason ?? string.Empty);
        var otherUser = await supabase.GetOtherUserAsync();
        var others = await new GroceryItemBuilder().WithAmount("1 l").InsertAsync(otherUser.Client);
        var planned = await PlanAsync(9, supabase.Client, ("Honey", "2 dl"));

        // RLS hides the other user's row, so the update matches nothing
        ResultAssert.Failed(await AddAsync(planned.Entries(existingId: others.Id, finalAmount: "9 l")), "The grocery item could not be found.");

        Assert.Equal("1 l", (await GetRowAsync(others.Id, otherUser.Client))?.Amount);
        Assert.Empty(await GetSourceRowsAsync(others.Id, otherUser.Client));
    }

    [Fact]
    public async Task GroceryItemSources_SamePlannedDayAndIngredientTwice_IsRejected()
    {
        Assert.SkipUnless(supabase.IsAvailable, supabase.SkipReason ?? string.Empty);
        var planned = await PlanAsync(10, supabase.Client, ("Sugar", "1 dl"));
        var item = await new GroceryItemBuilder().InsertAsync(supabase.Client);
        GroceryItemSourceDb NewSource() => new()
        {
            GroceryItemId = item.Id,
            PlannedMealId = planned.PlannedMealId,
            IngredientId = planned.Meal.Ingredients[0].Id,
            Amount = "1 dl",
            CreatedBy = supabase.UserId,
            CreatedAt = DateTime.UtcNow
        };
        await supabase.Client.From<GroceryItemSourceDb>().Insert(NewSource(), cancellationToken: CancellationToken);

        await Assert.ThrowsAsync<Supabase.Postgrest.Exceptions.PostgrestException>(async () =>
            await supabase.Client.From<GroceryItemSourceDb>().Insert(NewSource(), cancellationToken: CancellationToken));
    }

    [Fact]
    public async Task UnlinkGroceryItems_DeletesTheItemsSourcesAndMarksItManual()
    {
        Assert.SkipUnless(supabase.IsAvailable, supabase.SkipReason ?? string.Empty);
        var (_, _, items) = await PlanAndAddAsync(supabase.Client, StaleWeek(0), ("Rice", "2 dl"));

        ResultAssert.Ok(await UnlinkAsync(items[0].Id));

        var row = await GetRowAsync(items[0].Id);
        Assert.NotNull(row);
        Assert.True(row.IsManual);
        Assert.NotNull(row.UpdatedAt);
        Assert.Empty(await GetSourceRowsAsync(items[0].Id));
    }

    [Fact]
    public async Task UnlinkGroceryItems_KeepsOtherItemsSources()
    {
        Assert.SkipUnless(supabase.IsAvailable, supabase.SkipReason ?? string.Empty);
        var (_, _, items) = await PlanAndAddAsync(supabase.Client, StaleWeek(1), ("Rice", "2 dl"), ("Beans", "1 can"));
        var rice = items.Single(i => i.Name == "Rice");
        var beans = items.Single(i => i.Name == "Beans");

        ResultAssert.Ok(await UnlinkAsync(rice.Id));

        Assert.Single(await GetSourceRowsAsync(beans.Id));
        Assert.False((await GetRowAsync(beans.Id))!.IsManual);
    }

    [Fact]
    public async Task UnlinkGroceryItems_AnotherUsersItem_IsLeftAlone()
    {
        Assert.SkipUnless(supabase.IsAvailable, supabase.SkipReason ?? string.Empty);
        var otherUser = await supabase.GetOtherUserAsync();
        var (_, _, items) = await PlanAndAddAsync(otherUser.Client, StaleWeek(2), ("Honey", "2 dl"));

        ResultAssert.Ok(await UnlinkAsync(items[0].Id));

        Assert.Single(await GetSourceRowsAsync(items[0].Id, otherUser.Client));
        Assert.False((await GetRowAsync(items[0].Id, otherUser.Client))!.IsManual);
    }

    [Fact]
    public async Task UnlinkGroceryItems_NoIds_ChangesNothing()
    {
        Assert.SkipUnless(supabase.IsAvailable, supabase.SkipReason ?? string.Empty);
        var (_, _, items) = await PlanAndAddAsync(supabase.Client, StaleWeek(3), ("Rice", "2 dl"));

        ResultAssert.Ok(await UnlinkAsync());

        Assert.Single(await GetSourceRowsAsync(items[0].Id));
        Assert.False((await GetRowAsync(items[0].Id))!.IsManual);
    }

    [Fact]
    public async Task Source_FreshlyAdded_IsNotStale()
    {
        Assert.SkipUnless(supabase.IsAvailable, supabase.SkipReason ?? string.Empty);
        var (_, _, items) = await PlanAndAddAsync(supabase.Client, StaleWeek(4), ("Rice", "2 dl"));

        var sources = ResultAssert.Ok(await GetSourcesAsync());

        Assert.False(Assert.Single(sources, s => s.GroceryItemId == items[0].Id).IsStale);
        Assert.DoesNotContain(StaleGroceryItems.Find(items, sources), i => i.Id == items[0].Id);
    }

    [Fact]
    public async Task Source_AfterTheMealIsRemovedFromThePlan_IsStaleWithTheOldMealsName()
    {
        Assert.SkipUnless(supabase.IsAvailable, supabase.SkipReason ?? string.Empty);
        var (planned, _, items) = await PlanAndAddAsync(supabase.Client, StaleWeek(5), ("Butter", "50 g"));

        ResultAssert.Ok(await new RemoveMealFromMealPlan.Handler(supabase.Client)
            .Handle(new RemoveMealFromMealPlan.Command(planned.Meal.Id, planned.Plan.Id, planned.Date), CancellationToken));

        var sources = ResultAssert.Ok(await GetSourcesAsync());
        var source = Assert.Single(sources, s => s.GroceryItemId == items[0].Id);
        Assert.Null(source.PlannedMealId);
        Assert.True(source.IsStale);
        Assert.Equal(planned.Meal.Name, source.MealName);
        Assert.Contains(StaleGroceryItems.Find(items, sources), i => i.Id == items[0].Id);
    }

    [Fact]
    public async Task Source_AfterTheMealIsSwapped_IsStaleWithTheOldMealsName()
    {
        Assert.SkipUnless(supabase.IsAvailable, supabase.SkipReason ?? string.Empty);
        var (planned, plannedMealId, items) = await PlanAndAddAsync(supabase.Client, StaleWeek(6), ("Pasta", "400 g"));
        var other = await new MealBuilder().InsertAsync(supabase.Client);

        ResultAssert.Ok(await new SwapMealInMealPlan.Handler(supabase.Client)
            .Handle(new SwapMealInMealPlan.Command(planned.Plan.Id, planned.Date, planned.Meal.Id, other.Id), CancellationToken));

        var sources = ResultAssert.Ok(await GetSourcesAsync());
        var source = Assert.Single(sources, s => s.GroceryItemId == items[0].Id);
        Assert.Equal(plannedMealId, source.PlannedMealId);
        Assert.Equal(other.Id, source.PlannedDayMealId);
        Assert.Equal(planned.Meal.Id, source.IngredientMealId);
        Assert.True(source.IsStale);
        Assert.Equal(planned.Meal.Name, source.MealName);
        Assert.Contains(StaleGroceryItems.Find(items, sources), i => i.Id == items[0].Id);
    }

    [Fact]
    public async Task Source_AfterTheIngredientIsDeleted_IsStale()
    {
        Assert.SkipUnless(supabase.IsAvailable, supabase.SkipReason ?? string.Empty);
        var (planned, _, items) = await PlanAndAddAsync(supabase.Client, StaleWeek(7), ("Cream", "2 dl"));
        var ingredientId = planned.Meal.Ingredients[0].Id;

        await supabase.Client.From<IngredientDb>().Where(x => x.Id == ingredientId).Delete(cancellationToken: CancellationToken);

        var sources = ResultAssert.Ok(await GetSourcesAsync());
        Assert.True(Assert.Single(sources, s => s.GroceryItemId == items[0].Id).IsStale);
        Assert.Contains(StaleGroceryItems.Find(items, sources), i => i.Id == items[0].Id);
    }

    [Fact]
    public async Task Item_LinkedFromTwoDays_StaysCurrentWhenOnlyOneDayIsRemoved()
    {
        Assert.SkipUnless(supabase.IsAvailable, supabase.SkipReason ?? string.Empty);
        var first = await new MealBuilder().WithIngredient(i => i.WithName("Tomatoes").WithAmount("400 g")).InsertAsync(supabase.Client);
        var second = await new MealBuilder().WithIngredient(i => i.WithName("tomatoes").WithAmount("200 g")).InsertAsync(supabase.Client);
        var monday = StaleWeek(8);
        var plan = await new MealPlanBuilder().ForWeekOf(monday).WithMeal(monday, first).WithMeal(monday.AddDays(1), second).InsertAsync(supabase.Client);
        var added = ResultAssert.Ok(await AddAsync(
        [
            new(plan.PlannedMealIdOnDate[monday], first.Ingredients[0], first.Name, null, "600 g"),
            new(plan.PlannedMealIdOnDate[monday.AddDays(1)], second.Ingredients[0], second.Name, null, "600 g")
        ]));

        ResultAssert.Ok(await new RemoveMealFromMealPlan.Handler(supabase.Client)
            .Handle(new RemoveMealFromMealPlan.Command(first.Id, plan.Id, monday), CancellationToken));

        var sources = ResultAssert.Ok(await GetSourcesAsync());
        Assert.Empty(StaleGroceryItems.Find(added, sources));
    }

    [Fact]
    public async Task StaleItem_AfterUnlink_IsNoLongerStale()
    {
        Assert.SkipUnless(supabase.IsAvailable, supabase.SkipReason ?? string.Empty);
        var (planned, _, items) = await PlanAndAddAsync(supabase.Client, StaleWeek(9), ("Oats", "100 g"));
        ResultAssert.Ok(await new RemoveMealFromMealPlan.Handler(supabase.Client)
            .Handle(new RemoveMealFromMealPlan.Command(planned.Meal.Id, planned.Plan.Id, planned.Date), CancellationToken));

        ResultAssert.Ok(await UnlinkAsync(items[0].Id));

        var sources = ResultAssert.Ok(await GetSourcesAsync());
        var reloaded = ResultAssert.Ok(await new GetAllGroceryItems.Handler(supabase.Client)
            .Handle(new GetAllGroceryItems.Query(), CancellationToken));
        Assert.DoesNotContain(StaleGroceryItems.Find(reloaded, sources), i => i.Id == items[0].Id);
    }

    [Fact]
    public async Task StaleItem_DeletedWithDeleteGroceryItems_LosesItsSources()
    {
        Assert.SkipUnless(supabase.IsAvailable, supabase.SkipReason ?? string.Empty);
        var (planned, _, items) = await PlanAndAddAsync(supabase.Client, StaleWeek(10), ("Sugar", "1 dl"));
        ResultAssert.Ok(await new RemoveMealFromMealPlan.Handler(supabase.Client)
            .Handle(new RemoveMealFromMealPlan.Command(planned.Meal.Id, planned.Plan.Id, planned.Date), CancellationToken));

        ResultAssert.Ok(await DeleteItemsAsync(items[0].Id));

        Assert.Null(await GetRowAsync(items[0].Id));
        Assert.Empty(await GetSourceRowsAsync(items[0].Id));
    }

    /// <summary>Each stale-source test plans its own week from 2034 (a plan and a planned date are unique per user).</summary>
    private static DateTime StaleWeek(int n) => new DateTime(2034, 1, 2).AddDays(7 * n);

    /// <summary>Plans a new meal with the ingredients on <paramref name="day"/> and adds its ingredients to the list.</summary>
    private static async Task<(Planned Planned, int PlannedMealId, List<GroceryItem> Items)> PlanAndAddAsync(
        Supabase.Client client, DateTime day, params (string Name, string Amount)[] ingredients)
    {
        var builder = new MealBuilder();
        foreach (var (name, amount) in ingredients) builder.WithIngredient(i => i.WithName(name).WithAmount(amount));
        var meal = await builder.InsertAsync(client);
        var plan = await new MealPlanBuilder().ForWeekOf(day).WithMeal(day, meal).InsertAsync(client);
        var planned = new Planned(plan, meal, day, plan.PlannedMealIdOnDate[day]);
        var result = await new AddPlanIngredientsToGroceryList.Handler(client)
            .Handle(new AddPlanIngredientsToGroceryList.Command(planned.Entries()), CancellationToken);
        return (planned, planned.PlannedMealId, ResultAssert.Ok(result));
    }

    private Task<FluentResults.Result> UnlinkAsync(params int[] ids) =>
        new UnlinkGroceryItems.Handler(supabase.Client).Handle(new UnlinkGroceryItems.Command([.. ids]), CancellationToken);

    /// <summary>The class user's tests each plan their own week (a plan and a planned date are unique per user).</summary>
    private static DateTime Monday(int week) => new DateTime(2032, 2, 2).AddDays(7 * week);

    private sealed record Planned(MealPlan Plan, Meal Meal, DateTime Date, int PlannedMealId)
    {
        public List<PlanIngredientToAdd> Entries(int? existingId = null, string? finalAmount = null) =>
            Meal.Ingredients.ConvertAll(i => new PlanIngredientToAdd(PlannedMealId, i, Meal.Name, existingId, finalAmount ?? i.Amount));
    }

    /// <summary>A meal with the ingredients, planned on the Monday of its own week.</summary>
    private static async Task<Planned> PlanAsync(int week, Supabase.Client client, params (string Name, string Amount)[] ingredients)
    {
        var builder = new MealBuilder();
        foreach (var (name, amount) in ingredients) builder.WithIngredient(i => i.WithName(name).WithAmount(amount));
        var meal = await builder.InsertAsync(client);
        var monday = Monday(week);
        var plan = await new MealPlanBuilder().ForWeekOf(monday).WithMeal(monday, meal).InsertAsync(client);
        return new Planned(plan, meal, monday, plan.PlannedMealIdOnDate[monday]);
    }

    private Task<FluentResults.Result<List<GroceryItem>>> AddAsync(List<PlanIngredientToAdd> items) =>
        new AddPlanIngredientsToGroceryList.Handler(supabase.Client).Handle(new AddPlanIngredientsToGroceryList.Command(items), CancellationToken);

    private Task<FluentResults.Result<List<GroceryItemSource>>> GetSourcesAsync() =>
        new GetGroceryItemSources.Handler(supabase.Client).Handle(new GetGroceryItemSources.Query(), CancellationToken);

    private async Task<List<GroceryItemSourceDb>> GetSourceRowsAsync(int groceryItemId, Supabase.Client? client = null)
    {
        var response = await (client ?? supabase.Client).From<GroceryItemSourceDb>().Where(x => x.GroceryItemId == groceryItemId).Get(CancellationToken);
        return response.Models;
    }

    private Task<FluentResults.Result<int>> UpdateAsync(GroceryItem item) =>
        new UpdateGroceryItem.Handler(supabase.Client).Handle(new UpdateGroceryItem.Command(item), CancellationToken);

    private Task<FluentResults.Result<int>> DeleteAsync(GroceryItem item) =>
        new DeleteGroceryItem.Handler(supabase.Client).Handle(new DeleteGroceryItem.Command(item), CancellationToken);

    private Task<FluentResults.Result> DeleteItemsAsync(params int[] ids) =>
        new DeleteGroceryItems.Handler(supabase.Client).Handle(new DeleteGroceryItems.Command([.. ids]), CancellationToken);

    private async Task<GroceryItemDb?> GetRowAsync(int id, Supabase.Client? client = null)
    {
        var response = await (client ?? supabase.Client).From<GroceryItemDb>().Where(x => x.Id == id).Get(CancellationToken);
        return response.Models.SingleOrDefault();
    }
}
