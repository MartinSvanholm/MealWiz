using Microsoft.AspNetCore.Components;

namespace MealWiz.Shared.Features.Authentication.EmailLinks;

/// <summary>Builds the absolute URL an email's link should open (password reset, email change).</summary>
public class EmailLinkBuilder(NavigationManager navigation, EmailLinkOptions options)
{
    /// <summary>True when the link opens in the browser instead of the app that asked for it (MAUI).</summary>
    public bool OpensInBrowser => !string.IsNullOrWhiteSpace(options.WebAppUrl);

    /// <summary>The absolute URL of <paramref name="relativePath"/> (e.g. <c>reset-password</c>) on the web app.</summary>
    public string Build(string relativePath)
    {
        var path = relativePath.TrimStart('/');
        return OpensInBrowser
            ? $"{options.WebAppUrl!.TrimEnd('/')}/{path}"
            : navigation.ToAbsoluteUri(path).ToString();
    }
}
