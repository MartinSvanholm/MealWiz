using System.Net;
using System.Reflection;
using System.Text;
using System.Text.Json;
using FluentResults;
using MealWiz.Functions.Functions.ScrapeRecipe;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace MealWiz.Functions.Tests.Functions.ScrapeRecipe.UnitTests;

/// <summary>
/// Covers the HTTP trigger: request validation, and the status it answers with for each handler outcome.
/// The handler is mocked, so each test decides how the import went.
/// </summary>
public class ScrapeRecipeFunctionTests : IDisposable
{
    private static readonly Guid JobId = Guid.Parse("3f2a1c9e-1234-4abc-9def-0123456789ab");
    private static readonly string ValidBody = $$"""{"jobId":"{{JobId}}","url":"https://example.com/recipe"}""";

    private readonly Mock<IScrapeRecipeHandler> _handler = new();
    private readonly IHost _workerHost = CreateWorkerHost();
    private readonly FunctionContext _functionContext;

    public ScrapeRecipeFunctionTests()
    {
        _functionContext = CreateFunctionContext();
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
        _handler.Verify(h => h.HandleAsync(It.IsAny<ScrapeRecipeRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandlerResolvedTheRow_AcksWithJobId()
    {
        SetupHandler(Result.Ok());

        var response = await RunAsync(ValidBody);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        _handler.Verify(h => h.HandleAsync(
            new ScrapeRecipeRequest(JobId, "https://example.com/recipe"), It.IsAny<CancellationToken>()), Times.Once);

        response.Body.Position = 0;
        using var json = await JsonDocument.ParseAsync(response.Body, cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal(JobId, json.RootElement.GetProperty("jobId").GetGuid());
    }

    // The row is still pending, so the client must see a failure to fail the row itself. The handler's
    // error can carry Supabase internals (keys, hosts), so none of it goes into the response.
    [Fact]
    public async Task HandlerCouldNotReachTheRow_Returns502WithEmptyBody()
    {
        SetupHandler(Result.Fail("Unauthorized: UNAUTHORIZED_DISABLED_LEGACY_KEY"));

        var response = await RunAsync(ValidBody);

        Assert.Equal(HttpStatusCode.BadGateway, response.StatusCode);
        Assert.Equal(0, response.Body.Length);
    }

    // The function key is the only thing stopping casual calls (see the project README), so the
    // trigger must not drift to Anonymous or pick up extra verbs.
    [Fact]
    public void HttpTrigger_RequiresFunctionKeyAndOnlyAcceptsPost()
    {
        var trigger = typeof(ScrapeRecipeFunction)
            .GetMethod(nameof(ScrapeRecipeFunction.Run))!
            .GetParameters()
            .Select(p => p.GetCustomAttribute<HttpTriggerAttribute>())
            .Single(a => a is not null)!;

        Assert.Equal(AuthorizationLevel.Function, trigger.AuthLevel);
        Assert.NotNull(trigger.Methods);
        Assert.Equal(["post"], trigger.Methods);
    }

    private void SetupHandler(Result result) => _handler
        .Setup(h => h.HandleAsync(It.IsAny<ScrapeRecipeRequest>(), It.IsAny<CancellationToken>()))
        .ReturnsAsync(result);

    private async Task<HttpResponseData> RunAsync(string body)
    {
        var function = new ScrapeRecipeFunction(_handler.Object, NullLogger<ScrapeRecipeFunction>.Instance);

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
