using FluentResults;
using MealWiz.Functions.Shared.Fetching;
using MealWiz.Functions.Shared.Ssrf;
using MealWiz.Functions.Tests.TestInfrastructure;
using Xunit;

namespace MealWiz.Functions.Tests.Shared.IntegrationTests.Fetching;

/// <summary>
/// Real HTTP over real sockets against <see cref="RecipeTestServer"/>. Doesn't need Supabase.
/// </summary>
[Trait("Category", "Integration")]
public class RecipeFetcherTests(RecipeTestServer server) : IClassFixture<RecipeTestServer>
{
    private readonly RecipeFetcher _fetcher = new(
        new SsrfGuardedHttpClientFactory(new LoopbackAllowingIpAddressGuard(new IpAddressGuard())));

    [Fact]
    public async Task FetchAsync_ReturnsPageBody()
    {
        var result = await FetchAsync("/recipe");

        Assert.True(result.IsSuccess, Describe(result));
        Assert.Contains(RecipeTestServer.RecipeName, result.Value);
    }

    [Fact]
    public async Task FetchAsync_FollowsRelativeRedirect()
    {
        var result = await FetchAsync("/redirect/relative");

        Assert.True(result.IsSuccess, Describe(result));
        Assert.Contains(RecipeTestServer.RecipeName, result.Value);
    }

    [Fact]
    public async Task FetchAsync_FollowsUpToFiveRedirects()
    {
        var result = await FetchAsync("/redirect/chain/5");

        Assert.True(result.IsSuccess, Describe(result));
    }

    [Fact]
    public async Task FetchAsync_FailsOnTheSixthRedirect()
    {
        var result = await FetchAsync("/redirect/chain/6");

        Assert.True(result.IsFailed);
        Assert.Equal("Too many redirects.", Describe(result));
    }

    [Fact]
    public async Task FetchAsync_BlocksRedirectToCloudMetadataEndpoint()
    {
        var result = await FetchAsync("/redirect/metadata");

        Assert.True(result.IsFailed);
        Assert.Contains("169.254.169.254", Describe(result));
        Assert.Contains("publicly routable", Describe(result));
    }

    [Fact]
    public async Task FetchAsync_BlocksRedirectToNonHttpScheme()
    {
        var result = await FetchAsync("/redirect/ftp");

        Assert.True(result.IsFailed);
        Assert.Equal("URL scheme 'ftp' is not allowed.", Describe(result));
    }

    [Fact]
    public async Task FetchAsync_FailsOnNonSuccessStatus()
    {
        var result = await FetchAsync("/missing");

        Assert.True(result.IsFailed);
        Assert.Equal("Request failed with status 404.", Describe(result));
    }

    [Fact]
    public async Task FetchAsync_FailsWhenResponseExceedsSizeCap()
    {
        var result = await FetchAsync("/oversized");

        Assert.True(result.IsFailed);
        Assert.Contains("byte cap", Describe(result));
    }

    [Fact]
    public async Task FetchAsync_WithProductionGuard_BlocksLoopbackServer()
    {
        var productionFetcher = new RecipeFetcher(new SsrfGuardedHttpClientFactory(new IpAddressGuard()));

        var result = await productionFetcher.FetchAsync(new Uri(server.Url("/recipe")), TestContext.Current.CancellationToken);

        Assert.True(result.IsFailed);
        Assert.Contains("publicly routable", Describe(result));
    }

    private Task<Result<string>> FetchAsync(string path) =>
        _fetcher.FetchAsync(new Uri(server.Url(path)), TestContext.Current.CancellationToken);

    private static string Describe(ResultBase result) => string.Join("; ", result.Errors.Select(e => e.Message));
}
