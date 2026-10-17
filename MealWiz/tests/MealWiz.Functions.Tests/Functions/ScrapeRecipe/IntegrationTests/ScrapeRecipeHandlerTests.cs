using FluentResults;
using MealWiz.Functions.Functions.ScrapeRecipe;
using MealWiz.Functions.Functions.ScrapeRecipe.Parsing;
using MealWiz.Functions.Shared.Fetching;
using MealWiz.Functions.Shared.Ingredients;
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
        Assert.Collection(row.ParsedMeal.Ingredients,
            i => AssertIngredient(i, "2½ dl", "sødmælk, stuetempereret"),
            i => AssertIngredient(i, "1 knivspids", "salt"),
            i => AssertIngredient(i, "1 (400 g) dåse", "hakkede tomater"),
            i => AssertIngredient(i, string.Empty, "salt og peber"));
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

    [Fact]
    public async Task SucceededRow_IsLeftUntouched()
    {
        Assert.SkipUnless(supabase.IsAvailable, supabase.SkipReason ?? string.Empty);
        var succeededRow = await supabase.InsertRowAsync(new MealImportDb
        {
            SourceUrl = server.Url("/no-recipe"),
            Status = RecipeImportStatus.Succeeded.ToDbValue(),
            ParsedMeal = new MealImportPayload { Name = "Original meal" }
        });

        var row = await HandleAsync(succeededRow.Id, succeededRow.SourceUrl);

        Assert.Equal(RecipeImportStatus.Succeeded, RecipeImportStatusExtensions.FromDbValue(row.Status));
        Assert.NotNull(row.ParsedMeal);
        Assert.Equal("Original meal", row.ParsedMeal.Name);
        Assert.Null(row.ErrorMessage);
        Assert.Null(row.UpdatedAt);
    }

    [Fact]
    public async Task FailedRow_IsLeftUntouched()
    {
        Assert.SkipUnless(supabase.IsAvailable, supabase.SkipReason ?? string.Empty);
        var failedRow = await supabase.InsertRowAsync(new MealImportDb
        {
            SourceUrl = server.Url("/recipe"),
            Status = RecipeImportStatus.Failed.ToDbValue(),
            ErrorMessage = "Original error"
        });

        var row = await HandleAsync(failedRow.Id, failedRow.SourceUrl);

        Assert.Equal(RecipeImportStatus.Failed, RecipeImportStatusExtensions.FromDbValue(row.Status));
        Assert.Null(row.ParsedMeal);
        Assert.Equal("Original error", row.ErrorMessage);
        Assert.Null(row.UpdatedAt);
    }

    [Fact]
    public async Task MismatchedRequestUrl_ScrapesStoredSourceUrl()
    {
        Assert.SkipUnless(supabase.IsAvailable, supabase.SkipReason ?? string.Empty);
        var pendingRow = await supabase.InsertPendingRowAsync(server.Url("/recipe"));

        var row = await HandleAsync(pendingRow.Id, server.Url("/redirect/metadata"));

        Assert.Equal(RecipeImportStatus.Succeeded, RecipeImportStatusExtensions.FromDbValue(row.Status));
        Assert.NotNull(row.ParsedMeal);
        Assert.Equal(RecipeTestServer.RecipeName, row.ParsedMeal.Name);
    }

    [Fact]
    public async Task RowResolvedDuringScrape_KeepsFirstResolution()
    {
        Assert.SkipUnless(supabase.IsAvailable, supabase.SkipReason ?? string.Empty);
        var pendingRow = await supabase.InsertPendingRowAsync(server.Url("/recipe"));

        // Stands in for the client marking the row failed after a timeout while the Function is still scraping.
        var fetcher = new BeforeFetchRecipeFetcher(CreateRecipeFetcher(), () => supabase.Client
            .From<MealImportDb>()
            .Where(x => x.Id == pendingRow.Id)
            .Set(x => x.Status, RecipeImportStatus.Failed.ToDbValue())
            .Set(x => x.ErrorMessage!, "Client timed out")
            .Update());

        var row = await HandleAsync(pendingRow.Id, pendingRow.SourceUrl, fetcher);

        Assert.Equal(RecipeImportStatus.Failed, RecipeImportStatusExtensions.FromDbValue(row.Status));
        Assert.Null(row.ParsedMeal);
        Assert.Equal("Client timed out", row.ErrorMessage);
    }

    private async Task<MealImportDb> ImportAsync(string url)
    {
        Assert.SkipUnless(supabase.IsAvailable, supabase.SkipReason ?? string.Empty);

        var pendingRow = await supabase.InsertPendingRowAsync(url);

        return await HandleAsync(pendingRow.Id, url);
    }

    private async Task<MealImportDb> HandleAsync(Guid jobId, string url, IRecipeFetcher? fetcher = null)
    {
        await CreateHandler(fetcher).HandleAsync(
            new ScrapeRecipeRequest(jobId, url),
            TestContext.Current.CancellationToken);

        var row = await supabase.GetRowAsync(jobId);
        Assert.NotNull(row);
        return row;
    }

    private ScrapeRecipeHandler CreateHandler(IRecipeFetcher? fetcher = null) => new(
        new ServiceRoleSupabaseClientFactory(supabase.Configuration),
        fetcher ?? CreateRecipeFetcher(),
        new RecipeParser(new JsonLdRecipeParser(), new MicrodataRecipeParser(), new OpenGraphRecipeParser()),
        new RecipeMapper(new IngredientLineParser()),
        NullLogger<ScrapeRecipeHandler>.Instance);

    private static RecipeFetcher CreateRecipeFetcher() =>
        new(new SsrfGuardedHttpClientFactory(new LoopbackAllowingIpAddressGuard(new IpAddressGuard())));

    private sealed class BeforeFetchRecipeFetcher(IRecipeFetcher inner, Func<Task> beforeFetch) : IRecipeFetcher
    {
        public async Task<Result<string>> FetchAsync(Uri url, CancellationToken cancellationToken)
        {
            await beforeFetch();
            return await inner.FetchAsync(url, cancellationToken);
        }
    }

    private static void AssertIngredient(MealImportIngredientPayload ingredient, string expectedAmount, string expectedName)
    {
        Assert.Equal(expectedAmount, ingredient.Amount);
        Assert.Equal(expectedName, ingredient.Name);
    }

    private static void AssertFailed(MealImportDb row, string expectedError)
    {
        Assert.Equal(RecipeImportStatus.Failed, RecipeImportStatusExtensions.FromDbValue(row.Status));
        Assert.Null(row.ParsedMeal);
        Assert.Equal(expectedError, row.ErrorMessage);
        Assert.NotNull(row.UpdatedAt);
    }
}
