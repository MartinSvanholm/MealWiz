using System.Web;
using FluentResults;

namespace MealWiz.Shared.Features.Authentication.ResetPassword;

/// <summary>Reads the fragment of the URL a password reset email's link landed on.</summary>
public static class ResetLink
{
    /// <summary>The failure reason when the link has no session or error of its own: diagnostic, never shown.</summary>
    public const string InvalidLinkMessage = "The reset link has no recovery session.";

    /// <summary>The recovery session GoTrue put in the link.</summary>
    public record Tokens(string AccessToken, string RefreshToken);

    /// <summary>
    /// The link's tokens, or a failure with GoTrue's reason (an expired or used link) or <see cref="InvalidLinkMessage"/>.
    /// The reasons are English diagnostics: the page and the handler show <c>Reset_LinkInvalid</c> instead.
    /// </summary>
    public static Result<Tokens> Read(Uri link)
    {
        var fragment = HttpUtility.ParseQueryString(link.Fragment.TrimStart('#'));

        if (fragment["error"] is not null || fragment["error_description"] is not null)
            return Result.Fail(fragment["error_description"] ?? InvalidLinkMessage);

        var accessToken = fragment["access_token"];
        var refreshToken = fragment["refresh_token"];
        return string.IsNullOrEmpty(accessToken) || string.IsNullOrEmpty(refreshToken) || fragment["type"] != "recovery"
            ? Result.Fail(InvalidLinkMessage)
            : Result.Ok(new Tokens(accessToken, refreshToken));
    }
}
