using MealWiz.Shared.Features.Ingredients.Models;
using MealWiz.Shared.Features.Meals.Models;

namespace MealWiz.Shared.Features.MealImport.Models;

/// <summary>
/// The <c>meal_imports.parsed_meal</c> (jsonb) contract written by the import Function. Independently
/// duplicated in the Function project as <c>MealImportPayload</c>
/// (<c>MealWiz/src/MealWiz.Functions/Shared/Models/MealImportPayload.cs</c>) since the two projects can't
/// share code — keep the property names (PascalCase in the JSON) in sync on both sides.
/// </summary>
public class ParsedMeal
{
    public string Name { get; set; } = string.Empty;

    /// <summary>Steps joined with "\n", matching <see cref="Meal.RecipeSteps"/>.</summary>
    public string Recipe { get; set; } = string.Empty;

    public List<ParsedMealIngredient> Ingredients { get; set; } = [];

    /// <summary>
    /// An unsaved <see cref="Meal"/> pre-filled from the import, for the Save Meal form or a direct save.
    /// </summary>
    public Meal ToMeal()
    {
        return new Meal
        {
            Name = Name,
            Recipe = Recipe,
            Ingredients = Ingredients.Select(ingredient => new Ingredient
            {
                Name = ingredient.Name,
                Amount = ingredient.Amount,
                ShowOnGroceryList = true
            }).ToList()
        };
    }
}

public class ParsedMealIngredient
{
    public string Name { get; set; } = string.Empty;

    /// <summary>Free text ("2½ dl", "1 knivspids"); may be empty.</summary>
    public string Amount { get; set; } = string.Empty;
}
