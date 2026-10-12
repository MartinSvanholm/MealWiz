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
    bool IsBackgrounded { get; }

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
/// app-lifetime in both WASM and BlazorWebView, so a backgrounded job keeps resolving after the drawer closes.
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
    public bool IsBackgrounded { get; private set; }

    public bool IsImportInProgress => JobStatus is RecipeImportJobStatus.Submitting or RecipeImportJobStatus.AwaitingResult;
    public bool LatestImportFailed => LatestImport?.Status == RecipeImportStatus.Failed;
    public string? LatestErrorMessage => LatestImportFailed ? LatestImport!.ErrorMessage ?? ErrorMessages.GenericError : null;

    public async Task LoadLatestImport()
    {
        var result = await mediator.Send(new GetLatestRecipeImport.GetLatestRecipeImport.Query());
        if (result.Handle(snackbar).IsFailed) return;

        var loaded = result.Value;

        // A job in this session is newer than (or the same as) anything the query could return, and a
        // pending copy read before that job resolved mustn't overwrite its result.
        if (IsImportInProgress) return;
        if (loaded is not null && loaded.Id == LatestImport?.Id && LatestImport.IsResolved && !loaded.IsResolved) return;

        LatestImport = loaded;
        NotifyStateChanged();
    }

    public async Task<Result> StartImport(string url)
    {
        if (IsImportInProgress) return Result.Fail(ImportInProgressMessage);

        if (!Uri.TryCreate(url?.Trim(), UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            return Result.Fail(InvalidUrlMessage);
        }

        var sourceUrl = uri.AbsoluteUri;
        var jobId = Guid.NewGuid();

        CurrentImport = null;
        JobErrorMessage = null;
        CanContinueInBackground = false;
        IsBackgrounded = false;
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

        IsBackgrounded = true;
        NotifyStateChanged();
    }

    public void ResetJob()
    {
        if (IsImportInProgress) return;

        JobStatus = RecipeImportJobStatus.Idle;
        CurrentImport = null;
        JobErrorMessage = null;
        CanContinueInBackground = false;
        IsBackgrounded = false;
        NotifyStateChanged();
    }

    /// <summary>Completes when all fire-and-forget work started so far (Function call, timer, resolution) has finished.</summary>
    internal Task WhenBackgroundWorkCompleted()
    {
        Task[] pending;
        lock (backgroundWork) pending = backgroundWork.ToArray();
        return Task.WhenAll(pending);
    }

    private async Task OnRowUpdated(Guid jobId, Models.RecipeImport update)
    {
        if (update.IsResolved)
            await Resolve(jobId, update);
    }

    private async Task RunScrape(Guid jobId, string sourceUrl)
    {
        try
        {
            var triggerResult = await mediator.Send(new TriggerScrapeRecipe.TriggerScrapeRecipe.Command(jobId, sourceUrl));
            if (!IsActive(jobId)) return;

            // The Function only acks after writing the row, so by now it's normally resolved. Reading it covers a
            // missed Realtime event — and if the Function did record an error, that's the one we show.
            var rowResult = await mediator.Send(new GetRecipeImport.GetRecipeImport.Query(jobId));
            if (rowResult.IsSuccess && rowResult.Value.IsResolved)
            {
                await Resolve(jobId, rowResult.Value);
                return;
            }

            // Acked but still pending — keep waiting for Realtime.
            if (triggerResult.IsSuccess) return;

            // The Function couldn't be reached, so nothing will ever progress the row: record the failure on it.
            var markResult = await mediator.Send(new MarkRecipeImportFailed.MarkRecipeImportFailed.Command(jobId, ServiceUnreachableMessage));
            if (markResult.IsSuccess && markResult.Value is not null)
            {
                await Resolve(jobId, markResult.Value);
                return;
            }

            if (markResult.IsSuccess)
            {
                // No longer pending: the Function resolved it after all. Show what it wrote.
                var rereadResult = await mediator.Send(new GetRecipeImport.GetRecipeImport.Query(jobId));
                if (rereadResult.IsSuccess && rereadResult.Value.IsResolved)
                {
                    await Resolve(jobId, rereadResult.Value);
                    return;
                }
            }

            await ResolveLocallyAsFailed(jobId, ServiceUnreachableMessage);
        }
        catch (Exception)
        {
            await ResolveLocallyAsFailed(jobId, ErrorMessages.GenericError);
        }
    }

    // Couldn't get a result onto the row; fail the job in memory only (the row may stay pending).
    private Task ResolveLocallyAsFailed(Guid jobId, string errorMessage)
    {
        var failed = new Models.RecipeImport
        {
            Id = jobId,
            CreatedBy = CurrentImport?.CreatedBy ?? Guid.Empty,
            SourceUrl = CurrentImport?.SourceUrl ?? string.Empty,
            CreatedAt = CurrentImport?.CreatedAt ?? DateTime.UtcNow,
            Status = RecipeImportStatus.Failed,
            ErrorMessage = errorMessage
        };

        return Resolve(jobId, failed);
    }

    private async Task Resolve(Guid jobId, Models.RecipeImport row)
    {
        lock (jobLock)
        {
            if (activeJobId != jobId) return;
            activeJobId = null;
        }

        backgroundEligibilityCts?.Cancel();
        await DisposeSubscription();

        // A "succeeded" row without a payload can't be used, so treat it as a failure.
        if (row.Status == RecipeImportStatus.Succeeded && row.ParsedMeal is null)
        {
            row.Status = RecipeImportStatus.Failed;
            row.ErrorMessage ??= ErrorMessages.GenericError;
        }

        CurrentImport = row;
        LatestImport = row;
        CanContinueInBackground = false;

        try
        {
            if (row.Status == RecipeImportStatus.Succeeded)
            {
                JobErrorMessage = null;
                JobStatus = RecipeImportJobStatus.Succeeded;

                if (IsBackgrounded)
                    await SaveImportedMeal(row.ParsedMeal!);
            }
            else
            {
                JobErrorMessage = row.ErrorMessage ?? ErrorMessages.GenericError;
                JobStatus = RecipeImportJobStatus.Failed;

                if (IsBackgrounded)
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

    // Backgrounded success skips the review step: save the meal + ingredients straight away.
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

    private void StartBackgroundEligibilityTimer(Guid jobId)
    {
        backgroundEligibilityCts?.Dispose();
        var cts = new CancellationTokenSource();
        backgroundEligibilityCts = cts;

        Track(WaitForBackgroundEligibility(jobId, cts.Token));
    }

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

    private bool IsActive(Guid jobId)
    {
        lock (jobLock) return activeJobId == jobId;
    }

    private async Task DisposeSubscription()
    {
        var current = Interlocked.Exchange(ref subscription, null);
        if (current is not null)
            await current.DisposeAsync();
    }

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
