using MealWiz.Shared.Services.FunctionApp;
using System.Net;
using System.Net.Http.Json;
using FluentResults;
using MealWiz.Shared.Services.Authentication;
using MediatR;
using Microsoft.Extensions.Localization;
using Client = Supabase.Client;

namespace MealWiz.Shared.Features.Account.DeleteAccount;

public static class DeleteAccount
{
    /// <summary>
    /// Deletes the signed-in user's account and everything they own, after the Function has checked the password.
    /// Only a success signs out, and only locally: the user is gone, so GoTrue would refuse a server-side sign-out.
    /// </summary>
    public record Command(string Password) : IRequest<Result>;

    public class Handler(
        HttpClient httpClient,
        FunctionAppOptions options,
        Client supabaseClient,
        SessionKeeper sessionKeeper,
        IStringLocalizer<AccountResource> localizer) : IRequestHandler<Command, Result>
    {
        public async Task<Result> Handle(Command request, CancellationToken cancellationToken)
        {
            var token = supabaseClient.Auth.CurrentSession?.AccessToken;
            if (string.IsNullOrWhiteSpace(options.Url) || string.IsNullOrEmpty(token))
                return Failed();

            using var httpRequest = new HttpRequestMessage(HttpMethod.Post, $"{options.Url.TrimEnd('/')}/api/DeleteAccount")
            {
                Content = JsonContent.Create(new { password = request.Password })
            };
            httpRequest.Headers.Authorization = new("Bearer", token);
            if (!string.IsNullOrWhiteSpace(options.Key))
                httpRequest.Headers.Add("x-functions-key", options.Key);

            HttpStatusCode status;
            try
            {
                using var response = await httpClient.SendAsync(httpRequest, cancellationToken);
                status = response.StatusCode;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                return Failed();
            }

            switch (status)
            {
                case HttpStatusCode.NoContent:
                    await sessionKeeper.SignOutLocallyAsync();
                    return Result.Ok();
                case HttpStatusCode.Forbidden:
                    return Result.Fail(localizer["DeleteAccount_WrongPassword"].Value);
                case HttpStatusCode.Unauthorized:
                    return Result.Fail(localizer["DeleteAccount_SessionExpired"].Value);
                default:
                    return Failed();
            }
        }

        private Result Failed() => Result.Fail(localizer["DeleteAccount_Failed"].Value);
    }
}
