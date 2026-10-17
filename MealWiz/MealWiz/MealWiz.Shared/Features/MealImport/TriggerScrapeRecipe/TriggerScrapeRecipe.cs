using System.Net.Http.Json;
using FluentResults;
using MealWiz.Shared.Features.MealImport.Models;
using MediatR;

namespace MealWiz.Shared.Features.MealImport.TriggerScrapeRecipe;

/// <summary>
/// Calls the ScrapeRecipe Azure Function. Its response is only an ack — the meal_imports row (observed via
/// Realtime) is the source of truth for the outcome.
/// </summary>
public static class TriggerScrapeRecipe
{
    public record Command(Guid JobId, string Url) : IRequest<Result>;

    public class Handler(HttpClient httpClient, MealImportFunctionOptions options) : IRequestHandler<Command, Result>
    {
        public async Task<Result> Handle(Command request, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(options.Url))
                return Result.Fail("The meal import service is not configured.");

            var endpoint = $"{options.Url.TrimEnd('/')}/api/ScrapeRecipe";

            using var httpRequest = new HttpRequestMessage(HttpMethod.Post, endpoint)
            {
                Content = JsonContent.Create(new { jobId = request.JobId, url = request.Url })
            };

            if (!string.IsNullOrWhiteSpace(options.Key))
                httpRequest.Headers.Add("x-functions-key", options.Key);

            var sendResult = await Result.Try(async Task<HttpResponseMessage> () =>
                await httpClient.SendAsync(httpRequest, cancellationToken));

            if (sendResult.IsFailed) return sendResult.ToResult();

            using var response = sendResult.Value;
            return response.IsSuccessStatusCode
                ? Result.Ok()
                : Result.Fail($"The meal import service responded with {(int)response.StatusCode}.");
        }
    }
}
