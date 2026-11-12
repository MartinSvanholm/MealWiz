using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace MealWiz.Functions.Functions.DeleteAccount;

public enum DeleteAccountOutcome { Deleted, Unauthorized, WrongPassword, Failed }

public interface IDeleteAccountHandler
{
    Task<DeleteAccountOutcome> HandleAsync(string bearerToken, string password, CancellationToken cancellationToken);
}

/// <summary>
/// Deletes the caller's account through GoTrue with the service-role key: who the token belongs to, whether the
/// password is theirs, then the admin delete (the created_by foreign keys cascade). The admin delete only runs after
/// the password check passed. GoTrue's responses can carry internals, so failures are logged here and not returned.
/// </summary>
public class DeleteAccountHandler(
    IHttpClientFactory httpClientFactory,
    IConfiguration configuration,
    ILogger<DeleteAccountHandler> logger) : IDeleteAccountHandler
{
    public const string HttpClientName = "GoTrue";

    public async Task<DeleteAccountOutcome> HandleAsync(string bearerToken, string password, CancellationToken cancellationToken)
    {
        try
        {
            var url = configuration["Supabase:Url"]
                ?? throw new InvalidOperationException("Supabase:Url is not configured.");
            var serviceRoleKey = configuration["Supabase:ServiceRoleKey"]
                ?? throw new InvalidOperationException("Supabase:ServiceRoleKey is not configured.");
            var baseUrl = url.TrimEnd('/');
            var http = httpClientFactory.CreateClient(HttpClientName);

            // 1. Who is the caller?
            using var userRequest = Request(HttpMethod.Get, $"{baseUrl}/auth/v1/user", serviceRoleKey, bearerToken);
            using var userResponse = await http.SendAsync(userRequest, cancellationToken);
            if (userResponse.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
                return DeleteAccountOutcome.Unauthorized;
            if (!userResponse.IsSuccessStatusCode)
                return Fail("reading the user", userResponse.StatusCode);

            using var user = await JsonDocument.ParseAsync(
                await userResponse.Content.ReadAsStreamAsync(cancellationToken), cancellationToken: cancellationToken);
            var id = user.RootElement.TryGetProperty("id", out var idElement) ? idElement.GetString() : null;
            var email = user.RootElement.TryGetProperty("email", out var emailElement) ? emailElement.GetString() : null;
            if (string.IsNullOrEmpty(id) || string.IsNullOrEmpty(email))
                return Fail("reading the user", userResponse.StatusCode);

            // 2. Is the password theirs? (Creates a throwaway session that is deleted with the user.)
            using var passwordRequest = Request(HttpMethod.Post, $"{baseUrl}/auth/v1/token?grant_type=password", serviceRoleKey, null);
            passwordRequest.Content = JsonContent.Create(new { email, password });
            using var passwordResponse = await http.SendAsync(passwordRequest, cancellationToken);
            if (passwordResponse.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.Unauthorized)
                return DeleteAccountOutcome.WrongPassword;
            if (!passwordResponse.IsSuccessStatusCode)
                return Fail("checking the password", passwordResponse.StatusCode);

            // 3. Hard delete.
            using var deleteRequest = Request(HttpMethod.Delete, $"{baseUrl}/auth/v1/admin/users/{id}", serviceRoleKey, serviceRoleKey);
            using var deleteResponse = await http.SendAsync(deleteRequest, cancellationToken);
            return deleteResponse.IsSuccessStatusCode
                ? DeleteAccountOutcome.Deleted
                : Fail("deleting the user", deleteResponse.StatusCode);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "DeleteAccount failed.");
            return DeleteAccountOutcome.Failed;
        }
    }

    private static HttpRequestMessage Request(HttpMethod method, string url, string apiKey, string? bearer)
    {
        var request = new HttpRequestMessage(method, url);
        request.Headers.Add("apikey", apiKey);
        if (bearer is not null) request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearer);
        return request;
    }

    private DeleteAccountOutcome Fail(string step, HttpStatusCode status)
    {
        logger.LogError("DeleteAccount: GoTrue answered {Status} while {Step}.", (int)status, step);
        return DeleteAccountOutcome.Failed;
    }
}
