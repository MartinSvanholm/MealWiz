using System.Net;
using System.Text;
using System.Text.Json;
using MealWiz.Functions.Functions.ScrapeRecipe;
using MealWiz.Functions.Functions.ScrapeRecipe.Parsing;
using MealWiz.Functions.Shared.Fetching;
using MealWiz.Functions.Shared.SupabaseAccess;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace MealWiz.Functions.Tests.Functions.ScrapeRecipe.UnitTests;

/// <summary>
/// Covers the HTTP trigger's request validation. The handler is real, but its Supabase client
/// factory is mocked so each test can check whether the import was actually started.
/// </summary>
public class ScrapeRecipeFunctionTests : IDisposable
{
    private readonly Mock<IServiceRoleSupabaseClientFactory> _clientFactory = new();
    private readonly IHost _workerHost = CreateWorkerHost();
    private readonly FunctionContext _functionContext;

    public ScrapeRecipeFunctionTests()
    {
        _functionContext = CreateFunctionContext();

        // Stops the import right at its first step; these tests only care whether it started.
        _clientFactory
            .Setup(f => f.CreateAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Not used in these tests."));
    }

    [Theory]
    [InlineData("{not json")]
    [InlineData("""{"jobId":"not-a-guid","url":"https://example.com"}""")]
    [InlineData("""{"jobId":"00000000-0000-0000-0000-000000000000","url":"https://example.com"}""")]
    [InlineData("""{"jobId":"3f2a1c9e-1234-4abc-9def-0123456789ab"}""")]
    [InlineData("""{"jobId":"3f2a1c9e-1234-4abc-9def-0123456789ab","url":"  "}""")]
    public async Task InvalidBody_Returns400WithoutStartingTheImport(string body)
    {
        var response = await RunAsync(body);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        _clientFactory.Verify(f => f.CreateAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ValidBody_StartsTheImportAndAcksWithJobId()
    {
        var jobId = Guid.Parse("3f2a1c9e-1234-4abc-9def-0123456789ab");

        var response = await RunAsync($$"""{"jobId":"{{jobId}}","url":"https://example.com/recipe"}""");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        _clientFactory.Verify(f => f.CreateAsync(It.IsAny<CancellationToken>()), Times.Once);

        response.Body.Position = 0;
        using var json = await JsonDocument.ParseAsync(response.Body, cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal(jobId, json.RootElement.GetProperty("jobId").GetGuid());
    }

    private async Task<HttpResponseData> RunAsync(string body)
    {
        var handler = new ScrapeRecipeHandler(
            _clientFactory.Object,
            Mock.Of<IRecipeFetcher>(),
            Mock.Of<IRecipeParser>(),
            Mock.Of<IRecipeMapper>(),
            NullLogger<ScrapeRecipeHandler>.Instance);

        var function = new ScrapeRecipeFunction(handler, NullLogger<ScrapeRecipeFunction>.Instance);

        return await function.Run(CreateRequest(body), TestContext.Current.CancellationToken);
    }

    private HttpRequestData CreateRequest(string body)
    {
        var request = new Mock<HttpRequestData>(_functionContext);
        request.SetupGet(r => r.Body).Returns(new MemoryStream(Encoding.UTF8.GetBytes(body)));
        request.Setup(r => r.CreateResponse()).Returns(() => CreateResponse());
        return request.Object;
    }

    private HttpResponseData CreateResponse()
    {
        var response = new Mock<HttpResponseData>(_functionContext);
        response.SetupProperty(r => r.StatusCode);
        response.SetupProperty(r => r.Headers, new HttpHeadersCollection());
        response.SetupProperty(r => r.Body, new MemoryStream());
        return response.Object;
    }

    public void Dispose() => _workerHost.Dispose();

    // ReadFromJsonAsync/WriteAsJsonAsync resolve the serializer from FunctionContext.InstanceServices.
    // Building a host the same way Program.cs does (ConfigureFunctionsWorkerDefaults) gives the
    // test the deployed function's exact serializer settings. AddFunctionsWorkerCore() alone is not
    // enough: it leaves property matching case-sensitive, so "jobId" would never bind to JobId.
    private static IHost CreateWorkerHost() => new HostBuilder().ConfigureFunctionsWorkerDefaults().Build();

    private FunctionContext CreateFunctionContext()
    {
        var context = new Mock<FunctionContext>();
        context.SetupProperty(c => c.InstanceServices, _workerHost.Services);
        return context.Object;
    }
}
