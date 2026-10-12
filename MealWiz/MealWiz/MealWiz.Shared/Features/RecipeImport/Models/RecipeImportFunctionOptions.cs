namespace MealWiz.Shared.Features.RecipeImport.Models;

/// <summary>
/// Bound from the <c>RecipeImportFunction</c> appsettings section. <see cref="Url"/> is the Function App's
/// base URL. <see cref="Key"/> is the function key — not a true secret, since it ships in the client bundle.
/// </summary>
public record RecipeImportFunctionOptions(string Url, string? Key);
