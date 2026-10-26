using MealWiz.Functions.Functions.ScrapeRecipe;
using MealWiz.Functions.Functions.ScrapeRecipe.Parsing;
using MealWiz.Functions.Shared.Fetching;
using MealWiz.Functions.Shared.SupabaseAccess;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace MealWiz.Functions.Tests.Functions.ScrapeRecipe.UnitTests;

/// <summary>
/// The handler outcomes that need no database. The ones that do are in the IntegrationTests version of this class.
/// </summary>
public class ScrapeRecipeHandlerTests
{
    // E.g. Supabase__ServiceRoleKey missing from the Function App settings: the row can't be reached at all.
    [Fact]
    public async Task SupabaseNotConfigured_FailsWithoutScraping()
    {
        var fetcher = new Mock<IRecipeFetcher>();
        var handler = new ScrapeRecipeHandler(
            new ServiceRoleSupabaseClientFactory(new ConfigurationBuilder().Build()),
            fetcher.Object,
            Mock.Of<IRecipeParser>(),
            Mock.Of<IRecipeMapper>(),
            NullLogger<ScrapeRecipeHandler>.Instance);

        var result = await handler.HandleAsync(
            new ScrapeRecipeRequest(Guid.NewGuid(), "https://example.com/recipe"),
            TestContext.Current.CancellationToken);

        Assert.True(result.IsFailed);
        Assert.Equal("Supabase:Url is not configured.", Assert.Single(result.Errors).Message);
        fetcher.Verify(f => f.FetchAsync(It.IsAny<Uri>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
