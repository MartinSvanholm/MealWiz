using FluentResults;
using MealWiz.Shared.Features.Authentication;
using MealWiz.Shared.Helpers;
using MediatR;
using Microsoft.Extensions.Localization;
using Supabase.Gotrue;
using Supabase.Gotrue.Exceptions;
using Client = Supabase.Client;

namespace MealWiz.Shared.Features.Account.ChangePassword;

public static class ChangePassword
{
    /// <summary>Sets the signed-in user's password. GoTrue keeps this session and revokes the user's others.</summary>
    public record Command(string NewPassword) : IRequest<Result>;

    public class Handler(Client supabaseClient, IStringLocalizer<AccountResource> localizer) : IRequestHandler<Command, Result>
    {
        public async Task<Result> Handle(Command request, CancellationToken cancellationToken)
        {
            var result = await Result.Try(
                async Task () =>
                {
                    await supabaseClient.Auth.Update(new UserAttributes { Password = request.NewPassword });
                },
                // GoTrue's own reason for refusing the password, when it is one the user can act on.
                ex => ex is GotrueException gotrue && GotrueErrorCode.Read(gotrue.Content) is { } code && MessageFor(code) is { } message
                    ? new Error(message).CausedBy(ex)
                    : ResultHelper.MapException(ex));

            return result.IsSuccess ? result.WithSuccess(localizer["ChangePassword_Done"].Value) : result;
        }

        private string? MessageFor(string code) => code switch
        {
            GotrueErrorCode.WeakPassword => localizer["ChangePassword_Weak"].Value,
            GotrueErrorCode.SamePassword => localizer["ChangePassword_Same"].Value,
            _ => null
        };
    }
}
