using FluentResults;
using MealWiz.Functions.Shared.Models;
using MealWiz.Functions.Shared.SupabaseAccess;
using Microsoft.Extensions.Logging;

namespace MealWiz.Functions.Functions.CleanUpMealPictures;

public interface ICleanUpMealPicturesHandler
{
    /// <summary>Removes the pictures created before <paramref name="cutoff"/>; the result is how many were removed.</summary>
    Task<Result<int>> HandleAsync(DateTimeOffset cutoff, CancellationToken cancellationToken);
}

/// <summary>
/// Deletes the pictures in the pictures bucket that are older than a cutoff. The app deletes a job's pictures when the
/// meal is saved or discarded; this catches the rest (a closed tab, a failed job, a deleted account). Storage can't list
/// objects by age, so the names come from <c>public.expired_meal_import_pictures</c> (service role only), and the objects
/// are removed through the Storage API, never with SQL. A run removes at most <see cref="MaxPerRun"/> pictures, in
/// batches of <see cref="BatchSize"/>, and never touches anything newer than the cutoff.
/// </summary>
public class CleanUpMealPicturesHandler(
    IServiceRoleSupabaseClientFactory clientFactory,
    ILogger<CleanUpMealPicturesHandler> logger) : ICleanUpMealPicturesHandler
{
    public const int BatchSize = 100;
    public const int MaxPerRun = 5000;

    public async Task<Result<int>> HandleAsync(DateTimeOffset cutoff, CancellationToken cancellationToken)
    {
        var clientResult = await Result.Try(() => clientFactory.CreateAsync(cancellationToken));
        if (clientResult.IsFailed)
        {
            logger.LogError("Failed to create the Supabase service-role client for the picture clean-up: {Error}",
                string.Join("; ", clientResult.Errors.Select(e => e.Message)));
            return clientResult.ToResult<int>();
        }

        var client = clientResult.Value;
        var removed = 0;

        while (removed < MaxPerRun)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var namesResult = await Result.Try(() => client.Rpc<List<string>>(
                "expired_meal_import_pictures", new Dictionary<string, object> { ["cutoff"] = cutoff.UtcDateTime.ToString("o") }));
            if (namesResult.IsFailed)
            {
                logger.LogError("Listing the expired pictures failed: {Error}",
                    string.Join("; ", namesResult.Errors.Select(e => e.Message)));
                return removed == 0 ? namesResult.ToResult<int>() : Result.Ok(removed);
            }

            var names = (namesResult.Value ?? []).Take(Math.Min(BatchSize, MaxPerRun - removed)).ToList();
            if (names.Count == 0) break;

            // A fresh From() per call: it snapshots the auth headers.
            var removeResult = await Result.Try(() => client.Storage.From(MealImportPictures.BucketId).Remove(names));
            if (removeResult.IsFailed)
            {
                // Stop here: the same names would come back next time round. The next daily run tries again.
                logger.LogWarning("Removing {Count} expired pictures failed: {Error}",
                    names.Count, string.Join("; ", removeResult.Errors.Select(e => e.Message)));
                break;
            }

            removed += names.Count;
        }

        logger.LogInformation("Picture clean-up removed {Count} pictures created before {Cutoff:o}.", removed, cutoff);
        return Result.Ok(removed);
    }
}
