using System.Net;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;

namespace MealWiz.Functions.Functions.ScrapeRecipe;

public record ScrapeRecipeRequestBody(Guid JobId, string Url);

public class ScrapeRecipeFunction(ScrapeRecipeHandler handler, ILogger<ScrapeRecipeFunction> logger)
{
    [Function("ScrapeRecipe")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Function, "post", Route = "ScrapeRecipe")] HttpRequestData req,
        CancellationToken cancellationToken)
    {
        ScrapeRecipeRequestBody? body;
        try
        {
            body = await req.ReadFromJsonAsync<ScrapeRecipeRequestBody>(cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Could not parse ScrapeRecipe request body.");
            return req.CreateResponse(HttpStatusCode.BadRequest);
        }

        if (body is null || body.JobId == Guid.Empty || string.IsNullOrWhiteSpace(body.Url))
        {
            return req.CreateResponse(HttpStatusCode.BadRequest);
        }

        // The client's source of truth is the meal_imports row (via Realtime), not this response.
        // HandleAsync never lets an exception escape, so this always completes and acks.
        await handler.HandleAsync(new ScrapeRecipeRequest(body.JobId, body.Url), cancellationToken);

        var response = req.CreateResponse(HttpStatusCode.OK);
        await response.WriteAsJsonAsync(new { jobId = body.JobId }, cancellationToken);
        return response;
    }
}
