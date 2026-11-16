using MealWiz.Shared.Features.GroceryCategories.CreateDefaultGroceryCategories;
using MealWiz.Shared.Features.GroceryCategories.GetRememberedGroceryNames;
using MealWiz.Shared.Features.GroceryCategories.Models;
using MealWiz.Shared.Features.GroceryCategories.RememberGroceryName;
using MealWiz.Shared.Helpers;
using MealWiz.Shared.Tests.TestInfrastructure;
using Xunit;

namespace MealWiz.Shared.Tests.Features.GroceryCategories.IntegrationTests;

/// <summary>Remembering a grocery name's category against local Supabase. The tests share the class's user.</summary>
[Trait("Category", "Integration")]
public class RememberedGroceryNamesHandlerTests(LocalSupabaseFixture supabase) : IClassFixture<LocalSupabaseFixture>
{
    private static CancellationToken CancellationToken => TestContext.Current.CancellationToken;

    private async Task<List<GroceryCategory>> CategoriesAsync(Supabase.Client client) =>
        ResultAssert.Ok(await new CreateDefaultGroceryCategories.Handler(client).Handle(new CreateDefaultGroceryCategories.Command(), CancellationToken));

    private async Task<List<RememberedGroceryName>> NamesAsync(Supabase.Client client) =>
        ResultAssert.Ok(await new GetRememberedGroceryNames.Handler(client).Handle(new GetRememberedGroceryNames.Query(), CancellationToken));

    private static int IdOf(List<GroceryCategory> categories, DefaultCategoryKey key) => categories.Single(c => c.DefaultKey == key).Id;

    [Fact]
    public async Task RememberGroceryName_StoresTheNormalisedName_AndAgainMovesIt()
    {
        Assert.SkipUnless(supabase.IsAvailable, supabase.SkipReason ?? string.Empty);
        var categories = await CategoriesAsync(supabase.Client);
        var handler = new RememberGroceryName.Handler(supabase.Client);

        ResultAssert.Ok(await handler.Handle(new RememberGroceryName.Command("  Smoked   Tofu ", IdOf(categories, DefaultCategoryKey.Deli)), CancellationToken));
        ResultAssert.Ok(await handler.Handle(new RememberGroceryName.Command("smoked tofu", IdOf(categories, DefaultCategoryKey.MeatFish)), CancellationToken));

        var name = Assert.Single(await NamesAsync(supabase.Client), n => n.Name == "smoked tofu");
        Assert.Equal(IdOf(categories, DefaultCategoryKey.MeatFish), name.CategoryId);
    }

    [Fact]
    public async Task GetRememberedGroceryNames_DoesNotReturnAnotherUsersNames()
    {
        Assert.SkipUnless(supabase.IsAvailable, supabase.SkipReason ?? string.Empty);
        var other = await supabase.GetOtherUserAsync();
        var theirs = await CategoriesAsync(other.Client);
        ResultAssert.Ok(await new RememberGroceryName.Handler(other.Client)
            .Handle(new RememberGroceryName.Command("their seitan", IdOf(theirs, DefaultCategoryKey.Pantry)), CancellationToken));

        var mine = await NamesAsync(supabase.Client);

        Assert.DoesNotContain(mine, n => n.Name == "their seitan");
    }

    [Fact]
    public async Task RememberGroceryName_AnotherUsersCategory_Fails()
    {
        Assert.SkipUnless(supabase.IsAvailable, supabase.SkipReason ?? string.Empty);
        var other = await supabase.GetOtherUserAsync();
        var theirs = await CategoriesAsync(other.Client);

        var result = await new RememberGroceryName.Handler(supabase.Client)
            .Handle(new RememberGroceryName.Command("tempeh", IdOf(theirs, DefaultCategoryKey.Pantry)), CancellationToken);

        ResultAssert.Failed(result);
    }

    [Fact]
    public async Task RememberGroceryName_SignedOut_FailsWithNotSignedIn()
    {
        Assert.SkipUnless(supabase.IsAvailable, supabase.SkipReason ?? string.Empty);
        var signedOut = await supabase.CreateSignedOutClientAsync();

        var result = await new RememberGroceryName.Handler(signedOut).Handle(new RememberGroceryName.Command("tofu", 1), CancellationToken);

        ResultAssert.Failed(result, ErrorMessages.NotSignedIn);
    }
}
