namespace MealWiz.Functions.Shared.Models;

/// <summary>
/// The shape written into <c>meal_imports.parsed_meal</c> (jsonb). Mirrors the client's
/// Meal/Ingredient domain models closely enough that the client's pre-fill form (MEA-14) can
/// deserialize it directly, without this project referencing MealWiz.Shared. Shared across any
/// import function that writes to meal_imports (e.g. a future vision-model-based variant), not
/// just ScrapeRecipe.
/// </summary>
public class MealImportPayload
{
    public required string Name { get; init; }
    public string Recipe { get; init; } = string.Empty;
    public List<MealImportIngredientPayload> Ingredients { get; init; } = [];
}

public class MealImportIngredientPayload
{
    public required string Name { get; init; }

    /// <summary>
    /// Free text, like the rest of the codebase ("2½ dl", "1 (400 g) dåse"). Split heuristically from the
    /// start of the ingredient line by IngredientLineParser; empty when the line has no recognisable
    /// leading quantity, in which case the whole line is in <see cref="Name"/>. The user reviews/edits on
    /// the pre-fill form before saving.
    /// </summary>
    public string Amount { get; init; } = string.Empty;
}
