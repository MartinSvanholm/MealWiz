using FluentResults;
using MediatR;
using MealWiz.Shared.Services.Authentication;
using Supabase;

namespace MealWiz.Shared.Features.Authentication.Logout;

public static class Logout
{
    public record Command() : IRequest<Result>;

    public class Handler(Client supabaseClient, SessionKeeper sessionKeeper) : IRequestHandler<Command, Result>
    {
        public async Task<Result> Handle(Command request, CancellationToken cancellationToken)
        {
            var result = await Result.Try(() => supabaseClient.Auth.SignOut());

            // On failure nothing changes: the user stays signed in and sees the error.
            if (result.IsSuccess)
                await sessionKeeper.SignOutLocallyAsync();

            return result;
        }
    }
}
