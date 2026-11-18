using TheMealWorkshop.Shared.Features.GroceryCategories;
using TheMealWorkshop.Shared.Features.GroceryCategories.ApplyLearnedSectionOrder;
using TheMealWorkshop.Shared.Features.GroceryCategories.CreateDefaultGroceryCategories;
using TheMealWorkshop.Shared.Features.GroceryCategories.GetGroceryCategories;
using TheMealWorkshop.Shared.Features.GroceryCategories.GetGroceryListSettings;
using TheMealWorkshop.Shared.Features.GroceryCategories.ReorderGroceryCategories;
using TheMealWorkshop.Shared.Features.GroceryCategories.Models;
using TheMealWorkshop.Shared.Features.GroceryCategories.SetSectionOrderMode;
using TheMealWorkshop.Shared.Helpers;
using TheMealWorkshop.Shared.Tests.TestInfrastructure;
using Xunit;

namespace TheMealWorkshop.Shared.Tests.Features.GroceryCategories.IntegrationTests;

/// <summary>The section order's settings, reorder and learned order against local Supabase.</summary>
[Trait("Category", "Integration")]
public class SectionOrderHandlerTests(LocalSupabaseFixture supabase) : IClassFixture<LocalSupabaseFixture>
{
    private static CancellationToken CancellationToken => TestContext.Current.CancellationToken;

    private static async Task<GroceryListSettings> SettingsAsync(Supabase.Client client) =>
        ResultAssert.Ok(await new GetGroceryListSettings.Handler(client).Handle(new GetGroceryListSettings.Query(), CancellationToken));

    [Fact]
    public async Task GetGroceryListSettings_NewUser_IsLearnedWithNothingLearned()
    {
        Assert.SkipUnless(supabase.IsAvailable, supabase.SkipReason ?? string.Empty);
        var client = (await supabase.CreateSignedInUserAsync()).Client;

        Assert.Equal(new GroceryListSettings(SectionOrderMode.Learned, null), await SettingsAsync(client));
    }

    [Fact]
    public async Task SetSectionOrderMode_RoundTrips()
    {
        Assert.SkipUnless(supabase.IsAvailable, supabase.SkipReason ?? string.Empty);
        var client = (await supabase.CreateSignedInUserAsync()).Client;
        var handler = new SetSectionOrderMode.Handler(client);

        ResultAssert.Ok(await handler.Handle(new SetSectionOrderMode.Command(SectionOrderMode.Manual), CancellationToken));
        Assert.Equal(SectionOrderMode.Manual, (await SettingsAsync(client)).OrderMode);

        ResultAssert.Ok(await handler.Handle(new SetSectionOrderMode.Command(SectionOrderMode.Learned), CancellationToken));
        Assert.Equal(SectionOrderMode.Learned, (await SettingsAsync(client)).OrderMode);
    }

    [Fact]
    public async Task SetSectionOrderMode_SignedOut_FailsWithNotSignedIn()
    {
        Assert.SkipUnless(supabase.IsAvailable, supabase.SkipReason ?? string.Empty);
        var signedOut = await supabase.CreateSignedOutClientAsync();

        var result = await new SetSectionOrderMode.Handler(signedOut).Handle(new SetSectionOrderMode.Command(SectionOrderMode.Manual), CancellationToken);

        ResultAssert.Failed(result, ErrorMessages.NotSignedIn);
    }

    private static readonly Microsoft.Extensions.Localization.IStringLocalizer<GroceryCategoriesResource> Localizer =
        TestLocalizer.For<GroceryCategoriesResource>();

    private static async Task<List<GroceryCategory>> DefaultsAsync(Supabase.Client client) =>
        ResultAssert.Ok(await new CreateDefaultGroceryCategories.Handler(client).Handle(new CreateDefaultGroceryCategories.Command(), CancellationToken));

    [Fact]
    public async Task ApplyLearnedSectionOrder_WritesPositionsScoresAndLearnedUntil()
    {
        Assert.SkipUnless(supabase.IsAvailable, supabase.SkipReason ?? string.Empty);
        var client = (await supabase.CreateSignedInUserAsync()).Client;
        var categories = await DefaultsAsync(client);
        var learnedUntil = new DateTime(2026, 11, 15, 10, 20, 0, DateTimeKind.Utc);
        var orders = SectionOrderLearner.Learn(categories, [categories[4].Id, categories[1].Id]);

        ResultAssert.Ok(await new ApplyLearnedSectionOrder.Handler(client, Localizer)
            .Handle(new ApplyLearnedSectionOrder.Command(orders, learnedUntil), CancellationToken));

        var after = ResultAssert.Ok(await new GetGroceryCategories.Handler(client).Handle(new GetGroceryCategories.Query(), CancellationToken));
        Assert.Equal(orders.Select(o => (o.CategoryId, o.Position, o.Score)), after.Select(c => (c.Id, c.Position, c.OrderScore ?? -1)));
        Assert.Equal(learnedUntil, (await SettingsAsync(client)).LearnedUntil?.ToUniversalTime());
    }

    [Fact]
    public async Task ApplyLearnedSectionOrder_WithNoCategories_OnlyMovesLearnedUntil()
    {
        Assert.SkipUnless(supabase.IsAvailable, supabase.SkipReason ?? string.Empty);
        var client = (await supabase.CreateSignedInUserAsync()).Client;
        await DefaultsAsync(client);
        var learnedUntil = new DateTime(2026, 11, 15, 12, 0, 0, DateTimeKind.Utc);

        ResultAssert.Ok(await new ApplyLearnedSectionOrder.Handler(client, Localizer)
            .Handle(new ApplyLearnedSectionOrder.Command([], learnedUntil), CancellationToken));

        Assert.Equal(learnedUntil, (await SettingsAsync(client)).LearnedUntil?.ToUniversalTime());
    }

    [Fact]
    public async Task ReorderGroceryCategories_WritesThePositionsInOrder_AndStopsLearning()
    {
        Assert.SkipUnless(supabase.IsAvailable, supabase.SkipReason ?? string.Empty);
        var client = (await supabase.CreateSignedInUserAsync()).Client;
        var categories = await DefaultsAsync(client);
        var ids = categories.Select(c => c.Id).ToList();
        (ids[0], ids[3]) = (ids[3], ids[0]);

        ResultAssert.Ok(await new ReorderGroceryCategories.Handler(client, Localizer)
            .Handle(new ReorderGroceryCategories.Command(ids), CancellationToken));

        var after = ResultAssert.Ok(await new GetGroceryCategories.Handler(client).Handle(new GetGroceryCategories.Query(), CancellationToken));
        Assert.Equal(ids, after.Select(c => c.Id));
        Assert.Equal(SectionOrderMode.Manual, (await SettingsAsync(client)).OrderMode);
    }

    [Fact]
    public async Task ReorderGroceryCategories_WithAnotherUsersCategory_Fails()
    {
        Assert.SkipUnless(supabase.IsAvailable, supabase.SkipReason ?? string.Empty);
        var client = (await supabase.CreateSignedInUserAsync()).Client;
        var mine = await DefaultsAsync(client);
        var theirs = await DefaultsAsync((await supabase.CreateSignedInUserAsync()).Client);
        var ids = mine.Select(c => c.Id).ToList();
        ids[0] = theirs[0].Id;

        var result = await new ReorderGroceryCategories.Handler(client, Localizer)
            .Handle(new ReorderGroceryCategories.Command(ids), CancellationToken);

        ResultAssert.Failed(result, "That category no longer exists");
    }
}
