using System.Security.Claims;
using MealWiz.Shared.Features.Authentication.Login;
using MealWiz.Shared.Features.Authentication.Logout;
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

    private static Mock<AuthenticationStateProvider> CreateAuthStateProvider()
    {
        var provider = new Mock<AuthenticationStateProvider>();
        provider.Setup(p => p.GetAuthenticationStateAsync())
            .ReturnsAsync(new AuthenticationState(new ClaimsPrincipal(new ClaimsIdentity())));
        return provider;
    }
}
