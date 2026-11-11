using System.Net;
using System.Reflection;
using System.Text;
using MealWiz.Functions.Functions.DeleteAccount;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace MealWiz.Functions.Tests.Functions.DeleteAccount.UnitTests;

/// <summary>The HTTP trigger: request validation and the status it answers with for each handler outcome.</summary>
public class DeleteAccountFunctionTests : IDisposable
{
    private const string ValidBody = """{"password":"hunter2"}""";

    private readonly Mock<IDeleteAccountHandler> _handler = new();
    private readonly IHost _workerHost = new HostBuilder().ConfigureFunctionsWorkerDefaults().Build();
    private readonly FunctionContext _functionContext;

    public DeleteAccountFunctionTests()
    {
        var context = new Mock<FunctionContext>();
        context.SetupProperty(c => c.InstanceServices, _workerHost.Services);
        _functionContext = context.Object;
    }

    [Theory]
    [InlineData("{not json")]
    [InlineData("""{}""")]
    [InlineData("""{"password":""}""")]
    [InlineData("""{"password":"   "}""")]
    public async Task InvalidBody_Returns400WithoutCallingTheHandler(string body)
    {
        var response = await RunAsync(body, "Bearer token");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        VerifyHandlerNotCalled();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("Basic abc")]
    [InlineData("Bearer ")]
    public async Task MissingBearerToken_Returns401WithoutCallingTheHandler(string? authorization)
    {
        var response = await RunAsync(ValidBody, authorization);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        VerifyHandlerNotCalled();
    }

    [Theory]
    [InlineData(DeleteAccountOutcome.Deleted, HttpStatusCode.NoContent)]
    [InlineData(DeleteAccountOutcome.Unauthorized, HttpStatusCode.Unauthorized)]
    [InlineData(DeleteAccountOutcome.WrongPassword, HttpStatusCode.Forbidden)]
    [InlineData(DeleteAccountOutcome.Failed, HttpStatusCode.BadGateway)]
    public async Task Outcome_MapsToStatus_AndPassesTokenAndPassword(DeleteAccountOutcome outcome, HttpStatusCode expected)
    {
        _handler.Setup(h => h.HandleAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(outcome);

        var response = await RunAsync(ValidBody, "Bearer the-token");

        Assert.Equal(expected, response.StatusCode);
        Assert.Equal(0, response.Body.Length);
        _handler.Verify(h => h.HandleAsync("the-token", "hunter2", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public void HttpTrigger_RequiresFunctionKeyAndOnlyAcceptsPost()
    {
        var trigger = typeof(DeleteAccountFunction)
            .GetMethod(nameof(DeleteAccountFunction.Run))!
            .GetParameters()
            .Select(p => p.GetCustomAttribute<HttpTriggerAttribute>())
            .Single(a => a is not null)!;

        Assert.Equal(AuthorizationLevel.Function, trigger.AuthLevel);
        Assert.NotNull(trigger.Methods);
        Assert.Equal(["post"], trigger.Methods);
    }

    private void VerifyHandlerNotCalled() => _handler.Verify(
        h => h.HandleAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);

    private async Task<HttpResponseData> RunAsync(string body, string? authorization)
    {
        var function = new DeleteAccountFunction(_handler.Object, NullLogger<DeleteAccountFunction>.Instance);
        return await function.Run(CreateRequest(body, authorization), TestContext.Current.CancellationToken);
    }

    private HttpRequestData CreateRequest(string body, string? authorization)
    {
        var headers = new HttpHeadersCollection();
        if (authorization is not null) headers.Add("Authorization", authorization);

        var request = new Mock<HttpRequestData>(_functionContext);
        request.SetupGet(r => r.Body).Returns(new MemoryStream(Encoding.UTF8.GetBytes(body)));
        request.SetupGet(r => r.Headers).Returns(headers);
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
