using FluentResults;
using MealWiz.Shared.Features.Meals.State;
using MealWiz.Shared.Features.RecipeImport.Models;
using MealWiz.Shared.Helpers;
using MediatR;
using MudBlazor;

namespace MealWiz.Shared.Features.RecipeImport.State;

public interface IRecipeImportStateContainer
{
    RecipeImportJobStatus JobStatus { get; }

    /// <summary>The row of the current/last job started in this session.</summary>
    Models.RecipeImport? CurrentImport { get; }

    /// <summary>Error to show for a failed job (also set when the job failed before a row existed).</summary>
    string? JobErrorMessage { get; }

    /// <summary>The user's newest import — from the DB on load, then kept current by jobs in this session.</summary>
    Models.RecipeImport? LatestImport { get; }
    bool LatestImportFailed { get; }
    string? LatestErrorMessage { get; }

    bool IsImportInProgress { get; }

    /// <summary>True once a job has been awaiting its result for <see cref="RecipeImportStateContainer.BackgroundEligibilityDelay"/>.</summary>
    bool CanContinueInBackground { get; }

    /// <summary>True when the user chose "Continue in background"; the result is then saved/toasted without the drawer.</summary>
    bool IsRunningInBackground { get; }

    event Action OnStateChanged;

    void NotifyStateChanged();
    Task LoadLatestImport();
    Task<Result> StartImport(string url);
    void ContinueInBackground();

    /// <summary>Clears a finished job back to <see cref="RecipeImportJobStatus.Idle"/>. No-op while one is in progress.</summary>
    void ResetJob();
}

/// <summary>
/// Runs one recipe import job at a time: inserts the pending meal_imports row, subscribes to it via Realtime,
/// then calls the Function. The row is the source of truth for the outcome. Registered as scoped, which is
/// app-lifetime in both WASM and BlazorWebView, so a background job keeps resolving after the drawer closes.
///
/// Realtime callbacks can arrive off the UI thread (MAUI), so components should subscribe to
/// <see cref="StateContainerBase.OnStateChanged"/> with <c>() => InvokeAsync(StateHasChanged)</c>.
/// </summary>
public class RecipeImportStateContainer(
    IMediator mediator,
    IRecipeImportRealtimeSubscriber realtimeSubscriber,
    IMealsStateContainer mealsStateContainer,
    ISnackbar snackbar,
    TimeProvider timeProvider) : StateContainerBase, IRecipeImportStateContainer, IDisposable
{
    public static readonly TimeSpan BackgroundEligibilityDelay = TimeSpan.FromSeconds(30);

    public const string ServiceUnreachableMessage = "We couldn't reach the recipe import service. Please try again.";
    public const string InvalidUrlMessage = "Please enter a valid recipe URL (starting with http:// or https://).";
    public const string ImportInProgressMessage = "A recipe import is already in progress.";

    private readonly object jobLock = new();
    private readonly List<Task> backgroundWork = [];

    // Id of the job still awaiting its result; cleared exactly once, by whichever path resolves it first.
    private Guid? activeJobId;
    private IAsyncDisposable? subscription;
    private CancellationTokenSource? backgroundEligibilityCts;

    public RecipeImportJobStatus JobStatus { get; private set; } = RecipeImportJobStatus.Idle;
    public Models.RecipeImport? CurrentImport { get; private set; }
    public string? JobErrorMessage { get; private set; }
    public Models.RecipeImport? LatestImport { get; private set; }
    public bool CanContinueInBackground { get; private set; }
    public bool IsRunningInBackground { get; private set; }

    public bool IsImportInProgress => JobStatus is RecipeImportJobStatus.Submitting or RecipeImportJobStatus.AwaitingResult;
    public bool LatestImportFailed => LatestImport?.Status == RecipeImportStatus.Failed;
    public string? LatestErrorMessage => LatestImportFailed ? LatestImport!.ErrorMessage ?? ErrorMessages.GenericError : null;

    public async Task LoadLatestImport()
    {
        // A job in this session is newer than (or the same as) anything the query could return.
        if (IsImportInProgress) return;

        var result = await mediator.Send(new GetLatestRecipeImport.GetLatestRecipeImport.Query());
        if (result.Handle(snackbar).IsFailed) return;

        // Re-check after the await: a job may have started, or resolved, while the query was in flight.
        if (IsImportInProgress) return;
        if (RecipeImportJobHelper.IsStalePendingCopy(result.Value, LatestImport)) return;

        LatestImport = result.Value;
        NotifyStateChanged();
    }

    public async Task<Result> StartImport(string url)
    {
        if (IsImportInProgress) return Result.Fail(ImportInProgressMessage);
        if (!RecipeImportJobHelper.TryNormalizeRecipeUrl(url, out var sourceUrl)) return Result.Fail(InvalidUrlMessage);

        var jobId = Guid.NewGuid();

        CurrentImport = null;
        JobErrorMessage = null;
        CanContinueInBackground = false;
        IsRunningInBackground = false;
        JobStatus = RecipeImportJobStatus.Submitting;
        NotifyStateChanged();

        var createResult = await mediator.Send(new CreateRecipeImport.CreateRecipeImport.Command(jobId, sourceUrl));
        if (createResult.IsFailed)
        {
            JobErrorMessage = createResult.Errors.First().Message;
            JobStatus = RecipeImportJobStatus.Failed;
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

        JobStatus = RecipeImportJobStatus.AwaitingResult;
        NotifyStateChanged();

        StartBackgroundEligibilityTimer(jobId);
        Track(RunScrape(jobId, sourceUrl));

        return Result.Ok();
    }

    public void ContinueInBackground()
    {
        if (JobStatus != RecipeImportJobStatus.AwaitingResult || !CanContinueInBackground) return;

        IsRunningInBackground = true;
        NotifyStateChanged();
    }

    public void ResetJob()
    {
        if (IsImportInProgress) return;

        JobStatus = RecipeImportJobStatus.Idle;
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
    private async Task OnRowUpdated(Guid jobId, Models.RecipeImport update)
    {
        if (update.IsResolved)
            await Resolve(jobId, update);
    }

    /// <summary>
    /// Calls the Function, then makes sure the job ends up resolved even if Realtime misses the update:
    /// <list type="number">
    /// <item>Re-read the row. If the Function already wrote a result (or an error), use that.</item>
    /// <item>If the Function acked but the row is still pending, keep waiting for Realtime.</item>
    /// <item>If the Function couldn't be reached, mark the still-pending row failed in the DB.</item>
    /// <item>If even that isn't possible, fail the job in memory only.</item>
    /// </list>
    /// </summary>
    private async Task RunScrape(Guid jobId, string sourceUrl)
    {
        try
        {
            var triggerResult = await mediator.Send(new TriggerScrapeRecipe.TriggerScrapeRecipe.Command(jobId, sourceUrl));
            if (!IsActive(jobId)) return;

            var rowResult = await mediator.Send(new GetRecipeImport.GetRecipeImport.Query(jobId));
            if (rowResult.IsSuccess && rowResult.Value.IsResolved)
            {
                await Resolve(jobId, rowResult.Value);
                return;
            }

            if (triggerResult.IsSuccess) return;

            var markResult = await mediator.Send(new MarkRecipeImportFailed.MarkRecipeImportFailed.Command(jobId, ServiceUnreachableMessage));
            if (markResult.IsSuccess && markResult.Value is not null)
            {
                await Resolve(jobId, markResult.Value);
                return;
            }

            if (markResult.IsSuccess)
            {
                // The conditional update matched no rows, so the row stopped being pending — the Function
                // resolved it after all. Show what it wrote.
                var rereadResult = await mediator.Send(new GetRecipeImport.GetRecipeImport.Query(jobId));
                if (rereadResult.IsSuccess && rereadResult.Value.IsResolved)
                {
                    await Resolve(jobId, rereadResult.Value);
                    return;
                }
            }

            await Resolve(jobId, RecipeImportJobHelper.CreateLocallyFailedImport(
                jobId, CurrentImport, ServiceUnreachableMessage, timeProvider.GetUtcNow().UtcDateTime));
        }
        catch (Exception)
        {
            await Resolve(jobId, RecipeImportJobHelper.CreateLocallyFailedImport(
                jobId, CurrentImport, ErrorMessages.GenericError, timeProvider.GetUtcNow().UtcDateTime));
        }
    }

    /// <summary>
    /// Finishes the job with its final row. Realtime, the post-ack re-read and the failure fallback can all race
    /// to get here, so only the first call for the active job does anything. It stops the subscription and the
    /// 30 s timer, then publishes the result. For a background job it also does what the drawer would have done:
    /// saves the meal on success, or toasts the error on failure.
    /// </summary>
    private async Task Resolve(Guid jobId, Models.RecipeImport row)
    {
        lock (jobLock)
        {
            if (activeJobId != jobId) return;
            activeJobId = null;
        }

        backgroundEligibilityCts?.Cancel();
        await DisposeSubscription();

        row = RecipeImportJobHelper.EnsureUsableResult(row);

        CurrentImport = row;
        LatestImport = row;
        CanContinueInBackground = false;

        try
        {
            if (row.Status == RecipeImportStatus.Succeeded)
            {
                JobErrorMessage = null;
                JobStatus = RecipeImportJobStatus.Succeeded;

                if (IsRunningInBackground)
                    await SaveImportedMeal(row.ParsedMeal!);
            }
            else
            {
                JobErrorMessage = row.ErrorMessage ?? ErrorMessages.GenericError;
                JobStatus = RecipeImportJobStatus.Failed;

                if (IsRunningInBackground)
                    snackbar.Add($"Recipe import failed: {JobErrorMessage}", Severity.Error);
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
    private async Task SaveImportedMeal(ParsedRecipeMeal parsedMeal)
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

    /// <summary>Starts the 30 s countdown after which the drawer may offer "Continue in background".</summary>
    private void StartBackgroundEligibilityTimer(Guid jobId)
    {
        backgroundEligibilityCts?.Dispose();
        var cts = new CancellationTokenSource();
        backgroundEligibilityCts = cts;

        Track(WaitForBackgroundEligibility(jobId, cts.Token));
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
        backgroundEligibilityCts?.Cancel();
        backgroundEligibilityCts?.Dispose();
        _ = DisposeSubscription();
    }
}
