using FluentResults;
using MealWiz.Functions.Shared.Models;
using MealWiz.Functions.Shared.SupabaseAccess;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Supabase.Postgrest;
using static Supabase.Postgrest.Constants;

namespace MealWiz.Functions.Functions.AnalyzeMealPicture;

public record AnalyzeMealPictureRequest(Guid JobId);

public interface IAnalyzeMealPictureHandler
{
    Task<Result> HandleAsync(AnalyzeMealPictureRequest request, CancellationToken cancellationToken);
}

/// <summary>
/// Resolves one pending picture job: claim the row, check it (fresh, its own pictures, the daily limit), download the
/// pictures, analyse them and write the meal or an error code back. Its load and write-back are copied from
/// <see cref="ScrapeRecipe.ScrapeRecipeHandler"/> on purpose; the returned <see cref="Result"/> means the same: it
/// fails only when the row couldn't be reached, in which case the row stays pending and the caller fails it.
/// Never logs picture bytes, the API key or the hint.
/// </summary>
public class AnalyzeMealPictureHandler(
    IServiceRoleSupabaseClientFactory clientFactory,
    IMealPictureAnalyzer analyzer,
    IConfiguration configuration,
    ILogger<AnalyzeMealPictureHandler> logger) : IAnalyzeMealPictureHandler
{
    private static readonly TimeSpan DownloadBudget = TimeSpan.FromSeconds(25);

    public async Task<Result> HandleAsync(AnalyzeMealPictureRequest request, CancellationToken cancellationToken)
    {
        var clientResult = await Result.Try(() => clientFactory.CreateAsync(cancellationToken));
        if (clientResult.IsFailed)
        {
            logger.LogError("Failed to create the Supabase service-role client for job {JobId}: {Error}",
                request.JobId, Describe(clientResult.Errors));
            return clientResult.ToResult();
        }

        var supabaseClient = clientResult.Value;

        var importRowResult = await Result.Try(() => supabaseClient
            .From<MealImportDb>()
            .Where(x => x.Id == request.JobId)
            .Single());

        if (importRowResult.IsFailed)
        {
            logger.LogError("meal_imports row {JobId} could not be loaded: {Error}",
                request.JobId, Describe(importRowResult.Errors));
            return importRowResult.ToResult();
        }

        if (importRowResult.Value is null)
        {
            logger.LogError("meal_imports row {JobId} was not found.", request.JobId);
            return Result.Fail($"meal_imports row {request.JobId} was not found.");
        }

        var importRow = importRowResult.Value;

        // The function key ships in the WASM bundle, so a caller holding it can send any jobId. Only a still-pending
        // picture job is resolved, and only from what its owner stored on it.
        if (RecipeImportStatusExtensions.FromDbValue(importRow.Status) != RecipeImportStatus.Pending
            || importRow.SourceUrl is not null)
        {
            logger.LogWarning("meal_imports row {JobId} is not a pending picture job; skipping.", request.JobId);
            return Result.Ok();
        }

        // Claim it, so a duplicate call can't analyse the same job twice (and be counted twice).
        var claim = await Result.Try(() => supabaseClient
            .From<MealImportDb>()
            .Where(x => x.Id == request.JobId)
            .Where(x => x.Status == RecipeImportStatusExtensions.PendingValue)
            .Filter("updated_at", Operator.Is, "null")
            .Set(x => x.UpdatedAt!, DateTime.UtcNow)
            .Update());

        if (claim.IsFailed)
        {
            logger.LogError("meal_imports row {JobId} could not be claimed: {Error}", request.JobId, Describe(claim.Errors));
            return claim.ToResult();
        }

        if (claim.Value.Models.Count == 0)
        {
            logger.LogWarning("meal_imports row {JobId} is already being handled; skipping.", request.JobId);
            return Result.Ok();
        }

        var payloadResult = await AnalyzeAsync(supabaseClient, importRow, cancellationToken);

        if (payloadResult.IsSuccess)
        {
            importRow.Status = RecipeImportStatus.Succeeded.ToDbValue();
            importRow.ParsedMeal = payloadResult.Value;
            importRow.ErrorCode = null;
            importRow.ErrorMessage = null;
        }
        else
        {
            var errorMessage = Describe(payloadResult.Errors);
            var errorCode = CodeOf(payloadResult.Errors);
            logger.LogWarning("Picture import {JobId} failed ({ErrorCode}): {Error}", request.JobId, errorCode, errorMessage);

            importRow.Status = RecipeImportStatus.Failed.ToDbValue();
            importRow.ParsedMeal = null;
            importRow.ErrorCode = errorCode.ToDbValue();
            importRow.ErrorMessage = errorMessage;
        }

        importRow.UpdatedAt = DateTime.UtcNow;

        // The first resolution wins (see ScrapeRecipeHandler): only a still-pending row is written.
        var updateResult = await Result.Try(() => supabaseClient
            .From<MealImportDb>()
            .Where(x => x.Status == RecipeImportStatusExtensions.PendingValue)
            .Update(importRow));

        if (updateResult.IsFailed)
        {
            logger.LogError("Failed to write meal_imports row {JobId} back: {Error}",
                request.JobId, Describe(updateResult.Errors));
            return updateResult.ToResult();
        }

        if (updateResult.Value.Models.Count == 0)
        {
            logger.LogWarning("meal_imports row {JobId} stopped being pending during the analysis; result discarded.",
                request.JobId);
        }

        return Result.Ok();
    }

    private async Task<Result<MealImportPayload>> AnalyzeAsync(
        Supabase.Client supabaseClient, MealImportDb row, CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        if (!PictureJobRules.IsFresh(row.CreatedAt.ToUniversalTime(), now))
            return Fail(RecipeImportErrorCode.TimedOut, "The job is too old to analyse.");

        if (!PictureJobRules.AreValidPaths(row.CreatedBy, row.PicturePaths))
            return Fail(RecipeImportErrorCode.PictureUnreadable, "The picture paths are not valid for this job's owner.");

        var limit = PictureJobRules.DailyLimit(configuration);
        var countResult = await CountTodaysPictureJobsAsync(supabaseClient, row.CreatedBy, now);
        if (countResult.IsFailed)
            return Result.Fail<MealImportPayload>(countResult.Errors);
        if (countResult.Value > limit)
            return Fail(RecipeImportErrorCode.DailyLimitReached, "The daily limit for picture imports is reached.");

        var downloadResult = await DownloadAsync(supabaseClient, row.PicturePaths!, cancellationToken);
        if (downloadResult.IsFailed)
            return Fail(RecipeImportErrorCode.PictureUnreadable, "A picture could not be downloaded.");

        return await analyzer.AnalyzeAsync(
            new MealPictureAnalysisRequest(downloadResult.Value, row.Language ?? "en", row.Hint), cancellationToken);
    }

    /// <summary>The owner's picture jobs (any status, this one included) created in the current UTC day.</summary>
    private async Task<Result<int>> CountTodaysPictureJobsAsync(Supabase.Client supabaseClient, Guid ownerId, DateTime nowUtc)
    {
        var dayStart = nowUtc.Date;
        var count = await Result.Try(() => supabaseClient
            .From<MealImportDb>()
            .Where(x => x.CreatedBy == ownerId)
            .Filter("source_url", Operator.Is, "null")
            .Filter("created_at", Operator.GreaterThanOrEqual, dayStart.ToString("o"))
            .Filter("created_at", Operator.LessThan, dayStart.AddDays(1).ToString("o"))
            .Count(CountType.Exact));

        if (count.IsFailed)
            logger.LogError("Counting today's picture jobs failed: {Error}", Describe(count.Errors));
        return count;
    }

    private async Task<Result<List<byte[]>>> DownloadAsync(
        Supabase.Client supabaseClient, IReadOnlyList<string> paths, CancellationToken cancellationToken)
    {
        using var budget = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        budget.CancelAfter(DownloadBudget);

        var pictures = new List<byte[]>();
        foreach (var path in paths)
        {
            // One at a time (Storage isn't safe in parallel), and a fresh From() per call, which snapshots the auth headers.
            // Storage has no cancellation; WaitAsync only stops waiting.
            var download = await Result.Try(() => supabaseClient.Storage
                .From(MealImportPictures.BucketId)
                .Download(path, (EventHandler<float>?)null)
                .WaitAsync(budget.Token));

            if (download.IsFailed || download.Value is not { Length: > 0 })
            {
                logger.LogWarning("A picture of a picture job could not be downloaded: {Error}", Describe(download.Errors));
                return Result.Fail("A picture could not be downloaded.");
            }

            pictures.Add(download.Value);
        }

        return Result.Ok(pictures);
    }

    private static Result<MealImportPayload> Fail(RecipeImportErrorCode code, string message) =>
        Result.Fail<MealImportPayload>(new PictureImportError(code, message));

    /// <summary>The code of the first <see cref="PictureImportError"/>; anything else is <see cref="RecipeImportErrorCode.Unknown"/>.</summary>
    private static RecipeImportErrorCode CodeOf(IReadOnlyList<IError> errors) =>
        errors.OfType<PictureImportError>().Select(e => e.Code).DefaultIfEmpty(RecipeImportErrorCode.Unknown).First();

    private static string Describe(IReadOnlyList<IError> errors) => string.Join("; ", errors.Select(e => e.Message));
}
