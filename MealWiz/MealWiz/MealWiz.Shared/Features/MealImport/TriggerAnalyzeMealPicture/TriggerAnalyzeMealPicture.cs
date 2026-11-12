using System.Net.Http.Json;
using FluentResults;
using MealWiz.Shared.Features.MealImport.Models;
using MediatR;
using Microsoft.Extensions.Localization;

namespace MealWiz.Shared.Features.MealImport.TriggerAnalyzeMealPicture;

/// <summary>
/// Calls the AnalyzeMealPicture Azure Function. Like <see cref="TriggerScrapeRecipe.TriggerScrapeRecipe"/>, the
/// meal_imports row is the source of truth for the outcome: a 2xx means the row tells how the job went, an error status
/// (502 when the Function couldn't load or write the row) fails this command so the caller fails the pending row.
/// </summary>
public static class TriggerAnalyzeMealPicture
{
    public record Command(Guid JobId) : IRequest<Result>;

    public class Handler(HttpClient httpClient, MealImportFunctionOptions options, IStringLocalizer<MealImportResource> localizer) : IRequestHandler<Command, Result>
    {
        public async Task<Result> Handle(Command request, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(options.Url))
                return Result.Fail(localizer["Import_ServiceNotConfigured"]);

            var endpoint = $"{options.Url.TrimEnd('/')}/api/AnalyzeMealPicture";

            using var httpRequest = new HttpRequestMessage(HttpMethod.Post, endpoint)
            {
                Content = JsonContent.Create(new { jobId = request.JobId })
            };

            if (!string.IsNullOrWhiteSpace(options.Key))
                httpRequest.Headers.Add("x-functions-key", options.Key);

            var sendResult = await Result.Try(async Task<HttpResponseMessage> () =>
                await httpClient.SendAsync(httpRequest, cancellationToken));

            if (sendResult.IsFailed) return sendResult.ToResult();

            using var response = sendResult.Value;
            return response.IsSuccessStatusCode
                ? Result.Ok()
                : Result.Fail(localizer["Import_ServiceResponded", (int)response.StatusCode]);
        }
    }
}
