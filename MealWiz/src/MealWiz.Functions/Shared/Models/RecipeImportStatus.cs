namespace MealWiz.Functions.Shared.Models;

public enum RecipeImportStatus
{
    Pending,
    Succeeded,
    Failed
}

/// <summary>
/// Mirrors the client's MealType/MealTypeExtensions convention: string DB values are only ever
/// produced/consumed through these helpers, never written as raw literals.
/// </summary>
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
