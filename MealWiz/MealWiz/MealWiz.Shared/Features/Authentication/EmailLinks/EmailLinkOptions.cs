namespace MealWiz.Shared.Features.Authentication.EmailLinks;

/// <summary>
/// <paramref name="WebAppUrl"/> is where the web app lives, set only in the MAUI head (<c>WebApp:url</c>): a link in
/// an email can't open the MAUI app, so it opens the web app in the browser. The web head passes <c>null</c> and links
/// to itself.
/// </summary>
public record EmailLinkOptions(string? WebAppUrl);
