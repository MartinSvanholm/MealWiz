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
            try
            {
                await supabaseClient.Auth.Update(new UserAttributes { Password = request.NewPassword });
                return Result.Ok().WithSuccess(localizer["ChangePassword_Done"].Value);
            }
            catch (GotrueException ex) when (GotrueErrorCode.Read(ex.Content) is { } code && MessageFor(code) is { } message)
            {
                return Result.Fail(new Error(message).CausedBy(ex));
            }
            catch (Exception ex)
            {
                return Result.Fail(ResultHelper.MapException(ex));
            }
        }

        private string? MessageFor(string code) => code switch
        {
            GotrueErrorCode.WeakPassword => localizer["ChangePassword_Weak"].Value,
            GotrueErrorCode.SamePassword => localizer["ChangePassword_Same"].Value,
            _ => null
        };
    }
}
