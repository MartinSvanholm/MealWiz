using FluentResults;
using MealWiz.Shared.Features.Meals.State;
using MealWiz.Shared.Features.MealImport.Models;
using MealWiz.Shared.Helpers;
using MediatR;
using MudBlazor;

namespace MealWiz.Shared.Features.MealImport.State;

public interface IMealImportStateContainer
{
    MealImportJobStatus JobStatus { get; }

    /// <summary>The row of the current/last job started in this session.</summary>
    Models.MealImport? CurrentImport { get; }

    /// <summary>Error to show for a failed job (also set when the job failed before a row existed).</summary>
    string? JobErrorMessage { get; }

    /// <summary>The user's newest import — from the DB on load, then kept current by jobs in this session.</summary>
    Models.MealImport? LatestImport { get; }
    bool LatestImportFailed { get; }
    string? LatestErrorMessage { get; }

    bool IsImportInProgress { get; }

    /// <summary>What the import entry points (Meals page menu, Import Meal page) should show.</summary>
    MealImportEntryState EntryState { get; }

    /// <summary>
    /// True once a job has been awaiting its result for <see cref="MealImportStateContainer.BackgroundEligibilityDelay"/>,
    /// i.e. when the Import page should offer "Continue in background".
    /// </summary>
    bool CanContinueInBackground { get; }

    /// <summary>
    /// True once the job was backgrounded ("Continue in background", or the user left the Import page while it ran);
    /// the result is then saved/toasted without the page.
    /// </summary>
    bool IsRunningInBackground { get; }

    event Action OnStateChanged;

    void NotifyStateChanged();
    Task LoadLatestImport();
    Task<Result> StartImport(string url);

    /// <summary>
    /// Hands the running job over to the container, which then saves a success (no review step) or toasts a failure.
    /// Allowed at any point while the job is in progress, so leaving the Import page early backgrounds it too.
    /// No-op when nothing is in progress.
    /// </summary>
    void ContinueInBackground();

    /// <summary>Clears a finished job back to <see cref="MealImportJobStatus.Idle"/>. No-op while one is in progress.</summary>
    void ResetJob();
}

/// <summary>
/// Runs one meal import job at a time: inserts the pending meal_imports row, subscribes to it via Realtime,
/// then calls the Function. The row is the source of truth for the outcome. Registered as scoped, which is
/// app-lifetime in both WASM and BlazorWebView, so a background job keeps resolving after the user leaves the Import page.
///
/// Realtime callbacks can arrive off the UI thread (MAUI), so components should subscribe to
/// <see cref="StateContainerBase.OnStateChanged"/> with <c>() => InvokeAsync(StateHasChanged)</c>.
/// </summary>
public class MealImportStateContainer(
    IMediator mediator,
    IMealImportRealtimeSubscriber realtimeSubscriber,
    IMealsStateContainer mealsStateContainer,
    ISnackbar snackbar,
    TimeProvider timeProvider) : StateContainerBase, IMealImportStateContainer, IDisposable
{
    public static readonly TimeSpan BackgroundEligibilityDelay = TimeSpan.FromSeconds(30);

    /// <summary>
    /// How long a job may stay pending before the client gives up and fails it. A safety net for a Function that acks
    /// without ever writing the row; an unreachable or failing Function is already handled when its call returns.
    /// </summary>
    public static readonly TimeSpan ResultTimeout = TimeSpan.FromMinutes(3);

    public const string ServiceUnreachableMessage = "We couldn't reach the meal import service. Please try again.";
    public const string TimedOutMessage = "The meal import took too long. Please try again.";
    public const string InvalidUrlMessage = "Please enter a valid meal URL (starting with http:// or https://).";
    public const string ImportInProgressMessage = "A meal import is already in progress.";

    private readonly object jobLock = new();
    private readonly List<Task> backgroundWork = [];

    // Id of the job still awaiting its result; cleared exactly once, by whichever path resolves it first.
    private Guid? activeJobId;
    private IAsyncDisposable? subscription;
    private CancellationTokenSource? jobTimersCts;

    public MealImportJobStatus JobStatus { get; private set; } = MealImportJobStatus.Idle;
    public Models.MealImport? CurrentImport { get; private set; }
    public string? JobErrorMessage { get; private set; }
    public Models.MealImport? LatestImport { get; private set; }
    public bool CanContinueInBackground { get; private set; }
    public bool IsRunningInBackground { get; private set; }

    public bool IsImportInProgress => JobStatus is MealImportJobStatus.Submitting or MealImportJobStatus.AwaitingResult;
    public bool LatestImportFailed => LatestImport?.Status == MealImportStatus.Failed;
    public string? LatestErrorMessage => LatestImportFailed ? LatestImport!.ErrorMessage ?? ErrorMessages.GenericError : null;
    public MealImportEntryState EntryState => MealImportJobHelper.GetEntryState(IsImportInProgress, LatestImportFailed);

    public async Task LoadLatestImport()
    {
        // A job in this session is newer than (or the same as) anything the query could return.
        if (IsImportInProgress) return;

        var result = await mediator.Send(new GetLatestMealImport.GetLatestMealImport.Query());
        if (result.Handle(snackbar).IsFailed) return;

        // Re-check after the await: a job may have started, or resolved, while the query was in flight.
        if (IsImportInProgress) return;
        if (MealImportJobHelper.IsStalePendingCopy(result.Value, LatestImport)) return;

        LatestImport = result.Value;
        NotifyStateChanged();
    }

    public async Task<Result> StartImport(string url)
    {
        if (IsImportInProgress) return Result.Fail(ImportInProgressMessage);
        if (!MealImportJobHelper.TryNormalizeMealUrl(url, out var sourceUrl)) return Result.Fail(InvalidUrlMessage);

        var jobId = Guid.NewGuid();

        CurrentImport = null;
        JobErrorMessage = null;
        CanContinueInBackground = false;
        IsRunningInBackground = false;
        JobStatus = MealImportJobStatus.Submitting;
        NotifyStateChanged();

        var createResult = await mediator.Send(new CreateMealImport.CreateMealImport.Command(jobId, sourceUrl));
        if (createResult.IsFailed)
        {
            JobErrorMessage = createResult.Errors.First().Message;
            JobStatus = MealImportJobStatus.Failed;

            // Backgrounded while submitting (the user left the Import page), so nothing else would show this.
            if (IsRunningInBackground)
                snackbar.Add($"Meal import failed: {JobErrorMessage}", Severity.Error);

            NotifyStateChanged();
            return createResult.ToResult();
        }

        lock (jobLock) activeJobId = jobId;
        CurrentImport = createResult.Value;
        LatestImport = createResult.Value;

        // Subscribe before the Function runs so its write-back can't be missed. If subscribing fails we
        // carry on anyway: the row is re-read once the Function has acked (see RunScrape).
        var subscribeResult = await realtimeSubscriber.Subscribe(jobId, update => Track(OnRowUpdated(jobId, update)));
        if (subscribeResult.IsSuccess)
            subscription = subscribeResult.Value;

        JobStatus = MealImportJobStatus.AwaitingResult;
        NotifyStateChanged();

        StartJobTimers(jobId);
        Track(RunScrape(jobId, sourceUrl));

        return Result.Ok();
    }

    public void ContinueInBackground()
    {
        if (!IsImportInProgress) return;

        IsRunningInBackground = true;
        NotifyStateChanged();
    }

    public void ResetJob()
    {
        if (IsImportInProgress) return;

        JobStatus = MealImportJobStatus.Idle;
        CurrentImport = null;
        JobErrorMessage = null;
        CanContinueInBackground = false;
        IsRunningInBackground = false;
        NotifyStateChanged();
    }

    /// <summary>Completes when all fire-and-forget work started so far (Function call, timer, resolution) has finished.</summary>
    internal Task WhenBackgroundWorkCompleted()
    {
        Task[] pending;
        lock (backgroundWork) pending = backgroundWork.ToArray();
        return Task.WhenAll(pending);
    }

    /// <summary>
    /// Realtime callback for the job's row. Only a final status (succeeded/failed) resolves the job;
    /// other updates to a still-pending row are ignored.
    /// </summary>
    private async Task OnRowUpdated(Guid jobId, Models.MealImport update)
    {
        if (update.IsResolved)
            await Resolve(jobId, update);
    }

    /// <summary>
    /// Calls the Function, then makes sure the job ends up resolved even if Realtime misses the update:
    /// <list type="number">
    /// <item>Re-read the row. If the Function already wrote a result (or an error), use that.</item>
    /// <item>If the Function acked but the row is still pending, keep waiting for Realtime.</item>
    /// <item>If the Function couldn't be reached, or answered with an error (it couldn't reach the row), fail the
    /// job via <see cref="FailPendingJob"/>.</item>
    /// </list>
    /// </summary>
    private async Task RunScrape(Guid jobId, string sourceUrl)
    {
        try
        {
            var triggerResult = await mediator.Send(new TriggerScrapeRecipe.TriggerScrapeRecipe.Command(jobId, sourceUrl));
            if (!IsActive(jobId)) return;

            if (await TryResolveFromRow(jobId)) return;

            if (triggerResult.IsSuccess) return;

            await FailPendingJob(jobId, ServiceUnreachableMessage);
        }
        catch (Exception)
        {
            await Resolve(jobId, MealImportJobHelper.CreateLocallyFailedImport(
                jobId, CurrentImport, ErrorMessages.GenericError, timeProvider.GetUtcNow().UtcDateTime));
        }
    }

    /// <summary>Re-reads the job's row and resolves the job if the row already has a final status.</summary>
    private async Task<bool> TryResolveFromRow(Guid jobId)
    {
        var rowResult = await mediator.Send(new GetMealImport.GetMealImport.Query(jobId));
        if (!rowResult.IsSuccess || !rowResult.Value.IsResolved) return false;

        await Resolve(jobId, rowResult.Value);
        return true;
    }

    /// <summary>
    /// Fails a job whose row is still pending although nothing is going to resolve it:
    /// <list type="number">
    /// <item>Mark the still-pending row failed in the DB, so it doesn't stay pending.</item>
    /// <item>If the row stopped being pending first, the Function resolved it after all: show what it wrote.</item>
    /// <item>If neither is possible, fail the job in memory only.</item>
    /// </list>
    /// </summary>
    private async Task FailPendingJob(Guid jobId, string errorMessage)
    {
        var markResult = await mediator.Send(new MarkMealImportFailed.MarkMealImportFailed.Command(jobId, errorMessage));
        if (markResult.IsSuccess && markResult.Value is not null)
        {
            await Resolve(jobId, markResult.Value);
            return;
        }

        // A success without a row means the conditional update matched no rows, so the row stopped being pending.
        if (markResult.IsSuccess && await TryResolveFromRow(jobId)) return;

        await Resolve(jobId, MealImportJobHelper.CreateLocallyFailedImport(
            jobId, CurrentImport, errorMessage, timeProvider.GetUtcNow().UtcDateTime));
    }

    /// <summary>
    /// Finishes the job with its final row. Realtime, the post-ack re-read and the failure fallback can all race
    /// to get here, so only the first call for the active job does anything. It stops the subscription and the
    /// job's timers, then publishes the result. For a background job it also does what the Import page would have done:
    /// saves the meal on success, or toasts the error on failure.
    /// </summary>
    private async Task Resolve(Guid jobId, Models.MealImport row)
    {
        lock (jobLock)
        {
            if (activeJobId != jobId) return;
            activeJobId = null;
        }

        jobTimersCts?.Cancel();
        await DisposeSubscription();

        row = MealImportJobHelper.EnsureUsableResult(row);

        CurrentImport = row;
        LatestImport = row;
        CanContinueInBackground = false;

        try
        {
            if (row.Status == MealImportStatus.Succeeded)
            {
                JobErrorMessage = null;
                JobStatus = MealImportJobStatus.Succeeded;

                if (IsRunningInBackground)
                    await SaveImportedMeal(row.ParsedMeal!);
            }
            else
            {
                JobErrorMessage = row.ErrorMessage ?? ErrorMessages.GenericError;
                JobStatus = MealImportJobStatus.Failed;

                if (IsRunningInBackground)
                    snackbar.Add($"Meal import failed: {JobErrorMessage}", Severity.Error);
            }
        }
        catch (Exception)
        {
            snackbar.Add(ErrorMessages.GenericError, Severity.Error);
        }
        finally
        {
            NotifyStateChanged();
        }
    }

    /// <summary>
    /// A background success skips the review form, so the meal and its ingredients are saved straight away,
    /// then the meal list is reloaded and the user is told via a toast.
    /// </summary>
    private async Task SaveImportedMeal(ParsedMeal parsedMeal)
    {
        var meal = parsedMeal.ToMeal();

        var saveResult = await mediator.Send(new Meals.SaveMeal.SaveMeal.Command(meal));
        if (saveResult.IsFailed)
        {
            snackbar.Add($"\"{meal.Name}\" was imported but couldn't be saved: {saveResult.Errors.First().Message}", Severity.Error);
            return;
        }

        var savedMealId = saveResult.Value.Id;
        foreach (var ingredient in meal.Ingredients)
        {
            var ingredientResult = await mediator.Send(new Ingredients.SaveIngredient.SaveIngredient.Command(ingredient, savedMealId));
            if (ingredientResult.IsFailed)
            {
                snackbar.Add($"\"{meal.Name}\" was imported but some ingredients couldn't be saved: {ingredientResult.Errors.First().Message}", Severity.Error);
                break;
            }
        }

        // LoadMeals reports through the Meals container's snackbar, which is only set once the Meals page has been opened.
        mealsStateContainer.CurrentSnackbar ??= snackbar;
        await mealsStateContainer.LoadMeals();

        snackbar.Add($"\"{meal.Name}\" was imported and added to your meals.", Severity.Success);
    }

    /// <summary>
    /// Starts the job's countdowns: 30 s until the Import page may offer "Continue in background", and
    /// <see cref="ResultTimeout"/> until the job is given up on. Resolving the job cancels both.
    /// </summary>
    private void StartJobTimers(Guid jobId)
    {
        jobTimersCts?.Dispose();
        var cts = new CancellationTokenSource();
        jobTimersCts = cts;

        Track(WaitForBackgroundEligibility(jobId, cts.Token));
        Track(WaitForResultTimeout(jobId, cts.Token));
    }

    /// <summary>
    /// Waits out the countdown, then raises <see cref="CanContinueInBackground"/> — unless the job resolved
    /// first, which cancels the wait.
    /// </summary>
    private async Task WaitForBackgroundEligibility(Guid jobId, CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(BackgroundEligibilityDelay, timeProvider, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        if (!IsActive(jobId)) return;

        CanContinueInBackground = true;
        NotifyStateChanged();
    }

    /// <summary>
    /// Waits out <see cref="ResultTimeout"/>, then fails a job that is still pending, so the import never spins
    /// forever. A result written in the meantime (and missed by Realtime) still wins.
    /// </summary>
    private async Task WaitForResultTimeout(Guid jobId, CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(ResultTimeout, timeProvider, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        if (!IsActive(jobId)) return;

        try
        {
            if (await TryResolveFromRow(jobId)) return;

            await FailPendingJob(jobId, TimedOutMessage);
        }
        catch (Exception)
        {
            await Resolve(jobId, MealImportJobHelper.CreateLocallyFailedImport(
                jobId, CurrentImport, TimedOutMessage, timeProvider.GetUtcNow().UtcDateTime));
        }
    }

    /// <summary>Whether <paramref name="jobId"/> is still the job awaiting a result (i.e. hasn't been resolved yet).</summary>
    private bool IsActive(Guid jobId)
    {
        lock (jobLock) return activeJobId == jobId;
    }

    /// <summary>Unsubscribes from the job's Realtime channel, at most once.</summary>
    private async Task DisposeSubscription()
    {
        var current = Interlocked.Exchange(ref subscription, null);
        if (current is not null)
            await current.DisposeAsync();
    }

    /// <summary>
    /// Keeps hold of fire-and-forget tasks (Function call, timer, Realtime resolution) so tests can await them
    /// via <see cref="WhenBackgroundWorkCompleted"/>. Completed tasks are pruned as new ones are added.
    /// </summary>
    private void Track(Task task)
    {
        lock (backgroundWork)
        {
            backgroundWork.RemoveAll(t => t.IsCompleted);
            backgroundWork.Add(task);
        }
    }

    public void Dispose()
    {
        lock (jobLock) activeJobId = null;
        jobTimersCts?.Cancel();
        jobTimersCts?.Dispose();
        _ = DisposeSubscription();
    }
}
