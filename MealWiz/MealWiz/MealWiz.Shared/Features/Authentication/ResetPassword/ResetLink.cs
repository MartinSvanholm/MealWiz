using System.Web;
using FluentResults;

namespace MealWiz.Shared.Features.Authentication.ResetPassword;

/// <summary>Reads the fragment of the URL a password reset email's link landed on.</summary>
public static class ResetLink
{
    public const string InvalidLinkMessage = "This reset link is invalid or has expired. Ask for a new one.";

    /// <summary>The recovery session GoTrue put in the link.</summary>
    public record Tokens(string AccessToken, string RefreshToken);

    /// <summary>The link's tokens, or GoTrue's error (an expired or used link) or <see cref="InvalidLinkMessage"/>.</summary>
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
