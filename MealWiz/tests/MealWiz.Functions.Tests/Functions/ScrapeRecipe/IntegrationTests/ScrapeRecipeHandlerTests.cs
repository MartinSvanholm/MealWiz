using MealWiz.Functions.Functions.ScrapeRecipe;
using MealWiz.Functions.Functions.ScrapeRecipe.Parsing;
using MealWiz.Functions.Shared.Fetching;
using MealWiz.Functions.Shared.Models;
using MealWiz.Functions.Shared.Ssrf;
using MealWiz.Functions.Shared.SupabaseAccess;
using MealWiz.Functions.Tests.TestInfrastructure;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace MealWiz.Functions.Tests.Functions.ScrapeRecipe.IntegrationTests;

/// <summary>
/// End-to-end through the handler: fetch from <see cref="RecipeTestServer"/>, parse, map, and write
/// back to a real meal_imports row in local Supabase — then read the row back, the same way the
/// client observes the result. Skips (with the reason) when local Supabase isn't running.
/// </summary>
[Trait("Category", "Integration")]
public class ScrapeRecipeHandlerTests(LocalSupabaseFixture supabase, RecipeTestServer server)
    : IClassFixture<LocalSupabaseFixture>, IClassFixture<RecipeTestServer>
{
    [Fact]
    public async Task RecipePage_WritesSucceededStatusAndParsedMeal()
    {
        var row = await ImportAsync(server.Url("/recipe"));

        Assert.Equal(RecipeImportStatus.Succeeded, RecipeImportStatusExtensions.FromDbValue(row.Status));
        Assert.Null(row.ErrorMessage);
        Assert.NotNull(row.UpdatedAt);

        Assert.NotNull(row.ParsedMeal);
        Assert.Equal(RecipeTestServer.RecipeName, row.ParsedMeal.Name);
        Assert.Equal(RecipeTestServer.RecipeInstructions, row.ParsedMeal.Recipe);
        Assert.Equal(RecipeTestServer.RecipeIngredients, row.ParsedMeal.Ingredients.Select(i => i.Name));
        Assert.All(row.ParsedMeal.Ingredients, i => Assert.Equal(string.Empty, i.Amount));
    }

    [Fact]
    public async Task PageWithoutRecipe_WritesFailedStatus()
    {
        var row = await ImportAsync(server.Url("/no-recipe"));

        AssertFailed(row, "Could not find recipe data on the page.");
    }

    [Fact]
    public async Task MissingPage_WritesFailedStatus()
    {
        var row = await ImportAsync(server.Url("/missing"));

        AssertFailed(row, "Request failed with status 404.");
    }

    [Fact]
    public async Task RedirectToCloudMetadataEndpoint_WritesFailedStatus()
    {
        var row = await ImportAsync(server.Url("/redirect/metadata"));

        Assert.Equal(RecipeImportStatus.Failed, RecipeImportStatusExtensions.FromDbValue(row.Status));
        Assert.Null(row.ParsedMeal);
        Assert.Contains("publicly routable", row.ErrorMessage);
    }

    [Fact]
    public async Task NonHttpUrl_WritesFailedStatus()
    {
        var row = await ImportAsync("file:///etc/passwd");

        AssertFailed(row, "The URL must be an absolute http or https URL.");
    }

    [Fact]
    public async Task UnknownJobId_CompletesWithoutCreatingARow()
    {
        Assert.SkipUnless(supabase.IsAvailable, supabase.SkipReason ?? string.Empty);
        var unknownId = Guid.NewGuid();

        await CreateHandler().HandleAsync(
            new ScrapeRecipeRequest(unknownId, server.Url("/recipe")),
            TestContext.Current.CancellationToken);

        Assert.Null(await supabase.GetRowAsync(unknownId));
    }

    private async Task<MealImportDb> ImportAsync(string url)
    {
        Assert.SkipUnless(supabase.IsAvailable, supabase.SkipReason ?? string.Empty);

        var pendingRow = await supabase.InsertPendingRowAsync(url);

        await CreateHandler().HandleAsync(
            new ScrapeRecipeRequest(pendingRow.Id, url),
            TestContext.Current.CancellationToken);

        var row = await supabase.GetRowAsync(pendingRow.Id);
        Assert.NotNull(row);
        return row;
    }

    private ScrapeRecipeHandler CreateHandler() => new(
        new ServiceRoleSupabaseClientFactory(supabase.Configuration),
        new RecipeFetcher(new SsrfGuardedHttpClientFactory(new LoopbackAllowingIpAddressGuard(new IpAddressGuard()))),
        new RecipeParser(new JsonLdRecipeParser(), new MicrodataRecipeParser(), new OpenGraphRecipeParser()),
        new RecipeMapper(),
        NullLogger<ScrapeRecipeHandler>.Instance);

    private static void AssertFailed(MealImportDb row, string expectedError)
    {
        Assert.Equal(RecipeImportStatus.Failed, RecipeImportStatusExtensions.FromDbValue(row.Status));
        Assert.Null(row.ParsedMeal);
        Assert.Equal(expectedError, row.ErrorMessage);
        Assert.NotNull(row.UpdatedAt);
    }
}
