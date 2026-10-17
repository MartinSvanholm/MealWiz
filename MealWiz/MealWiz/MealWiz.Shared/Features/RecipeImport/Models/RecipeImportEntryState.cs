namespace MealWiz.Shared.Features.RecipeImport.Models;

/// <summary>
/// How the import entry points (the Meals page menu and the Import Recipe page) should present themselves.
/// </summary>
public enum RecipeImportEntryState
{
    Default,
    InProgress,
    Failed
}
