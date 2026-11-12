using System.Text.Json;
using Anthropic;
using Anthropic.Exceptions;
using Anthropic.Helpers;
using Anthropic.Models.Messages;
using FluentResults;
using MealWiz.Functions.Shared.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace MealWiz.Functions.Functions.AnalyzeMealPicture;

/// <summary>
/// Sends the pictures to Claude in one request and reads the structured answer. Never logs picture bytes, the API key
/// or the hint. The client is a singleton (see ServiceCollectionExtensions) with its own retry and per-attempt timeout;
/// <see cref="Budget"/> caps the whole call so a job always fits the Function's two minutes.
/// </summary>
public class AnthropicMealPictureAnalyzer(
    IAnthropicClient client,
    IConfiguration configuration,
    ILogger<AnthropicMealPictureAnalyzer> logger,
    TimeSpan? budget = null) : IMealPictureAnalyzer
{
    public const string DefaultModel = "claude-haiku-5-5";
    public static readonly TimeSpan DefaultBudget = TimeSpan.FromSeconds(80);
    private const string SpendLimitMessagePrefix = "You have reached your specified";

    private TimeSpan Budget => budget ?? DefaultBudget;

    public async Task<Result<MealImportPayload>> AnalyzeAsync(MealPictureAnalysisRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(configuration["Anthropic:ApiKey"]))
        {
            logger.LogError("Anthropic:ApiKey is not set; picture analysis is unavailable.");
            return Fail(RecipeImportErrorCode.AiUnavailable, "The AI service is not configured.");
        }

        using var timeBudget = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeBudget.CancelAfter(Budget);

        try
        {
            var message = await client.Messages.Create(BuildParameters(request), timeBudget.Token);
            return ReadAnswer(message);
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            return MapException(ex);
        }
        catch (OperationCanceledException)
        {
            return Fail(RecipeImportErrorCode.Unknown, "The request was cancelled.");
        }
    }

    private MessageCreateParams BuildParameters(MealPictureAnalysisRequest request)
    {
        var content = new List<ContentBlockParam>();
        for (var i = 0; i < request.Pictures.Count; i++)
        {
            content.Add(new TextBlockParam { Text = $"Image {i + 1}:" });
            content.Add(new ImageBlockParam
            {
                Source = new Base64ImageSource { Data = Convert.ToBase64String(request.Pictures[i]), MediaType = MediaType.ImageJpeg }
            });
        }
        content.Add(new TextBlockParam { Text = MealPicturePrompt.Instruction(request.Language, request.Hint) });

        // Not Messages.Create<T>(): it replaces output_config and silently drops the effort.
        return new MessageCreateParams
        {
            Model = configuration["Anthropic:Model"] is { Length: > 0 } model ? model : DefaultModel,
            MaxTokens = 4096,
            System = MealPicturePrompt.System,
            OutputConfig = new OutputConfig
            {
                Effort = Effort.Low,
                Format = StructuredOutput.CreateJsonFormat<MealPictureReading>()
            },
            Messages = [new UserMessageParam { Content = content }]
        };
    }

    private Result<MealImportPayload> ReadAnswer(Message message)
    {
        // Check why it stopped before reading: a refusal or a cut-off answer is not a result.
        if (message.StopReason != StopReason.EndTurn)
        {
            logger.LogWarning("The AI service stopped with {StopReason}.", message.StopReason);
            return Fail(RecipeImportErrorCode.AiUnavailable, $"The AI service stopped early ({message.StopReason}).");
        }

        logger.LogInformation("Picture analysis used {InputTokens} input and {OutputTokens} output tokens.",
            message.Usage.InputTokens, message.Usage.OutputTokens);

        try
        {
            var json = string.Concat(message.Content.Select(b => b.Value).OfType<TextBlock>().Select(t => t.Text));
            return MealPictureMapper.Map(StructuredOutput.Parse<MealPictureReading>(json));
        }
        catch (Exception ex) when (ex is JsonException or AnthropicInvalidDataException)
        {
            logger.LogWarning(ex, "The AI service's answer could not be read.");
            return Fail(RecipeImportErrorCode.AiUnavailable, "The AI service's answer could not be read.");
        }
    }

    private Result<MealImportPayload> MapException(Exception ex)
    {
        switch (ex)
        {
            case OperationCanceledException or TimeoutException:
                logger.LogWarning("The AI call timed out.");
                return Fail(RecipeImportErrorCode.TimedOut, "The AI service took too long.");
            case AnthropicContentTooLargeException:
                return Fail(RecipeImportErrorCode.PictureUnreadable, "The pictures are too large.");
            case AnthropicBadRequestException bad when bad.ApiMessage?.StartsWith(SpendLimitMessagePrefix, StringComparison.Ordinal) == true:
                logger.LogError("The self-set Anthropic spend limit is reached.");
                return Fail(RecipeImportErrorCode.AiUnavailable, "The AI service is unavailable.");
            case AnthropicBadRequestException bad when bad.ApiMessage?.Contains("image", StringComparison.OrdinalIgnoreCase) == true:
                logger.LogWarning("The AI service rejected a picture: {Message}", bad.ApiMessage);
                return Fail(RecipeImportErrorCode.PictureUnreadable, "The AI service could not read a picture.");
            case AnthropicApiException api:
                logger.LogWarning("The AI service answered {StatusCode}: {Type}", (int)api.StatusCode, api.Type);
                return Fail(RecipeImportErrorCode.AiUnavailable, "The AI service is unavailable.");
            default:
                logger.LogWarning(ex, "The AI call failed.");
                return Fail(RecipeImportErrorCode.AiUnavailable, "The AI service is unavailable.");
        }
    }

    private static Result<MealImportPayload> Fail(RecipeImportErrorCode code, string message) =>
        Result.Fail<MealImportPayload>(new PictureImportError(code, message));
}
