using TheMealWorkshop.Shared.Features.GroceryCategories;
using TheMealWorkshop.Shared.Features.GroceryCategories.CreateDefaultGroceryCategories;
using TheMealWorkshop.Shared.Features.GroceryCategories.DeleteGroceryCategory;
using TheMealWorkshop.Shared.Features.GroceryCategories.GetGroceryCategories;
using TheMealWorkshop.Shared.Features.GroceryCategories.GetRememberedGroceryNames;
using TheMealWorkshop.Shared.Features.GroceryCategories.Models;
using TheMealWorkshop.Shared.Features.GroceryCategories.RememberGroceryName;
using TheMealWorkshop.Shared.Tests.TestInfrastructure;
using Xunit;

namespace TheMealWorkshop.Shared.Tests.Features.GroceryCategories.IntegrationTests;

/// <summary>Deleting a category against local Supabase. Each test signs in a user of its own.</summary>
[Trait("Category", "Integration")]
public class DeleteGroceryCategoryHandlerTests(LocalSupabaseFixture supabase) : IClassFixture<LocalSupabaseFixture>
{
    private static CancellationToken CancellationToken => TestContext.Current.CancellationToken;
    private static readonly Microsoft.Extensions.Localization.IStringLocalizer<GroceryCategoriesResource> Localizer =
        TestLocalizer.For<GroceryCategoriesResource>();

    private static int IdOf(List<GroceryCategory> categories, DefaultCategoryKey key) => categories.Single(c => c.DefaultKey == key).Id;

    [Fact]
    public async Task DeleteGroceryCategory_HandsItsNamesAndDictionaryWordsToThePickedCategory()
    {
        Assert.SkipUnless(supabase.IsAvailable, supabase.SkipReason ?? string.Empty);
        var client = (await supabase.CreateSignedInUserAsync()).Client;
        var categories = ResultAssert.Ok(await new CreateDefaultGroceryCategories.Handler(client)
            .Handle(new CreateDefaultGroceryCategories.Command(), CancellationToken));
        var dairy = IdOf(categories, DefaultCategoryKey.DairyEggs);
        var frozen = IdOf(categories, DefaultCategoryKey.Frozen);
        ResultAssert.Ok(await new RememberGroceryName.Handler(client).Handle(new RememberGroceryName.Command("tofu", dairy), CancellationToken));

        ResultAssert.Ok(await new DeleteGroceryCategory.Handler(client, Localizer).Handle(new DeleteGroceryCategory.Command(dairy, frozen), CancellationToken));

        var after = ResultAssert.Ok(await new GetGroceryCategories.Handler(client).Handle(new GetGroceryCategories.Query(), CancellationToken));
        var names = ResultAssert.Ok(await new GetRememberedGroceryNames.Handler(client).Handle(new GetRememberedGroceryNames.Query(), CancellationToken))
            .ToDictionary(n => n.Name, n => n.CategoryId);
        Assert.DoesNotContain(after, c => c.Id == dairy);
        Assert.Equal(frozen, names["tofu"]);
        Assert.Equal(frozen, CategoryResolver.Resolve("milk", after, names)?.Id);
    }

    [Fact]
    public async Task DeleteGroceryCategory_Other_IsRefused()
    {
        Assert.SkipUnless(supabase.IsAvailable, supabase.SkipReason ?? string.Empty);
        var client = (await supabase.CreateSignedInUserAsync()).Client;
        var categories = ResultAssert.Ok(await new CreateDefaultGroceryCategories.Handler(client)
            .Handle(new CreateDefaultGroceryCategories.Command(), CancellationToken));

        var result = await new DeleteGroceryCategory.Handler(client, Localizer).Handle(
            new DeleteGroceryCategory.Command(IdOf(categories, DefaultCategoryKey.Other), IdOf(categories, DefaultCategoryKey.Pantry)), CancellationToken);

        ResultAssert.Failed(result, "Other can't be deleted");
    }
}
