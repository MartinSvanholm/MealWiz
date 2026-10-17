namespace MealWiz.Shared.Features.MealImport.Models;

/// <summary>
/// Bound from the <c>MealImportFunction</c> appsettings section. <see cref="Url"/> is the Function App's
/// base URL. <see cref="Key"/> is the function key — not a true secret, since it ships in the client bundle.
/// </summary>
public record MealImportFunctionOptions(string Url, string? Key);
