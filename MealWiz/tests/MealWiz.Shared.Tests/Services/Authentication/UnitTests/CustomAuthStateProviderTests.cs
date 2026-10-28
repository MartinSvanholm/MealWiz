using System.Security.Claims;
using System.Text;
using MealWiz.Shared.Services.Authentication;
using Microsoft.AspNetCore.Components.Authorization;
using Supabase;
using Supabase.Gotrue;
using Supabase.Gotrue.Interfaces;
using Xunit;
using Client = Supabase.Client;

namespace MealWiz.Shared.Tests.Services.Authentication.UnitTests;

/// <summary>
/// Uses a real Supabase client that never touches the network: the session is loaded from a fake persistence store,
/// the same way the app restores one from local storage on startup.
/// </summary>
public class CustomAuthStateProviderTests
{
    private const string UserId = "6f1c8a52-5d6e-4b0f-9a63-2b5d6c1e7f10";

    [Fact]
    public async Task GetAuthenticationStateAsync_NoSession_IsAnonymous()
    {
        var provider = new CustomAuthStateProvider(ClientWithSession(null));

        var state = await provider.GetAuthenticationStateAsync();

        Assert.False(state.User.Identity?.IsAuthenticated);
        Assert.Empty(state.User.Claims);
    }

    [Fact]
    public async Task GetAuthenticationStateAsync_ExpiredSession_IsAnonymous()
    {
        var session = Session(Jwt(Payload()));
        session.CreatedAt = DateTime.Now.AddHours(-2);
        session.ExpiresIn = 3600;
        var provider = new CustomAuthStateProvider(ClientWithSession(session));

        var state = await provider.GetAuthenticationStateAsync();

        Assert.False(state.User.Identity?.IsAuthenticated);
    }

    [Fact]
    public async Task GetAuthenticationStateAsync_ValidSession_IsAuthenticatedWithTheJwtClaims()
    {
        var provider = new CustomAuthStateProvider(ClientWithSession(Session(Jwt(Payload()))));

        var state = await provider.GetAuthenticationStateAsync();

        Assert.True(state.User.Identity?.IsAuthenticated);
        Assert.Equal("jwt", state.User.Identity?.AuthenticationType);
        Assert.Equal(UserId, state.User.FindFirst("sub")?.Value);
        Assert.Equal("ada@example.com", state.User.FindFirst("email")?.Value);
        Assert.Equal("authenticated", state.User.FindFirst("role")?.Value);
    }

    [Fact]
    public async Task GetAuthenticationStateAsync_PayloadEncodedWithBase64UrlCharacters_IsParsed()
    {
        // JWTs are base64url: '~' bytes encode to '-' in base64url (and '+' in plain base64).
        var payload = Payload(nickname: "~~~~~~");
        var jwt = Jwt(payload);
        Assert.Contains('-', jwt.Split('.')[1]);

        var provider = new CustomAuthStateProvider(ClientWithSession(Session(jwt)));

        var state = await provider.GetAuthenticationStateAsync();

        Assert.True(state.User.Identity?.IsAuthenticated);
        Assert.Equal("~~~~~~", state.User.FindFirst("nickname")?.Value);
    }

    [Fact]
    public async Task GetAuthenticationStateAsync_NotifiesSubscribers()
    {
        var provider = new CustomAuthStateProvider(ClientWithSession(Session(Jwt(Payload()))));
        Task<AuthenticationState>? notified = null;
        provider.AuthenticationStateChanged += task => notified = task;

        var state = await provider.GetAuthenticationStateAsync();

        Assert.NotNull(notified);
        Assert.Same(state, await notified);
    }

    private static Client ClientWithSession(Session? session)
    {
        var client = new Client("http://127.0.0.1:54321", "test-anon-key", new SupabaseOptions
        {
            AutoRefreshToken = false,
            AutoConnectRealtime = false,
            SessionHandler = new FakeSessionPersistence(session)
        });
        client.Auth.LoadSession();
        return client;
    }

    private static Session Session(string accessToken) => new()
    {
        AccessToken = accessToken,
        RefreshToken = "refresh",
        TokenType = "bearer",
        CreatedAt = DateTime.Now,
        ExpiresIn = 3600,
        User = new User { Id = UserId, Email = "ada@example.com" }
    };

    private static string Payload(string nickname = "ada") =>
        $$"""{"sub":"{{UserId}}","email":"ada@example.com","role":"authenticated","nickname":"{{nickname}}"}""";

    // An unsigned token is enough: the provider only decodes the payload, it doesn't verify the signature.
    private static string Jwt(string payloadJson) =>
        $"{Base64Url("""{"alg":"HS256","typ":"JWT"}""")}.{Base64Url(payloadJson)}.signature";

    private static string Base64Url(string json) =>
        Convert.ToBase64String(Encoding.UTF8.GetBytes(json)).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private sealed class FakeSessionPersistence(Session? session) : IGotrueSessionPersistence<Session>
    {
        public void SaveSession(Session saved) => session = saved;
        public void DestroySession() => session = null;
        public Session? LoadSession() => session;
    }
}
