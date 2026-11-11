using FluentResults;
using MediatR;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.Localization;
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

    public class Handler(Client supabaseClient, AuthenticationStateProvider authenticationStateProvider, IStringLocalizer<AuthenticationResource> L) : IRequestHandler<Command, Result<Session?>>
    {
        private readonly Client _supabaseClient = supabaseClient;

        public async Task<Result<Session?>> Handle(Command request, CancellationToken cancellationToken)
        {
            var tokens = ResetLink.Read(request.ResetLink);
            if (tokens.IsFailed)
                return Result.Fail<Session?>(L["Reset_LinkInvalid"]);

            var result = await Result.Try(() => SetNewPasswordAsync(tokens.Value, request.NewPassword));

            await authenticationStateProvider.GetAuthenticationStateAsync();

            return result;
        }

        // Update needs the recovery session, and adopting it signs the user in. If the new password is refused (too
        // short, say), sign out again: the user is signed in only once the password is set.
        private async Task<Session?> SetNewPasswordAsync(ResetLink.Tokens tokens, string newPassword)
        {
            await _supabaseClient.Auth.SetSession(tokens.AccessToken, tokens.RefreshToken);
            try
            {
                await _supabaseClient.Auth.Update(new UserAttributes { Password = newPassword });
            }
            catch
            {
                await _supabaseClient.Auth.SignOut();
                throw;
            }

            return _supabaseClient.Auth.CurrentSession;
        }
    }
}
