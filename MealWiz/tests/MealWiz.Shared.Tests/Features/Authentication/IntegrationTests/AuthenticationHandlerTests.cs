using System.Security.Claims;
using MealWiz.Shared.Features.Authentication.Login;
using MealWiz.Shared.Features.Authentication.Logout;
using MealWiz.Shared.Features.Authentication.ResetPassword;
using MealWiz.Shared.Features.Authentication.SignUp;
using MealWiz.Shared.Tests.TestInfrastructure;
using Microsoft.AspNetCore.Components.Authorization;
using MealWiz.Shared.Services.Authentication;
using Moq;
using Supabase;
using Supabase.Gotrue;
using Supabase.Gotrue.Interfaces;
using Client = Supabase.Client;
using Xunit;

namespace MealWiz.Shared.Tests.Features.Authentication.IntegrationTests;

/// <summary>
/// Signs the class's user in and out through the handlers against the local GoTrue, each on a client of its own so
/// the fixture's session isn't touched. The auth state provider is a mock: the handlers only ask it to refresh.
/// </summary>
[Trait("Category", "Integration")]
public class AuthenticationHandlerTests(LocalSupabaseFixture supabase) : IClassFixture<LocalSupabaseFixture>
{
    private static CancellationToken CancellationToken => TestContext.Current.CancellationToken;

    private readonly Mock<AuthenticationStateProvider> _authStateProvider = CreateAuthStateProvider();

    [Fact]
    public async Task Login_CorrectCredentials_SignsInAndRefreshesTheAuthState()
    {
        Assert.SkipUnless(supabase.IsAvailable, supabase.SkipReason ?? string.Empty);
        var client = await supabase.CreateSignedOutClientAsync();

        var result = await new Login.Handler(client, _authStateProvider.Object)
            .Handle(new Login.Command(supabase.User.Email, supabase.User.Password), CancellationToken);

        var session = ResultAssert.Ok(result);
        Assert.NotNull(session);
        Assert.Equal(supabase.UserId.ToString(), session.User?.Id);
        Assert.Equal(supabase.UserId.ToString(), client.Auth.CurrentUser?.Id);
        _authStateProvider.Verify(p => p.GetAuthenticationStateAsync(), Times.Once);
    }

    [Fact]
    public async Task Login_WrongPassword_FailsAndStillRefreshesTheAuthState()
    {
        Assert.SkipUnless(supabase.IsAvailable, supabase.SkipReason ?? string.Empty);
        var client = await supabase.CreateSignedOutClientAsync();

        var result = await new Login.Handler(client, _authStateProvider.Object)
            .Handle(new Login.Command(supabase.User.Email, "not-the-password"), CancellationToken);

        ResultAssert.Failed(result);
        Assert.Null(client.Auth.CurrentSession);
        _authStateProvider.Verify(p => p.GetAuthenticationStateAsync(), Times.Once);
    }

    [Fact]
    public async Task Logout_SignsOutAndClearsTheStoredSession()
    {
        Assert.SkipUnless(supabase.IsAvailable, supabase.SkipReason ?? string.Empty);
        var (client, persistence, keeper) = await SignedInClientAsync(supabase.User.Email, supabase.User.Password);

        var result = await new Logout.Handler(client, keeper).Handle(new Logout.Command(), CancellationToken);

        ResultAssert.Ok(result);
        Assert.Null(client.Auth.CurrentSession);
        Assert.True(persistence.Destroyed);
        Assert.True(keeper.IsSignedOut);
    }

    // The server refuses the sign-out of a deleted user (its token is dead), but the local sign-out still works.
    [Fact]
    public async Task LocalSignOut_ClearsTheSessionEvenForAUserDeletedOnTheServer()
    {
        Assert.SkipUnless(supabase.IsAvailable, supabase.SkipReason ?? string.Empty);
        var user = await supabase.CreateSignedInUserAsync();
        var (client, persistence, keeper) = await SignedInClientAsync(user.Email, user.Password);
        await supabase.DeleteUserNowAsync(user.Id);

        await keeper.SignOutLocallyAsync();

        Assert.True(keeper.IsSignedOut);
        Assert.True(persistence.Destroyed);
        var state = await new CustomAuthStateProvider(client, keeper).GetAuthenticationStateAsync();
        Assert.False(state.User.Identity?.IsAuthenticated);
    }

    [Fact]
    public async Task Logout_WhenTheServerRefusesTheSignOut_FailsAndKeepsTheSession()
    {
        Assert.SkipUnless(supabase.IsAvailable, supabase.SkipReason ?? string.Empty);
        var user = await supabase.CreateSignedInUserAsync();
        var (client, persistence, keeper) = await SignedInClientAsync(user.Email, user.Password);
        await supabase.DeleteUserNowAsync(user.Id);

        var result = await new Logout.Handler(client, keeper).Handle(new Logout.Command(), CancellationToken);

        ResultAssert.Failed(result);
        Assert.False(persistence.Destroyed);
        Assert.False(keeper.IsSignedOut);
    }

    private async Task<(Client Client, MemoryPersistence Persistence, SessionKeeper Keeper)> SignedInClientAsync(string email, string password)
    {
        var persistence = new MemoryPersistence();
        var client = await supabase.CreateSignedOutClientAsync(persistence);
        await client.Auth.SignIn(email, password);
        Assert.NotNull(client.Auth.CurrentSession);
        var keeper = new SessionKeeper(client, persistence, TimeProvider.System);
        await keeper.StartAsync();
        return (client, persistence, keeper);
    }

    private sealed class MemoryPersistence : IGotrueSessionPersistence<Session>
    {
        private Session? _session;
        public bool Destroyed { get; private set; }
        public void SaveSession(Session session) { _session = session; Destroyed = false; }
        public void DestroySession() { _session = null; Destroyed = true; }
        public Session? LoadSession() => _session;
    }

    [Fact]
    public async Task SignUp_NewEmail_CreatesTheUserSignsInAndRefreshesTheAuthState()
    {
        Assert.SkipUnless(supabase.IsAvailable, supabase.SkipReason ?? string.Empty);
        var client = await supabase.CreateSignedOutClientAsync();
        var email = $"signup-{Guid.NewGuid():N}@mealwiz.test";

        var result = await new SignUp.Handler(client, _authStateProvider.Object)
            .Handle(new SignUp.Command(email, Guid.NewGuid().ToString("N")), CancellationToken);

        var session = ResultAssert.Ok(result);
        Assert.NotNull(session?.User?.Id);
        supabase.DeleteOnDispose(Guid.Parse(session.User.Id));
        Assert.Equal(email, session.User.Email);
        Assert.Equal(session.User.Id, client.Auth.CurrentUser?.Id);
        _authStateProvider.Verify(p => p.GetAuthenticationStateAsync(), Times.Once);
    }

    [Fact]
    public async Task SignUp_EmailAlreadyRegistered_FailsWithoutSigningIn()
    {
        Assert.SkipUnless(supabase.IsAvailable, supabase.SkipReason ?? string.Empty);
        var client = await supabase.CreateSignedOutClientAsync();

        var result = await new SignUp.Handler(client, _authStateProvider.Object)
            .Handle(new SignUp.Command(supabase.User.Email, Guid.NewGuid().ToString("N")), CancellationToken);

        ResultAssert.Failed(result);
        Assert.Null(client.Auth.CurrentSession);
        _authStateProvider.Verify(p => p.GetAuthenticationStateAsync(), Times.Once);
    }

    // A user of its own: the class's user keeps its password for the other tests.
    [Fact]
    public async Task ResetPassword_FromTheEmailLink_SetsTheNewPasswordSignsInAndRefreshesTheAuthState()
    {
        Assert.SkipUnless(supabase.IsAvailable, supabase.SkipReason ?? string.Empty);
        var user = await supabase.CreateSignedInUserAsync();
        var link = await supabase.FollowPasswordResetLinkAsync(user.Email, ResetPasswordRedirect);
        Assert.Equal(ResetPasswordRedirect, link.GetLeftPart(UriPartial.Path));
        var client = await supabase.CreateSignedOutClientAsync();
        var newPassword = Guid.NewGuid().ToString("N");

        var result = await new ResetPassword.Handler(client, _authStateProvider.Object)
            .Handle(new ResetPassword.Command(link, newPassword), CancellationToken);

        var session = ResultAssert.Ok(result);
        Assert.Equal(user.Id.ToString(), session?.User?.Id);
        Assert.Equal(user.Id.ToString(), client.Auth.CurrentUser?.Id);
        _authStateProvider.Verify(p => p.GetAuthenticationStateAsync(), Times.Once);

        var signIn = await supabase.CreateSignedOutClientAsync();
        Assert.NotNull(await signIn.Auth.SignIn(user.Email, newPassword));
        await Assert.ThrowsAnyAsync<Exception>(() => signIn.Auth.SignIn(user.Email, user.Password));
    }

    // What GoTrue redirects to for an expired or already used link.
    [Fact]
    public async Task ResetPassword_ExpiredLink_FailsWithoutSigningIn()
    {
        Assert.SkipUnless(supabase.IsAvailable, supabase.SkipReason ?? string.Empty);
        var client = await supabase.CreateSignedOutClientAsync();
        var link = new Uri($"{ResetPasswordRedirect}#error=access_denied&error_code=otp_expired&error_description=Email+link+is+invalid+or+has+expired");

        var result = await new ResetPassword.Handler(client, _authStateProvider.Object)
            .Handle(new ResetPassword.Command(link, Guid.NewGuid().ToString("N")), CancellationToken);

        ResultAssert.Failed(result);
        Assert.Null(client.Auth.CurrentSession);
    }

    [Fact]
    public async Task ResetPassword_NewPasswordRefused_FailsAndLeavesTheUserSignedOut()
    {
        Assert.SkipUnless(supabase.IsAvailable, supabase.SkipReason ?? string.Empty);
        var user = await supabase.CreateSignedInUserAsync();
        var link = await supabase.FollowPasswordResetLinkAsync(user.Email, ResetPasswordRedirect);
        var client = await supabase.CreateSignedOutClientAsync();

        // Below the minimum of 6 in supabase/config.toml.
        var result = await new ResetPassword.Handler(client, _authStateProvider.Object)
            .Handle(new ResetPassword.Command(link, "short"), CancellationToken);

        ResultAssert.Failed(result);
        Assert.Null(client.Auth.CurrentSession);
        _authStateProvider.Verify(p => p.GetAuthenticationStateAsync(), Times.Once);
    }

    /// <summary>On the redirect allow list in supabase/config.toml, like the E2E app host's.</summary>
    private const string ResetPasswordRedirect = "http://127.0.0.1:5053/reset-password";

    private static Mock<AuthenticationStateProvider> CreateAuthStateProvider()
    {
        var provider = new Mock<AuthenticationStateProvider>();
        provider.Setup(p => p.GetAuthenticationStateAsync())
            .ReturnsAsync(new AuthenticationState(new ClaimsPrincipal(new ClaimsIdentity())));
        return provider;
    }
}
