using MealWiz.Shared.Features.GroceryCategories;
using MealWiz.Shared.Features.GroceryCategories.AddGroceryCategory;
using MealWiz.Shared.Features.GroceryCategories.CreateDefaultGroceryCategories;
using MealWiz.Shared.Features.GroceryCategories.GetGroceryCategories;
using MealWiz.Shared.Features.GroceryCategories.Models;
using MealWiz.Shared.Features.GroceryCategories.UpdateGroceryCategory;
using MealWiz.Shared.Helpers;
using MealWiz.Shared.Tests.TestInfrastructure;
using Xunit;

namespace MealWiz.Shared.Tests.Features.GroceryCategories.IntegrationTests;

/// <summary>Adding and renaming categories against local Supabase. Each test signs in a user of its own.</summary>
[Trait("Category", "Integration")]
public class GroceryCategoryEditHandlerTests(LocalSupabaseFixture supabase) : IClassFixture<LocalSupabaseFixture>
{
    private static CancellationToken CancellationToken => TestContext.Current.CancellationToken;
    private static readonly Microsoft.Extensions.Localization.IStringLocalizer<GroceryCategoriesResource> Localizer =
        TestLocalizer.For<GroceryCategoriesResource>();

    private async Task<(Supabase.Client Client, List<GroceryCategory> Categories)> UserWithDefaultsAsync()
    {
        var user = await supabase.CreateSignedInUserAsync();
        var categories = ResultAssert.Ok(await new CreateDefaultGroceryCategories.Handler(user.Client)
            .Handle(new CreateDefaultGroceryCategories.Command(), CancellationToken));
        return (user.Client, categories);
    }

    private static async Task<List<GroceryCategory>> CategoriesAsync(Supabase.Client client) =>
        ResultAssert.Ok(await new GetGroceryCategories.Handler(client).Handle(new GetGroceryCategories.Query(), CancellationToken));

    [Fact]
    public async Task AddGroceryCategory_LandsJustAboveOther_WithItsEmoji()
    {
        Assert.SkipUnless(supabase.IsAvailable, supabase.SkipReason ?? string.Empty);
        var (client, _) = await UserWithDefaultsAsync();

        var added = ResultAssert.Ok(await new AddGroceryCategory.Handler(client, Localizer)
            .Handle(new AddGroceryCategory.Command(" Health food ", " 🥗 "), CancellationToken));

        Assert.Equal(("Health food", "🥗", (DefaultCategoryKey?)null), (added.Name, added.Emoji, added.DefaultKey));
        var categories = await CategoriesAsync(client);
        Assert.Equal(12, categories.Count);
        Assert.Equal(added.Id, categories[^2].Id);
        Assert.True(categories[^1].IsOther);
    }

    [Fact]
    public async Task UpdateGroceryCategory_RenamesAndSetsTheEmoji_AndAnEmptyEmojiClearsIt()
    {
        Assert.SkipUnless(supabase.IsAvailable, supabase.SkipReason ?? string.Empty);
        var (client, categories) = await UserWithDefaultsAsync();
        var meat = categories.Single(c => c.DefaultKey == DefaultCategoryKey.MeatFish);
        var handler = new UpdateGroceryCategory.Handler(client, Localizer);

        var renamed = ResultAssert.Ok(await handler.Handle(new UpdateGroceryCategory.Command(meat.Id, "Protein", "🥩"), CancellationToken));
        Assert.Equal(("Protein", "🥩", DefaultCategoryKey.MeatFish), (renamed.Name, renamed.Emoji, renamed.DefaultKey!.Value));

        var cleared = ResultAssert.Ok(await handler.Handle(new UpdateGroceryCategory.Command(meat.Id, "Protein", " "), CancellationToken));
        Assert.Null(cleared.Emoji);
    }

    [Fact]
    public async Task UpdateGroceryCategory_AnotherCategorysName_FailsWithTheMessage()
    {
        Assert.SkipUnless(supabase.IsAvailable, supabase.SkipReason ?? string.Empty);
        var (client, categories) = await UserWithDefaultsAsync();
        var handler = new UpdateGroceryCategory.Handler(client, Localizer);
        ResultAssert.Ok(await handler.Handle(new UpdateGroceryCategory.Command(categories[0].Id, "Veg", null), CancellationToken));

        var result = await handler.Handle(new UpdateGroceryCategory.Command(categories[1].Id, "VEG", null), CancellationToken);

        ResultAssert.Failed(result, "A category with that name already exists");
    }

    [Fact]
    public async Task UpdateGroceryCategory_AnotherUsersCategory_FailsAsNotFound()
    {
        Assert.SkipUnless(supabase.IsAvailable, supabase.SkipReason ?? string.Empty);
        var (_, theirs) = await UserWithDefaultsAsync();

        var result = await new UpdateGroceryCategory.Handler(supabase.Client, Localizer)
            .Handle(new UpdateGroceryCategory.Command(theirs[0].Id, "Mine now", null), CancellationToken);

        ResultAssert.Failed(result, "That category no longer exists");
    }

    [Fact]
    public async Task AddGroceryCategory_SignedOut_FailsWithNotSignedIn()
    {
        Assert.SkipUnless(supabase.IsAvailable, supabase.SkipReason ?? string.Empty);
        var signedOut = await supabase.CreateSignedOutClientAsync();

        var result = await new AddGroceryCategory.Handler(signedOut, Localizer).Handle(new AddGroceryCategory.Command("X", null), CancellationToken);

        ResultAssert.Failed(result, ErrorMessages.NotSignedIn);
    }
}
