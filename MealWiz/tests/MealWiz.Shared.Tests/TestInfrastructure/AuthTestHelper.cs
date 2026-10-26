using MealWiz.Shared.Layout;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;

namespace MealWiz.Shared.Tests.TestInfrastructure;

public static class AuthTestHelper
{
    /// <summary>
    /// The routing wiring from Routes.razor for one page, without the Router or MainLayout:
    /// <c>CascadingAuthenticationState > AuthorizeRouteView > NotAuthorized > RedirectToLogin</c>.
    /// Use it with bUnit's <c>AddAuthorization()</c> to see what an (un)authorized user gets for that page.
    /// </summary>
    public static RenderFragment RoutedPage(Type pageType) => builder =>
    {
        builder.OpenComponent<CascadingAuthenticationState>(0);
        builder.AddAttribute(1, nameof(CascadingAuthenticationState.ChildContent), (RenderFragment)(routeBuilder =>
        {
            routeBuilder.OpenComponent<AuthorizeRouteView>(0);
            routeBuilder.AddAttribute(1, nameof(AuthorizeRouteView.RouteData), new RouteData(pageType, new Dictionary<string, object?>()));
            routeBuilder.AddAttribute(2, nameof(AuthorizeRouteView.NotAuthorized), (RenderFragment<AuthenticationState>)(_ => redirect =>
            {
                redirect.OpenComponent<RedirectToLogin>(0);
                redirect.CloseComponent();
            }));
            routeBuilder.CloseComponent();
        }));
        builder.CloseComponent();
    };
}
