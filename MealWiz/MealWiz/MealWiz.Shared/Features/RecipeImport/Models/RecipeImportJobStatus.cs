namespace MealWiz.Shared.Features.RecipeImport.Models;

/// <summary>
/// Client-side lifecycle of the current import job (not the DB row status — see <see cref="RecipeImportStatus"/>).
/// </summary>
public enum RecipeImportJobStatus
{
    Idle,
    Submitting,
    AwaitingResult,
    Succeeded,
    Failed
}
