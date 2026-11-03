using System.Web;
using FluentResults;
using MediatR;
using Microsoft.AspNetCore.Components.Authorization;
using Supabase.Gotrue;
using Client = Supabase.Client;

namespace MealWiz.Shared.Features.Authentication.ResetPassword;

public static class ResetPassword
{
    /// <summary>
    /// <paramref name="ResetLink"/> is the URL the reset email's link landed on: GoTrue puts the recovery session in
    /// its fragment (<c>#access_token=…&amp;refresh_token=…&amp;type=recovery</c>), or an error when the link has
    /// expired or was used already.
    /// </summary>
    public record Command(Uri ResetLink, string NewPassword) : IRequest<Result<Session?>>;

    public class Handler(Client supabaseClient, AuthenticationStateProvider authenticationStateProvider) : IRequestHandler<Command, Result<Session?>>
    {
        private readonly Client _supabaseClient = supabaseClient;

        public async Task<Result<Session?>> Handle(Command request, CancellationToken cancellationToken)
        {
            var link = ResetLink.Read(request.ResetLink);
            if (link.Error is not null)
                return Result.Fail<Session?>(link.Error);

            // Adopting the recovery session signs the user in (and persists it), which Update needs.
            var result = await Result.Try(() => SetNewPasswordAsync(link.AccessToken!, link.RefreshToken!, request.NewPassword));

            await authenticationStateProvider.GetAuthenticationStateAsync();

            return result;
        }

        private async Task<Session?> SetNewPasswordAsync(string accessToken, string refreshToken, string newPassword)
        {
            await _supabaseClient.Auth.SetSession(accessToken, refreshToken);
            await _supabaseClient.Auth.Update(new UserAttributes { Password = newPassword });
            return _supabaseClient.Auth.CurrentSession;
        }
    }
}

/// <summary>Reads the fragment of the URL a password reset email's link landed on.</summary>
public static class ResetLink
{
    public const string InvalidLinkMessage = "This reset link is invalid or has expired. Ask for a new one.";

    /// <summary>Either the recovery session's tokens, or an <see cref="Error"/> to show.</summary>
    public record Contents(string? AccessToken, string? RefreshToken, string? Error);

    public static Contents Read(Uri link)
    {
        var fragment = HttpUtility.ParseQueryString(link.Fragment.TrimStart('#'));

        if (fragment["error"] is not null || fragment["error_description"] is not null)
            return new Contents(null, null, fragment["error_description"] ?? InvalidLinkMessage);

        var accessToken = fragment["access_token"];
        var refreshToken = fragment["refresh_token"];
        return string.IsNullOrEmpty(accessToken) || string.IsNullOrEmpty(refreshToken) || fragment["type"] != "recovery"
            ? new Contents(null, null, InvalidLinkMessage)
            : new Contents(accessToken, refreshToken, null);
    }
}
