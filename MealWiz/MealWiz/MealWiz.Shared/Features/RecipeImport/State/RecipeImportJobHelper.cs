using MealWiz.Shared.Features.RecipeImport.Models;
using MealWiz.Shared.Helpers;

namespace MealWiz.Shared.Features.RecipeImport.State;

/// <summary>
/// Pure decision logic used by <see cref="RecipeImportStateContainer"/> — no services, no state, so it can be
/// reasoned about (and tested) on its own.
/// </summary>
public static class RecipeImportJobHelper
{
    /// <summary>
    /// Accepts only absolute http(s) URLs. <paramref name="normalizedUrl"/> is the trimmed, still-escaped form
    /// (<see cref="Uri.AbsoluteUri"/>), so encoded characters like %20 survive the round trip.
    /// </summary>
    public static bool TryNormalizeRecipeUrl(string? url, out string normalizedUrl)
    {
        normalizedUrl = string.Empty;

        if (!Uri.TryCreate(url?.Trim(), UriKind.Absolute, out var uri)) return false;
        if (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps) return false;

        normalizedUrl = uri.AbsoluteUri;
        return true;
    }

    /// <summary>
    /// True when <paramref name="loaded"/> is a still-pending copy of the import we already hold as resolved —
    /// i.e. it was read from the DB just before the job finished, and must not overwrite the result.
    /// </summary>
    public static bool IsStalePendingCopy(Models.RecipeImport? loaded, Models.RecipeImport? current)
    {
        return loaded is not null
            && current is not null
            && loaded.Id == current.Id
            && current.IsResolved
            && !loaded.IsResolved;
    }

    /// <summary>
    /// A "succeeded" row without a parsed meal can't be pre-filled or saved, so it's treated as a failure.
    /// Returns the same instance, adjusted in place.
    /// </summary>
    public static Models.RecipeImport EnsureUsableResult(Models.RecipeImport row)
    {
        if (row.Status == RecipeImportStatus.Succeeded && row.ParsedMeal is null)
        {
            row.Status = RecipeImportStatus.Failed;
            row.ErrorMessage ??= ErrorMessages.GenericError;
        }

        return row;
    }

    /// <summary>
    /// An in-memory failed copy of the job's row, for when the failure couldn't be written to the DB
    /// (the row itself may stay pending). Keeps what we know about the row from <paramref name="current"/>.
    /// </summary>
    public static Models.RecipeImport CreateLocallyFailedImport(
        Guid jobId, Models.RecipeImport? current, string errorMessage, DateTime now)
    {
        return new Models.RecipeImport
        {
            Id = jobId,
            CreatedBy = current?.CreatedBy ?? Guid.Empty,
            SourceUrl = current?.SourceUrl ?? string.Empty,
            CreatedAt = current?.CreatedAt ?? now,
            Status = RecipeImportStatus.Failed,
            ErrorMessage = errorMessage
        };
    }
}
