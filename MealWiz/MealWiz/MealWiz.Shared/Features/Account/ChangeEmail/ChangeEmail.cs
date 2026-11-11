using System.Net.Http.Json;
using FluentResults;
using MealWiz.Shared.Features.Authentication;
using MealWiz.Shared.Features.Authentication.EmailLinks;
using MealWiz.Shared.Helpers;
using MediatR;
using Microsoft.Extensions.Localization;
using Client = Supabase.Client;

namespace MealWiz.Shared.Features.Account.ChangeEmail;

public static class ChangeEmail
{
    /// <summary>
    /// Asks GoTrue to change the signed-in user's email. Both the old and the new address get a link; the change
    /// happens when both are clicked. Calls GoTrue directly because the SDK's <c>Update</c> can't send a redirect.
    /// </summary>
    public record Command(string NewEmail) : IRequest<Result>;

    public class Handler(
        Client supabaseClient,
        HttpClient httpClient,
        SupabaseAuthSettings settings,
        EmailLinkBuilder links,
        IStringLocalizer<AccountResource> localizer) : IRequestHandler<Command, Result>
    {
        public async Task<Result> Handle(Command request, CancellationToken cancellationToken)
        {
            var accessToken = supabaseClient.Auth.CurrentSession?.AccessToken;
            if (string.IsNullOrEmpty(accessToken))
                return Result.Fail(ErrorMessages.GenericError);

            using var httpRequest = new HttpRequestMessage(HttpMethod.Put, $"{settings.Url.TrimEnd('/')}/auth/v1/user")
            {
                Content = JsonContent.Create(new { email = request.NewEmail })
            };
            httpRequest.Headers.Add("apikey", settings.AnonKey);
            httpRequest.Headers.Add("Authorization", $"Bearer {accessToken}");
            httpRequest.Headers.Add("redirect_to", links.Build("email-change"));

            var sent = await Result.Try(async Task<(bool Ok, string Body)> () =>
            {
                using var response = await httpClient.SendAsync(httpRequest, cancellationToken);
                return (response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync(cancellationToken));
            });
            if (sent.IsFailed)
                return sent.ToResult();

            if (sent.Value.Ok)
                return Result.Ok();

            return Result.Fail(GotrueErrorCode.Read(sent.Value.Body) switch
            {
                GotrueErrorCode.EmailExists => localizer["ChangeEmail_Exists"].Value,
                GotrueErrorCode.ValidationFailed => localizer["ChangeEmail_Invalid"].Value,
                _ => ErrorMessages.GenericError
            });
        }
    }
}
