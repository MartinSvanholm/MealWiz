using Microsoft.AspNetCore.Components.Authorization;
using System.Security.Claims;

namespace MealWizWASM.Providers;

public class CustomAuthStateProvider : AuthenticationStateProvider
{
    public override Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        var identity = new ClaimsIdentity(
        [
            new Claim(ClaimTypes.Name, "mrfibuli"),
    ], "Custom Authentication");

        var user = new ClaimsPrincipal(identity);

        return Task.FromResult(new AuthenticationState(user));
    }
}
