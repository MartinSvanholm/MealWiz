using Microsoft.AspNetCore.Components.Authorization;
using Supabase;
using System.Security.Claims;
using System.Text.Json;

namespace MealWiz.Shared.Services.Authentication;

public class CustomAuthStateProvider : AuthenticationStateProvider
{
    private readonly Client _client;
    private readonly SessionKeeper _keeper;

    /// <summary>
    /// Signed in means a session with a refresh token that the keeper hasn't ended. An expired access token alone
    /// doesn't sign the user out: the keeper refreshes it, and offline the user stays signed in.
    /// The state changes are pushed from the SDK's auth events and the keeper, never from reading the state.
    /// </summary>
    public CustomAuthStateProvider(Client client, SessionKeeper keeper)
    {
        _client = client;
        _keeper = keeper;
        _keeper.Changed += Notify;
    }

    public override Task<AuthenticationState> GetAuthenticationStateAsync() => Task.FromResult(BuildState());

    private void Notify() => NotifyAuthenticationStateChanged(Task.FromResult(BuildState()));

    private AuthenticationState BuildState()
    {
        var identity = new ClaimsIdentity();
        var session = _client.Auth.CurrentSession;

        if (!_keeper.IsSignedOut && !string.IsNullOrEmpty(session?.AccessToken) && !string.IsNullOrEmpty(session.RefreshToken))
        {
            identity = new ClaimsIdentity(ParseClaimsFromJwt(session.AccessToken), "jwt");
        }

        return new AuthenticationState(new ClaimsPrincipal(identity));
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
        // JWT segments are base64url: map its alphabet back to standard base64 before decoding.
        base64 = base64.Replace('-', '+').Replace('_', '/');

        switch (base64.Length % 4)
        {
            case 2: base64 += "=="; break;
            case 3: base64 += "="; break;
        }
        return Convert.FromBase64String(base64);
    }
}