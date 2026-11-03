using FluentResults;
using MediatR;
using Supabase.Gotrue;
using Client = Supabase.Client;

namespace MealWiz.Shared.Features.Authentication.ForgotPassword;

public static class RequestPasswordReset
{
    /// <summary>
    /// <paramref name="RedirectTo"/> is where the email's link lands, the web app's <c>/reset-password</c>. GoTrue
    /// sends the user to the project's Site URL instead when it isn't on the redirect allow list. It answers the same
    /// for an email with no account, so the page can't tell the two apart either.
    /// </summary>
    public record Command(string Email, string RedirectTo) : IRequest<Result>;

    public class Handler(Client supabaseClient) : IRequestHandler<Command, Result>
    {
        private readonly Client _supabaseClient = supabaseClient;

        // The implicit flow (the default): the link carries the session in its fragment, so it works in any browser.
        // The returned state only holds a PKCE verifier, which this flow doesn't use.
        public async Task<Result> Handle(Command request, CancellationToken cancellationToken) =>
            (await Result.Try(() => _supabaseClient.Auth.ResetPasswordForEmail(
                new ResetPasswordForEmailOptions(request.Email) { RedirectTo = request.RedirectTo }))).ToResult();
    }
}
