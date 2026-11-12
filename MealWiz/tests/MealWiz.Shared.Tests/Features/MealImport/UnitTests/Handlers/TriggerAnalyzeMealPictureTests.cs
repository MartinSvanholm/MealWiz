using System.Net;
using System.Text.Json;
using FluentResults;
using MealWiz.Shared.Features.MealImport;
using MealWiz.Shared.Features.MealImport.Models;
using MealWiz.Shared.Tests.TestInfrastructure;
using Trigger = MealWiz.Shared.Features.MealImport.TriggerAnalyzeMealPicture.TriggerAnalyzeMealPicture;
using Xunit;

namespace MealWiz.Shared.Tests.Features.MealImport.UnitTests.Handlers;

/// <summary>Like <see cref="TriggerScrapeRecipeTests"/>: every non-2xx answer from the Function must come back as a failure.</summary>
public class TriggerAnalyzeMealPictureTests
{
    private static readonly Guid JobId = Guid.Parse("3f2a1c9e-1234-4abc-9def-0123456789ab");

    [Fact]
    public async Task FunctionAcks_PostsTheJobIdWithTheKeyToTheAnalyzeEndpoint()
    {
        var stub = new StubHandler(HttpStatusCode.OK);

        var result = await SendAsync(stub, new MealImportFunctionOptions("https://functions.example.com/", "secret"));

        Assert.True(result.IsSuccess);
        Assert.Equal(HttpMethod.Post, stub.Request!.Method);
        Assert.Equal("https://functions.example.com/api/AnalyzeMealPicture", stub.Request.RequestUri!.ToString());
        Assert.Equal("secret", stub.Request.Headers.GetValues("x-functions-key").Single());
        using var body = JsonDocument.Parse(stub.Body!);
        Assert.Equal(JobId, body.RootElement.GetProperty("jobId").GetGuid());
        Assert.False(body.RootElement.TryGetProperty("url", out _));
    }

    [Theory]
    [InlineData(HttpStatusCode.BadGateway)]
    [InlineData(HttpStatusCode.BadRequest)]
    [InlineData(HttpStatusCode.Unauthorized)]
    public async Task FunctionAnswersWithAnError_Fails(HttpStatusCode statusCode)
    {
        var result = await SendAsync(new StubHandler(statusCode), new MealImportFunctionOptions("https://functions.example.com", "key"));

        Assert.True(result.IsFailed);
        Assert.Equal($"The meal import service responded with {(int)statusCode}.", result.Errors[0].Message);
    }

    [Fact]
    public async Task NoFunctionUrl_Fails()
    {
        var stub = new StubHandler(HttpStatusCode.OK);

        var result = await SendAsync(stub, new MealImportFunctionOptions("", ""));

        Assert.True(result.IsFailed);
        Assert.Null(stub.Request);
    }

    private static async Task<Result> SendAsync(StubHandler stub, MealImportFunctionOptions options)
    {
        using var httpClient = new HttpClient(stub);
        var handler = new Trigger.Handler(httpClient, options, TestLocalizer.For<MealImportResource>());
        return await handler.Handle(new Trigger.Command(JobId), TestContext.Current.CancellationToken);
    }

    private sealed class StubHandler(HttpStatusCode statusCode) : HttpMessageHandler
    {
        public HttpRequestMessage? Request { get; private set; }
        public string? Body { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Request = request;
            Body = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
            return new HttpResponseMessage(statusCode);
        }
    }
}
