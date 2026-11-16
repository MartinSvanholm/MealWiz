using MealWiz.Shared.Features.GroceryCategories.CreateDefaultGroceryCategories;
using MealWiz.Shared.Features.GroceryCategories.GetGroceryCategories;
using MealWiz.Shared.Features.GroceryCategories.Models;
using MealWiz.Shared.Helpers;
using MealWiz.Shared.Tests.TestInfrastructure;
using Xunit;

namespace MealWiz.Shared.Tests.Features.GroceryCategories.IntegrationTests;

/// <summary>
/// Runs the GroceryCategories handlers against local Supabase. Each test signs in a fresh user, so it starts with no
/// categories.
/// </summary>
[Trait("Category", "Integration")]
public class GroceryCategoriesHandlerTests(LocalSupabaseFixture supabase) : IClassFixture<LocalSupabaseFixture>
{
    private static CancellationToken CancellationToken => TestContext.Current.CancellationToken;

    private static readonly string[] DefaultOrder =
        ["produce", "bakery", "meat_fish", "deli", "dairy_eggs", "frozen", "pantry", "drinks", "snacks", "household", "other"];

    // GoTrue allows 30 sign-ins per 5 minutes locally, so only tests that need an empty start sign in a new user.
    private Task<TestUser> FreshUserAsync() => supabase.CreateSignedInUserAsync();

    [Fact]
    public async Task CreateDefaultGroceryCategories_CreatesTheElevenDefaultsInOrder_OwnedByTheUser()
    {
        Assert.SkipUnless(supabase.IsAvailable, supabase.SkipReason ?? string.Empty);
        var user = await FreshUserAsync();

        var created = ResultAssert.Ok(await new CreateDefaultGroceryCategories.Handler(user.Client).Handle(new CreateDefaultGroceryCategories.Command(), CancellationToken));

        Assert.Equal(DefaultOrder, created.Select(c => c.DefaultKey!.Value.ToDbValue()));
        Assert.Equal(Enumerable.Range(1, 11), created.Select(c => c.Position));
        Assert.All(created, c =>
        {
            Assert.True(c.Id > 0);
            Assert.Null(c.Name);
            Assert.Null(c.Emoji);
            Assert.Equal(user.Id, c.CreatedBy);
        });

        var read = ResultAssert.Ok(await new GetGroceryCategories.Handler(user.Client).Handle(new GetGroceryCategories.Query(), CancellationToken));
        Assert.Equal(created.Select(c => c.Id), read.Select(c => c.Id));
    }

    [Fact]
    public async Task CreateDefaultGroceryCategories_Twice_StillLeavesOneSet()
    {
        Assert.SkipUnless(supabase.IsAvailable, supabase.SkipReason ?? string.Empty);
        var user = await FreshUserAsync();
        var handler = new CreateDefaultGroceryCategories.Handler(user.Client);

        var first = ResultAssert.Ok(await handler.Handle(new CreateDefaultGroceryCategories.Command(), CancellationToken));
        var second = ResultAssert.Ok(await handler.Handle(new CreateDefaultGroceryCategories.Command(), CancellationToken));

        Assert.Equal(11, second.Count);
        Assert.Equal(first.Select(c => c.Id), second.Select(c => c.Id));
    }

    [Fact]
    public async Task CreateDefaultGroceryCategories_AtOnceFromTwoDevices_LeavesOneSet()
    {
        Assert.SkipUnless(supabase.IsAvailable, supabase.SkipReason ?? string.Empty);
        var user = await FreshUserAsync();
        var handler = new CreateDefaultGroceryCategories.Handler(user.Client);

        var results = await Task.WhenAll(
            handler.Handle(new CreateDefaultGroceryCategories.Command(), CancellationToken),
            handler.Handle(new CreateDefaultGroceryCategories.Command(), CancellationToken));

        Assert.All(results, r => Assert.Equal(11, ResultAssert.Ok(r).Count));
    }

    [Fact]
    public async Task GetGroceryCategories_NewUser_ReturnsNone_EvenWhenAnotherUserHasCategories()
    {
        Assert.SkipUnless(supabase.IsAvailable, supabase.SkipReason ?? string.Empty);
        var user = await FreshUserAsync();
        var other = await supabase.GetOtherUserAsync();
        ResultAssert.Ok(await new CreateDefaultGroceryCategories.Handler(other.Client).Handle(new CreateDefaultGroceryCategories.Command(), CancellationToken));

        var mine = ResultAssert.Ok(await new GetGroceryCategories.Handler(user.Client).Handle(new GetGroceryCategories.Query(), CancellationToken));

        Assert.Empty(mine);
    }

    [Fact]
    public async Task CreateDefaultGroceryCategories_SignedOut_FailsWithNotSignedIn()
    {
        Assert.SkipUnless(supabase.IsAvailable, supabase.SkipReason ?? string.Empty);
        var signedOut = await supabase.CreateSignedOutClientAsync();

        var result = await new CreateDefaultGroceryCategories.Handler(signedOut).Handle(new CreateDefaultGroceryCategories.Command(), CancellationToken);

        Assert.True(result.IsFailed);
        Assert.Equal(ErrorMessages.NotSignedIn, result.Errors[0].Message);
    }
}
