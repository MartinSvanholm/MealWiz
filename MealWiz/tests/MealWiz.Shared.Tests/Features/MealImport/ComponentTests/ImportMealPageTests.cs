using Bunit;
using FluentResults;
using MealWiz.Shared.Features.MealImport;
using MealWiz.Shared.Features.MealImport.ImportMeal;
using MealWiz.Shared.Features.MealImport.Models;
using MealWiz.Shared.Features.MealImport.State;
using MealWiz.Shared.Tests.TestInfrastructure;
using Microsoft.AspNetCore.Components;
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
    private const string EnglishDetail = "English detail from the Function";

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
        MealImport.Setup(m => m.StartImport(It.IsAny<string>())).ReturnsAsync(Result.Fail(TestLocalizer.For<MealImportResource>()["Import_InvalidUrl"].Value));
        var cut = Render<ImportMealPage>();

        cut.Find(InputOf("import-url-input")).Input("not a url");
        cut.Find(TestId("import-submit")).Click();

        cut.WaitForAssertion(() => Assert.Contains(TestLocalizer.For<MealImportResource>()["Import_InvalidUrl"].Value, cut.Markup));
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
        MealImport.SetupGet(m => m.CurrentImport).Returns(new MealImportModel { SourceUrl = Url, Status = MealImportStatus.Failed });

        var cut = Render<ImportMealPage>();

        Assert.Contains("We couldn't find a recipe on that page.", cut.Find(TestId("import-job-error")).TextContent);
        Assert.Contains(Url, cut.Find(TestId("import-job-error")).TextContent);
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

    public static TheoryData<MealImportErrorCode> ErrorCodes() => new(Enum.GetValues<MealImportErrorCode>());

    private static string MessageFor(MealImportErrorCode? code) => TestLocalizer.For<MealImportResource>().ToMessage(code);

    private void JobFailed(MealImportErrorCode? code)
    {
        MealImport.SetupGet(m => m.JobStatus).Returns(MealImportJobStatus.Failed);
        MealImport.SetupGet(m => m.JobErrorMessage).Returns(MessageFor(code));
        MealImport.SetupGet(m => m.CurrentImport).Returns(new MealImportModel
        {
            SourceUrl = Url,
            Status = MealImportStatus.Failed,
            ErrorCode = code,
            ErrorMessage = EnglishDetail
        });
    }

    private void LatestFailed(MealImportErrorCode? code)
    {
        MealImport.SetupGet(m => m.LatestImportFailed).Returns(true);
        MealImport.SetupGet(m => m.LatestErrorMessage).Returns(MessageFor(code));
        MealImport.SetupGet(m => m.LatestImport).Returns(new MealImportModel
        {
            SourceUrl = Url,
            Status = MealImportStatus.Failed,
            ErrorCode = code,
            ErrorMessage = EnglishDetail
        });
    }

    [Theory]
    [MemberData(nameof(ErrorCodes))]
    public void A_failed_job_shows_the_message_for_its_code_not_the_stored_detail(MealImportErrorCode code)
    {
        JobFailed(code);

        var cut = Render<ImportMealPage>();

        var alert = cut.Find(TestId("import-job-error")).TextContent;
        Assert.Contains("The import failed", alert);
        Assert.Contains(MessageFor(code), alert);
        Assert.DoesNotContain(EnglishDetail, cut.Markup);
    }

    [Fact]
    public void A_failed_job_without_a_code_shows_the_generic_message()
    {
        JobFailed(null);

        var cut = Render<ImportMealPage>();

        Assert.Contains(TestLocalizer.For<MealImportResource>()["ImportError_Unknown"].Value, cut.Find(TestId("import-job-error")).TextContent);
        Assert.DoesNotContain(EnglishDetail, cut.Markup);
    }

    [Theory]
    [MemberData(nameof(ErrorCodes))]
    public void A_previously_failed_import_shows_the_message_for_its_code_not_the_stored_detail(MealImportErrorCode code)
    {
        LatestFailed(code);

        var cut = Render<ImportMealPage>();

        Assert.Contains(MessageFor(code), cut.Find(TestId("import-latest-failed")).TextContent);
        Assert.DoesNotContain(EnglishDetail, cut.Markup);
    }

    [Fact]
    public void A_previously_failed_import_without_a_code_shows_the_generic_message()
    {
        LatestFailed(null);

        var cut = Render<ImportMealPage>();

        Assert.Contains(TestLocalizer.For<MealImportResource>()["ImportError_Unknown"].Value, cut.Find(TestId("import-latest-failed")).TextContent);
        Assert.DoesNotContain(EnglishDetail, cut.Markup);
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
    public void Shows_the_sub_page_header_and_the_form_card()
    {
        var cut = Render<ImportMealPage>();

        Assert.Equal("From a recipe link", cut.Find(TestId("screen-header-label")).TextContent.Trim());
        Assert.Equal("Import meal", cut.Find(TestId("screen-header-title")).TextContent.Trim());
        var form = cut.Find(TestId("import-form"));
        Assert.Contains("Paste a link to a meal", form.TextContent);
        Assert.NotNull(form.QuerySelector(TestId("import-submit")));
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

    // ---- Picture import ----

    private static ShrunkPicture Shrunk(byte id) => new(true, [id], $"blob:test/{id}");
    private static ShrunkPicture Unreadable() => new(false, null, null);

    private void ChoosePictures(string inputTestId, params ShrunkPicture[] pictures)
    {
        PictureShrinker
            .Setup(p => p.ShrinkSelectedAsync(It.IsAny<ElementReference>(), It.IsAny<int>()))
            .ReturnsAsync(pictures);
        Render_ = Render_ ?? throw new InvalidOperationException("Render the page first.");
        Render_.Find(InputOf(inputTestId)).Change(new Microsoft.AspNetCore.Components.ChangeEventArgs());
    }

    private IRenderedComponent<ImportMealPage>? Render_;

    private IRenderedComponent<ImportMealPage> RenderPage()
    {
        MealImport.Setup(m => m.StartPictureImport(It.IsAny<IReadOnlyList<byte[]>>())).ReturnsAsync(Result.Ok());
        Render_ = Render<ImportMealPage>();
        return Render_;
    }

    [Fact]
    public void The_picture_section_has_both_buttons_the_privacy_note_and_a_disabled_submit()
    {
        var cut = RenderPage();

        Assert.Equal("environment", cut.Find(InputOf("import-picture-camera-input")).GetAttribute("capture"));
        Assert.NotNull(cut.Find(InputOf("import-picture-gallery-input")).GetAttribute("multiple"));
        Assert.Equal("image/*", cut.Find(InputOf("import-picture-gallery-input")).GetAttribute("accept"));
        Assert.Contains("Your pictures are sent to an AI service (Anthropic, USA)", cut.Find(TestId("import-picture-privacy")).TextContent);
        Assert.True(cut.Find(TestId("import-picture-submit")).HasAttribute("disabled"));
        Assert.Equal("Import from pictures", cut.Find(TestId("import-picture-submit")).TextContent.Trim());
    }

    [Fact]
    public void Chosen_pictures_show_as_thumbnails_in_order_and_enable_the_submit()
    {
        var cut = RenderPage();

        ChoosePictures("import-picture-gallery-input", Shrunk(1), Shrunk(2));

        Assert.Equal("blob:test/1", cut.Find(TestId("import-picture-thumb-0")).GetAttribute("src"));
        Assert.Equal("blob:test/2", cut.Find(TestId("import-picture-thumb-1")).GetAttribute("src"));
        Assert.False(cut.Find(TestId("import-picture-submit")).HasAttribute("disabled"));
    }

    [Fact]
    public void A_picture_can_be_removed_before_the_import_starts()
    {
        var cut = RenderPage();
        ChoosePictures("import-picture-gallery-input", Shrunk(1), Shrunk(2));

        cut.Find(TestId("import-picture-remove-0")).Click();

        Assert.Single(cut.FindAll("[data-testid^='import-picture-thumb-']"));
        Assert.Equal("blob:test/2", cut.Find(TestId("import-picture-thumb-0")).GetAttribute("src"));
        PictureShrinker.Verify(p => p.RevokePreviewAsync("blob:test/1"), Times.Once);
    }

    [Fact]
    public void More_than_three_pictures_are_not_added_and_the_user_is_told()
    {
        var cut = RenderPage();
        ChoosePictures("import-picture-gallery-input", Shrunk(1), Shrunk(2), Shrunk(3));

        ChoosePictures("import-picture-camera-input", Shrunk(4));

        Assert.Equal(3, cut.FindAll("[data-testid^='import-picture-thumb-']").Count);
        Assert.Contains("You can add up to 3 pictures.", cut.Find(TestId("import-picture-error")).TextContent);
        PictureShrinker.Verify(p => p.RevokePreviewAsync("blob:test/4"), Times.Once);
    }

    [Fact]
    public void A_picture_the_browser_cannot_read_is_not_added_and_the_error_shows()
    {
        var cut = RenderPage();

        ChoosePictures("import-picture-gallery-input", Unreadable());

        Assert.Empty(cut.FindAll("[data-testid^='import-picture-thumb-']"));
        Assert.Contains("We couldn't read the picture.", cut.Find(TestId("import-picture-error")).TextContent);
        MealImport.Verify(m => m.StartPictureImport(It.IsAny<IReadOnlyList<byte[]>>()), Times.Never);
    }

    [Fact]
    public void Submitting_starts_the_picture_import_with_the_bytes_in_order()
    {
        var cut = RenderPage();
        ChoosePictures("import-picture-gallery-input", Shrunk(1), Shrunk(2));

        cut.Find(TestId("import-picture-submit")).Click();

        MealImport.Verify(m => m.StartPictureImport(It.Is<IReadOnlyList<byte[]>>(
            b => b.Count == 2 && b[0][0] == 1 && b[1][0] == 2)), Times.Once);
        MealImport.Verify(m => m.StartImport(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public void The_progress_card_of_a_picture_job_says_so_and_shows_no_url()
    {
        MealImport.SetupGet(m => m.JobStatus).Returns(MealImportJobStatus.AwaitingResult);
        MealImport.SetupGet(m => m.IsImportInProgress).Returns(true);
        MealImport.SetupGet(m => m.CurrentImport).Returns(new MealImportModel { PicturePaths = ["u/j/0.jpg"], Language = "en" });

        var cut = Render<ImportMealPage>();

        var progress = cut.Find(TestId("import-progress")).TextContent;
        Assert.Contains("Reading your pictures", progress);
        Assert.Empty(cut.FindAll(".mw-import-url"));
    }

    [Fact]
    public void A_failed_picture_job_shows_its_error_without_a_url_line_and_keeps_the_pictures()
    {
        var cut = RenderPage();
        ChoosePictures("import-picture-gallery-input", Shrunk(1));
        MealImport.SetupGet(m => m.JobStatus).Returns(MealImportJobStatus.Failed);
        MealImport.SetupGet(m => m.JobErrorMessage).Returns(MessageFor(MealImportErrorCode.DailyLimitReached));
        MealImport.SetupGet(m => m.CurrentImport).Returns(new MealImportModel
        {
            PicturePaths = ["u/j/0.jpg"], Language = "en", Status = MealImportStatus.Failed, ErrorCode = MealImportErrorCode.DailyLimitReached
        });

        cut.Render();

        Assert.Contains("You've reached today's limit for picture imports.", cut.Find(TestId("import-job-error")).TextContent);
        Assert.Empty(cut.Find(TestId("import-job-error")).QuerySelectorAll(".mw-import-url"));
        Assert.Equal("Try again", cut.Find(TestId("import-picture-submit")).TextContent.Trim());
        Assert.Single(cut.FindAll("[data-testid^='import-picture-thumb-']"));
    }

    [Fact]
    public async Task Leaving_the_page_revokes_the_thumbnail_urls()
    {
        var cut = RenderPage();
        ChoosePictures("import-picture-gallery-input", Shrunk(1));

        await DisposeComponentsAsync();

        PictureShrinker.Verify(p => p.RevokePreviewAsync("blob:test/1"), Times.Once);
    }
}

/// <summary>
/// The import page's texts come from <see cref="MealImportResource"/> (UI-03): every localizer echoes its key as
/// <c>[Key]</c>, so a hard-coded text fails these tests. The English texts are asserted by <see cref="ImportMealPageTests"/>.
/// </summary>
public class ImportMealPageTextTests() : ComponentTestBase(echoResourceKeys: true)
{
    [Fact]
    public void Idle_page_texts_come_from_resources()
    {
        var cut = Render<ImportMealPage>();

        Assert.Equal("[ImportPage_HeaderLabel]", cut.Find(TestId("screen-header-label")).TextContent.Trim());
        Assert.Equal("[ImportPage_HeaderTitle]", cut.Find(TestId("screen-header-title")).TextContent.Trim());
        Assert.Contains("[ImportPage_Intro]", cut.Find(TestId("import-form")).TextContent);
        Assert.Contains("[ImportPage_UrlLabel]", cut.Markup);
        Assert.Equal("[ImportPage_Import]", cut.Find(TestId("import-submit")).TextContent.Trim());
    }

    [Fact]
    public void Failed_job_texts_come_from_resources()
    {
        MealImport.SetupGet(m => m.JobStatus).Returns(MealImportJobStatus.Failed);
        MealImport.SetupGet(m => m.CurrentImport).Returns(new MealImportModel { Status = MealImportStatus.Failed });

        var cut = Render<ImportMealPage>();

        Assert.Contains("[ImportPage_JobFailed]", cut.Find(TestId("import-job-error")).TextContent);
        Assert.Equal("[ImportPage_TryAgain]", cut.Find(TestId("import-submit")).TextContent.Trim());
    }

    [Fact]
    public void Latest_failed_title_comes_from_resources()
    {
        MealImport.SetupGet(m => m.LatestImportFailed).Returns(true);

        var cut = Render<ImportMealPage>();

        Assert.Contains("[ImportPage_LatestFailed]", cut.Find(TestId("import-latest-failed")).TextContent);
    }

    [Theory]
    [InlineData(false, false, "[ImportPage_Fetching]")]
    [InlineData(true, false, "[ImportPage_SlowHint]")]
    [InlineData(true, false, "[ImportPage_ContinueInBackground]")]
    [InlineData(false, true, "[ImportPage_InBackgroundTitle]")]
    [InlineData(false, true, "[ImportPage_InBackgroundHint]")]
    [InlineData(false, true, "[ImportPage_BackToMeals]")]
    public void Progress_texts_come_from_resources(bool canContinueInBackground, bool inBackground, string expected)
    {
        MealImport.SetupGet(m => m.JobStatus).Returns(MealImportJobStatus.AwaitingResult);
        MealImport.SetupGet(m => m.IsImportInProgress).Returns(true);
        MealImport.SetupGet(m => m.CurrentImport).Returns(new MealImportModel { SourceUrl = "https://example.com" });
        MealImport.SetupGet(m => m.CanContinueInBackground).Returns(canContinueInBackground);
        MealImport.SetupGet(m => m.IsRunningInBackground).Returns(inBackground);

        var cut = Render<ImportMealPage>();

        Assert.Contains(expected, cut.Find(TestId("import-progress")).TextContent);
    }
}
