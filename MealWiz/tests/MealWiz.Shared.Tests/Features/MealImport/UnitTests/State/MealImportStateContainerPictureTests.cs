using System.Globalization;
using FluentResults;
using MealWiz.Shared.Features.Meals.SaveMeal;
using MealWiz.Shared.Features.Meals.State;
using MealWiz.Shared.Features.MealImport;
using MealWiz.Shared.Features.MealImport.CreatePictureMealImport;
using MealWiz.Shared.Features.MealImport.DeleteMealImportPictures;
using MealWiz.Shared.Features.MealImport.GetMealImport;
using MealWiz.Shared.Features.MealImport.MarkMealImportFailed;
using MealWiz.Shared.Features.MealImport.Models;
using MealWiz.Shared.Features.MealImport.State;
using MealWiz.Shared.Features.MealImport.TriggerAnalyzeMealPicture;
using MealWiz.Shared.Features.MealImport.TriggerScrapeRecipe;
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
/// The picture half of the job lifecycle: upload, then the row, then the AnalyzeMealPicture call. The shared part
/// (reading the row, timers, background mode) is covered by <see cref="MealImportStateContainerTests"/>.
/// </summary>
public class MealImportStateContainerPictureTests
{
    private static readonly Microsoft.Extensions.Localization.IStringLocalizer<MealImportResource> Localizer =
        TestLocalizer.For<MealImportResource>();

    private static readonly IReadOnlyList<byte[]> Pictures = [[1, 2], [3, 4]];
    private static readonly IReadOnlyList<string> Paths = ["u/j/0.jpg", "u/j/1.jpg"];

    private readonly Mock<IMediator> _mediator = new();
    private readonly Mock<IMealsStateContainer> _mealsState = new();
    private readonly Mock<ISnackbar> _snackbar = new();
    private readonly FakeTimeProvider _time = new();
    private readonly TaskCompletionSource<Result> _functionAck = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly List<string> _calls = [];
    private readonly MealImportStateContainer _container;

    private Guid _jobId;
    private CreatePictureMealImport.Command? _createCommand;

    public MealImportStateContainerPictureTests()
    {
        _mediator
            .Setup(m => m.Send(It.IsAny<UploadMealImportPictures.Command>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((UploadMealImportPictures.Command c, CancellationToken _) =>
            {
                _calls.Add("upload");
                _jobId = c.JobId;
                return Result.Ok(Paths);
            });
        _mediator
            .Setup(m => m.Send(It.IsAny<CreatePictureMealImport.Command>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((CreatePictureMealImport.Command c, CancellationToken _) =>
            {
                _calls.Add("create");
                _createCommand = c;
                return Result.Ok(Row(MealImportStatus.Pending));
            });
        _mediator
            .Setup(m => m.Send(It.IsAny<DeleteMealImportPictures.Command>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() =>
            {
                _calls.Add("delete");
                return Result.Ok();
            });
        _mediator
            .Setup(m => m.Send(It.IsAny<TriggerAnalyzeMealPicture.Command>(), It.IsAny<CancellationToken>()))
            .Returns(() =>
            {
                _calls.Add("trigger");
                return _functionAck.Task;
            });

        _container = new MealImportStateContainer(
            _mediator.Object, _mealsState.Object, _snackbar.Object, _time, Localizer);
    }

    [Fact]
    public async Task StartPictureImport_UploadsThenInsertsTheRowThenCallsTheFunction()
    {
        var result = await _container.StartPictureImport(Pictures);

        Assert.True(result.IsSuccess);
        Assert.Equal(new[] { "upload", "create", "trigger" }, _calls);
        Assert.Equal(MealImportJobStatus.AwaitingResult, _container.JobStatus);
        Assert.True(_container.IsImportInProgress);
        Assert.Equal(_jobId, _container.CurrentImport?.Id);
        _mediator.Verify(m => m.Send(
            It.Is<UploadMealImportPictures.Command>(c => c.Pictures == Pictures), It.IsAny<CancellationToken>()), Times.Once);
        _mediator.Verify(m => m.Send(
            It.Is<TriggerAnalyzeMealPicture.Command>(c => c.JobId == _jobId), It.IsAny<CancellationToken>()), Times.Once);
        _mediator.Verify(m => m.Send(It.IsAny<TriggerScrapeRecipe.Command>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task StartPictureImport_SendsThePathsTheAppLanguageAndNoHint()
    {
        using var _ = CultureScope.Use("da");

        await _container.StartPictureImport(Pictures);

        Assert.NotNull(_createCommand);
        Assert.Equal(_jobId, _createCommand.Id);
        Assert.Equal(Paths, _createCommand.PicturePaths);
        Assert.Equal("da", _createCommand.Language);
        Assert.Null(_createCommand.Hint);
    }

    [Fact]
    public async Task StartPictureImport_UploadFails_FailsTheJobWithoutCreatingARow()
    {
        _mediator
            .Setup(m => m.Send(It.IsAny<UploadMealImportPictures.Command>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Fail<IReadOnlyList<string>>("Upload failed"));

        var result = await _container.StartPictureImport(Pictures);

        Assert.True(result.IsFailed);
        Assert.Equal(MealImportJobStatus.Failed, _container.JobStatus);
        Assert.Equal("Upload failed", _container.JobErrorMessage);
        _mediator.Verify(m => m.Send(It.IsAny<CreatePictureMealImport.Command>(), It.IsAny<CancellationToken>()), Times.Never);
        _mediator.Verify(m => m.Send(It.IsAny<TriggerAnalyzeMealPicture.Command>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task StartPictureImport_InsertFails_DeletesTheUploadedPicturesAndFailsTheJob()
    {
        _mediator
            .Setup(m => m.Send(It.IsAny<CreatePictureMealImport.Command>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Fail<MealImportModel>("Insert failed"));

        var result = await _container.StartPictureImport(Pictures);

        Assert.True(result.IsFailed);
        Assert.Equal(MealImportJobStatus.Failed, _container.JobStatus);
        Assert.Equal("Insert failed", _container.JobErrorMessage);
        _mediator.Verify(m => m.Send(
            It.Is<DeleteMealImportPictures.Command>(c => c.Paths == Paths), It.IsAny<CancellationToken>()), Times.Once);
        _mediator.Verify(m => m.Send(It.IsAny<TriggerAnalyzeMealPicture.Command>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task StartPictureImport_WhileAJobIsInProgress_IsRejectedWhateverItsKind()
    {
        await _container.StartPictureImport(Pictures);

        var second = await _container.StartPictureImport(Pictures);
        var link = await _container.StartImport("https://example.com/recipe");

        Assert.Equal(Localizer["Import_AlreadyInProgress"].Value, second.Errors[0].Message);
        Assert.Equal(Localizer["Import_AlreadyInProgress"].Value, link.Errors[0].Message);
        _mediator.Verify(m => m.Send(It.IsAny<UploadMealImportPictures.Command>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ASuccess_ResolvesThePictureJob()
    {
        await _container.StartPictureImport(Pictures);
        var row = Row(MealImportStatus.Succeeded);
        row.ParsedMeal = new ParsedMeal { Name = "Lasagne", IsGuessed = true };

        await FunctionWrote(row);

        Assert.Equal(MealImportJobStatus.Succeeded, _container.JobStatus);
        Assert.Equal("Lasagne", _container.CurrentImport?.ParsedMeal?.Name);
        Assert.False(_container.IsImportInProgress);
    }

    [Fact]
    public async Task ABackgroundedSuccess_SavesTheMealLikeALinkImportDoes()
    {
        _mediator
            .Setup(m => m.Send(It.IsAny<SaveMeal.Command>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Ok(new MealWiz.Shared.Features.Meals.Models.Meal { Id = 7, Name = "Lasagne" }));
        await _container.StartPictureImport(Pictures);
        _container.ContinueInBackground();
        var row = Row(MealImportStatus.Succeeded);
        row.ParsedMeal = new ParsedMeal { Name = "Lasagne" };

        await FunctionWrote(row);

        _mediator.Verify(m => m.Send(
            It.Is<SaveMeal.Command>(c => c.Meal.Name == "Lasagne"), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task AFunctionThatCannotBeReached_FailsThePendingRowAsUnreachable()
    {
        _mediator
            .Setup(m => m.Send(It.IsAny<GetMealImport.Query>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Ok(Row(MealImportStatus.Pending)));
        _mediator
            .Setup(m => m.Send(It.IsAny<MarkMealImportFailed.Command>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((MarkMealImportFailed.Command c, CancellationToken _) =>
                Result.Ok<MealImportModel?>(Row(MealImportStatus.Failed, c.ErrorCode, c.ErrorMessage)));
        await _container.StartPictureImport(Pictures);

        _functionAck.TrySetResult(Result.Fail("502"));
        await Eventually(() => _container.JobStatus == MealImportJobStatus.Failed);

        Assert.Equal(Localizer.ToMessage(MealImportErrorCode.Unreachable), _container.JobErrorMessage);
        _mediator.Verify(m => m.Send(
            It.Is<MarkMealImportFailed.Command>(c => c.ErrorCode == MealImportErrorCode.Unreachable), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    /// <summary>The Function writes the row and answers; the job then reads it.</summary>
    private async Task FunctionWrote(MealImportModel row)
    {
        _mediator
            .Setup(m => m.Send(It.IsAny<GetMealImport.Query>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Ok(row));
        _functionAck.TrySetResult(Result.Ok());
        await Eventually(() => !_container.IsImportInProgress);
    }

    private MealImportModel Row(MealImportStatus status, MealImportErrorCode? errorCode = null, string? errorMessage = null) => new()
    {
        Id = _jobId,
        SourceUrl = null,
        PicturePaths = Paths,
        Language = "en",
        Status = status,
        ErrorCode = errorCode,
        ErrorMessage = errorMessage
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
