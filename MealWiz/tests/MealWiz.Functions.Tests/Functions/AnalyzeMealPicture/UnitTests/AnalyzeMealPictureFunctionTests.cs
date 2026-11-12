using System.Net;
using System.Reflection;
using System.Text;
using System.Text.Json;
using FluentResults;
using MealWiz.Functions.Functions.AnalyzeMealPicture;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace MealWiz.Functions.Tests.Functions.AnalyzeMealPicture.UnitTests;

/// <summary>The HTTP trigger: request validation and the status it answers with for each handler outcome.</summary>
public class AnalyzeMealPictureFunctionTests : IDisposable
{
    private static readonly Guid JobId = Guid.Parse("3f2a1c9e-1234-4abc-9def-0123456789ab");
    private static readonly string ValidBody = $$"""{"jobId":"{{JobId}}"}""";

    private readonly Mock<IAnalyzeMealPictureHandler> _handler = new();
    private readonly IHost _workerHost = new HostBuilder().ConfigureFunctionsWorkerDefaults().Build();
    private readonly FunctionContext _functionContext;

    public AnalyzeMealPictureFunctionTests()
    {
        var context = new Mock<FunctionContext>();
        context.SetupProperty(c => c.InstanceServices, _workerHost.Services);
        _functionContext = context.Object;
    }

    [Theory]
    [InlineData("{not json")]
    [InlineData("""{"jobId":"not-a-guid"}""")]
    [InlineData("""{"jobId":"00000000-0000-0000-0000-000000000000"}""")]
    [InlineData("{}")]
    public async Task InvalidBody_Returns400WithoutStartingTheAnalysis(string body)
    {
        var response = await RunAsync(body);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        _handler.Verify(h => h.HandleAsync(It.IsAny<AnalyzeMealPictureRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task OtherFieldsInTheBody_AreIgnored()
    {
        _handler.Setup(h => h.HandleAsync(It.IsAny<AnalyzeMealPictureRequest>(), It.IsAny<CancellationToken>())).ReturnsAsync(Result.Ok());

        var response = await RunAsync($$"""{"jobId":"{{JobId}}","paths":["x/y.jpg"],"url":"https://example.com"}""");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        _handler.Verify(h => h.HandleAsync(new AnalyzeMealPictureRequest(JobId), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandlerResolvedTheRow_AcksWithJobId()
    {
        _handler.Setup(h => h.HandleAsync(It.IsAny<AnalyzeMealPictureRequest>(), It.IsAny<CancellationToken>())).ReturnsAsync(Result.Ok());

        var response = await RunAsync(ValidBody);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        response.Body.Position = 0;
        using var json = await JsonDocument.ParseAsync(response.Body, cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal(JobId, json.RootElement.GetProperty("jobId").GetGuid());
    }

    [Fact]
    public async Task HandlerCouldNotReachTheRow_Returns502WithEmptyBody()
    {
        _handler.Setup(h => h.HandleAsync(It.IsAny<AnalyzeMealPictureRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Fail("Unauthorized: UNAUTHORIZED_DISABLED_LEGACY_KEY"));

        var response = await RunAsync(ValidBody);

        Assert.Equal(HttpStatusCode.BadGateway, response.StatusCode);
        Assert.Equal(0, response.Body.Length);
    }

    [Fact]
    public void HttpTrigger_RequiresFunctionKeyAndOnlyAcceptsPost()
    {
        var trigger = typeof(AnalyzeMealPictureFunction)
            .GetMethod(nameof(AnalyzeMealPictureFunction.Run))!
            .GetParameters()
            .Select(p => p.GetCustomAttribute<HttpTriggerAttribute>())
            .Single(a => a is not null)!;

        Assert.Equal(AuthorizationLevel.Function, trigger.AuthLevel);
        Assert.Equal(["post"], trigger.Methods);
        Assert.Equal("AnalyzeMealPicture", trigger.Route);
    }

    private async Task<HttpResponseData> RunAsync(string body)
    {
        var function = new AnalyzeMealPictureFunction(_handler.Object, NullLogger<AnalyzeMealPictureFunction>.Instance);
        return await function.Run(CreateRequest(body), TestContext.Current.CancellationToken);
    }

    private HttpRequestData CreateRequest(string body)
    {
        var request = new Mock<HttpRequestData>(_functionContext);
        request.SetupGet(r => r.Body).Returns(new MemoryStream(Encoding.UTF8.GetBytes(body)));
        request.Setup(r => r.CreateResponse()).Returns(() =>
        {
            var response = new Mock<HttpResponseData>(_functionContext);
            response.SetupProperty(r => r.StatusCode);
            response.SetupProperty(r => r.Headers, new HttpHeadersCollection());
            response.SetupProperty(r => r.Body, new MemoryStream());
            return response.Object;
        });
        return request.Object;
    }

    public void Dispose() => _workerHost.Dispose();
}
