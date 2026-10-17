using FluentResults;
using MealWiz.Functions.Functions.ScrapeRecipe.Parsing;
using MealWiz.Functions.Shared.Fetching;
using MealWiz.Functions.Shared.Models;
using MealWiz.Functions.Shared.SupabaseAccess;
using Microsoft.Extensions.Logging;

namespace MealWiz.Functions.Functions.ScrapeRecipe;

public record ScrapeRecipeRequest(Guid JobId, string Url);

/// <summary>
/// Orchestrates fetch -> parse -> map -> write-back for a single meal_imports row. Every fallible
/// step returns a <see cref="Result{T}"/> instead of throwing — same convention as the main project's
/// CQRS handlers (<c>Result.Try(...)</c> around every Supabase call). This is what makes "never an
/// unhandled exception" (required because the client only reads the meal_imports row via Realtime,
/// never this Function's HTTP response) hold without a top-level try/catch: every operation that can
/// throw is wrapped in <c>Result.Try</c> right at the point it happens, so there's nothing left to
/// escape.
/// </summary>
public class ScrapeRecipeHandler(
    IServiceRoleSupabaseClientFactory clientFactory,
    IRecipeFetcher recipeFetcher,
    IRecipeParser recipeParser,
    IRecipeMapper recipeMapper,
    ILogger<ScrapeRecipeHandler> logger)
{
    public async Task HandleAsync(ScrapeRecipeRequest request, CancellationToken cancellationToken)
    {
        var clientResult = await Result.Try(() => clientFactory.CreateAsync(cancellationToken));
        if (clientResult.IsFailed)
        {
            logger.LogError("Failed to create the Supabase service-role client for job {JobId}: {Error}",
                request.JobId, Describe(clientResult.Errors));
            return;
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
            return;
        }

        if (importRowResult.Value is null)
        {
            logger.LogWarning("meal_imports row {JobId} was not found.", request.JobId);
            return;
        }

        var importRow = importRowResult.Value;

        // The function key ships in the WASM bundle, so a caller holding it can send any jobId and url.
        // Only a still-pending row is resolved, and only from the URL the owner stored on it.
        if (RecipeImportStatusExtensions.FromDbValue(importRow.Status) != RecipeImportStatus.Pending)
        {
            logger.LogWarning("meal_imports row {JobId} is no longer pending (status {Status}); skipping.",
                request.JobId, importRow.Status);
            return;
        }

        if (!string.Equals(request.Url, importRow.SourceUrl, StringComparison.Ordinal))
        {
            logger.LogWarning("Request URL for meal_imports row {JobId} doesn't match its source_url; scraping source_url.",
                request.JobId);
        }

        var parsedMealResult = await ScrapeAndParseAsync(importRow.SourceUrl, cancellationToken);

        if (parsedMealResult.IsSuccess)
        {
            importRow.Status = RecipeImportStatus.Succeeded.ToDbValue();
            importRow.ParsedMeal = parsedMealResult.Value;
            importRow.ErrorMessage = null;
        }
        else
        {
            var errorMessage = Describe(parsedMealResult.Errors);
            logger.LogWarning("Recipe import {JobId} failed: {Error}", request.JobId, errorMessage);

            importRow.Status = RecipeImportStatus.Failed.ToDbValue();
            importRow.ParsedMeal = null;
            importRow.ErrorMessage = errorMessage;
        }

        importRow.UpdatedAt = DateTime.UtcNow;

        // The row can change while the scrape runs (a duplicate request, or the client failing it after a
        // timeout), so the write only applies if it's still pending: the first resolution wins.
        var updateResult = await Result.Try(() => supabaseClient
            .From<MealImportDb>()
            .Where(x => x.Status == RecipeImportStatusExtensions.PendingValue)
            .Update(importRow));

        if (updateResult.IsFailed)
        {
            logger.LogError("Failed to write meal_imports row {JobId} back: {Error}",
                request.JobId, Describe(updateResult.Errors));
            return;
        }

        if (updateResult.Value.Models.Count == 0)
        {
            logger.LogWarning("meal_imports row {JobId} stopped being pending during the scrape; result discarded.",
                request.JobId);
        }
    }

    private async Task<Result<MealImportPayload>> ScrapeAndParseAsync(string url, CancellationToken cancellationToken)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var parsedUrl) ||
            (parsedUrl.Scheme != Uri.UriSchemeHttp && parsedUrl.Scheme != Uri.UriSchemeHttps))
        {
            return Result.Fail("The URL must be an absolute http or https URL.");
        }

        var fetchResult = await recipeFetcher.FetchAsync(parsedUrl, cancellationToken);
        if (fetchResult.IsFailed)
        {
            return Result.Fail<MealImportPayload>(fetchResult.Errors);
        }

        var parseResult = await Result.Try(() => recipeParser.ParseAsync(fetchResult.Value));
        if (parseResult.IsFailed)
        {
            return Result.Fail<MealImportPayload>(parseResult.Errors);
        }

        if (parseResult.Value is null)
        {
            return Result.Fail("Could not find recipe data on the page.");
        }

        return Result.Ok(recipeMapper.Map(parseResult.Value));
    }

    private static string Describe(IReadOnlyList<IError> errors) => string.Join("; ", errors.Select(e => e.Message));
}
