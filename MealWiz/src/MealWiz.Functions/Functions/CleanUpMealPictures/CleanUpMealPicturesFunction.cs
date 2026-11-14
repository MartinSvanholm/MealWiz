using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace MealWiz.Functions.Functions.CleanUpMealPictures;

/// <summary>
/// Every day at 03:17 UTC, deletes the pictures older than 24 hours (so none outlives about two days). The timer needs
/// <c>AzureWebJobsStorage</c>, which the app already has.
/// </summary>
public class CleanUpMealPicturesFunction(ICleanUpMealPicturesHandler handler, ILogger<CleanUpMealPicturesFunction> logger)
{
    public static readonly TimeSpan MaxAge = TimeSpan.FromHours(24);

    [Function("CleanUpMealPictures")]
    public async Task Run([TimerTrigger("0 17 3 * * *")] TimerInfo timer, CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(DateTimeOffset.UtcNow - MaxAge, cancellationToken);

        // The handler has logged why; a timer has no caller to answer, and tomorrow's run tries again.
        if (result.IsFailed)
            logger.LogWarning("The picture clean-up could not run.");
    }
}
