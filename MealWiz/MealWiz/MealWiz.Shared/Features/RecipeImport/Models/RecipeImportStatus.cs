namespace MealWiz.Shared.Features.RecipeImport.Models;

/// <summary>
/// Status of a <c>meal_imports</c> row. Independently duplicated in the Function project
/// (<c>MealWiz/src/MealWiz.Functions/Shared/Models/RecipeImportStatus.cs</c>) — keep the DB values in sync.
/// </summary>
public enum RecipeImportStatus
{
    Pending,
    Succeeded,
    Failed
}

public static class RecipeImportStatusExtensions
{
    public const string PendingValue = "pending";
    public const string SucceededValue = "succeeded";
    public const string FailedValue = "failed";

    public static string ToDbValue(this RecipeImportStatus status) => status switch
    {
        RecipeImportStatus.Succeeded => SucceededValue,
        RecipeImportStatus.Failed => FailedValue,
        _ => PendingValue
    };

    public static RecipeImportStatus FromDbValue(string? value) => value switch
    {
        SucceededValue => RecipeImportStatus.Succeeded,
        FailedValue => RecipeImportStatus.Failed,
        _ => RecipeImportStatus.Pending
    };
}
