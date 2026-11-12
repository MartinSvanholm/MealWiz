using System.Text.Json;
using MauiWASM.Tests.TestInfrastructure.E2E;
using Microsoft.Playwright;
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
    public async Task ExpiredAccessToken_IsRefreshedOnStartup_AndTheUserLandsInTheApp()
    {
        await LoginAsync();
        var before = await StoredSessionAsync("access_token");

        await RewriteStoredSessionAsync("s.created_at = new Date(Date.now() - 2 * 3600 * 1000).toISOString();");
        await Page.ReloadAsync();

        await Expect(Page.GetByTestId("meal-plan-week-title")).ToBeVisibleAsync();
        await Expect(Page).ToHaveURLAsync(UrlFor(""));
        Assert.NotEqual(before, await StoredSessionAsync("access_token"));
    }

    [Fact]
    public async Task RevokedRefreshToken_SendsTheUserToLogin()
    {
        await LoginAsync();

        await RewriteStoredSessionAsync(
            "s.created_at = new Date(Date.now() - 2 * 3600 * 1000).toISOString(); s.refresh_token = 'not-a-real-refresh-token';");
        await Page.ReloadAsync();

        await Expect(Page).ToHaveURLAsync(UrlFor("login"));
        await Expect(Page.GetByTestId("login-email")).ToBeVisibleAsync();
        Assert.Null(await StoredSessionAsync("refresh_token")); // nothing usable is left in storage
    }

    [Fact]
    public async Task Logout_ReloadsOnLogin_KeepsDarkMode_AndCannotBeGoneBackFrom()
    {
        await LoginAsync();
        await Page.GetByTestId("account-button").ClickAsync();
        await Page.GetByTestId("dark-mode-switch").ClickAsync();
        await Expect(Page.GetByTestId("theme-root")).ToHaveAttributeAsync("data-theme", "dark");

        await Page.GetByTestId("logout-button").ClickAsync();
        await Expect(Page).ToHaveURLAsync(UrlFor("login"));
        await Expect(Page.GetByTestId("login-email")).ToBeVisibleAsync();
        await Expect(Page.GetByTestId("theme-root")).ToHaveAttributeAsync("data-theme", "dark");

        await Page.GoBackAsync();
        await Expect(Page).ToHaveURLAsync(UrlFor("login"));

        await GoToAsync("");
        await Expect(Page).ToHaveURLAsync(UrlFor("login"));
    }

    /// <summary>Blazored.LocalStorage stores the session JSON as a JSON string, so it is parsed twice.</summary>
    private Task RewriteStoredSessionAsync(string mutation) => Page.EvaluateAsync(
        $"() => {{ const s = JSON.parse(JSON.parse(localStorage.getItem('SUPABASE_SESSION'))); {mutation} " +
        "localStorage.setItem('SUPABASE_SESSION', JSON.stringify(JSON.stringify(s))); }");

    private Task<string?> StoredSessionAsync(string field) => Page.EvaluateAsync<string?>(
        $"() => {{ const v = localStorage.getItem('SUPABASE_SESSION'); if (!v) return null; return JSON.parse(JSON.parse(v))['{field}'] ?? null; }}");

    [Fact]
    public async Task NewVisitor_CreatesAnAccountFromLogin_AndStaysSignedInAcrossAReload()
    {
        var email = $"e2e-signup-{Guid.NewGuid():N}@mealwiz.test";
        var password = Guid.NewGuid().ToString("N");
        await GoToAsync("login");

        await Page.GetByTestId("login-create-account").ClickAsync();
        await Expect(Page).ToHaveURLAsync(UrlFor("signup"));
        await FillAsync(Page.GetByTestId("signup-email"), email);
        await FillAsync(Page.GetByTestId("signup-password"), password);
        await FillAsync(Page.GetByTestId("signup-password-repeat"), password);
        await Page.GetByTestId("signup-submit").ClickAsync();

        await Expect(Page.GetByTestId("meal-plan-week-title")).ToBeVisibleAsync();
        await Expect(Page).ToHaveURLAsync(UrlFor(""));
        await DeleteSignedInUserAfterTheTestAsync(email, password);

        await Page.ReloadAsync();
        await Expect(Page.GetByTestId("meal-plan-week-title")).ToBeVisibleAsync();
        await Expect(Page).ToHaveURLAsync(UrlFor(""));
    }

    [Fact]
    public async Task SignedOutUser_ResetsAForgottenPasswordByEmail_AndLogsInWithTheNewOne()
    {
        var resetPage = new Uri(Env.App.BaseUrl, "reset-password").ToString();
        var newPassword = Guid.NewGuid().ToString("N");
        // Stand in for GoTrue's send, so the test sends no email and hits no email rate limit. The link it would have
        // emailed comes from the admin API below.
        string? sentRequest = null;
        await Page.RouteAsync("**/auth/v1/recover**", async route =>
        {
            var headers = new Dictionary<string, string>
            {
                ["Access-Control-Allow-Origin"] = "*",
                ["Access-Control-Allow-Headers"] = "*",
                ["Access-Control-Allow-Methods"] = "POST, OPTIONS"
            };
            if (route.Request.Method == "POST")
                sentRequest = Uri.UnescapeDataString(route.Request.Url) + route.Request.PostData;
            await route.FulfillAsync(new RouteFulfillOptions { Status = 200, Headers = headers, ContentType = "application/json", Body = "{}" });
        });
        await GoToAsync("login");

        await Page.GetByTestId("login-forgot-password").ClickAsync();
        await Expect(Page).ToHaveURLAsync(UrlFor("forgot-password"));
        await FillAsync(Page.GetByTestId("forgot-email"), User.Email);
        await Page.GetByTestId("forgot-submit").ClickAsync();
        await Expect(Page.GetByTestId("forgot-check-email")).ToContainTextAsync(User.Email);
        Assert.Contains(resetPage, sentRequest);

        await Page.GotoAsync(await Supabase.GenerateRecoveryLinkAsync(User, resetPage));
        await Expect(Page.GetByTestId("reset-password")).ToBeVisibleAsync();
        await Expect(Page).ToHaveURLAsync(UrlFor("reset-password"));
        await FillAsync(Page.GetByTestId("reset-password"), newPassword);
        await FillAsync(Page.GetByTestId("reset-password-repeat"), newPassword);
        await Page.GetByTestId("reset-submit").ClickAsync();
        await Expect(Page.GetByTestId("meal-plan-week-title")).ToBeVisibleAsync();
        await Expect(Page).ToHaveURLAsync(UrlFor(""));

        await Page.GetByTestId("account-button").ClickAsync();
        await Page.GetByTestId("logout-button").ClickAsync();
        await Expect(Page).ToHaveURLAsync(UrlFor("login"));
        await FillAsync(Page.GetByTestId("login-email"), User.Email);
        await FillAsync(Page.GetByTestId("login-password"), newPassword);
        await Page.GetByTestId("login-submit").ClickAsync();
        await Expect(Page.GetByTestId("meal-plan-week-title")).ToBeVisibleAsync();
    }

    [Fact]
    public async Task SignedInUser_ChangesThePassword_AndLogsInWithTheNewOne()
    {
        var newPassword = Guid.NewGuid().ToString("N");
        await LoginAsync();
        await Page.GetByTestId("account-button").ClickAsync();

        await FillAsync(Page.GetByTestId("change-password-new"), newPassword);
        await FillAsync(Page.GetByTestId("change-password-repeat"), newPassword);
        await Page.GetByTestId("change-password-submit").ClickAsync();
        await Expect(Page.GetByText("Password changed")).ToBeVisibleAsync();
        await Expect(Page.GetByTestId("account-page")).ToBeVisibleAsync(); // still signed in

        await Page.GetByTestId("logout-button").ClickAsync();
        await Expect(Page).ToHaveURLAsync(UrlFor("login"));
        await FillAsync(Page.GetByTestId("login-email"), User.Email);
        await FillAsync(Page.GetByTestId("login-password"), newPassword);
        await Page.GetByTestId("login-submit").ClickAsync();
        await Expect(Page.GetByTestId("meal-plan-week-title")).ToBeVisibleAsync();
    }

    // The local GoTrue applies the change on the first click of the new address's link (autoconfirm), so the "click
    // the other address's link" state is covered by component tests.
    [Fact]
    public async Task SignedInUser_ChangesTheEmail_FromTheEmailLink_AndLogsInWithTheNewAddress()
    {
        var newEmail = $"e2e-changed-{Guid.NewGuid():N}@mealwiz.test";
        await LoginAsync();
        await Page.GetByTestId("account-button").ClickAsync();

        await FillAsync(Page.GetByTestId("change-email-new"), newEmail);
        await Page.GetByTestId("change-email-submit").ClickAsync();
        await Expect(Page.GetByTestId("account-email-pending")).ToContainTextAsync(newEmail);

        await Page.GotoAsync(await Supabase.ReadEmailChangeLinkAsync(newEmail));
        await Expect(Page.GetByTestId("email-change-done")).ToBeVisibleAsync();
        await Expect(Page).ToHaveURLAsync(UrlFor("email-change"));

        await Page.GetByTestId("email-change-account-link").ClickAsync();
        await Expect(Page.GetByTestId("account-email")).ToHaveTextAsync(newEmail);
        await Expect(Page.GetByTestId("account-email-pending")).ToHaveCountAsync(0);

        await Page.GetByTestId("logout-button").ClickAsync();
        await Expect(Page).ToHaveURLAsync(UrlFor("login"));
        await FillAsync(Page.GetByTestId("login-email"), newEmail);
        await FillAsync(Page.GetByTestId("login-password"), User.Password);
        await Page.GetByTestId("login-submit").ClickAsync();
        await Expect(Page.GetByTestId("meal-plan-week-title")).ToBeVisibleAsync();
    }

    /// <summary>The new user's id is only known to the app, from the session it stored in localStorage.</summary>
    private async Task DeleteSignedInUserAfterTheTestAsync(string email, string password)
    {
        var stored = await Page.EvaluateAsync<string>("() => localStorage.getItem('SUPABASE_SESSION')");
        using var json = JsonDocument.Parse(stored);
        // Blazored.LocalStorage stores the session JSON as a JSON string, so it's encoded twice.
        using var session = json.RootElement.ValueKind == JsonValueKind.String ? JsonDocument.Parse(json.RootElement.GetString()!) : null;
        var id = (session ?? json).RootElement.GetProperty("user").GetProperty("id").GetString();
        DeleteAfterTheTest(new E2EUser(Guid.Parse(id!), email, password));
    }

    // The Function isn't running in E2E, so the route plays it: it deletes the user through the admin API (as the
    // Function would) and answers 204. The app must then drop its session and reload on /login.
    [Fact]
    public async Task SignedInUser_DeletesTheAccount_AndCannotLogInAgain()
    {
        await Page.RouteAsync($"**{E2EAppHost.FunctionsPath}/api/DeleteAccount", async route =>
        {
            await Supabase.DeleteUserAsync(User);
            await route.FulfillAsync(new RouteFulfillOptions { Status = 204 });
        });
        await LoginAsync();
        await Page.GetByTestId("account-button").ClickAsync();

        await Page.GetByTestId("delete-account-open").ClickAsync();
        await FillAsync(Page.GetByTestId("delete-account-password"), User.Password);
        await Page.GetByTestId("delete-account-confirm").ClickAsync();

        await Expect(Page).ToHaveURLAsync(UrlFor("login"));
        await FillAsync(Page.GetByTestId("login-email"), User.Email);
        await FillAsync(Page.GetByTestId("login-password"), User.Password);
        await Page.GetByTestId("login-submit").ClickAsync();
        await Expect(Page.Locator(".mud-snackbar")).ToBeVisibleAsync(); // the login error
        await Expect(Page.GetByTestId("meal-plan-week-title")).ToHaveCountAsync(0);
        await Expect(Page).ToHaveURLAsync(UrlFor("login"));
    }

    [Fact]
    public async Task SignedInUser_EntersTheWrongPassword_SeesAnErrorAndStaysOnTheAccountPage()
    {
        await Page.RouteAsync($"**{E2EAppHost.FunctionsPath}/api/DeleteAccount", route =>
            route.FulfillAsync(new RouteFulfillOptions { Status = 403 }));
        await LoginAsync();
        await Page.GetByTestId("account-button").ClickAsync();

        await Page.GetByTestId("delete-account-open").ClickAsync();
        await FillAsync(Page.GetByTestId("delete-account-password"), "not-my-password");
        await Page.GetByTestId("delete-account-confirm").ClickAsync();

        await Expect(Page.GetByTestId("delete-account-error")).ToHaveTextAsync("That password isn't right.");
        await Expect(Page.GetByTestId("account-page")).ToBeVisibleAsync();
        await Expect(Page).ToHaveURLAsync(UrlFor("account"));
    }

    [Fact]
    public async Task SignedOutVisitor_IsSentToLogin()
    {
        await GoToAsync("grocerylist");

        await Expect(Page).ToHaveURLAsync(UrlFor("login"));
        await Expect(Page.GetByTestId("login-submit")).ToBeVisibleAsync();
    }
}
