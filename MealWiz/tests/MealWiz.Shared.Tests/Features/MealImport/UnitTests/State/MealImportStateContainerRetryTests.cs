using FluentResults;
using MealWiz.Shared.Features.Meals.Models;
using MealWiz.Shared.Features.Meals.SaveMeal;
using MealWiz.Shared.Features.Meals.State;
using MealWiz.Shared.Features.MealImport;
using MealWiz.Shared.Features.MealImport.CreatePictureMealImport;
using MealWiz.Shared.Features.MealImport.DeleteMealImportPictures;
using MealWiz.Shared.Features.MealImport.Models;
using MealWiz.Shared.Features.MealImport.State;
using MealWiz.Shared.Features.MealImport.TriggerAnalyzeMealPicture;
using MealWiz.Shared.Features.MealImport.UploadMealImportPictures;
using MealWiz.Shared.Tests.TestInfrastructure;
using MediatR;
using Microsoft.Extensions.Time.Testing;
using Moq;
using MudBlazor;
using Xunit;
using MealImportModel = MealWiz.Shared.Features.MealImport.Models.MealImport;

namespace MealWiz.Shared.Tests.Features.MealImport.UnitTests.State;

/// <summary>
/// The Edit meal form's side of a picture job: <c>FormImport</c>, the retry with a hint, and deleting the pictures when
/// the form is saved or left (or a backgrounded job finishes).
/// </summary>
public class MealImportStateContainerRetryTests
{
    private static readonly Microsoft.Extensions.Localization.IStringLocalizer<MealImportResource> Localizer =
        TestLocalizer.For<MealImportResource>();

    private static readonly IReadOnlyList<string> Paths = ["u/j/0.jpg", "u/j/1.jpg"];

    private readonly Mock<IMediator> _mediator = new();
    private readonly Mock<IMealImportRealtimeSubscriber> _subscriber = new();
    private readonly Mock<IMealsStateContainer> _mealsState = new();
    private readonly Mock<ISnackbar> _snackbar = new();
    private readonly Mock<IAsyncDisposable> _subscription = new();
    private readonly FakeTimeProvider _time = new();
    private readonly List<CreatePictureMealImport.Command> _creates = [];
    private readonly MealImportStateContainer _container;

    private Guid _jobId;
    private Action<MealImportModel>? _onRealtimeUpdate;
    private Meal _mealToEdit = new();

    public MealImportStateContainerRetryTests()
    {
        _mediator
            .Setup(m => m.Send(It.IsAny<UploadMealImportPictures.Command>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((UploadMealImportPictures.Command c, CancellationToken _) =>
            {
                _jobId = c.JobId;
                return Result.Ok(Paths);
            });
        _mediator
            .Setup(m => m.Send(It.IsAny<CreatePictureMealImport.Command>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((CreatePictureMealImport.Command c, CancellationToken _) =>
            {
                _creates.Add(c);
                _jobId = c.Id;
                return Result.Ok(Row(MealImportStatus.Pending));
            });
        _mediator
            .Setup(m => m.Send(It.IsAny<MealWiz.Shared.Features.MealImport.CreateMealImport.CreateMealImport.Command>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((MealWiz.Shared.Features.MealImport.CreateMealImport.CreateMealImport.Command c, CancellationToken _) =>
            {
                _jobId = c.Id;
                return Result.Ok(new MealImportModel { Id = c.Id, SourceUrl = c.SourceUrl });
            });
        _mediator
            .Setup(m => m.Send(It.IsAny<MealWiz.Shared.Features.MealImport.TriggerScrapeRecipe.TriggerScrapeRecipe.Command>(), It.IsAny<CancellationToken>()))
            .Returns(() => new TaskCompletionSource<Result>().Task);
        _mediator
            .Setup(m => m.Send(It.IsAny<DeleteMealImportPictures.Command>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Ok());
        _mediator
            .Setup(m => m.Send(It.IsAny<TriggerAnalyzeMealPicture.Command>(), It.IsAny<CancellationToken>()))
            // Never answers: the result arrives over Realtime, as in production.
            .Returns(() => new TaskCompletionSource<Result>().Task);
        _mediator
            .Setup(m => m.Send(It.IsAny<SaveMeal.Command>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Ok(new Meal { Id = 7, Name = "Saved" }));
        _subscriber
            .Setup(s => s.Subscribe(It.IsAny<Guid>(), It.IsAny<Action<MealImportModel>>()))
            .ReturnsAsync((Guid _, Action<MealImportModel> onUpdate) =>
            {
                _onRealtimeUpdate = onUpdate;
                return Result.Ok(_subscription.Object);
            });
        _subscription.Setup(s => s.DisposeAsync()).Returns(ValueTask.CompletedTask);
        _mealsState.SetupSet(m => m.MealToEdit = It.IsAny<Meal>()).Callback<Meal>(m => _mealToEdit = m);

        _container = new MealImportStateContainer(
            _mediator.Object, _subscriber.Object, _mealsState.Object, _snackbar.Object, _time, Localizer);
    }

    // ---- FormImport ----

    [Fact]
    public async Task OpenInForm_MakesTheResolvedJobTheFormsJob()
    {
        await OpenFormWith(Guessed("Lasagne"));

        Assert.Equal("Lasagne", _container.FormImport?.ParsedMeal?.Name);
        Assert.True(_container.FormImport!.IsPictureImport);
        Assert.False(_container.IsRetrying);
    }

    [Fact]
    public async Task ReleaseFormImport_DeletesThePicturesAndForgetsTheJob()
    {
        await OpenFormWith(Guessed("Lasagne"));

        await _container.ReleaseFormImport();

        Assert.Null(_container.FormImport);
        _mediator.Verify(m => m.Send(
            It.Is<DeleteMealImportPictures.Command>(c => c.Paths.SequenceEqual(Paths)), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ReleaseFormImport_WithoutAPictureJob_DoesNothing()
    {
        await _container.ReleaseFormImport();

        _mediator.Verify(m => m.Send(It.IsAny<DeleteMealImportPictures.Command>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ReleaseFormImport_ForALinkJob_DeletesNothing()
    {
        await OpenFormWith(new MealImportModel { SourceUrl = "https://example.com/r", Status = MealImportStatus.Succeeded, ParsedMeal = new ParsedMeal { Name = "Link" } });

        await _container.ReleaseFormImport();

        Assert.Null(_container.FormImport);
        _mediator.Verify(m => m.Send(It.IsAny<DeleteMealImportPictures.Command>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ReleaseFormImport_WhenTheDeleteFails_ChangesNothingTheUserSees()
    {
        await OpenFormWith(Guessed("Lasagne"));
        _mediator
            .Setup(m => m.Send(It.IsAny<DeleteMealImportPictures.Command>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("storage down"));

        await _container.ReleaseFormImport();

        Assert.Null(_container.FormImport);
        _snackbar.Verify(s => s.Add(It.IsAny<string>(), It.IsAny<Severity>(), It.IsAny<Action<SnackbarOptions>>(), It.IsAny<string>()), Times.Never);
    }

    // ---- Retry with a hint ----

    [Fact]
    public async Task RetryWithHint_StartsANewJobOnTheSamePicturesWithTheTrimmedHint_WithoutUploading()
    {
        await OpenFormWith(Guessed("Lasagne"));
        _creates.Clear();

        var result = await _container.RetryWithHint("  it's pizza ");

        Assert.True(result.IsSuccess);
        var create = Assert.Single(_creates);
        Assert.Equal(Paths, create.PicturePaths);
        Assert.Equal("it's pizza", create.Hint);
        Assert.Equal("en", create.Language);
        Assert.True(_container.IsRetrying);
        _mediator.Verify(m => m.Send(It.IsAny<UploadMealImportPictures.Command>(), It.IsAny<CancellationToken>()), Times.Once); // the first job's only
        _mediator.Verify(m => m.Send(
            It.Is<TriggerAnalyzeMealPicture.Command>(c => c.JobId == create.Id), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task RetryWithHint_BlankHint_IsRefused(string hint)
    {
        await OpenFormWith(Guessed("Lasagne"));
        _creates.Clear();

        var result = await _container.RetryWithHint(hint);

        Assert.Equal(Localizer["Import_HintRequired"].Value, result.Errors[0].Message);
        Assert.Empty(_creates);
    }

    [Fact]
    public async Task RetryWithHint_TooLongHint_IsRefused()
    {
        await OpenFormWith(Guessed("Lasagne"));
        _creates.Clear();

        var result = await _container.RetryWithHint(new string('x', MealImportPictures.MaxHintLength + 1));

        Assert.True(result.IsFailed);
        Assert.Empty(_creates);
    }

    [Fact]
    public async Task RetryWithHint_WithoutAPictureJobInTheForm_IsRefused()
    {
        var result = await _container.RetryWithHint("pizza");

        Assert.True(result.IsFailed);
        Assert.Empty(_creates);
    }

    [Fact]
    public async Task RetryWithHint_WhileAJobRuns_IsRefused()
    {
        await OpenFormWith(Guessed("Lasagne"));
        await _container.RetryWithHint("one");

        var second = await _container.RetryWithHint("two");

        Assert.Equal(Localizer["Import_AlreadyInProgress"].Value, second.Errors[0].Message);
    }

    [Fact]
    public async Task ASuccessfulRetry_ReplacesTheFormAndItsJob()
    {
        await OpenFormWith(Guessed("Lasagne"));
        await _container.RetryWithHint("pizza");

        await ResolveWith(Written("Pizza"));

        Assert.Equal("Pizza", _mealToEdit.Name);
        Assert.Equal("Pizza", _container.FormImport?.ParsedMeal?.Name);
        Assert.False(_container.FormImport!.ParsedMeal!.IsGuessed);
        Assert.False(_container.IsRetrying);
        Assert.Null(_container.RetryErrorMessage);
        Assert.Equal(MealImportJobStatus.Idle, _container.JobStatus);
        Assert.Null(_container.CurrentImport);
        _mediator.Verify(m => m.Send(It.IsAny<DeleteMealImportPictures.Command>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task AFailedRetry_LeavesTheFormAsItWasAndShowsWhy()
    {
        await OpenFormWith(Guessed("Lasagne"));
        var before = _mealToEdit;
        await _container.RetryWithHint("pizza");

        await ResolveWith(Failed(MealImportErrorCode.NoMealFound));

        Assert.Same(before, _mealToEdit);
        Assert.Equal("Lasagne", _container.FormImport?.ParsedMeal?.Name);
        Assert.Equal(Localizer.ToMessage(MealImportErrorCode.NoMealFound), _container.RetryErrorMessage);
        Assert.False(_container.IsRetrying);
        Assert.False(_container.LatestImportFailed);

        // The next attempt clears the message.
        await _container.RetryWithHint("pasta");
        Assert.Null(_container.RetryErrorMessage);
    }

    [Fact]
    public async Task ARetryThatCannotInsertItsRow_ShowsTheReasonAndLeavesTheForm()
    {
        await OpenFormWith(Guessed("Lasagne"));
        _mediator
            .Setup(m => m.Send(It.IsAny<CreatePictureMealImport.Command>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Fail<MealImportModel>("Insert failed"));

        var result = await _container.RetryWithHint("pizza");

        Assert.True(result.IsFailed);
        Assert.Equal("Insert failed", _container.RetryErrorMessage);
        Assert.Equal("Lasagne", _container.FormImport?.ParsedMeal?.Name);
        Assert.Equal(MealImportJobStatus.Idle, _container.JobStatus);
        Assert.False(_container.IsRetrying);
    }

    [Fact]
    public async Task ARetryLeftInTheBackground_SavesTheMealToastsAndDeletesThePictures()
    {
        await OpenFormWith(Guessed("Lasagne"));
        await _container.RetryWithHint("pizza");
        _container.ContinueInBackground();

        await ResolveWith(Written("Pizza"));

        _mediator.Verify(m => m.Send(
            It.Is<SaveMeal.Command>(c => c.Meal.Name == "Pizza"), It.IsAny<CancellationToken>()), Times.Once);
        _mediator.Verify(m => m.Send(
            It.Is<DeleteMealImportPictures.Command>(c => c.Paths.SequenceEqual(Paths)), It.IsAny<CancellationToken>()), Times.Once);
        Assert.Null(_container.FormImport);
        Assert.NotSame(_mealToEdit.Name, "Pizza"); // the form was not touched
    }

    [Fact]
    public async Task AFailedRetryLeftInTheBackground_ToastsAndDeletesThePictures()
    {
        await OpenFormWith(Guessed("Lasagne"));
        await _container.RetryWithHint("pizza");
        _container.ContinueInBackground();

        await ResolveWith(Failed(MealImportErrorCode.AiUnavailable));

        _snackbar.Verify(s => s.Add(It.Is<string>(m => m.Contains(Localizer.ToMessage(MealImportErrorCode.AiUnavailable))),
            Severity.Error, It.IsAny<Action<SnackbarOptions>>(), It.IsAny<string>()), Times.Once);
        _mediator.Verify(m => m.Send(It.IsAny<DeleteMealImportPictures.Command>(), It.IsAny<CancellationToken>()), Times.Once);
        Assert.Null(_container.FormImport);
    }

    [Fact]
    public async Task ABackgroundedFirstJob_DeletesItsPicturesAfterSavingTheMeal()
    {
        await _container.StartPictureImport([[1], [2]]);
        _container.ContinueInBackground();

        await ResolveWith(Guessed("Lasagne"));

        _mediator.Verify(m => m.Send(It.IsAny<SaveMeal.Command>(), It.IsAny<CancellationToken>()), Times.Once);
        _mediator.Verify(m => m.Send(
            It.Is<DeleteMealImportPictures.Command>(c => c.Paths.SequenceEqual(Paths)), It.IsAny<CancellationToken>()), Times.Once);
    }

    // ---- helpers ----

    private async Task OpenFormWith(MealImportModel resolved)
    {
        if (resolved.IsPictureImport)
        {
            await _container.StartPictureImport([[1], [2]]);
            await ResolveWith(resolved);
        }
        else
        {
            _onRealtimeUpdate = null;
            await _container.StartImport("https://example.com/r");
            resolved.Id = _jobId;
            await ResolveWith(resolved);
        }

        _container.OpenInForm();
        _container.ResetJob();
    }

    private async Task ResolveWith(MealImportModel row)
    {
        row.Id = _jobId;
        _onRealtimeUpdate!(row);
        await Eventually(() => !_container.IsImportInProgress);
    }

    private static MealImportModel Guessed(string name) => PictureRow(name, guessed: true);
    private static MealImportModel Written(string name) => PictureRow(name, guessed: false);

    private static MealImportModel PictureRow(string name, bool guessed) => new()
    {
        PicturePaths = Paths,
        Language = "en",
        Status = MealImportStatus.Succeeded,
        ParsedMeal = new ParsedMeal { Name = name, IsGuessed = guessed, Recipe = "Cook" }
    };

    private static MealImportModel Failed(MealImportErrorCode code) => new()
    {
        PicturePaths = Paths,
        Language = "en",
        Status = MealImportStatus.Failed,
        ErrorCode = code,
        ErrorMessage = "detail"
    };

    private MealImportModel Row(MealImportStatus status) => new()
    {
        Id = _jobId,
        PicturePaths = Paths,
        Language = "en",
        Status = status
    };

    private static async Task Eventually(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (!condition())
        {
            if (DateTime.UtcNow > deadline) Assert.Fail("Condition was not met in time.");
            await Task.Delay(10, TestContext.Current.CancellationToken);
        }
    }
}
