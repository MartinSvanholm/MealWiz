using FluentResults;
using MealWiz.Shared.Features.Ingredients.SaveIngredient;
using MealWiz.Shared.Features.Meals.Models;
using MealWiz.Shared.Features.Meals.SaveMeal;
using MealWiz.Shared.Features.Meals.State;
using MealWiz.Shared.Features.MealImport.CreateMealImport;
using MealWiz.Shared.Features.MealImport.GetLatestMealImport;
using MealWiz.Shared.Features.MealImport.GetMealImport;
using MealWiz.Shared.Features.MealImport.MarkMealImportFailed;
using MealWiz.Shared.Features.MealImport.Models;
using MealWiz.Shared.Features.MealImport.State;
using MealWiz.Shared.Features.MealImport.TriggerScrapeRecipe;
using MediatR;
using Microsoft.Extensions.Time.Testing;
using Moq;
using MudBlazor;
using Xunit;
using MealImportModel = MealWiz.Shared.Features.MealImport.Models.MealImport;

namespace MealWiz.Shared.Tests.Features.MealImport.UnitTests.State;

/// <summary>
/// Covers the job lifecycle: MediatR, the Realtime subscription and the clock are faked, and the Function's
/// HTTP ack is held open with a TaskCompletionSource so each test decides when (and how) it completes.
/// </summary>
public class MealImportStateContainerTests
{
    private const string Url = "https://example.com/recipe";

    private readonly Mock<IMediator> _mediator = new();
    private readonly Mock<IMealImportRealtimeSubscriber> _subscriber = new();
    private readonly Mock<IMealsStateContainer> _mealsState = new();
    private readonly Mock<ISnackbar> _snackbar = new();
    private readonly Mock<IAsyncDisposable> _subscription = new();
    private readonly FakeTimeProvider _time = new();
    private readonly TaskCompletionSource<Result> _functionAck = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly List<string> _calls = [];
    private readonly MealImportStateContainer _container;

    private Guid _jobId;
    private Action<MealImportModel>? _onRealtimeUpdate;

    public MealImportStateContainerTests()
    {
        _mediator
            .Setup(m => m.Send(It.IsAny<CreateMealImport.Command>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((CreateMealImport.Command c, CancellationToken _) =>
            {
                _calls.Add("create");
                _jobId = c.Id;
                return Result.Ok(Row(MealImportStatus.Pending));
            });

        _subscriber
            .Setup(s => s.Subscribe(It.IsAny<Guid>(), It.IsAny<Action<MealImportModel>>()))
            .ReturnsAsync((Guid _, Action<MealImportModel> onUpdate) =>
            {
                _calls.Add("subscribe");
                _onRealtimeUpdate = onUpdate;
                return Result.Ok(_subscription.Object);
            });

        _mediator
            .Setup(m => m.Send(It.IsAny<TriggerScrapeRecipe.Command>(), It.IsAny<CancellationToken>()))
            .Returns(() =>
            {
                _calls.Add("trigger");
                return _functionAck.Task;
            });

        _subscription.Setup(s => s.DisposeAsync()).Returns(ValueTask.CompletedTask);

        _container = new MealImportStateContainer(
            _mediator.Object, _subscriber.Object, _mealsState.Object, _snackbar.Object, _time);
    }

    [Fact]
    public async Task StartImport_InsertsTheRowAndSubscribesBeforeCallingTheFunction()
    {
        var result = await _container.StartImport(Url);

        Assert.True(result.IsSuccess);
        Assert.Equal(new[] { "create", "subscribe", "trigger" }, _calls);
        Assert.Equal(MealImportJobStatus.AwaitingResult, _container.JobStatus);
        Assert.True(_container.IsImportInProgress);
        Assert.Equal(_jobId, _container.CurrentImport?.Id);
        _subscriber.Verify(s => s.Subscribe(_jobId, It.IsAny<Action<MealImportModel>>()), Times.Once);
    }

    [Theory]
    [InlineData("")]
    [InlineData("not a url")]
    [InlineData("ftp://example.com/recipe")]
    [InlineData("/relative/path")]
    public async Task StartImport_InvalidUrl_FailsWithoutInsertingARow(string url)
    {
        var result = await _container.StartImport(url);

        Assert.True(result.IsFailed);
        Assert.Equal(MealImportStateContainer.InvalidUrlMessage, result.Errors[0].Message);
        Assert.Equal(MealImportJobStatus.Idle, _container.JobStatus);
        Assert.Empty(_calls);
    }

    [Fact]
    public async Task StartImport_WhileAJobIsInProgress_IsRejected()
    {
        await _container.StartImport(Url);

        var second = await _container.StartImport("https://example.com/other");

        Assert.True(second.IsFailed);
        Assert.Equal(MealImportStateContainer.ImportInProgressMessage, second.Errors[0].Message);
        _mediator.Verify(m => m.Send(It.IsAny<CreateMealImport.Command>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task StartImport_InsertFails_FailsTheJobWithoutSubscribingOrCallingTheFunction()
    {
        _mediator
            .Setup(m => m.Send(It.IsAny<CreateMealImport.Command>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Fail<MealImportModel>("Insert failed"));

        var result = await _container.StartImport(Url);

        Assert.True(result.IsFailed);
        Assert.Equal(MealImportJobStatus.Failed, _container.JobStatus);
        Assert.Equal("Insert failed", _container.JobErrorMessage);
        _subscriber.Verify(s => s.Subscribe(It.IsAny<Guid>(), It.IsAny<Action<MealImportModel>>()), Times.Never);
        _mediator.Verify(m => m.Send(It.IsAny<TriggerScrapeRecipe.Command>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CanContinueInBackground_BecomesTrueOnlyAfter30Seconds()
    {
        await _container.StartImport(Url);

        _time.Advance(TimeSpan.FromSeconds(29));
        await Task.Delay(50, TestContext.Current.CancellationToken);
        Assert.False(_container.CanContinueInBackground);

        _time.Advance(TimeSpan.FromSeconds(1));
        await Eventually(() => _container.CanContinueInBackground);
    }

    [Fact]
    public async Task ContinueInBackground_Before30Seconds_StillBackgroundsTheJob()
    {
        await _container.StartImport(Url);

        _container.ContinueInBackground();

        Assert.False(_container.CanContinueInBackground);
        Assert.True(_container.IsRunningInBackground);
    }

    [Fact]
    public void ContinueInBackground_WithNoJob_IsANoOp()
    {
        _container.ContinueInBackground();

        Assert.False(_container.IsRunningInBackground);
    }

    [Fact]
    public async Task ContinueInBackground_AfterTheJobResolved_IsANoOp()
    {
        await _container.StartImport(Url);
        await PushRealtimeUpdate(Row(MealImportStatus.Failed, errorMessage: "Nope"));

        _container.ContinueInBackground();

        Assert.False(_container.IsRunningInBackground);
        VerifyNoSnackbar();
    }

    [Fact]
    public async Task RealtimeSuccess_BackgroundedBefore30Seconds_SavesTheMeal()
    {
        _mediator
            .Setup(m => m.Send(It.IsAny<SaveMeal.Command>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Ok(new Meal { Id = 42, Name = "Pasta" }));
        _mediator
            .Setup(m => m.Send(It.IsAny<SaveIngredient.Command>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Ok());

        await _container.StartImport(Url);
        _container.ContinueInBackground();
        await PushRealtimeUpdate(Row(MealImportStatus.Succeeded, parsedMeal: Pasta()));

        _mediator.Verify(m => m.Send(It.Is<SaveMeal.Command>(c => c.Meal.Name == "Pasta"), It.IsAny<CancellationToken>()), Times.Once);
        VerifySnackbar(Severity.Success, "Pasta");
    }

    [Fact]
    public async Task StartImport_InsertFailsAfterBeingBackgroundedWhileSubmitting_Toasts()
    {
        var insert = new TaskCompletionSource<Result<MealImportModel>>(TaskCreationOptions.RunContinuationsAsynchronously);
        _mediator
            .Setup(m => m.Send(It.IsAny<CreateMealImport.Command>(), It.IsAny<CancellationToken>()))
            .Returns(insert.Task);

        var start = _container.StartImport(Url);
        Assert.Equal(MealImportJobStatus.Submitting, _container.JobStatus);

        _container.ContinueInBackground();
        insert.SetResult(Result.Fail<MealImportModel>("Insert failed"));
        await start;

        Assert.Equal(MealImportJobStatus.Failed, _container.JobStatus);
        VerifySnackbar(Severity.Error, "Insert failed");
    }

    [Fact]
    public async Task StartImport_InsertFailsInForeground_DoesNotToast()
    {
        _mediator
            .Setup(m => m.Send(It.IsAny<CreateMealImport.Command>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Fail<MealImportModel>("Insert failed"));

        await _container.StartImport(Url);

        VerifyNoSnackbar();
    }

    [Fact]
    public async Task EntryState_FollowsTheJobAndTheLatestImport()
    {
        Assert.Equal(MealImportEntryState.Default, _container.EntryState);

        await _container.StartImport(Url);
        Assert.Equal(MealImportEntryState.InProgress, _container.EntryState);

        await PushRealtimeUpdate(Row(MealImportStatus.Failed, errorMessage: "Nope"));
        Assert.Equal(MealImportEntryState.Failed, _container.EntryState);

        _container.ResetJob();
        Assert.Equal(MealImportEntryState.Failed, _container.EntryState);
    }

    [Fact]
    public async Task RealtimeSuccess_InForeground_ExposesTheParsedMealWithoutSavingIt()
    {
        await _container.StartImport(Url);

        await PushRealtimeUpdate(Row(MealImportStatus.Succeeded, parsedMeal: Pasta()));

        Assert.Equal(MealImportJobStatus.Succeeded, _container.JobStatus);
        Assert.False(_container.IsImportInProgress);
        Assert.Equal("Pasta", _container.CurrentImport?.ParsedMeal?.Name);
        Assert.False(_container.LatestImportFailed);
        _subscription.Verify(s => s.DisposeAsync(), Times.Once);
        _mediator.Verify(m => m.Send(It.IsAny<SaveMeal.Command>(), It.IsAny<CancellationToken>()), Times.Never);
        VerifyNoSnackbar();
    }

    [Fact]
    public async Task RealtimeFailure_InForeground_ExposesTheErrorWithoutAToast()
    {
        await _container.StartImport(Url);

        await PushRealtimeUpdate(Row(MealImportStatus.Failed, errorMessage: "No recipe found on the page."));

        Assert.Equal(MealImportJobStatus.Failed, _container.JobStatus);
        Assert.Equal("No recipe found on the page.", _container.JobErrorMessage);
        Assert.True(_container.LatestImportFailed);
        Assert.Equal("No recipe found on the page.", _container.LatestErrorMessage);
        VerifyNoSnackbar();
    }

    [Fact]
    public async Task RealtimeSuccess_InBackground_SavesTheMealAndIngredientsThenToasts()
    {
        _mediator
            .Setup(m => m.Send(It.IsAny<SaveMeal.Command>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Ok(new Meal { Id = 42, Name = "Pasta" }));
        _mediator
            .Setup(m => m.Send(It.IsAny<SaveIngredient.Command>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Ok());

        await StartAndBackground();
        await PushRealtimeUpdate(Row(MealImportStatus.Succeeded, parsedMeal: Pasta()));

        Assert.Equal(MealImportJobStatus.Succeeded, _container.JobStatus);
        _mediator.Verify(m => m.Send(It.Is<SaveMeal.Command>(c => c.Meal.Name == "Pasta" && c.Meal.Id == 0), It.IsAny<CancellationToken>()), Times.Once);
        _mediator.Verify(m => m.Send(It.Is<SaveIngredient.Command>(c => c.MealId == 42 && c.Ingredient.Name == "pasta"), It.IsAny<CancellationToken>()), Times.Once);
        _mediator.Verify(m => m.Send(It.Is<SaveIngredient.Command>(c => c.MealId == 42 && c.Ingredient.Name == "salt"), It.IsAny<CancellationToken>()), Times.Once);
        _mealsState.Verify(s => s.LoadMeals(), Times.Once);
        VerifySnackbar(Severity.Success, "Pasta");
    }

    [Fact]
    public async Task RealtimeFailure_InBackground_ToastsAndSavesNothing()
    {
        await StartAndBackground();
        await PushRealtimeUpdate(Row(MealImportStatus.Failed, errorMessage: "No recipe found on the page."));

        Assert.True(_container.LatestImportFailed);
        _mediator.Verify(m => m.Send(It.IsAny<SaveMeal.Command>(), It.IsAny<CancellationToken>()), Times.Never);
        VerifySnackbar(Severity.Error, "No recipe found on the page.");
    }

    [Fact]
    public async Task FunctionAcked_RealtimeEventMissed_ResolvesFromTheRow()
    {
        SetupGetMealImport(Row(MealImportStatus.Succeeded, parsedMeal: Pasta()));
        await _container.StartImport(Url);

        await CompleteFunctionCall(Result.Ok());

        Assert.Equal(MealImportJobStatus.Succeeded, _container.JobStatus);
        VerifyNoMarkFailed();
    }

    [Fact]
    public async Task FunctionAcked_RowStillPending_KeepsWaitingForRealtime()
    {
        SetupGetMealImport(Row(MealImportStatus.Pending));
        await _container.StartImport(Url);

        await CompleteFunctionCall(Result.Ok());

        Assert.Equal(MealImportJobStatus.AwaitingResult, _container.JobStatus);
        VerifyNoMarkFailed();

        await PushRealtimeUpdate(Row(MealImportStatus.Succeeded, parsedMeal: Pasta()));
        Assert.Equal(MealImportJobStatus.Succeeded, _container.JobStatus);
    }

    [Fact]
    public async Task FunctionUnreachable_RowStillPending_MarksTheRowFailed()
    {
        SetupGetMealImport(Row(MealImportStatus.Pending));
        _mediator
            .Setup(m => m.Send(It.IsAny<MarkMealImportFailed.Command>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((MarkMealImportFailed.Command c, CancellationToken _) =>
                Result.Ok<MealImportModel?>(Row(MealImportStatus.Failed, errorMessage: c.ErrorMessage)));
        await _container.StartImport(Url);

        await CompleteFunctionCall(Result.Fail("Network error"));

        Assert.Equal(MealImportJobStatus.Failed, _container.JobStatus);
        Assert.Equal(MealImportStateContainer.ServiceUnreachableMessage, _container.JobErrorMessage);
        Assert.True(_container.LatestImportFailed);
        _mediator.Verify(m => m.Send(It.Is<MarkMealImportFailed.Command>(c => c.Id == _jobId), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task FunctionUnreachable_RowAlreadyFailed_ShowsTheExistingErrorWithoutUpdatingTheRow()
    {
        SetupGetMealImport(Row(MealImportStatus.Failed, errorMessage: "No recipe found on the page."));
        await _container.StartImport(Url);

        await CompleteFunctionCall(Result.Fail("Timed out"));

        Assert.Equal(MealImportJobStatus.Failed, _container.JobStatus);
        Assert.Equal("No recipe found on the page.", _container.JobErrorMessage);
        VerifyNoMarkFailed();
    }

    [Fact]
    public async Task FunctionUnreachable_FunctionResolvesTheRowFirst_ShowsWhatTheFunctionWrote()
    {
        _mediator
            .SetupSequence(m => m.Send(It.IsAny<GetMealImport.Query>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Ok(Row(MealImportStatus.Pending)))
            .ReturnsAsync(Result.Ok(Row(MealImportStatus.Succeeded, parsedMeal: Pasta())));
        _mediator
            .Setup(m => m.Send(It.IsAny<MarkMealImportFailed.Command>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Ok<MealImportModel?>(null));
        await _container.StartImport(Url);

        await CompleteFunctionCall(Result.Fail("Timed out"));

        Assert.Equal(MealImportJobStatus.Succeeded, _container.JobStatus);
    }

    [Fact]
    public async Task FunctionUnreachable_RowCannotBeUpdated_FailsTheJobLocally()
    {
        SetupGetMealImport(Row(MealImportStatus.Pending));
        _mediator
            .Setup(m => m.Send(It.IsAny<MarkMealImportFailed.Command>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Fail<MealImportModel?>("Offline"));
        await _container.StartImport(Url);

        await CompleteFunctionCall(Result.Fail("Offline"));

        Assert.Equal(MealImportJobStatus.Failed, _container.JobStatus);
        Assert.Equal(MealImportStateContainer.ServiceUnreachableMessage, _container.JobErrorMessage);
    }

    // The Function answers 502 when it couldn't load or write the row (e.g. its Supabase key was rejected), and
    // TriggerScrapeRecipe reports that as a failure. Nothing will resolve the row, so the client fails it itself.
    [Fact]
    public async Task FunctionReturnedServerError_RowStillPending_MarksTheRowFailed()
    {
        SetupGetMealImport(Row(MealImportStatus.Pending));
        SetupMarkFailedReturnsTheFailedRow();
        await _container.StartImport(Url);

        await CompleteFunctionCall(Result.Fail("The meal import service responded with 502."));

        Assert.Equal(MealImportJobStatus.Failed, _container.JobStatus);
        Assert.Equal(MealImportStateContainer.ServiceUnreachableMessage, _container.JobErrorMessage);
        Assert.True(_container.LatestImportFailed);
        _mediator.Verify(m => m.Send(
            It.Is<MarkMealImportFailed.Command>(c => c.Id == _jobId && c.ErrorMessage == MealImportStateContainer.ServiceUnreachableMessage),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task NoResultWithinTheTimeout_RowStillPending_MarksTheRowFailed()
    {
        SetupGetMealImport(Row(MealImportStatus.Pending));
        SetupMarkFailedReturnsTheFailedRow();
        await _container.StartImport(Url);

        _time.Advance(MealImportStateContainer.ResultTimeout - TimeSpan.FromSeconds(1));
        await Task.Delay(50, TestContext.Current.CancellationToken);
        Assert.Equal(MealImportJobStatus.AwaitingResult, _container.JobStatus);
        VerifyNoMarkFailed();

        _time.Advance(TimeSpan.FromSeconds(1));
        await Eventually(() => _container.JobStatus == MealImportJobStatus.Failed);

        Assert.Equal(MealImportStateContainer.TimedOutMessage, _container.JobErrorMessage);
        Assert.True(_container.LatestImportFailed);
        _mediator.Verify(m => m.Send(
            It.Is<MarkMealImportFailed.Command>(c => c.Id == _jobId && c.ErrorMessage == MealImportStateContainer.TimedOutMessage),
            It.IsAny<CancellationToken>()), Times.Once);
        _subscription.Verify(s => s.DisposeAsync(), Times.Once);
    }

    [Fact]
    public async Task NoResultWithinTheTimeout_RowAlreadyResolved_ShowsTheRowWithoutUpdatingIt()
    {
        SetupGetMealImport(Row(MealImportStatus.Succeeded, parsedMeal: Pasta()));
        await _container.StartImport(Url);

        _time.Advance(MealImportStateContainer.ResultTimeout);
        await Eventually(() => _container.JobStatus == MealImportJobStatus.Succeeded);

        VerifyNoMarkFailed();
    }

    [Fact]
    public async Task NoResultWithinTheTimeout_RowCannotBeUpdated_FailsTheJobLocally()
    {
        SetupGetMealImport(Row(MealImportStatus.Pending));
        _mediator
            .Setup(m => m.Send(It.IsAny<MarkMealImportFailed.Command>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Fail<MealImportModel?>("Offline"));
        await _container.StartImport(Url);

        _time.Advance(MealImportStateContainer.ResultTimeout);
        await Eventually(() => _container.JobStatus == MealImportJobStatus.Failed);

        Assert.Equal(MealImportStateContainer.TimedOutMessage, _container.JobErrorMessage);
    }

    [Fact]
    public async Task JobResolvedBeforeTheTimeout_TimeoutDoesNothing()
    {
        await _container.StartImport(Url);
        await PushRealtimeUpdate(Row(MealImportStatus.Failed, errorMessage: "No recipe found on the page."));

        _time.Advance(MealImportStateContainer.ResultTimeout);
        await Task.Delay(50, TestContext.Current.CancellationToken);

        Assert.Equal("No recipe found on the page.", _container.JobErrorMessage);
        _mediator.Verify(m => m.Send(It.IsAny<GetMealImport.Query>(), It.IsAny<CancellationToken>()), Times.Never);
        VerifyNoMarkFailed();
    }

    [Fact]
    public async Task SubscribeFails_StillCallsTheFunctionAndResolvesFromTheRow()
    {
        _subscriber
            .Setup(s => s.Subscribe(It.IsAny<Guid>(), It.IsAny<Action<MealImportModel>>()))
            .ReturnsAsync(Result.Fail<IAsyncDisposable>("Socket closed"));
        SetupGetMealImport(Row(MealImportStatus.Succeeded, parsedMeal: Pasta()));
        await _container.StartImport(Url);

        await CompleteFunctionCall(Result.Ok());

        Assert.Equal(MealImportJobStatus.Succeeded, _container.JobStatus);
    }

    [Fact]
    public async Task SecondResolution_ForTheSameJob_IsIgnored()
    {
        await _container.StartImport(Url);

        await PushRealtimeUpdate(Row(MealImportStatus.Failed, errorMessage: "First"));
        await PushRealtimeUpdate(Row(MealImportStatus.Succeeded, parsedMeal: Pasta()));

        Assert.Equal(MealImportJobStatus.Failed, _container.JobStatus);
        Assert.Equal("First", _container.JobErrorMessage);
        _subscription.Verify(s => s.DisposeAsync(), Times.Once);
    }

    [Fact]
    public async Task SucceededRowWithoutAPayload_IsTreatedAsAFailure()
    {
        await _container.StartImport(Url);

        await PushRealtimeUpdate(Row(MealImportStatus.Succeeded));

        Assert.Equal(MealImportJobStatus.Failed, _container.JobStatus);
        Assert.True(_container.LatestImportFailed);
    }

    [Fact]
    public async Task ResetJob_AfterAFinishedJob_ReturnsToIdleButKeepsTheLatestImport()
    {
        await _container.StartImport(Url);
        await PushRealtimeUpdate(Row(MealImportStatus.Failed, errorMessage: "Nope"));

        _container.ResetJob();

        Assert.Equal(MealImportJobStatus.Idle, _container.JobStatus);
        Assert.Null(_container.CurrentImport);
        Assert.Null(_container.JobErrorMessage);
        Assert.True(_container.LatestImportFailed);
    }

    [Fact]
    public async Task LoadLatestImport_ExposesWhetherTheNewestImportFailed()
    {
        _mediator
            .Setup(m => m.Send(It.IsAny<GetLatestMealImport.Query>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Ok<MealImportModel?>(Row(MealImportStatus.Failed, errorMessage: "No recipe found on the page.")));

        await _container.LoadLatestImport();

        Assert.True(_container.LatestImportFailed);
        Assert.Equal("No recipe found on the page.", _container.LatestErrorMessage);
    }

    [Fact]
    public async Task LoadLatestImport_WhileAJobIsInProgress_DoesNotQuery()
    {
        await _container.StartImport(Url);

        await _container.LoadLatestImport();

        _mediator.Verify(m => m.Send(It.IsAny<GetLatestMealImport.Query>(), It.IsAny<CancellationToken>()), Times.Never);
        Assert.Equal(_jobId, _container.LatestImport?.Id);
    }

    [Fact]
    public async Task LoadLatestImport_StalePendingCopyOfAResolvedJob_DoesNotOverwriteIt()
    {
        await _container.StartImport(Url);
        await PushRealtimeUpdate(Row(MealImportStatus.Failed, errorMessage: "Nope"));
        _mediator
            .Setup(m => m.Send(It.IsAny<GetLatestMealImport.Query>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Ok<MealImportModel?>(Row(MealImportStatus.Pending)));

        await _container.LoadLatestImport();

        Assert.True(_container.LatestImportFailed);
    }

    private async Task StartAndBackground()
    {
        await _container.StartImport(Url);
        _time.Advance(MealImportStateContainer.BackgroundEligibilityDelay);
        await Eventually(() => _container.CanContinueInBackground);
        _container.ContinueInBackground();
        Assert.True(_container.IsRunningInBackground);
    }

    private async Task PushRealtimeUpdate(MealImportModel row)
    {
        Assert.NotNull(_onRealtimeUpdate);
        _onRealtimeUpdate(row);
        await DrainBackgroundWork();
    }

    private async Task CompleteFunctionCall(Result result)
    {
        _functionAck.TrySetResult(result);
        await DrainBackgroundWork();
    }

    // Once a job has resolved (timer cancelled) and the Function call has returned, all tracked work completes.
    // Otherwise something is legitimately still pending, so just give the mocks' continuations a moment to run.
    private async Task DrainBackgroundWork()
    {
        var work = _container.WhenBackgroundWorkCompleted();
        if (_container.IsImportInProgress || !_functionAck.Task.IsCompleted)
        {
            await Task.WhenAny(work, Task.Delay(100, TestContext.Current.CancellationToken));
            return;
        }

        await work.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
    }

    private void SetupGetMealImport(MealImportModel row) =>
        _mediator
            .Setup(m => m.Send(It.IsAny<GetMealImport.Query>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Ok(row));

    private void SetupMarkFailedReturnsTheFailedRow() =>
        _mediator
            .Setup(m => m.Send(It.IsAny<MarkMealImportFailed.Command>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((MarkMealImportFailed.Command c, CancellationToken _) =>
                Result.Ok<MealImportModel?>(Row(MealImportStatus.Failed, errorMessage: c.ErrorMessage)));

    private void VerifyNoMarkFailed() =>
        _mediator.Verify(m => m.Send(It.IsAny<MarkMealImportFailed.Command>(), It.IsAny<CancellationToken>()), Times.Never);

    private void VerifySnackbar(Severity severity, string contains) =>
        _snackbar.Verify(s => s.Add(It.Is<string>(m => m.Contains(contains)), severity, It.IsAny<Action<SnackbarOptions>>(), It.IsAny<string>()), Times.Once);

    private void VerifyNoSnackbar() =>
        _snackbar.Verify(s => s.Add(It.IsAny<string>(), It.IsAny<Severity>(), It.IsAny<Action<SnackbarOptions>>(), It.IsAny<string>()), Times.Never);

    private MealImportModel Row(MealImportStatus status, ParsedMeal? parsedMeal = null, string? errorMessage = null) => new()
    {
        Id = _jobId,
        SourceUrl = Url,
        Status = status,
        ParsedMeal = parsedMeal,
        ErrorMessage = errorMessage
    };

    private static ParsedMeal Pasta() => new()
    {
        Name = "Pasta",
        Recipe = "Boil\nServe",
        Ingredients =
        [
            new ParsedMealIngredient { Name = "pasta", Amount = "500 g" },
            new ParsedMealIngredient { Name = "salt", Amount = "" }
        ]
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
