namespace MealWiz.Shared.Features.MealImport.Models;

/// <summary>
/// Status of a <c>meal_imports</c> row. Independently duplicated in the Function project
/// (<c>MealWiz/src/MealWiz.Functions/Shared/Models/MealImportStatus.cs</c>) — keep the DB values in sync.
/// </summary>
public enum MealImportStatus
{
    Pending,
    Succeeded,
    Failed
}

public static class MealImportStatusExtensions
{
    public const string PendingValue = "pending";
    public const string SucceededValue = "succeeded";
    public const string FailedValue = "failed";

    public static string ToDbValue(this MealImportStatus status) => status switch
    {
        MealImportStatus.Succeeded => SucceededValue,
        MealImportStatus.Failed => FailedValue,
        _ => PendingValue
    };

    public static MealImportStatus FromDbValue(string? value) => value switch
    {
        SucceededValue => MealImportStatus.Succeeded,
        FailedValue => MealImportStatus.Failed,
        _ => MealImportStatus.Pending
    };
}
