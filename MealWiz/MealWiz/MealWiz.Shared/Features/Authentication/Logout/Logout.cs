using FluentResults;
using MediatR;
using Microsoft.AspNetCore.Components.Authorization;
using Supabase;

namespace MealWiz.Shared.Features.Authentication.Logout;

public static class Logout
{
    public record Command() : IRequest<Result>;

    public class Handler(Client supabaseClient, AuthenticationStateProvider authenticationStateProvider) : IRequestHandler<Command, Result>
    {
        private readonly Client _supabaseClient = supabaseClient;

        public async Task<Result> Handle(Command request, CancellationToken cancellationToken)
        {
            var result = await Result.Try(() => _supabaseClient.Auth.SignOut());

            await authenticationStateProvider.GetAuthenticationStateAsync();

            return result;
        }
    }
}