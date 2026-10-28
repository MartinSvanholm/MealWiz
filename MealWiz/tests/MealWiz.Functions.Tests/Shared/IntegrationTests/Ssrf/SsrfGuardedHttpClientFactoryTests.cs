using System.Net;
using MealWiz.Functions.Shared.Ssrf;
using MealWiz.Functions.Tests.TestInfrastructure;
using Xunit;

namespace MealWiz.Functions.Tests.Shared.IntegrationTests.Ssrf;

/// <summary>
/// The guarded client on its own, over real sockets against <see cref="RecipeTestServer"/>. No outside
/// network needed: the rejected hosts fail before any connection is made.
/// </summary>
[Trait("Category", "Integration")]
public class SsrfGuardedHttpClientFactoryTests(RecipeTestServer server) : IClassFixture<RecipeTestServer>
{
    [Fact]
    public async Task HostResolvingOnlyToPrivateAddresses_IsRejected()
    {
        using var client = new SsrfGuardedHttpClientFactory(new IpAddressGuard()).Create();

        var exception = await Assert.ThrowsAsync<HttpRequestException>(() =>
            client.GetAsync($"http://localhost:{server.BaseUri.Port}/recipe", TestContext.Current.CancellationToken));

        Assert.Contains("did not resolve to a publicly routable address", DescribeChain(exception));
    }

    // .invalid is reserved by RFC 2606 and never resolves.
    [Fact]
    public async Task UnresolvableHost_FailsWithHttpRequestException()
    {
        using var client = new SsrfGuardedHttpClientFactory(new IpAddressGuard()).Create();

        var exception = await Assert.ThrowsAsync<HttpRequestException>(() =>
            client.GetAsync("http://recipe-host.invalid/recipe", TestContext.Current.CancellationToken));

        Assert.Contains("Could not resolve host 'recipe-host.invalid'", DescribeChain(exception));
    }

    // RecipeFetcher follows redirects itself so every hop goes through the guard; the handler must not
    // follow them on its own.
    [Fact]
    public async Task Redirect_IsReturnedNotFollowed()
    {
        using var client = new SsrfGuardedHttpClientFactory(
            new LoopbackAllowingIpAddressGuard(new IpAddressGuard())).Create();

        using var response = await client.GetAsync(server.Url("/redirect/relative"), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/recipe", response.Headers.Location?.OriginalString);
    }

    private static string DescribeChain(Exception exception)
    {
        var messages = new List<string>();
        for (var current = exception; current is not null; current = current.InnerException)
        {
            messages.Add(current.Message);
        }

        return string.Join(" -> ", messages);
    }
}
