namespace MealWiz.Functions.Shared.Models;

/// <summary>
/// Why a meal import failed, written to <c>meal_imports.error_code</c> together with <c>failed</c>. The client turns it
/// into a localized message; <c>error_message</c> keeps the English detail for diagnosis only. The Function writes
/// <see cref="InvalidUrl"/>, <see cref="NoRecipeFound"/>, <see cref="FetchFailed"/> and <see cref="Unknown"/>; the
/// client writes <see cref="Unreachable"/> and <see cref="TimedOut"/>. Independently duplicated on the client
/// (<c>MealWiz/MealWiz/MealWiz.Shared/Features/MealImport/Models/MealImportErrorCode.cs</c>);
/// <c>MealWiz/tests/Contracts/meal_import_error_code.json</c> checks both sides.
/// </summary>
public enum RecipeImportErrorCode
{
    InvalidUrl,
    NoRecipeFound,
    FetchFailed,
    Unreachable,
    TimedOut,
    Unknown
}

public static class RecipeImportErrorCodeExtensions
{
    public const string InvalidUrlValue = "invalid_url";
    public const string NoRecipeFoundValue = "no_recipe_found";
    public const string FetchFailedValue = "fetch_failed";
    public const string UnreachableValue = "unreachable";
    public const string TimedOutValue = "timed_out";
    public const string UnknownValue = "unknown";

    public static string ToDbValue(this RecipeImportErrorCode code) => code switch
    {
        RecipeImportErrorCode.InvalidUrl => InvalidUrlValue,
        RecipeImportErrorCode.NoRecipeFound => NoRecipeFoundValue,
        RecipeImportErrorCode.FetchFailed => FetchFailedValue,
        RecipeImportErrorCode.Unreachable => UnreachableValue,
        RecipeImportErrorCode.TimedOut => TimedOutValue,
        _ => UnknownValue
    };

    /// <summary>The code for a stored value, or null for none or a value this side doesn't know.</summary>
    public static RecipeImportErrorCode? FromDbValue(string? value) => value switch
    {
        InvalidUrlValue => RecipeImportErrorCode.InvalidUrl,
        NoRecipeFoundValue => RecipeImportErrorCode.NoRecipeFound,
        FetchFailedValue => RecipeImportErrorCode.FetchFailed,
        UnreachableValue => RecipeImportErrorCode.Unreachable,
        TimedOutValue => RecipeImportErrorCode.TimedOut,
        UnknownValue => RecipeImportErrorCode.Unknown,
        _ => null
    };
}
