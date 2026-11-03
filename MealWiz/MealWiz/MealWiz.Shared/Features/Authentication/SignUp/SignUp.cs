using FluentResults;
using MediatR;
using Microsoft.AspNetCore.Components.Authorization;
using Supabase.Gotrue;
using Client = Supabase.Client;

namespace MealWiz.Shared.Features.Authentication.SignUp;

public static class SignUp
{
    /// <summary>
    /// The session is null when the Supabase project requires email confirmation: the user exists but is not
    /// signed in until they confirm.
    /// </summary>
    public record Command(string Email, string Password) : IRequest<Result<Session?>>;

    public class Handler(Client supabaseClient, AuthenticationStateProvider authenticationStateProvider) : IRequestHandler<Command, Result<Session?>>
    {
        private readonly Client _supabaseClient = supabaseClient;

        public async Task<Result<Session?>> Handle(Command request, CancellationToken cancellationToken)
        {
            var result = await Result.Try(() => _supabaseClient.Auth.SignUp(request.Email, request.Password));

            await authenticationStateProvider.GetAuthenticationStateAsync();

            return result;
        }
    }
}
