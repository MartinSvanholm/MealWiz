namespace MealWiz.Shared.Services.FunctionApp;

/// <summary>
/// Where the Azure Functions app is, for every feature that calls it (meal import's ScrapeRecipe and
/// AnalyzeMealPicture, Account's DeleteAccount). Bound from the <c>MealImportFunction</c> appsettings section, which
/// keeps its name because deploy.yml injects the production key into it. <see cref="Url"/> is the Function app's
/// base URL. <see cref="Key"/> is the function key — not a true secret, since it ships in the client bundle.
/// </summary>
public record FunctionAppOptions(string Url, string? Key);
