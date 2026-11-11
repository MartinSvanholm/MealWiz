using MealWiz.Shared.Features.Account;
using MealWiz.Shared.Features.Account.ChangeEmail;
using MealWiz.Shared.Features.Account.ChangePassword;
using MealWiz.Shared.Features.Account.GetPendingEmailChange;
using MealWiz.Shared.Features.Authentication.EmailLinks;
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

    /// <summary>On the redirect allow list in supabase/config.toml, like the E2E app host's.</summary>
    private const string EmailChangeRedirect = "http://127.0.0.1:5053/email-change";

    private static readonly EmailLinkBuilder Links = new(null!, new EmailLinkOptions("http://127.0.0.1:5053/"));

    private ChangeEmail.Handler ChangeEmailHandler(TestUser user) =>
        new(user.Client, new HttpClient(), supabase.AuthSettings, Links, TestLocalizer.For<AccountResource>());

    private GetPendingEmailChange.Handler PendingHandler(TestUser user) =>
        new(user.Client, new HttpClient(), supabase.AuthSettings);

    private static string NewAddress() => $"changed-{Guid.NewGuid():N}@mealwiz.test";

    [Fact]
    public async Task ChangeEmail_ThenPendingQuery_ReturnsTheNewAddress_AndNothingBeforeIt()
    {
        Assert.SkipUnless(supabase.IsAvailable, supabase.SkipReason ?? string.Empty);
        var user = await supabase.CreateSignedInUserAsync();
        var newEmail = NewAddress();
        Assert.Null(ResultAssert.Ok(await PendingHandler(user).Handle(new GetPendingEmailChange.Query(), CancellationToken)));

        var result = await ChangeEmailHandler(user).Handle(new ChangeEmail.Command(newEmail), CancellationToken);

        ResultAssert.Ok(result);
        Assert.Equal(newEmail, ResultAssert.Ok(await PendingHandler(user).Handle(new GetPendingEmailChange.Query(), CancellationToken)));
    }

    [Fact]
    public async Task ChangeEmail_FollowingTheNewAddressLink_ChangesTheEmail()
    {
        Assert.SkipUnless(supabase.IsAvailable, supabase.SkipReason ?? string.Empty);
        var user = await supabase.CreateSignedInUserAsync();
        var newEmail = NewAddress();
        ResultAssert.Ok(await ChangeEmailHandler(user).Handle(new ChangeEmail.Command(newEmail), CancellationToken));
        var links = await supabase.ReadEmailChangeLinksAsync(user.Email, newEmail);

        var applied = await supabase.FollowLinkAsync(links.ToNew);

        Assert.Equal(EmailChangeRedirect, applied.GetLeftPart(UriPartial.Path));
        Assert.Contains("type=email_change", applied.Fragment);
        var signIn = await supabase.CreateSignedOutClientAsync();
        Assert.NotNull(await signIn.Auth.SignIn(newEmail, user.Password));
    }

    [Fact]
    public async Task ChangeEmail_AnAddressOfAnotherAccount_FailsWithItsOwnMessage()
    {
        Assert.SkipUnless(supabase.IsAvailable, supabase.SkipReason ?? string.Empty);
        var user = await supabase.CreateSignedInUserAsync();
        var other = await supabase.CreateSignedInUserAsync();

        var result = await ChangeEmailHandler(user).Handle(new ChangeEmail.Command(other.Email), CancellationToken);

        ResultAssert.Failed(result, "That email already belongs to another account.");
    }

    [Fact]
    public async Task ChangeEmail_NotAnAddress_FailsWithItsOwnMessage()
    {
        Assert.SkipUnless(supabase.IsAvailable, supabase.SkipReason ?? string.Empty);
        var user = await supabase.CreateSignedInUserAsync();

        var result = await ChangeEmailHandler(user).Handle(new ChangeEmail.Command("not-an-address"), CancellationToken);

        ResultAssert.Failed(result, "Enter a valid email address.");
    }
}
