using Microsoft.AspNetCore.Components.Authorization;
using Supabase;
using System.Security.Claims;
using System.Text.Json;

namespace MealWiz.Shared.Services.Authentication;

public class CustomAuthStateProvider(Client client) : AuthenticationStateProvider
{
    public override Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        var identity = new ClaimsIdentity();

        if (!string.IsNullOrEmpty(client.Auth.CurrentSession?.AccessToken) && !client.Auth.CurrentSession.Expired())
        {
            List<Claim> claims = [];
            claims.AddRange(ParseClaimsFromJwt(client.Auth.CurrentSession.AccessToken));

            identity = new ClaimsIdentity(claims, "jwt");
        }

        var user = new ClaimsPrincipal(identity);
        var state = new AuthenticationState(user);

        NotifyAuthenticationStateChanged(Task.FromResult(state));

        return Task.FromResult(state);
    }

    private static IEnumerable<Claim> ParseClaimsFromJwt(string jwt)
    {
        var payload = jwt.Split('.')[1];
        var jsonBytes = ParseBase64WithoutPadding(payload);
        var keyValuePairs = JsonSerializer.Deserialize<Dictionary<string, object>>(jsonBytes);
        return keyValuePairs.Select(kvp => new Claim(kvp.Key, kvp.Value.ToString()));
    }

    private static byte[] ParseBase64WithoutPadding(string base64)
    {
        switch (base64.Length % 4)
        {
            case 2: base64 += "=="; break;
            case 3: base64 += "="; break;
        }
        return Convert.FromBase64String(base64);
    }
}