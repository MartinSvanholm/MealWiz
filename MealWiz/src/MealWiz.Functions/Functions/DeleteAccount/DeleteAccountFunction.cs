using System.Net;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;

namespace MealWiz.Functions.Functions.DeleteAccount;

public record DeleteAccountRequestBody(string? Password);

/// <summary>
/// Deletes the signed-in user's account. The caller sends their access token as a bearer token and their current
/// password in the body. 204 deleted, 400 bad request, 401 the token is missing or not valid, 403 wrong password
/// (nothing deleted), 502 anything else (the reason is logged, not returned).
/// </summary>
public class DeleteAccountFunction(IDeleteAccountHandler handler, ILogger<DeleteAccountFunction> logger)
{
    [Function("DeleteAccount")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Function, "post", Route = "DeleteAccount")] HttpRequestData req,
        CancellationToken cancellationToken)
    {
        DeleteAccountRequestBody? body;
        try
        {
            body = await req.ReadFromJsonAsync<DeleteAccountRequestBody>(cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Could not parse DeleteAccount request body.");
            return req.CreateResponse(HttpStatusCode.BadRequest);
        }

        if (body is null || string.IsNullOrWhiteSpace(body.Password))
            return req.CreateResponse(HttpStatusCode.BadRequest);

        var token = BearerToken(req);
        if (token is null)
            return req.CreateResponse(HttpStatusCode.Unauthorized);

        var outcome = await handler.HandleAsync(token, body.Password, cancellationToken);
        return req.CreateResponse(outcome switch
        {
            DeleteAccountOutcome.Deleted => HttpStatusCode.NoContent,
            DeleteAccountOutcome.Unauthorized => HttpStatusCode.Unauthorized,
            DeleteAccountOutcome.WrongPassword => HttpStatusCode.Forbidden,
            _ => HttpStatusCode.BadGateway
        });
    }

    private static string? BearerToken(HttpRequestData req)
    {
        if (!req.Headers.TryGetValues("Authorization", out var values)) return null;
        var header = values.FirstOrDefault();
        const string prefix = "Bearer ";
        if (header is null || !header.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) return null;
        var token = header[prefix.Length..].Trim();
        return token.Length == 0 ? null : token;
    }
}
