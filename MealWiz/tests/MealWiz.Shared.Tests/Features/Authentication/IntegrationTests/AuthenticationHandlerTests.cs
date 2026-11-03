using System.Security.Claims;
using MealWiz.Shared.Features.Authentication.Login;
using MealWiz.Shared.Features.Authentication.Logout;
using MealWiz.Shared.Features.Authentication.ResetPassword;
using MealWiz.Shared.Features.Authentication.SignUp;
using MealWiz.Shared.Tests.TestInfrastructure;
using Microsoft.AspNetCore.Components.Authorization;
using Moq;
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
    public async Task Logout_SignsOutAndRefreshesTheAuthState()
    {
        Assert.SkipUnless(supabase.IsAvailable, supabase.SkipReason ?? string.Empty);
        var client = await supabase.CreateSignedOutClientAsync();
        await client.Auth.SignIn(supabase.User.Email, supabase.User.Password);
        Assert.NotNull(client.Auth.CurrentSession);

        var result = await new Logout.Handler(client, _authStateProvider.Object).Handle(new Logout.Command(), CancellationToken);

        ResultAssert.Ok(result);
        Assert.Null(client.Auth.CurrentSession);
        _authStateProvider.Verify(p => p.GetAuthenticationStateAsync(), Times.Once);
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
