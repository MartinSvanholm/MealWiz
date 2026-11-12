using System.Security.Claims;
using System.Text;
using MealWiz.Shared.Services.Authentication;
using Microsoft.AspNetCore.Components.Authorization;
using Supabase;
using Supabase.Gotrue;
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
        var provider = ProviderWith(null);

        var state = await provider.GetAuthenticationStateAsync();

        Assert.False(state.User.Identity?.IsAuthenticated);
        Assert.Empty(state.User.Claims);
    }

    [Fact]
    public async Task GetAuthenticationStateAsync_ValidSession_IsAuthenticatedWithTheJwtClaims()
    {
        var provider = ProviderWith(Session(Jwt(Payload())));

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

        var provider = ProviderWith(Session(jwt));

        var state = await provider.GetAuthenticationStateAsync();

        Assert.True(state.User.Identity?.IsAuthenticated);
        Assert.Equal("~~~~~~", state.User.FindFirst("nickname")?.Value);
    }

    [Fact]
    public async Task GetAuthenticationStateAsync_ExpiredAccessTokenWithRefreshToken_IsStillAuthenticated()
    {
        // The keeper refreshes it; offline the user must stay signed in.
        var session = Session(Jwt(Payload()));
        session.CreatedAt = DateTime.Now.AddHours(-2);
        session.ExpiresIn = 3600;
        var provider = ProviderWith(session);

        var state = await provider.GetAuthenticationStateAsync();

        Assert.True(state.User.Identity?.IsAuthenticated);
    }

    [Fact]
    public async Task GetAuthenticationStateAsync_NoRefreshToken_IsAnonymous()
    {
        var session = Session(Jwt(Payload()));
        session.RefreshToken = null;
        var provider = ProviderWith(session);

        var state = await provider.GetAuthenticationStateAsync();

        Assert.False(state.User.Identity?.IsAuthenticated);
    }

    [Fact]
    public async Task GetAuthenticationStateAsync_AfterLocalSignOut_IsAnonymous()
    {
        var provider = ProviderWith(Session(Jwt(Payload())), out var keeper);

        await keeper.SignOutLocallyAsync();
        var state = await provider.GetAuthenticationStateAsync();

        Assert.False(state.User.Identity?.IsAuthenticated);
    }

    [Fact]
    public async Task GetAuthenticationStateAsync_DoesNotNotifySubscribers()
    {
        var provider = ProviderWith(Session(Jwt(Payload())));
        var notified = false;
        provider.AuthenticationStateChanged += _ => notified = true;

        await provider.GetAuthenticationStateAsync();

        Assert.False(notified);
    }

    [Fact]
    public async Task KeeperChange_NotifiesSubscribersWithTheNewState()
    {
        var provider = ProviderWith(Session(Jwt(Payload())), out var keeper);
        Task<AuthenticationState>? notified = null;
        provider.AuthenticationStateChanged += task => notified = task;

        await keeper.SignOutLocallyAsync();

        Assert.NotNull(notified);
        Assert.False((await notified).User.Identity?.IsAuthenticated);
    }

    [Fact]
    public async Task SdkAuthEvent_NotifiesSubscribers()
    {
        var provider = ProviderWith(Session(Jwt(Payload())), out _, out var client);
        var notified = 0;
        provider.AuthenticationStateChanged += _ => notified++;

        ((Supabase.Gotrue.Client)client.Auth).NotifyAuthStateChange(Constants.AuthState.TokenRefreshed);
        ((Supabase.Gotrue.Client)client.Auth).NotifyAuthStateChange(Constants.AuthState.SignedOut);

        Assert.Equal(2, notified);
        await Task.CompletedTask;
    }

    private static CustomAuthStateProvider ProviderWith(Session? session) => ProviderWith(session, out _);

    private static CustomAuthStateProvider ProviderWith(Session? session, out SessionKeeper keeper) =>
        ProviderWith(session, out keeper, out _);

    private static CustomAuthStateProvider ProviderWith(Session? session, out SessionKeeper keeper, out Client client)
    {
        var persistence = new FakeSessionPersistence(session);
        client = new Client("http://127.0.0.1:54321", "test-anon-key", new SupabaseOptions
        {
            AutoRefreshToken = false,
            AutoConnectRealtime = false,
            SessionHandler = persistence
        });
        client.Auth.LoadSession();
        keeper = new SessionKeeper(client, persistence, TimeProvider.System);
        return new CustomAuthStateProvider(client, keeper);
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
