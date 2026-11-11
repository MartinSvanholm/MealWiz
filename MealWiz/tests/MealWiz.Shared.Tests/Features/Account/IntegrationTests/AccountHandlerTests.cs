using MealWiz.Shared.Features.Account;
using MealWiz.Shared.Features.Account.ChangePassword;
using MealWiz.Shared.Tests.TestInfrastructure;
using Xunit;

namespace MealWiz.Shared.Tests.Features.Account.IntegrationTests;

/// <summary>Change password and email against the local GoTrue, each test on a throwaway user of its own.</summary>
[Trait("Category", "Integration")]
public class AccountHandlerTests(LocalSupabaseFixture supabase) : IClassFixture<LocalSupabaseFixture>
{
    private static CancellationToken CancellationToken => TestContext.Current.CancellationToken;

    [Fact]
    public async Task ChangePassword_SetsTheNewPassword_AndKeepsTheCurrentSession()
    {
        Assert.SkipUnless(supabase.IsAvailable, supabase.SkipReason ?? string.Empty);
        var user = await supabase.CreateSignedInUserAsync();
        var newPassword = Guid.NewGuid().ToString("N");

        var result = await new ChangePassword.Handler(user.Client, TestLocalizer.For<AccountResource>())
            .Handle(new ChangePassword.Command(newPassword), CancellationToken);

        ResultAssert.Ok(result);
        Assert.Contains(result.Successes, s => s.Message == "Password changed");
        var signIn = await supabase.CreateSignedOutClientAsync();
        Assert.NotNull(await signIn.Auth.SignIn(user.Email, newPassword));
        var oldClient = await supabase.CreateSignedOutClientAsync();
        await Assert.ThrowsAnyAsync<Exception>(() => oldClient.Auth.SignIn(user.Email, user.Password));
        // The session that changed it still refreshes (the other sessions are the ones revoked).
        var current = user.Client.Auth.CurrentSession!;
        await ((Supabase.Gotrue.Client)user.Client.Auth).RefreshToken(current.AccessToken!, current.RefreshToken!);
        Assert.NotEqual(current.AccessToken, user.Client.Auth.CurrentSession!.AccessToken);
    }

    [Fact]
    public async Task ChangePassword_TheSamePassword_FailsWithItsOwnMessage()
    {
        Assert.SkipUnless(supabase.IsAvailable, supabase.SkipReason ?? string.Empty);
        var user = await supabase.CreateSignedInUserAsync();

        var result = await new ChangePassword.Handler(user.Client, TestLocalizer.For<AccountResource>())
            .Handle(new ChangePassword.Command(user.Password), CancellationToken);

        ResultAssert.Failed(result, "Choose a password different from your current one.");
    }

    [Fact]
    public async Task ChangePassword_ATooShortPassword_FailsWithItsOwnMessage()
    {
        Assert.SkipUnless(supabase.IsAvailable, supabase.SkipReason ?? string.Empty);
        var user = await supabase.CreateSignedInUserAsync();

        // Below the minimum of 6 in supabase/config.toml.
        var result = await new ChangePassword.Handler(user.Client, TestLocalizer.For<AccountResource>())
            .Handle(new ChangePassword.Command("short"), CancellationToken);

        ResultAssert.Failed(result, "Use at least 6 characters.");
    }
}
