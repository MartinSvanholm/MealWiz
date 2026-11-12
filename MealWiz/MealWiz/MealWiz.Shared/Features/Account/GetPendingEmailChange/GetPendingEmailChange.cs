using System.Net.Http.Json;
using System.Text.Json.Serialization;
using FluentResults;
using MealWiz.Shared.Features.Authentication;
using MediatR;
using Client = Supabase.Client;

namespace MealWiz.Shared.Features.Account.GetPendingEmailChange;

public static class GetPendingEmailChange
{
    /// <summary>The address the user asked to change to and hasn't confirmed yet, or <c>null</c> when none is pending.</summary>
    public record Query : IRequest<Result<string?>>;

    // The SDK's User drops new_email, so GoTrue's user endpoint is read directly.
    private sealed record UserResponse([property: JsonPropertyName("new_email")] string? NewEmail);

    public class Handler(Client supabaseClient, HttpClient httpClient, SupabaseAuthSettings settings) : IRequestHandler<Query, Result<string?>>
    {
        public async Task<Result<string?>> Handle(Query request, CancellationToken cancellationToken)
        {
            var accessToken = supabaseClient.Auth.CurrentSession?.AccessToken;
            if (string.IsNullOrEmpty(accessToken))
                return Result.Ok<string?>(null);

            using var httpRequest = new HttpRequestMessage(HttpMethod.Get, $"{settings.Url.TrimEnd('/')}/auth/v1/user");
            httpRequest.Headers.Add("apikey", settings.AnonKey);
            httpRequest.Headers.Add("Authorization", $"Bearer {accessToken}");

            return await Result.Try(async Task<string?> () =>
            {
                using var response = await httpClient.SendAsync(httpRequest, cancellationToken);
                response.EnsureSuccessStatusCode();
                var user = await response.Content.ReadFromJsonAsync<UserResponse>(cancellationToken);
                return string.IsNullOrEmpty(user?.NewEmail) ? null : user.NewEmail;
            });
        }
    }
}
