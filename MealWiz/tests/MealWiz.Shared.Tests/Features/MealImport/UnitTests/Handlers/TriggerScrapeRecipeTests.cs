using System.Net;
using FluentResults;
using MealWiz.Shared.Features.MealImport.Models;
using Trigger = MealWiz.Shared.Features.MealImport.TriggerScrapeRecipe.TriggerScrapeRecipe;
using Xunit;

namespace MealWiz.Shared.Tests.Features.MealImport.UnitTests.Handlers;

/// <summary>
/// The container fails a still-pending job only when this command fails, so every non-2xx answer from the
/// Function (502 when it couldn't reach the row) has to come back as a failure.
/// </summary>
public class TriggerScrapeRecipeTests
{
    [Fact]
    public async Task FunctionAcks_Succeeds()
    {
        var result = await SendAsync(HttpStatusCode.OK);

        Assert.True(result.IsSuccess);
    }

    [Theory]
    [InlineData(HttpStatusCode.BadGateway)]
    [InlineData(HttpStatusCode.ServiceUnavailable)]
    [InlineData(HttpStatusCode.InternalServerError)]
    [InlineData(HttpStatusCode.Unauthorized)]
    public async Task FunctionAnswersWithAnError_Fails(HttpStatusCode statusCode)
    {
        var result = await SendAsync(statusCode);

        Assert.True(result.IsFailed);
        Assert.Equal($"The meal import service responded with {(int)statusCode}.", result.Errors[0].Message);
    }

    private static async Task<Result> SendAsync(HttpStatusCode statusCode)
    {
        using var httpClient = new HttpClient(new StubHandler(statusCode));
        var handler = new Trigger.Handler(
            httpClient, new MealImportFunctionOptions("https://functions.example.com", "key"));

        return await handler.Handle(
            new Trigger.Command(Guid.NewGuid(), "https://example.com/recipe"),
            TestContext.Current.CancellationToken);
    }

    private sealed class StubHandler(HttpStatusCode statusCode) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(statusCode));
    }
}
