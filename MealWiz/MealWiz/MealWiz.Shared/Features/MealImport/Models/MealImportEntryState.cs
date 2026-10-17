namespace MealWiz.Shared.Features.MealImport.Models;

/// <summary>
/// How the import entry points (the Meals page menu and the Import Meal page) should present themselves.
/// </summary>
public enum MealImportEntryState
{
    Default,
    InProgress,
    Failed
}
