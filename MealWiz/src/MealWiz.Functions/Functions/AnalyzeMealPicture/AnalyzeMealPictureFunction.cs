using System.Net;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;

namespace MealWiz.Functions.Functions.AnalyzeMealPicture;

public record AnalyzeMealPictureRequestBody(Guid JobId);

public class AnalyzeMealPictureFunction(IAnalyzeMealPictureHandler handler, ILogger<AnalyzeMealPictureFunction> logger)
{
    [Function("AnalyzeMealPicture")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Function, "post", Route = "AnalyzeMealPicture")] HttpRequestData req,
        CancellationToken cancellationToken)
    {
        AnalyzeMealPictureRequestBody? body;
        try
        {
            body = await req.ReadFromJsonAsync<AnalyzeMealPictureRequestBody>(cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Could not parse AnalyzeMealPicture request body.");
            return req.CreateResponse(HttpStatusCode.BadRequest);
        }

        if (body is null || body.JobId == Guid.Empty)
        {
            return req.CreateResponse(HttpStatusCode.BadRequest);
        }

        // The client's source of truth is the meal_imports row, not this response. A failure means the row couldn't be
        // reached and is still pending, so the client has to fail it itself.
        var result = await handler.HandleAsync(new AnalyzeMealPictureRequest(body.JobId), cancellationToken);
        if (result.IsFailed)
        {
            return req.CreateResponse(HttpStatusCode.BadGateway);
        }

        var response = req.CreateResponse(HttpStatusCode.OK);
        await response.WriteAsJsonAsync(new { jobId = body.JobId }, cancellationToken);
        return response;
    }
}
