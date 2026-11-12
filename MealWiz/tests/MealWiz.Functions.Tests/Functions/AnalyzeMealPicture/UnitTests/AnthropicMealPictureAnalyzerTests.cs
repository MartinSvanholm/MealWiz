using System.Net;
using System.Text.Json;
using Anthropic;
using MealWiz.Functions.Functions.AnalyzeMealPicture;
using MealWiz.Functions.Shared.Models;
using MealWiz.Functions.Tests.TestInfrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace MealWiz.Functions.Tests.Functions.AnalyzeMealPicture.UnitTests;

/// <summary>The analyzer through the real SDK client, with the HTTP layer faked: request shape and every error mapping.</summary>
public class AnthropicMealPictureAnalyzerTests
{
    private const string RecipeReading = """
        {"kind":"written_recipe","name":" Æblepandekager ","ingredients":[{"amount":"2 dl","name":"mælk"},{"amount":"","name":"  "}],"recipe":"Pisk dejen.\r\nSteg dem."}
        """;

    private static readonly byte[] PictureOne = [1, 2, 3];
    private static readonly byte[] PictureTwo = [4, 5, 6];

    [Fact]
    public async Task WrittenRecipe_IsReadAsAMealThatIsNotGuessed()
    {
        var result = await AnalyzeAsync(FakeAnthropicHandler.Answering(RecipeReading));

        Assert.True(result.IsSuccess);
        Assert.Equal("Æblepandekager", result.Value.Name);
        Assert.False(result.Value.IsGuessed);
        Assert.Equal("Pisk dejen.\nSteg dem.", result.Value.Recipe);
        var ingredient = Assert.Single(result.Value.Ingredients);
        Assert.Equal(("2 dl", "mælk"), (ingredient.Amount, ingredient.Name));
    }

    [Fact]
    public async Task Dish_IsAGuessedMeal()
    {
        var result = await AnalyzeAsync(FakeAnthropicHandler.Answering(
            """{"kind":"dish","name":"Lasagne","ingredients":[{"amount":"500 g","name":"hakket oksekød"}],"recipe":"Lag lagene."}"""));

        Assert.True(result.IsSuccess);
        Assert.True(result.Value.IsGuessed);
    }

    [Fact]
    public async Task NoMeal_FailsWithNoMealFound() =>
        AssertFailsWith(RecipeImportErrorCode.NoMealFound, await AnalyzeAsync(FakeAnthropicHandler.Answering(
            """{"kind":"no_meal","name":"","ingredients":[],"recipe":""}""")));

    [Fact]
    public async Task Request_HasLabelledPicturesInOrderTheInstructionTheModelLowEffortAndTheSchema()
    {
        var handler = FakeAnthropicHandler.Answering(RecipeReading);

        await AnalyzeAsync(handler, new MealPictureAnalysisRequest([PictureOne, PictureTwo], "da", "  det er æbleflæsk "));

        var request = Assert.Single(handler.Requests).RootElement;
        Assert.Equal("claude-haiku-5-5", request.GetProperty("model").GetString());
        Assert.Equal(4096, request.GetProperty("max_tokens").GetInt32());
        Assert.Equal("low", request.GetProperty("output_config").GetProperty("effort").GetString());
        var format = request.GetProperty("output_config").GetProperty("format");
        Assert.Equal("json_schema", format.GetProperty("type").GetString());
        Assert.Contains("written_recipe", format.GetProperty("schema").GetRawText());
        Assert.False(request.TryGetProperty("temperature", out _));
        Assert.False(request.TryGetProperty("top_p", out _));
        Assert.False(request.TryGetProperty("top_k", out _));

        var content = request.GetProperty("messages")[0].GetProperty("content").EnumerateArray().ToList();
        Assert.Equal(5, content.Count);
        Assert.Equal("Image 1:", content[0].GetProperty("text").GetString());
        Assert.Equal(Convert.ToBase64String(PictureOne), content[1].GetProperty("source").GetProperty("data").GetString());
        Assert.Equal("image/jpeg", content[1].GetProperty("source").GetProperty("media_type").GetString());
        Assert.Equal("Image 2:", content[2].GetProperty("text").GetString());
        Assert.Equal(Convert.ToBase64String(PictureTwo), content[3].GetProperty("source").GetProperty("data").GetString());
        Assert.Equal("Write the meal in Danish.\nHint from the user: det er æbleflæsk", content[4].GetProperty("text").GetString());
    }

    [Fact]
    public async Task ConfiguredModel_IsUsed()
    {
        var handler = FakeAnthropicHandler.Answering(RecipeReading);

        await AnalyzeAsync(handler, model: "claude-sonnet-5-5");

        Assert.Equal("claude-sonnet-5-5", handler.Requests[0].RootElement.GetProperty("model").GetString());
    }

    [Theory]
    [InlineData("refusal")]
    [InlineData("max_tokens")]
    public async Task AnswerThatDidNotEndNormally_FailsWithAiUnavailable(string stopReason) =>
        AssertFailsWith(RecipeImportErrorCode.AiUnavailable,
            await AnalyzeAsync(FakeAnthropicHandler.Answering(RecipeReading, stopReason)));

    [Fact]
    public async Task AnswerThatIsNotTheSchema_FailsWithAiUnavailable() =>
        AssertFailsWith(RecipeImportErrorCode.AiUnavailable, await AnalyzeAsync(FakeAnthropicHandler.Answering("not json")));

    [Theory]
    [InlineData(HttpStatusCode.RequestEntityTooLarge, "request_too_large", "Request exceeds the maximum size.", RecipeImportErrorCode.PictureUnreadable)]
    [InlineData(HttpStatusCode.BadRequest, "invalid_request_error", "messages.0.content.1.image: Could not process image", RecipeImportErrorCode.PictureUnreadable)]
    [InlineData(HttpStatusCode.BadRequest, "invalid_request_error", "You have reached your specified API usage limits. You will regain access on 2026-12-01.", RecipeImportErrorCode.AiUnavailable)]
    [InlineData(HttpStatusCode.BadRequest, "invalid_request_error", "temperature is not supported", RecipeImportErrorCode.AiUnavailable)]
    [InlineData(HttpStatusCode.Unauthorized, "authentication_error", "invalid x-api-key", RecipeImportErrorCode.AiUnavailable)]
    [InlineData(HttpStatusCode.TooManyRequests, "rate_limit_error", "Number of requests exceeded.", RecipeImportErrorCode.AiUnavailable)]
    [InlineData(HttpStatusCode.InternalServerError, "api_error", "Internal server error", RecipeImportErrorCode.AiUnavailable)]
    [InlineData((HttpStatusCode)529, "overloaded_error", "Overloaded", RecipeImportErrorCode.AiUnavailable)]
    public async Task ApiError_MapsToItsCode(HttpStatusCode status, string errorType, string message, RecipeImportErrorCode expected) =>
        AssertFailsWith(expected, await AnalyzeAsync(FakeAnthropicHandler.Failing(status, errorType, message)));

    [Fact]
    public async Task NetworkError_FailsWithAiUnavailable() =>
        AssertFailsWith(RecipeImportErrorCode.AiUnavailable, await AnalyzeAsync(
            new FakeAnthropicHandler((_, _) => throw new HttpRequestException("connection reset"))));

    [Fact]
    public async Task CallThatOutlastsTheBudget_FailsWithTimedOut()
    {
        var handler = new FakeAnthropicHandler(async (_, ct) =>
        {
            await Task.Delay(TimeSpan.FromSeconds(30), ct);
            return FakeAnthropicHandler.Json(HttpStatusCode.OK, FakeAnthropicHandler.Message(RecipeReading));
        });

        var result = await AnalyzeAsync(handler, budget: TimeSpan.FromMilliseconds(200));

        AssertFailsWith(RecipeImportErrorCode.TimedOut, result);
    }

    [Fact]
    public async Task CancelledCaller_FailsWithUnknown()
    {
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();
        var handler = new FakeAnthropicHandler(async (_, ct) =>
        {
            await Task.Delay(TimeSpan.FromSeconds(30), ct);
            return FakeAnthropicHandler.Json(HttpStatusCode.OK, FakeAnthropicHandler.Message(RecipeReading));
        });

        var result = await AnalyzeAsync(handler, cancellationToken: cancelled.Token);

        AssertFailsWith(RecipeImportErrorCode.Unknown, result);
    }

    [Fact]
    public async Task MissingApiKey_NeverCallsTheApi()
    {
        var handler = FakeAnthropicHandler.Answering(RecipeReading);

        var result = await AnalyzeAsync(handler, apiKey: "  ");

        AssertFailsWith(RecipeImportErrorCode.AiUnavailable, result);
        Assert.Empty(handler.Requests);
    }

    private static void AssertFailsWith(RecipeImportErrorCode expected, FluentResults.Result<MealImportPayload> result)
    {
        Assert.True(result.IsFailed);
        var error = Assert.IsType<PictureImportError>(Assert.Single(result.Errors));
        Assert.Equal(expected, error.Code);
    }

    private static Task<FluentResults.Result<MealImportPayload>> AnalyzeAsync(
        FakeAnthropicHandler handler, MealPictureAnalysisRequest? request = null, string? model = null, string apiKey = "test-key",
        TimeSpan? budget = null, CancellationToken cancellationToken = default)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Anthropic:ApiKey"] = apiKey,
            ["Anthropic:Model"] = model
        }).Build();
        var client = new AnthropicClient
        {
            ApiKey = "test-key",
            HttpClient = new HttpClient(handler),
            MaxRetries = 0
        };
        var analyzer = new AnthropicMealPictureAnalyzer(client, configuration, NullLogger<AnthropicMealPictureAnalyzer>.Instance, budget);

        return analyzer.AnalyzeAsync(request ?? new MealPictureAnalysisRequest([PictureOne], "en", null), cancellationToken);
    }
}
