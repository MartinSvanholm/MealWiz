using MauiWASM.Tests.TestInfrastructure.E2E;
using Xunit;

namespace MauiWASM.Tests.E2ETests;

/// <summary>
/// The session lives in localStorage (CustomSupabaseSessionProvider) and is loaded while the Supabase client is
/// built at startup, so only a real reload in a real browser shows that it survives, and that logging out clears it.
/// </summary>
[Trait("Category", "E2E")]
[Trait("Category", "Smoke")]
public class AuthJourneyTests : E2ETestBase
{
    [Fact]
    public async Task LoggedInUser_StaysLoggedInAcrossAReload_AndIsSentToLoginAfterLoggingOut()
    {
        await LoginAsync();
        await Expect(Page).ToHaveURLAsync(UrlFor(""));

        await Page.ReloadAsync();
        await Expect(Page.GetByTestId("app-shell")).ToBeVisibleAsync();
        await Expect(Page.GetByTestId("meal-plan-week-title")).ToBeVisibleAsync();
        await Expect(Page).ToHaveURLAsync(UrlFor(""));

        await Page.GetByTestId("account-button").ClickAsync();
        await Expect(Page).ToHaveURLAsync(UrlFor("account"));

        await Page.GetByTestId("logout-button").ClickAsync();
        await Expect(Page).ToHaveURLAsync(UrlFor("login"));
        await Expect(Page.GetByTestId("login-email")).ToBeVisibleAsync();

        // A fresh page load must not find a session to restore.
        await GoToAsync("meals");
        await Expect(Page).ToHaveURLAsync(UrlFor("login"));
        await Expect(Page.GetByTestId("login-email")).ToBeVisibleAsync();
    }

    [Fact]
    public async Task SignedOutVisitor_IsSentToLogin()
    {
        await GoToAsync("grocerylist");

        await Expect(Page).ToHaveURLAsync(UrlFor("login"));
        await Expect(Page.GetByTestId("login-submit")).ToBeVisibleAsync();
    }
}
