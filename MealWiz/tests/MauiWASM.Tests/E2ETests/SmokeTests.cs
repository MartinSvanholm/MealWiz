using MauiWASM.Tests.TestInfrastructure.E2E;
using Microsoft.Playwright;
using Xunit;

namespace MauiWASM.Tests.E2ETests;

/// <summary>
/// The quick "does the app still work" check that <c>scripts/verify smoke</c> runs: the app boots cleanly and every
/// page in the nav renders. A page that throws fails here with its route and the browser's error in the message,
/// so the verify summary points straight at it.
/// </summary>
[Trait("Category", "E2E")]
[Trait("Category", "Smoke")]
public class SmokeTests : E2ETestBase
{
    [Fact]
    public async Task App_BootsWithoutBrowserErrors()
    {
        await GoToAsync("login");
        await Expect(Page.GetByTestId("login-email")).ToBeVisibleAsync();

        Assert.True(BrowserErrors.Count == 0, $"The app logged errors while booting:\n{Describe(BrowserErrors)}");
    }

    // The nav's Freezer link has no page yet (/freezer is the router's NotFound), so it isn't listed.
    [Theory]
    [InlineData("MealPlanPage", "/", "meal-plan-week-title")]
    [InlineData("GroceryListPage", "/grocerylist", "add-grocery-item-button")]
    [InlineData("MealsPage", "/meals", "meal-list-item")]
    [InlineData("AccountPage", "/account", "logout-button")]
    public async Task Page_Renders(string page, string path, string marker)
    {
        await LoginAsync();

        // A full load, not a nav click, so each page also has to cope with being opened directly.
        await Page.GotoAsync(path.TrimStart('/'));

        var content = Page.GetByTestId(marker).First;
        var errorUi = Page.Locator("#blazor-error-ui");
        try
        {
            await Expect(content.Or(errorUi)).ToBeVisibleAsync();
        }
        catch (PlaywrightException)
        {
            // Neither showed up in time. Reported below together with whatever the browser logged.
        }

        string? problem = null;
        if (await errorUi.IsVisibleAsync())
            problem = "Blazor's \"An unhandled error has occurred\" bar is showing";
        else if (!await content.IsVisibleAsync())
            problem = $"[data-testid={marker}] never became visible";
        else if (BrowserErrors.Count > 0)
            problem = "it rendered, but the browser logged errors";

        Assert.True(problem is null, $"{path} ({page}) didn't render: {problem}.\n{Describe(BrowserErrors)}");
    }

    private static string Describe(IReadOnlyList<string> errors)
    {
        if (errors.Count == 0) return "No browser errors were logged.";

        // The first error is usually the cause; the rest are often fallout. Keep it short enough to read in a summary.
        var first = errors[0].Length > 1500 ? errors[0][..1500] + "..." : errors[0];
        return $"Browser errors ({errors.Count}), first:\n{first}";
    }
}
