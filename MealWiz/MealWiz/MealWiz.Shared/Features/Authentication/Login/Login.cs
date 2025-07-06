using FluentResults;
using MediatR;
using Microsoft.AspNetCore.Components.Authorization;
using Supabase.Gotrue;
using Client = Supabase.Client;

namespace MealWiz.Shared.Features.Authentication.Login;

public static class Login
{
    public record Command(string Email, string Password) : IRequest<Result<Session?>>;

    public class Handler(Client supabaseClient, AuthenticationStateProvider authenticationStateProvider) : IRequestHandler<Command, Result<Session?>>
    {
        private readonly Client _supabaseClient = supabaseClient;

        public async Task<Result<Session?>> Handle(Command request, CancellationToken cancellationToken)
        {
            var result = await Result.Try(() => _supabaseClient.Auth.SignInWithPassword(request.Email, request.Password));

            return result;
        }
    }
}
