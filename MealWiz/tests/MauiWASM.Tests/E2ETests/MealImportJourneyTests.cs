using System.Text.Json;
using MauiWASM.Tests.TestInfrastructure.E2E;
using Microsoft.Playwright;
using Xunit;

namespace MauiWASM.Tests.E2ETests;

/// <summary>
/// The import from URL, with the Functions app played by the test. The browser's call to ScrapeRecipe is
/// intercepted and acknowledged straight away, leaving the row pending, and the test then writes the result with the
/// service role, as ScrapeRecipeHandler does. Because the app has already re-read the row after the acknowledgement,
/// the result can only reach it by polling the row. The scraping itself is covered by MealWiz.Functions.Tests.
/// </summary>
[Trait("Category", "E2E")]
public class MealImportJourneyTests : E2ETestBase
{
    private const string SourceUrl = "https://recipes.mealwiz.test/aeblepandekager";

    [Fact]
    public async Task ImportedMeal_ArrivesFromTheRow_AndCanBeSaved()
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
    public async Task FailedImport_ArrivesFromTheRow_AndShowsTheReason()
    {
        // The page shows the message for the row's error_code, never the Function's English error_message.
        const string detail = "Could not find recipe data on the page.";
        var jobId = await StartImportAsync(acknowledgeWith: 200);

        await Supabase.ResolveMealImportAsync(jobId, "failed", errorMessage: detail, errorCode: "no_recipe_found");

        await Expect(Page.GetByTestId("import-job-error")).ToContainTextAsync("We couldn't find a recipe on that page.");
        await Expect(Page.GetByTestId("import-job-error")).Not.ToContainTextAsync(detail);
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


    // ---- Create from pictures ----

    // A real 40x30 JPEG, so the browser can decode, shrink and upload it.
    private static readonly byte[] SmallJpeg = Convert.FromBase64String(
        "/9j/4AAQSkZJRgABAQAAAQABAAD/2wBDAAoHBwgHBgoICAgLCgoLDhgQDg0NDh0VFhEYIx8lJCIfIiEmKzcvJik0KSEiMEExNDk7Pj4+JS5ESUM8SDc9Pjv/2wBDAQoLCw4NDhwQEBw7KCIoOzs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozv/wAARCAAeACgDASIAAhEBAxEB/8QAHwAAAQUBAQEBAQEAAAAAAAAAAAECAwQFBgcICQoL/8QAtRAAAgEDAwIEAwUFBAQAAAF9AQIDAAQRBRIhMUEGE1FhByJxFDKBkaEII0KxwRVS0fAkM2JyggkKFhcYGRolJicoKSo0NTY3ODk6Q0RFRkdISUpTVFVWV1hZWmNkZWZnaGlqc3R1dnd4eXqDhIWGh4iJipKTlJWWl5iZmqKjpKWmp6ipqrKztLW2t7i5usLDxMXGx8jJytLT1NXW19jZ2uHi4+Tl5ufo6erx8vP09fb3+Pn6/8QAHwEAAwEBAQEBAQEBAQAAAAAAAAECAwQFBgcICQoL/8QAtREAAgECBAQDBAcFBAQAAQJ3AAECAxEEBSExBhJBUQdhcRMiMoEIFEKRobHBCSMzUvAVYnLRChYkNOEl8RcYGRomJygpKjU2Nzg5OkNERUZHSElKU1RVVldYWVpjZGVmZ2hpanN0dXZ3eHl6goOEhYaHiImKkpOUlZaXmJmaoqOkpaanqKmqsrO0tba3uLm6wsPExcbHyMnK0tPU1dbX2Nna4uPk5ebn6Onq8vP09fb3+Pn6/9oADAMBAAIRAxEAPwCaiiivlT6YKKKKACiiigAooooAKKKKACiiigD/2Q==");

    [Fact]
    public async Task PictureImport_ArrivesFromTheRow_AndCanBeSaved()
    {
        var analyzeCall = InterceptAnalyzeCall(acknowledgeWith: 200);

        await OpenPictureImportAsync();
        await Page.GetByTestId("import-picture-gallery-input").SetInputFilesAsync(
            new FilePayload { Name = "dish.jpg", MimeType = "image/jpeg", Buffer = SmallJpeg });
        await Expect(Page.GetByTestId("import-picture-thumb-0")).ToBeVisibleAsync();
        await Page.GetByTestId("import-picture-submit").ClickAsync();

        // Bounded by the test's own timeout rather than a sleep, like the link import.
        var jobId = await analyzeCall.WaitAsync(TimeSpan.FromSeconds(30), TestContext.Current.CancellationToken);
        await Expect(Page.GetByTestId("import-progress")).ToBeVisibleAsync();
        await Expect(Page.GetByTestId("import-progress")).ToContainTextAsync("Reading your pictures");

        var stored = await Supabase.GetMealImportPicturesAsync(jobId);
        Assert.NotNull(stored);
        try
        {
            var path = Assert.Single(stored.Value.Paths);
            Assert.StartsWith($"{User.Id:D}/{jobId:D}/", path);
            Assert.Equal("en", stored.Value.Language);

            await Supabase.ResolveMealImportAsync(jobId, "succeeded", parsedMeal: new
            {
                Name = "Lasagne",
                Recipe = "Layer.\nBake.",
                IsGuessed = true,
                Ingredients = new[] { new { Name = "pasta sheets", Amount = "250 g" } }
            });

            // Opens the meal form filled in with the result; nothing is saved until the user presses Save
            await Expect(Page).ToHaveURLAsync(UrlFor("meals/edit"));
            await Expect(Page.GetByTestId("meal-name-input")).ToHaveValueAsync("Lasagne");
            await Expect(Row("ingredient-card", "pasta sheets")).ToContainTextAsync("250 g");

            await Page.GetByTestId("save-meal-button").ClickAsync();
            await Expect(Page).ToHaveURLAsync(UrlFor("meals"));
            await Expect(Row("meal-list-item", "Lasagne")).ToBeVisibleAsync();
        }
        finally
        {
            await Supabase.DeleteStoredPicturesAsync(stored.Value.Paths);
        }
    }

    [Fact]
    public async Task UndecodablePicture_IsRejectedBeforeUpload()
    {
        var called = false;
        await Page.RouteAsync($"**{E2EAppHost.FunctionsPath}/api/AnalyzeMealPicture", async route =>
        {
            called = true;
            await route.FulfillAsync(new RouteFulfillOptions { Status = 200, ContentType = "application/json", Body = "{}" });
        });

        await OpenPictureImportAsync();
        await Page.GetByTestId("import-picture-gallery-input").SetInputFilesAsync(
            new FilePayload { Name = "x.jpg", MimeType = "image/jpeg", Buffer = System.Text.Encoding.UTF8.GetBytes("this is not a picture") });

        await Expect(Page.GetByTestId("import-picture-error")).ToContainTextAsync("We couldn't read the picture.");
        await Expect(Page.GetByTestId("import-picture-thumb-0")).ToHaveCountAsync(0);
        await Expect(Page.GetByTestId("import-picture-submit")).ToBeDisabledAsync();
        Assert.False(called);
        Assert.Equal(0, await Supabase.CountMealImportsAsync(User.Id));
    }

    [Fact]
    public async Task PictureImport_Failure_ShowsTheReason()
    {
        var analyzeCall = InterceptAnalyzeCall(acknowledgeWith: 200);

        await OpenPictureImportAsync();
        await Page.GetByTestId("import-picture-gallery-input").SetInputFilesAsync(
            new FilePayload { Name = "dish.jpg", MimeType = "image/jpeg", Buffer = SmallJpeg });
        await Page.GetByTestId("import-picture-submit").ClickAsync();
        var jobId = await analyzeCall.WaitAsync(TimeSpan.FromSeconds(30), TestContext.Current.CancellationToken);

        var stored = await Supabase.GetMealImportPicturesAsync(jobId);
        Assert.NotNull(stored);
        try
        {
            await Supabase.ResolveMealImportAsync(jobId, "failed", errorMessage: "Daily limit", errorCode: "daily_limit_reached");

            await Expect(Page.GetByTestId("import-job-error")).ToContainTextAsync("You've reached today's limit for picture imports.");
            await Expect(Page.GetByTestId("import-picture-submit")).ToHaveTextAsync("Try again");
            await Expect(Page.GetByTestId("import-picture-thumb-0")).ToBeVisibleAsync();
        }
        finally
        {
            await Supabase.DeleteStoredPicturesAsync(stored.Value.Paths);
        }
    }


    // ---- The guessed note, retry with a hint and deleting the pictures (MEA-143) ----

    private static object Meal(string name, bool guessed) => new
    {
        Name = name,
        Recipe = "Cook.",
        IsGuessed = guessed,
        Ingredients = new[] { new { Name = "salt", Amount = "1 tsk" } }
    };

    /// <summary>Opens the picture import, adds one picture, submits, and returns the job ids as the Function is called.</summary>
    private async Task<(System.Threading.Channels.ChannelReader<Guid> Calls, Guid FirstJob, string PicturePath)> StartPictureJobAsync()
    {
        var calls = InterceptAnalyzeCalls();
        await OpenPictureImportAsync();
        await Page.GetByTestId("import-picture-gallery-input").SetInputFilesAsync(
            new FilePayload { Name = "dish.jpg", MimeType = "image/jpeg", Buffer = SmallJpeg });
        await Page.GetByTestId("import-picture-submit").ClickAsync();

        var first = await calls.ReadAsync(TestContext.Current.CancellationToken).AsTask().WaitAsync(TimeSpan.FromSeconds(30), TestContext.Current.CancellationToken);
        var stored = await Supabase.GetMealImportPicturesAsync(first);
        return (calls, first, Assert.Single(stored!.Value.Paths));
    }

    [Fact]
    public async Task GuessedPictureMeal_ShowsTheNote_AndSavingDeletesThePictures()
    {
        var (_, jobId, path) = await StartPictureJobAsync();
        try
        {
            await Supabase.ResolveMealImportAsync(jobId, "succeeded", parsedMeal: Meal("Lasagne", guessed: true));

            await Expect(Page).ToHaveURLAsync(UrlFor("meals/edit"));
            await Expect(Page.GetByTestId("edit-meal-guessed-note")).ToContainTextAsync("Guessed from your picture");
            Assert.True(await Supabase.StoredPictureExistsAsync(path));

            await Page.GetByTestId("save-meal-button").ClickAsync();
            await Expect(Page).ToHaveURLAsync(UrlFor("meals"));

            await EventuallyAsync(async () => !await Supabase.StoredPictureExistsAsync(path));
        }
        finally
        {
            await Supabase.DeleteStoredPicturesAsync([path]);
        }
    }

    [Fact]
    public async Task WrittenRecipe_ShowsNoNote_ButOffersARetry()
    {
        var (_, jobId, path) = await StartPictureJobAsync();
        try
        {
            await Supabase.ResolveMealImportAsync(jobId, "succeeded", parsedMeal: Meal("Pancakes", guessed: false));

            await Expect(Page).ToHaveURLAsync(UrlFor("meals/edit"));
            await Expect(Page.GetByTestId("edit-meal-retry")).ToBeVisibleAsync();
            await Expect(Page.GetByTestId("edit-meal-guessed-note")).ToHaveCountAsync(0);
        }
        finally
        {
            await Supabase.DeleteStoredPicturesAsync([path]);
        }
    }

    [Fact]
    public async Task RetryWithHint_ReplacesTheForm_AndAFailedRetryLeavesItAsItWas()
    {
        var (calls, jobId, path) = await StartPictureJobAsync();
        try
        {
            await Supabase.ResolveMealImportAsync(jobId, "succeeded", parsedMeal: Meal("Lasagne", guessed: true));
            await Expect(Page.GetByTestId("meal-name-input")).ToHaveValueAsync("Lasagne");

            // A retry that fails: the form stays, the reason shows.
            await Page.GetByTestId("edit-meal-retry").ClickAsync();
            await Page.GetByTestId("edit-meal-hint-input").FillAsync("it's pizza");
            await Page.GetByTestId("edit-meal-hint-submit").ClickAsync();
            var failedRetry = await calls.ReadAsync(TestContext.Current.CancellationToken).AsTask().WaitAsync(TimeSpan.FromSeconds(30), TestContext.Current.CancellationToken);
            await Expect(Page.GetByTestId("save-meal-button")).ToBeDisabledAsync();
            await Supabase.ResolveMealImportAsync(failedRetry, "failed", errorMessage: "none", errorCode: "no_meal_found");

            await Expect(Page.GetByTestId("edit-meal-retry-error")).ToContainTextAsync("We couldn't find a meal or a recipe in the picture.");
            await Expect(Page.GetByTestId("meal-name-input")).ToHaveValueAsync("Lasagne");
            await Expect(Page.GetByTestId("save-meal-button")).ToBeEnabledAsync();

            // A retry that works: the form is replaced, the note goes, the pictures stay for now.
            await Page.GetByTestId("edit-meal-hint-submit").ClickAsync();
            var okRetry = await calls.ReadAsync(TestContext.Current.CancellationToken).AsTask().WaitAsync(TimeSpan.FromSeconds(30), TestContext.Current.CancellationToken);
            var retried = await Supabase.GetMealImportPicturesAsync(okRetry);
            Assert.Equal(path, Assert.Single(retried!.Value.Paths)); // the same picture, not uploaded again
            await Supabase.ResolveMealImportAsync(okRetry, "succeeded", parsedMeal: Meal("Pizza", guessed: false));

            await Expect(Page.GetByTestId("meal-name-input")).ToHaveValueAsync("Pizza");
            await Expect(Page.GetByTestId("edit-meal-guessed-note")).ToHaveCountAsync(0);
            Assert.True(await Supabase.StoredPictureExistsAsync(path));
        }
        finally
        {
            await Supabase.DeleteStoredPicturesAsync([path]);
        }
    }

    [Fact]
    public async Task LeavingTheForm_DeletesThePictures()
    {
        var (_, jobId, path) = await StartPictureJobAsync();
        try
        {
            await Supabase.ResolveMealImportAsync(jobId, "succeeded", parsedMeal: Meal("Lasagne", guessed: true));
            await Expect(Page).ToHaveURLAsync(UrlFor("meals/edit"));

            await Page.GetByTestId("edit-meal-back").ClickAsync();
            await Expect(Page).ToHaveURLAsync(UrlFor("meals"));

            await EventuallyAsync(async () => !await Supabase.StoredPictureExistsAsync(path));
        }
        finally
        {
            await Supabase.DeleteStoredPicturesAsync([path]);
        }
    }

    /// <summary>Like <see cref="InterceptAnalyzeCall"/>, but for several calls: every job id the browser sends, in order.</summary>
    private System.Threading.Channels.ChannelReader<Guid> InterceptAnalyzeCalls()
    {
        var channel = System.Threading.Channels.Channel.CreateUnbounded<Guid>();

        Page.RouteAsync($"**{E2EAppHost.FunctionsPath}/api/AnalyzeMealPicture", async route =>
        {
            var body = route.Request.PostDataJSON() ?? throw new InvalidOperationException("AnalyzeMealPicture was called without a body.");
            var jobId = body.GetProperty("jobId").GetGuid();

            await route.FulfillAsync(new RouteFulfillOptions
            {
                Status = 200,
                ContentType = "application/json",
                Body = JsonSerializer.Serialize(new { jobId })
            });

            channel.Writer.TryWrite(jobId);
        });

        return channel.Reader;
    }

    /// <summary>Polls a condition for up to 15 s (a delete the app does in the background has no UI to wait on).</summary>
    private static async Task EventuallyAsync(Func<Task<bool>> condition)
    {
        var deadline = DateTime.UtcNow.AddSeconds(15);
        while (!await condition())
        {
            if (DateTime.UtcNow > deadline) Assert.Fail("The condition was not met in time.");
            await Task.Delay(250, TestContext.Current.CancellationToken);
        }
    }

    /// <summary>Answers the browser's call to AnalyzeMealPicture and returns the job id it carried.</summary>
    private Task<Guid> InterceptAnalyzeCall(int acknowledgeWith)
    {
        var request = new TaskCompletionSource<Guid>(TaskCreationOptions.RunContinuationsAsynchronously);

        Page.RouteAsync($"**{E2EAppHost.FunctionsPath}/api/AnalyzeMealPicture", async route =>
        {
            var body = route.Request.PostDataJSON() ?? throw new InvalidOperationException("AnalyzeMealPicture was called without a body.");
            var jobId = body.GetProperty("jobId").GetGuid();

            await route.FulfillAsync(new RouteFulfillOptions
            {
                Status = acknowledgeWith,
                ContentType = "application/json",
                Body = acknowledgeWith == 200 ? JsonSerializer.Serialize(new { jobId }) : string.Empty
            });

            request.TrySetResult(jobId);
        });

        return request.Task;
    }

    private async Task OpenPictureImportAsync()
    {
        await LoginAsync();
        await Page.GetByTestId("nav-meals").ClickAsync();
        await Expect(Page).ToHaveURLAsync(UrlFor("meals"));

        await Page.GetByTestId("add-meal-menu").ClickAsync();
        await Page.GetByTestId("add-meal-import-picture").ClickAsync();
        await Expect(Page).ToHaveURLAsync(UrlFor("meals/import?source=picture"));
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
        // and then polls it, so the result can be written straight away.
        var (sentJobId, sentUrl) = await request.Task.WaitAsync(TimeSpan.FromSeconds(30), TestContext.Current.CancellationToken);
        Assert.Equal(SourceUrl, sentUrl);

        if (acknowledgeWith == 200)
            await Expect(Page.GetByTestId("import-progress")).ToBeVisibleAsync();

        return sentJobId;
    }
}
