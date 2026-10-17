namespace MealWiz.Shared.Features.MealImport.Models;

/// <summary>
/// Client-side lifecycle of the current import job (not the DB row status — see <see cref="MealImportStatus"/>).
/// </summary>
public enum MealImportJobStatus
{
    Idle,
    Submitting,
    AwaitingResult,
    Succeeded,
    Failed
}
