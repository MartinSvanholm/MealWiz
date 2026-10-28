using Bunit;
using FluentResults;
using MealWiz.Shared.Features.MealImport.ImportMeal;
using MealWiz.Shared.Features.MealImport.Models;
using MealWiz.Shared.Features.MealImport.State;
using MealWiz.Shared.Tests.TestInfrastructure;
using Microsoft.AspNetCore.Components.Web;
using Moq;
using Xunit;
using MealImportModel = MealWiz.Shared.Features.MealImport.Models.MealImport;

namespace MealWiz.Shared.Tests.Features.MealImport.ComponentTests;

/// <summary>
/// The import page in each job state. The state container is mocked: each test pins the state the page reads
/// (its own unit tests cover how the container gets there).
/// </summary>
public class ImportMealPageTests : ComponentTestBase
{
    private const string Url = "https://example.com/pancakes";

    public ImportMealPageTests()
    {
        MealImport.Setup(m => m.StartImport(It.IsAny<string>())).ReturnsAsync(Result.Ok());
    }

    private void InProgress(bool canContinueInBackground = false, bool inBackground = false)
    {
        MealImport.SetupGet(m => m.JobStatus).Returns(MealImportJobStatus.AwaitingResult);
        MealImport.SetupGet(m => m.IsImportInProgress).Returns(true);
        MealImport.SetupGet(m => m.CurrentImport).Returns(new MealImportModel { SourceUrl = Url });
        MealImport.SetupGet(m => m.CanContinueInBackground).Returns(canContinueInBackground);
        MealImport.SetupGet(m => m.IsRunningInBackground).Returns(inBackground);
    }

    private void Succeeded()
    {
        MealImport.SetupGet(m => m.JobStatus).Returns(MealImportJobStatus.Succeeded);
        MealImport.SetupGet(m => m.IsImportInProgress).Returns(false);
        MealImport.SetupGet(m => m.CurrentImport).Returns(new MealImportModel
        {
            SourceUrl = Url,
            Status = MealImportStatus.Succeeded,
            ParsedMeal = new ParsedMeal
            {
                Name = "Pancakes",
                Recipe = "Whisk\nFry",
                Ingredients = [new ParsedMealIngredient { Name = "Flour", Amount = "200g" }]
            }
        });
    }

    [Fact]
    public void Idle_shows_the_url_form_and_starts_clean()
    {
        var cut = Render<ImportMealPage>();

        Assert.NotNull(cut.Find(InputOf("import-url-input")));
        Assert.Equal("Import", cut.Find(TestId("import-submit")).TextContent.Trim());
        Assert.Empty(cut.FindAll(TestId("import-job-error")));
        Assert.Empty(cut.FindAll(TestId("import-latest-failed")));
        MealImport.Verify(m => m.ResetJob(), Times.Once);
        MealImport.Verify(m => m.LoadLatestImport(), Times.Once);
    }

    [Fact]
    public void Submitting_a_url_starts_the_import()
    {
        var cut = Render<ImportMealPage>();

        cut.Find(InputOf("import-url-input")).Input(Url);
        cut.Find(TestId("import-submit")).Click();

        MealImport.Verify(m => m.StartImport(Url), Times.Once);
    }

    [Fact]
    public void Pressing_Enter_in_the_url_field_starts_the_import()
    {
        var cut = Render<ImportMealPage>();

        cut.Find(InputOf("import-url-input")).Input(Url);
        cut.Find(InputOf("import-url-input")).KeyDown(new KeyboardEventArgs { Key = "Enter" });

        MealImport.Verify(m => m.StartImport(Url), Times.Once);
    }

    [Fact]
    public void A_rejected_url_shows_the_reason_on_the_field()
    {
        MealImport.Setup(m => m.StartImport(It.IsAny<string>())).ReturnsAsync(Result.Fail(MealImportStateContainer.InvalidUrlMessage));
        var cut = Render<ImportMealPage>();

        cut.Find(InputOf("import-url-input")).Input("not a url");
        cut.Find(TestId("import-submit")).Click();

        cut.WaitForAssertion(() => Assert.Contains(MealImportStateContainer.InvalidUrlMessage, cut.Markup));
    }

    [Fact]
    public void In_progress_shows_the_url_being_imported()
    {
        InProgress();

        var cut = Render<ImportMealPage>();

        Assert.Contains("Fetching and parsing your meal", cut.Markup);
        Assert.Contains(Url, cut.Markup);
        Assert.Empty(cut.FindAll(InputOf("import-url-input")));
        Assert.Empty(cut.FindAll(TestId("import-continue-background")));
        MealImport.Verify(m => m.ResetJob(), Times.Never);
    }

    [Fact]
    public void A_slow_import_can_continue_in_the_background()
    {
        InProgress(canContinueInBackground: true);
        var cut = Render<ImportMealPage>();

        cut.Find(TestId("import-continue-background")).Click();

        MealImport.Verify(m => m.ContinueInBackground(), Times.Once);
        Assert.Equal("meals", CurrentPath);
    }

    [Fact]
    public void A_background_import_says_so_and_offers_a_way_back()
    {
        InProgress(inBackground: true);
        var cut = Render<ImportMealPage>();

        Assert.Contains("Importing in the background", cut.Markup);

        cut.Find(TestId("import-back-to-meals")).Click();

        Assert.Equal("meals", CurrentPath);
    }

    [Fact]
    public void A_failed_job_shows_its_error_and_offers_a_retry()
    {
        MealImport.SetupGet(m => m.JobStatus).Returns(MealImportJobStatus.Failed);
        MealImport.SetupGet(m => m.JobErrorMessage).Returns("We couldn't find a recipe on that page.");

        var cut = Render<ImportMealPage>();

        Assert.Contains("We couldn't find a recipe on that page.", cut.Find(TestId("import-job-error")).TextContent);
        Assert.Equal("Try again", cut.Find(TestId("import-submit")).TextContent.Trim());
    }

    [Fact]
    public void A_previously_failed_import_is_shown_with_its_error_and_url()
    {
        MealImport.SetupGet(m => m.LatestImportFailed).Returns(true);
        MealImport.SetupGet(m => m.LatestErrorMessage).Returns("The site blocked us.");
        MealImport.SetupGet(m => m.LatestImport).Returns(new MealImportModel { SourceUrl = Url, Status = MealImportStatus.Failed });

        var cut = Render<ImportMealPage>();

        var alert = cut.Find(TestId("import-latest-failed")).TextContent;
        Assert.Contains("Your last import failed", alert);
        Assert.Contains("The site blocked us.", alert);
        Assert.Contains(Url, alert);
    }

    [Fact]
    public void A_success_while_waiting_opens_the_parsed_meal_in_the_edit_form()
    {
        InProgress();
        var cut = Render<ImportMealPage>();

        Succeeded();
        cut.InvokeAsync(() => MealImport.Raise(m => m.OnStateChanged += null));

        cut.WaitForAssertion(() => Assert.Equal("meals/edit", CurrentPath));
        Assert.Equal("Pancakes", Meals.MealToEdit.Name);
        Assert.Equal("Flour", Assert.Single(Meals.MealToEdit.Ingredients).Name);
        MealImport.Verify(m => m.ResetJob(), Times.Once);
    }

    [Fact]
    public void An_unclaimed_success_on_arrival_opens_the_edit_form()
    {
        Succeeded();

        Render<ImportMealPage>();

        Assert.Equal("meals/edit", CurrentPath);
        Assert.Equal("Pancakes", Meals.MealToEdit.Name);
    }

    [Fact]
    public void Back_returns_to_the_meals_page()
    {
        var cut = Render<ImportMealPage>();

        cut.Find(TestId("import-back")).Click();

        Assert.Equal("meals", CurrentPath);
    }

    [Fact]
    public async Task Leaving_while_the_import_runs_moves_it_to_the_background()
    {
        InProgress();
        Render<ImportMealPage>();

        await DisposeComponentsAsync();

        MealImport.Verify(m => m.ContinueInBackground(), Times.Once);
    }
}
