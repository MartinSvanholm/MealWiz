namespace MealWiz.Shared.Features.MealImport.Models;

/// <summary>
/// Why a <c>meal_imports</c> row failed (<c>error_code</c>). The Function writes <see cref="InvalidUrl"/>,
/// <see cref="NoRecipeFound"/>, <see cref="FetchFailed"/> and <see cref="Unknown"/>; this client writes
/// <see cref="Unreachable"/> and <see cref="TimedOut"/> (MarkMealImportFailed). The UI shows
/// <see cref="MealImportErrorMessages.ToMessage"/>, never the English <c>error_message</c>. Independently duplicated
/// in the Function project (<c>MealWiz/src/MealWiz.Functions/Shared/Models/RecipeImportErrorCode.cs</c>);
/// <c>MealWiz/tests/Contracts/meal_import_error_code.json</c> checks both sides.
/// </summary>
public enum MealImportErrorCode
{
    InvalidUrl,
    NoRecipeFound,
    FetchFailed,
    Unreachable,
    TimedOut,
    Unknown
}

public static class MealImportErrorCodeExtensions
{
    public const string InvalidUrlValue = "invalid_url";
    public const string NoRecipeFoundValue = "no_recipe_found";
    public const string FetchFailedValue = "fetch_failed";
    public const string UnreachableValue = "unreachable";
    public const string TimedOutValue = "timed_out";
    public const string UnknownValue = "unknown";

    public static string ToDbValue(this MealImportErrorCode code) => code switch
    {
        MealImportErrorCode.InvalidUrl => InvalidUrlValue,
        MealImportErrorCode.NoRecipeFound => NoRecipeFoundValue,
        MealImportErrorCode.FetchFailed => FetchFailedValue,
        MealImportErrorCode.Unreachable => UnreachableValue,
        MealImportErrorCode.TimedOut => TimedOutValue,
        _ => UnknownValue
    };

    /// <summary>The code for a stored value, or null for none or a value this app doesn't know.</summary>
    public static MealImportErrorCode? FromDbValue(string? value) => value switch
    {
        InvalidUrlValue => MealImportErrorCode.InvalidUrl,
        NoRecipeFoundValue => MealImportErrorCode.NoRecipeFound,
        FetchFailedValue => MealImportErrorCode.FetchFailed,
        UnreachableValue => MealImportErrorCode.Unreachable,
        TimedOutValue => MealImportErrorCode.TimedOut,
        UnknownValue => MealImportErrorCode.Unknown,
        _ => null
    };
}
