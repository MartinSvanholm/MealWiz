using System.Text.Json;
using MauiWASM.Tests.TestInfrastructure.E2E;
using Microsoft.Playwright;
using Xunit;

namespace MauiWASM.Tests.E2ETests;

/// <summary>
/// The import from URL, with the Functions app played by the test. The browser's call to ScrapeRecipe is
/// intercepted and acknowledged straight away, leaving the row pending, and the test then writes the result with the
/// service role, as ScrapeRecipeHandler does. Because the app has already re-read the row after the acknowledgement,
/// the result can only reach it over Realtime. The scraping itself is covered by MealWiz.Functions.Tests.
/// </summary>
[Trait("Category", "E2E")]
public class MealImportJourneyTests : E2ETestBase
{
    private const string SourceUrl = "https://recipes.mealwiz.test/aeblepandekager";

    [Fact]
    public async Task ImportedMeal_ArrivesOverRealtime_AndCanBeSaved()
    {
        var jobId = await StartImportAsync(acknowledgeWith: 200);

        await Supabase.ResolveMealImportAsync(jobId, "succeeded", parsedMeal: new
        {
            Name = "Æblepandekager",
            Recipe = "Pisk dejen.\nSteg pandekagerne.",
            Ingredients = new[]
            {
                new { Name = "sødmælk", Amount = "2½ dl" },
                new { Name = "salt", Amount = "" }
            }
        });

        // Opens the meal form filled in with the import, for the user to review
        await Expect(Page).ToHaveURLAsync(UrlFor("meals/edit"));
        await Expect(Page.GetByTestId("meal-name-input")).ToHaveValueAsync("Æblepandekager");
        await Expect(Page.GetByTestId("ingredient-card")).ToHaveCountAsync(2);
        await Expect(Row("ingredient-card", "sødmælk")).ToContainTextAsync("2½ dl");

        await Page.GetByTestId("save-meal-button").ClickAsync();
        await Expect(Page).ToHaveURLAsync(UrlFor("meals"));
        await Expect(Row("meal-list-item", "Æblepandekager")).ToBeVisibleAsync();

        await Page.ReloadAsync();
        await Row("meal-list-item", "Æblepandekager").ClickAsync();
        await Expect(Row("ingredient-card", "salt")).ToBeVisibleAsync();
        await Expect(Row("ingredient-card", "sødmælk")).ToContainTextAsync("2½ dl");
    }

    [Fact]
    public async Task FailedImport_ArrivesOverRealtime_AndShowsTheReason()
    {
        const string reason = "Could not find recipe data on the page.";
        var jobId = await StartImportAsync(acknowledgeWith: 200);

        await Supabase.ResolveMealImportAsync(jobId, "failed", errorMessage: reason);

        await Expect(Page.GetByTestId("import-job-error")).ToContainTextAsync(reason);
        await Expect(Page.GetByTestId("import-submit")).ToHaveTextAsync("Try again");
        await Expect(Page).ToHaveURLAsync(UrlFor("meals/import"));
    }

    [Fact]
    public async Task ImportFunctionError_FailsTheImport()
    {
        var jobId = await StartImportAsync(acknowledgeWith: 502);

        // The Function never wrote a result, so the app fails the row itself.
        await Expect(Page.GetByTestId("import-job-error")).ToBeVisibleAsync();
        await Expect(Page.GetByTestId("import-submit")).ToHaveTextAsync("Try again");
        Assert.Equal("failed", await Supabase.GetMealImportStatusAsync(jobId));
    }

    /// <summary>
    /// Signs in, submits <see cref="SourceUrl"/> on the import page and returns the job id the app sent to the
    /// Function, once the Function has answered with <paramref name="acknowledgeWith"/>.
    /// </summary>
    private async Task<Guid> StartImportAsync(int acknowledgeWith)
    {
        var request = new TaskCompletionSource<(Guid JobId, string Url)>(TaskCreationOptions.RunContinuationsAsynchronously);

        await Page.RouteAsync($"**{E2EAppHost.FunctionsPath}/api/ScrapeRecipe", async route =>
        {
            var body = route.Request.PostDataJSON() ?? throw new InvalidOperationException("ScrapeRecipe was called without a body.");
            var jobId = body.GetProperty("jobId").GetGuid();

            await route.FulfillAsync(new RouteFulfillOptions
            {
                Status = acknowledgeWith,
                ContentType = "application/json",
                Body = acknowledgeWith == 200 ? JsonSerializer.Serialize(new { jobId }) : string.Empty
            });

            request.TrySetResult((jobId, body.GetProperty("url").GetString() ?? string.Empty));
        });

        await LoginAsync();
        await Page.GetByTestId("nav-meals").ClickAsync();
        await Expect(Page).ToHaveURLAsync(UrlFor("meals"));

        await Page.GetByTestId("add-meal-menu").ClickAsync();
        await Page.GetByTestId("add-meal-import").ClickAsync();
        await Expect(Page).ToHaveURLAsync(UrlFor("meals/import"));

        await Page.GetByTestId("import-url-input").FillAsync(SourceUrl);
        await Page.GetByTestId("import-submit").ClickAsync();

        // Bounded by the test's own timeout rather than a sleep: the app calls the Function once the row is created
        // and its Realtime channel is subscribed as the user, so the result can be written straight away (MEA-43).
        var (sentJobId, sentUrl) = await request.Task.WaitAsync(TimeSpan.FromSeconds(30), TestContext.Current.CancellationToken);
        Assert.Equal(SourceUrl, sentUrl);

        if (acknowledgeWith == 200)
            await Expect(Page.GetByTestId("import-progress")).ToBeVisibleAsync();

        return sentJobId;
    }
}
