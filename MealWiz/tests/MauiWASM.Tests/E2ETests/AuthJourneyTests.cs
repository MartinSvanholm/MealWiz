using System.Text.Json;
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

    [Fact]
    public async Task SignedOutVisitor_IsSentToLogin()
    {
        await GoToAsync("grocerylist");

        await Expect(Page).ToHaveURLAsync(UrlFor("login"));
        await Expect(Page.GetByTestId("login-submit")).ToBeVisibleAsync();
    }
}
